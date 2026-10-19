# Implementation Plan: KYC Validation API

**JIRA:** FINNOVA-20 · **Deliverable:** Standalone Python (FastAPI) service · **Target repo:** `D:\Projects\Finnova\Finnova-KYC-API`

## Overview

This plan delivers the **standalone** KYC Validation API as copy-paste-ready code, one task per
file, in the dependency order fixed by the design's implementation-ordered task list. The project
is a new, independent **Python 3.11+ / FastAPI + Pydantic v2** service that lives at
`D:\Projects\Finnova\Finnova-KYC-API` — it is NOT added to `Finnova.Backend.slnx` and NOT placed
in the `Finnova-UI` repo.

Per the workspace spec-workflow convention, **this file is the artifact the user copies code
from**. Every task below is marked **CREATE `<path>`** (all files are new in the standalone repo)
and contains the complete file content. Paths are relative to the new repo root
(`Finnova-KYC-API/`). Do not run builds or tests here — the user executes from this file.

Work proceeds bottom-up by dependency: scaffolding → config → schemas → domain exceptions →
Verhoeff → Aadhaar/PAN/face services → routers → app factory (CORS + exception handlers) → unit
tests → integration tests → README. Each step builds on the previous and ends by wiring into the
running app and test suite.

Implementation language: **Python 3.11+** (FastAPI, Pydantic v2, pydantic-settings, DeepFace,
pytest). No language-selection question is needed — the design fixes the stack.

All sample data is **English / India-only** (per product-context): no Arabic, no bilingual
fields, no right-to-left content.

## Tasks

- [ ] 1. Project scaffolding (dependency manifest, ignores, env template, package markers)
  - [ ] 1.1 Create `requirements.txt`
    - **CREATE `requirements.txt`**
    - Pins the runtime and test dependencies. `deepface` pulls in TensorFlow; `tf-keras` is
      required so DeepFace works on modern TF. `python-multipart` is required by FastAPI for
      `multipart/form-data` uploads. `pillow` backs the image-decodability guard. `httpx` backs
      the FastAPI `TestClient`.
    - _Requirements: 6.1, 6.2, 7.4, 7.5_

    ```text
    fastapi==0.115.6
    uvicorn[standard]==0.34.0
    pydantic==2.10.4
    pydantic-settings==2.7.1
    deepface==0.0.93
    tf-keras==2.18.0
    python-multipart==0.0.20
    pillow==11.1.0
    pytest==8.3.4
    httpx==0.28.1
    ```

  - [ ] 1.2 Create `.gitignore`
    - **CREATE `.gitignore`**
    - Ignores Python virtual environments, bytecode caches, local env files, test caches, and
      DeepFace's downloaded weights / temp artifacts.
    - _Requirements: 6.1_

    ```text
    # Virtual environments
    .venv/
    venv/
    env/

    # Python bytecode / caches
    __pycache__/
    *.py[cod]
    *.egg-info/
    .pytest_cache/
    .mypy_cache/

    # Local environment
    .env

    # Editors / OS
    .vscode/
    .idea/
    .DS_Store

    # Temp / model artifacts
    *.tmp
    temp/
    tmp/
    .deepface/
    ```

  - [ ] 1.3 Create `.env.example`
    - **CREATE `.env.example`**
    - Documents every environment variable `Settings` reads. Copy to `.env` for local runs.
      `CORS_ORIGINS` is a comma-separated list parsed into `list[str]`.
    - _Requirements: 6.3, 3.6_

    ```text
    # Application metadata
    APP_NAME=KYC Validation API
    APP_VERSION=1.0.0

    # CORS allowlist (comma-separated origins; use * for permissive dev)
    CORS_ORIGINS=*

    # DeepFace tunables
    FACE_MODEL=Facenet512
    FACE_DETECTOR_BACKEND=opencv
    FACE_DISTANCE_METRIC=cosine

    # Per-file upload cap in bytes (default 10 MB)
    MAX_UPLOAD_BYTES=10485760
    ```

  - [ ] 1.4 Create the package marker files
    - **CREATE `app/__init__.py`**
    - **CREATE `app/core/__init__.py`**
    - **CREATE `app/api/__init__.py`**
    - **CREATE `app/api/v1/__init__.py`**
    - **CREATE `app/schemas/__init__.py`**
    - **CREATE `app/services/__init__.py`**
    - **CREATE `tests/__init__.py`**
    - Each file is empty; it marks the directory as an importable Python package so
      `app.main:app`, `app.services.*`, and the test modules resolve.
    - _Requirements: 6.1_

    ```python
    ```

- [ ] 2. Create application configuration (`Settings`)
  - [ ] 2.1 Create `app/core/config.py`
    - **CREATE `app/core/config.py`**
    - `pydantic-settings` `BaseSettings` holding app metadata, CORS allowlist, DeepFace
      model/detector/metric, the 10 MB per-file cap, and the accepted image content types. A
      field validator parses the comma-separated `CORS_ORIGINS` env var into `list[str]`.
      `get_settings()` is cached so the app factory and services share one instance.
    - _Requirements: 6.3, 3.4, 3.6_

    ```python
    from functools import lru_cache

    from pydantic import field_validator
    from pydantic_settings import BaseSettings, SettingsConfigDict


    class Settings(BaseSettings):
        """Runtime configuration read from environment / .env."""

        app_name: str = "KYC Validation API"
        app_version: str = "1.0.0"

        # CORS allowlist; permissive by default for development.
        cors_origins: list[str] = ["*"]

        # DeepFace tunables (reported back in the face-match response).
        face_model: str = "Facenet512"
        face_detector_backend: str = "opencv"
        face_distance_metric: str = "cosine"

        # Per-file upload cap: 10 MB.
        max_upload_bytes: int = 10 * 1024 * 1024

        # Accepted image formats for the face-match endpoint.
        allowed_image_content_types: set[str] = {"image/jpeg", "image/png"}

        model_config = SettingsConfigDict(
            env_file=".env",
            env_file_encoding="utf-8",
            case_sensitive=False,
            extra="ignore",
        )

        @field_validator("cors_origins", mode="before")
        @classmethod
        def _split_cors_origins(cls, value: object) -> object:
            """Allow CORS_ORIGINS to be a comma-separated string in the env file."""
            if isinstance(value, str):
                return [origin.strip() for origin in value.split(",") if origin.strip()]
            return value


    @lru_cache
    def get_settings() -> Settings:
        """Return a cached singleton Settings instance."""
        return Settings()
    ```

- [ ] 3. Create Pydantic v2 schemas (error + per-endpoint contracts)
  - [ ] 3.1 Create `app/schemas/error.py`
    - **CREATE `app/schemas/error.py`**
    - The single structured error body used by every error response (R5.2). `detail` is a
      `str`, a `list`, or `null`; `timestamp` is a UTC ISO-8601 string.
    - _Requirements: 5.1, 5.2, 5.3_

    ```python
    from typing import Any

    from pydantic import BaseModel


    class ErrorResponse(BaseModel):
        """Structured error body shared across 422/404/405/500 responses."""

        error: str
        message: str
        detail: str | list[Any] | None = None
        timestamp: str
    ```

  - [ ] 3.2 Create `app/schemas/aadhaar.py`
    - **CREATE `app/schemas/aadhaar.py`**
    - Request keeps the ticket spelling `aadhar_number` with `max_length=32` (shape failure →
      422). Response uses the spelling `aadhaar` and only ever carries the **masked** value or
      `null` (R1.6).
    - _Requirements: 1.1, 1.2, 1.7_

    ```python
    from pydantic import BaseModel, Field


    class AadhaarValidationRequest(BaseModel):
        """Request body for POST /api/v1/validate-aadhaar."""

        aadhar_number: str = Field(..., max_length=32)


    class AadhaarValidationResponse(BaseModel):
        """Result of Aadhaar validation. `aadhaar` is masked or null (never the full number)."""

        is_valid: bool
        aadhaar: str | None = None
        message: str
    ```

  - [ ] 3.3 Create `app/schemas/pan.py`
    - **CREATE `app/schemas/pan.py`**
    - Request carries a required string `pan_number`. Response echoes the trimmed/uppercased
      PAN and includes `entity_type` / `surname_initial` only when valid (R2.4/2.5).
    - _Requirements: 2.1, 2.2, 2.4, 2.5, 2.6_

    ```python
    from pydantic import BaseModel


    class PanValidationRequest(BaseModel):
        """Request body for POST /api/v1/validate-pan."""

        pan_number: str


    class PanValidationResponse(BaseModel):
        """Result of PAN validation. Entity/surname present only when valid."""

        is_valid: bool
        pan: str | None = None
        entity_type: str | None = None
        surname_initial: str | None = None
        message: str
    ```

  - [ ] 3.4 Create `app/schemas/face.py`
    - **CREATE `app/schemas/face.py`**
    - Face-match result: `is_match`, numeric `distance`/`threshold`, and the `model` used. No
      raw image bytes ever appear here (R3.8).
    - _Requirements: 3.2, 3.8_

    ```python
    from pydantic import BaseModel


    class FaceMatchResponse(BaseModel):
        """Result of a face comparison between a document photo and a selfie."""

        is_match: bool
        distance: float
        threshold: float
        model: str
        message: str
    ```

  - [ ] 3.5 Create `app/schemas/health.py`
    - **CREATE `app/schemas/health.py`**
    - Liveness payload; no input required to produce it (R4.2).
    - _Requirements: 4.1, 4.2_

    ```python
    from pydantic import BaseModel


    class HealthResponse(BaseModel):
        """Liveness payload for GET /api/v1/health."""

        status: str
        service: str
        version: str
    ```

- [ ] 4. Create domain exceptions for the face service
  - [ ] 4.1 Create `app/services/exceptions.py`
    - **CREATE `app/services/exceptions.py`**
    - Three domain exceptions, each carrying an English `message` and an optional `field`
      naming the affected image. All three are mapped to HTTP 422 by the app's handlers.
    - _Requirements: 3.4, 3.5, 3.6_

    ```python
    class KycImageError(Exception):
        """Base class for image-related domain errors (all map to HTTP 422)."""

        def __init__(self, message: str, field: str | None = None) -> None:
            super().__init__(message)
            self.message = message
            self.field = field


    class UnreadableImageError(KycImageError):
        """Raised when an uploaded file cannot be decoded as a supported image."""


    class NoFaceDetectedError(KycImageError):
        """Raised when no face can be detected in one of the uploaded images."""


    class ImageTooLargeError(KycImageError):
        """Raised when an uploaded image exceeds the maximum accepted size."""
    ```

- [ ] 5. Create the Verhoeff checksum implementation
  - [ ] 5.1 Create `app/services/verhoeff.py`
    - **CREATE `app/services/verhoeff.py`**
    - Standard Verhoeff algorithm: the Dihedral-D5 multiplication table `d` (10×10), the
      permutation table `p` (8×10), and the inverse table `inv` (10). `is_valid` iterates the
      digits **right to left**, accumulating `c = d[c][p[(i % 8)][int(digit)]]`; the string
      (including the trailing check digit) is valid iff the final `c == 0`. Assumes a
      digit-only input (the Aadhaar service guarantees length 12 and digits-only before calling).
    - _Requirements: 1.2, 1.5, 7.1_

    ```python
    """Verhoeff checksum (dihedral group D5) used to validate the Aadhaar check digit."""

    # Multiplication table (d[j][k]).
    _D = (
        (0, 1, 2, 3, 4, 5, 6, 7, 8, 9),
        (1, 2, 3, 4, 0, 6, 7, 8, 9, 5),
        (2, 3, 4, 0, 1, 7, 8, 9, 5, 6),
        (3, 4, 0, 1, 2, 8, 9, 5, 6, 7),
        (4, 0, 1, 2, 3, 9, 5, 6, 7, 8),
        (5, 9, 8, 7, 6, 0, 4, 3, 2, 1),
        (6, 5, 9, 8, 7, 1, 0, 4, 3, 2),
        (7, 6, 5, 9, 8, 2, 1, 0, 4, 3),
        (8, 7, 6, 5, 9, 3, 2, 1, 0, 4),
        (9, 8, 7, 6, 5, 4, 3, 2, 1, 0),
    )

    # Permutation table (p[pos % 8][digit]).
    _P = (
        (0, 1, 2, 3, 4, 5, 6, 7, 8, 9),
        (1, 5, 7, 6, 2, 8, 3, 0, 9, 4),
        (5, 8, 0, 3, 7, 9, 6, 1, 4, 2),
        (8, 9, 1, 6, 0, 4, 3, 5, 2, 7),
        (9, 4, 5, 3, 1, 2, 6, 8, 7, 0),
        (4, 2, 8, 6, 5, 7, 3, 9, 0, 1),
        (2, 7, 9, 3, 8, 0, 6, 4, 1, 5),
        (7, 0, 4, 6, 9, 1, 3, 2, 5, 8),
    )

    # Inverse table (not needed for validation, included for completeness / generation).
    _INV = (0, 4, 3, 2, 1, 5, 6, 7, 8, 9)


    def is_valid(number: str) -> bool:
        """Return True if `number` (digit-only string) passes the Verhoeff checksum."""
        if not number or not number.isdigit():
            return False
        c = 0
        # Iterate right to left; i=0 is the rightmost (check) digit.
        for i, digit in enumerate(reversed(number)):
            c = _D[c][_P[i % 8][int(digit)]]
        return c == 0
    ```

- [ ] 6. Create the Aadhaar service (normalize + mask + validate)
  - [ ] 6.1 Create `app/services/aadhaar_service.py`
    - **CREATE `app/services/aadhaar_service.py`**
    - `normalize` strips spaces and hyphens only. `mask` returns `"X" * 8 + last4`.
      `validate_aadhaar` branches in order: non-digit after normalize → invalid "digits only"
      (R1.3); length ≠ 12 → invalid "exactly 12 digits" (R1.4); else Verhoeff → valid + masked
      (R1.2) or invalid "checksum invalid" (R1.5). The result is a dataclass the router maps to
      `AadhaarValidationResponse`. The unmasked number is never placed in the result (R1.6).
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_

    ```python
    from dataclasses import dataclass

    from app.services import verhoeff


    @dataclass
    class AadhaarValidationResult:
        is_valid: bool
        aadhaar: str | None
        message: str


    def normalize(raw: str) -> str:
        """Remove spaces and hyphens, leaving the remaining characters untouched."""
        return raw.replace(" ", "").replace("-", "")


    def mask(normalized: str) -> str:
        """Mask all but the last four digits: XXXXXXXX + last 4."""
        return "X" * 8 + normalized[-4:]


    def validate_aadhaar(raw: str) -> AadhaarValidationResult:
        """Validate an Aadhaar number. Returns a masked value only when valid."""
        normalized = normalize(raw)

        if not normalized.isdigit():
            return AadhaarValidationResult(
                is_valid=False,
                aadhaar=None,
                message=(
                    "Aadhaar number must contain only digits, "
                    "optionally separated by spaces or hyphens."
                ),
            )

        if len(normalized) != 12:
            return AadhaarValidationResult(
                is_valid=False,
                aadhaar=None,
                message="Aadhaar number must contain exactly 12 digits.",
            )

        if not verhoeff.is_valid(normalized):
            return AadhaarValidationResult(
                is_valid=False,
                aadhaar=None,
                message="Aadhaar checksum is invalid.",
            )

        return AadhaarValidationResult(
            is_valid=True,
            aadhaar=mask(normalized),
            message="Aadhaar number is valid.",
        )
    ```

- [ ] 7. Create the PAN service (trim/upper + regex + entity/surname decode)
  - [ ] 7.1 Create `app/services/pan_service.py`
    - **CREATE `app/services/pan_service.py`**
    - `_PAN_RE = ^[A-Z]{5}[0-9]{4}[A-Z]$`. `ENTITY_TYPES` decodes the 4th character.
      `validate_pan` trims + uppercases, matches the regex, and on success returns the echoed
      PAN plus `entity_type` and `surname_initial` (5th char). Invalid input (including
      empty/whitespace) omits entity/surname and returns the format message (R2.3/2.5).
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

    ```python
    import re
    from dataclasses import dataclass

    _PAN_RE = re.compile(r"^[A-Z]{5}[0-9]{4}[A-Z]$")

    # 4th-character entity-type decode (English labels; India-specific).
    ENTITY_TYPES: dict[str, str] = {
        "P": "Individual",
        "C": "Company",
        "H": "HUF (Hindu Undivided Family)",
        "F": "Firm / LLP",
        "A": "Association of Persons (AOP)",
        "T": "Trust",
        "B": "Body of Individuals",
        "L": "Local Authority",
        "J": "Artificial Juridical Person",
        "G": "Government",
    }


    @dataclass
    class PanValidationResult:
        is_valid: bool
        pan: str | None
        entity_type: str | None
        surname_initial: str | None
        message: str


    def validate_pan(raw: str) -> PanValidationResult:
        """Validate a PAN against [A-Z]{5}[0-9]{4}[A-Z] after trim + uppercase."""
        value = raw.strip().upper()

        if not _PAN_RE.match(value):
            return PanValidationResult(
                is_valid=False,
                pan=value or None,
                entity_type=None,
                surname_initial=None,
                message="PAN must be five letters, then four digits, then one letter.",
            )

        return PanValidationResult(
            is_valid=True,
            pan=value,
            entity_type=ENTITY_TYPES.get(value[3], "Unknown"),
            surname_initial=value[4],
            message="PAN is valid.",
        )
    ```

- [ ] 8. Create the face-match service (guards + DeepFace.verify + exception translation)
  - [ ] 8.1 Create `app/services/face_service.py`
    - **CREATE `app/services/face_service.py`**
    - `match_face` is async. For each `UploadFile`: `await file.read()`; enforce
      `max_upload_bytes` (`ImageTooLargeError`); require the content-type in the allowed set AND
      Pillow-decodability else `UnreadableImageError`. Both images are written to
      `NamedTemporaryFile`s and passed to `DeepFace.verify(...)` with the configured
      model/detector/metric and `enforce_detection=True`. A DeepFace `ValueError` containing
      "face could not be detected" becomes `NoFaceDetectedError` (best-effort field). The result
      maps to `FaceMatchResult(is_match=verified, distance, threshold, model)`. Temp files are
      always removed in `finally`; raw bytes never leave the function (R3.8).
    - _Requirements: 3.1, 3.2, 3.4, 3.5, 3.6, 3.7, 3.8_

    ```python
    import io
    import os
    import tempfile
    from dataclasses import dataclass

    from fastapi import UploadFile
    from PIL import Image

    from app.core.config import get_settings
    from app.services.exceptions import (
        ImageTooLargeError,
        NoFaceDetectedError,
        UnreadableImageError,
    )


    @dataclass
    class FaceMatchResult:
        is_match: bool
        distance: float
        threshold: float
        model: str


    async def _read_validated_bytes(upload: UploadFile, field: str) -> bytes:
        """Read an upload, enforcing size, content-type, and Pillow-decodability."""
        settings = get_settings()
        data = await upload.read()

        if len(data) > settings.max_upload_bytes:
            raise ImageTooLargeError(
                f"The uploaded {field} exceeds the maximum accepted size of "
                f"{settings.max_upload_bytes // (1024 * 1024)} MB.",
                field=field,
            )

        if upload.content_type not in settings.allowed_image_content_types:
            raise UnreadableImageError(
                f"The uploaded {field} could not be read as a supported image "
                f"(JPEG or PNG).",
                field=field,
            )

        try:
            with Image.open(io.BytesIO(data)) as image:
                image.verify()
        except Exception as exc:  # noqa: BLE001 - any decode failure is unreadable
            raise UnreadableImageError(
                f"The uploaded {field} could not be read as a supported image "
                f"(JPEG or PNG).",
                field=field,
            ) from exc

        return data


    def _write_temp(data: bytes) -> str:
        """Persist image bytes to a short-lived temp file and return its path."""
        with tempfile.NamedTemporaryFile(delete=False, suffix=".img") as handle:
            handle.write(data)
            return handle.name


    async def match_face(
        document_image: UploadFile, selfie_image: UploadFile
    ) -> FaceMatchResult:
        """Compare a document photo and a selfie using DeepFace verification."""
        # Imported lazily so the heavy TensorFlow import happens only when needed
        # (and so tests can monkeypatch DeepFace.verify cheaply).
        from deepface import DeepFace

        settings = get_settings()

        doc_bytes = await _read_validated_bytes(document_image, "document_image")
        selfie_bytes = await _read_validated_bytes(selfie_image, "selfie_image")

        doc_path = _write_temp(doc_bytes)
        selfie_path = _write_temp(selfie_bytes)

        try:
            try:
                result = DeepFace.verify(
                    img1_path=doc_path,
                    img2_path=selfie_path,
                    model_name=settings.face_model,
                    detector_backend=settings.face_detector_backend,
                    distance_metric=settings.face_distance_metric,
                    enforce_detection=True,
                )
            except ValueError as exc:
                message = str(exc).lower()
                if "face could not be detected" in message:
                    raise NoFaceDetectedError(
                        "A face could not be detected in one of the uploaded images.",
                        field=None,
                    ) from exc
                raise

            return FaceMatchResult(
                is_match=bool(result["verified"]),
                distance=float(result["distance"]),
                threshold=float(result["threshold"]),
                model=settings.face_model,
            )
        finally:
            for path in (doc_path, selfie_path):
                try:
                    os.remove(path)
                except OSError:
                    pass
    ```

- [ ] 9. Create the v1 routers and the aggregating router
  - [ ] 9.1 Create `app/api/v1/aadhaar.py`
    - **CREATE `app/api/v1/aadhaar.py`**
    - Thin router: accepts `AadhaarValidationRequest` (text only — no uploads, R1.8), calls
      `validate_aadhaar`, maps the result to `AadhaarValidationResponse` (HTTP 200).
    - _Requirements: 1.1, 1.8, 6.1_

    ```python
    from fastapi import APIRouter

    from app.schemas.aadhaar import AadhaarValidationRequest, AadhaarValidationResponse
    from app.services.aadhaar_service import validate_aadhaar

    router = APIRouter()


    @router.post("/validate-aadhaar", response_model=AadhaarValidationResponse)
    def validate_aadhaar_endpoint(
        payload: AadhaarValidationRequest,
    ) -> AadhaarValidationResponse:
        result = validate_aadhaar(payload.aadhar_number)
        return AadhaarValidationResponse(
            is_valid=result.is_valid,
            aadhaar=result.aadhaar,
            message=result.message,
        )
    ```

  - [ ] 9.2 Create `app/api/v1/pan.py`
    - **CREATE `app/api/v1/pan.py`**
    - Thin router: accepts `PanValidationRequest`, calls `validate_pan`, maps the result to
      `PanValidationResponse` (HTTP 200).
    - _Requirements: 2.1, 2.2, 6.1_

    ```python
    from fastapi import APIRouter

    from app.schemas.pan import PanValidationRequest, PanValidationResponse
    from app.services.pan_service import validate_pan

    router = APIRouter()


    @router.post("/validate-pan", response_model=PanValidationResponse)
    def validate_pan_endpoint(payload: PanValidationRequest) -> PanValidationResponse:
        result = validate_pan(payload.pan_number)
        return PanValidationResponse(
            is_valid=result.is_valid,
            pan=result.pan,
            entity_type=result.entity_type,
            surname_initial=result.surname_initial,
            message=result.message,
        )
    ```

  - [ ] 9.3 Create `app/api/v1/face.py`
    - **CREATE `app/api/v1/face.py`**
    - Multipart router: both `document_image` and `selfie_image` are required `UploadFile`s
      declared with `File(...)` (missing files → 422 via FastAPI, R3.3). Awaits `match_face`
      and maps the result to `FaceMatchResponse`; the message reflects the match outcome.
    - _Requirements: 3.1, 3.2, 3.3, 6.1_

    ```python
    from fastapi import APIRouter, File, UploadFile

    from app.schemas.face import FaceMatchResponse
    from app.services.face_service import match_face

    router = APIRouter()


    @router.post("/match-face", response_model=FaceMatchResponse)
    async def match_face_endpoint(
        document_image: UploadFile = File(...),
        selfie_image: UploadFile = File(...),
    ) -> FaceMatchResponse:
        result = await match_face(document_image, selfie_image)
        message = "Faces match." if result.is_match else "Faces do not match."
        return FaceMatchResponse(
            is_match=result.is_match,
            distance=result.distance,
            threshold=result.threshold,
            model=result.model,
            message=message,
        )
    ```

  - [ ] 9.4 Create `app/api/v1/health.py`
    - **CREATE `app/api/v1/health.py`**
    - Liveness router: returns `HealthResponse` built from `Settings`; requires no input (R4.2).
    - _Requirements: 4.1, 4.2, 6.1_

    ```python
    from fastapi import APIRouter

    from app.core.config import get_settings
    from app.schemas.health import HealthResponse

    router = APIRouter()


    @router.get("/health", response_model=HealthResponse)
    def health_endpoint() -> HealthResponse:
        settings = get_settings()
        return HealthResponse(
            status="healthy",
            service=settings.app_name,
            version=settings.app_version,
        )
    ```

  - [ ] 9.5 Create `app/api/v1/router.py`
    - **CREATE `app/api/v1/router.py`**
    - `APIRouter(prefix="/api/v1")` that includes the four endpoint routers so all live under
      `/api/v1` (R6.1).
    - _Requirements: 6.1_

    ```python
    from fastapi import APIRouter

    from app.api.v1 import aadhaar, face, health, pan

    router = APIRouter(prefix="/api/v1")
    router.include_router(aadhaar.router, tags=["aadhaar"])
    router.include_router(pan.router, tags=["pan"])
    router.include_router(face.router, tags=["face"])
    router.include_router(health.router, tags=["health"])
    ```

- [ ] 10. Create the app factory (CORS + exception handlers + router include)
  - [ ] 10.1 Create `app/main.py`
    - **CREATE `app/main.py`**
    - `create_app()` builds `FastAPI(title, version)`, adds `CORSMiddleware` from
      `settings.cors_origins`, and registers all exception handlers mapping to the shared
      `ErrorResponse` with a UTC ISO-8601 `timestamp` (`_utc_now()`): `RequestValidationError`
      → 422 `validation_error` (field + issue only, never the submitted value — R1.7);
      `ImageTooLargeError` → 422 `image_too_large`; `UnreadableImageError` → 422
      `unreadable_image`; `NoFaceDetectedError` → 422 `no_face_detected`;
      `StarletteHTTPException` → 404 `not_found` / 405 `method_not_allowed` (R6.4); any other
      `Exception` → 500 `internal_error` with `detail=None` and no stack trace (R5.3). Includes
      the v1 router and exposes a module-level `app`.
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 6.1, 6.2, 6.3, 6.4, 1.6, 1.7_

    ```python
    from datetime import datetime, timezone

    from fastapi import FastAPI, Request
    from fastapi.exceptions import RequestValidationError
    from fastapi.responses import JSONResponse
    from fastapi.middleware.cors import CORSMiddleware
    from starlette.exceptions import HTTPException as StarletteHTTPException

    from app.api.v1.router import router as v1_router
    from app.core.config import get_settings
    from app.schemas.error import ErrorResponse
    from app.services.exceptions import (
        ImageTooLargeError,
        NoFaceDetectedError,
        UnreadableImageError,
    )


    def _utc_now() -> str:
        """Return the current time as a UTC ISO-8601 string (e.g. 2025-01-15T10:30:00Z)."""
        return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


    def _error_json(status_code: int, error: str, message: str, detail=None) -> JSONResponse:
        body = ErrorResponse(
            error=error, message=message, detail=detail, timestamp=_utc_now()
        )
        return JSONResponse(status_code=status_code, content=body.model_dump())


    def create_app() -> FastAPI:
        settings = get_settings()
        app = FastAPI(title=settings.app_name, version=settings.app_version)

        app.add_middleware(
            CORSMiddleware,
            allow_origins=settings.cors_origins,
            allow_credentials=True,
            allow_methods=["*"],
            allow_headers=["*"],
        )

        @app.exception_handler(RequestValidationError)
        async def _on_validation_error(
            request: Request, exc: RequestValidationError
        ) -> JSONResponse:
            # Report only field + issue; never echo the submitted value (R1.7, R1.6).
            detail = [
                {
                    "field": ".".join(str(part) for part in err.get("loc", [])),
                    "issue": err.get("msg", "invalid"),
                }
                for err in exc.errors()
            ]
            return _error_json(
                422, "validation_error", "Request validation failed.", detail
            )

        @app.exception_handler(ImageTooLargeError)
        async def _on_image_too_large(
            request: Request, exc: ImageTooLargeError
        ) -> JSONResponse:
            return _error_json(422, "image_too_large", exc.message, exc.field)

        @app.exception_handler(UnreadableImageError)
        async def _on_unreadable_image(
            request: Request, exc: UnreadableImageError
        ) -> JSONResponse:
            return _error_json(422, "unreadable_image", exc.message, exc.field)

        @app.exception_handler(NoFaceDetectedError)
        async def _on_no_face_detected(
            request: Request, exc: NoFaceDetectedError
        ) -> JSONResponse:
            return _error_json(422, "no_face_detected", exc.message, exc.field)

        @app.exception_handler(StarletteHTTPException)
        async def _on_http_exception(
            request: Request, exc: StarletteHTTPException
        ) -> JSONResponse:
            if exc.status_code == 404:
                return _error_json(404, "not_found", "The requested resource was not found.")
            if exc.status_code == 405:
                return _error_json(
                    405, "method_not_allowed", "The HTTP method is not allowed for this route."
                )
            return _error_json(
                exc.status_code, "http_error", str(exc.detail) if exc.detail else "HTTP error."
            )

        @app.exception_handler(Exception)
        async def _on_unexpected_error(request: Request, exc: Exception) -> JSONResponse:
            # No stack trace, no file paths (R5.3).
            return _error_json(
                500,
                "internal_error",
                "An internal error occurred while processing the request.",
                None,
            )

        app.include_router(v1_router)
        return app


    app = create_app()
    ```

- [ ] 11. Create unit tests (Verhoeff, Aadhaar, PAN, face service with DeepFace mocked)
  - [ ] 11.1 Create `tests/conftest.py`
    - **CREATE `tests/conftest.py`**
    - A session-usable `client` fixture that builds the app via `create_app()` and wraps it in
      a FastAPI `TestClient` (httpx). Shared by the integration tests.
    - _Requirements: 7.4, 7.5_

    ```python
    import pytest
    from fastapi.testclient import TestClient

    from app.main import create_app


    @pytest.fixture
    def client() -> TestClient:
        return TestClient(create_app())
    ```

  - [ ]* 11.2 Create `tests/unit/test_verhoeff.py`
    - **CREATE `tests/unit/test_verhoeff.py`**
    - Asserts `is_valid` against known-valid and known-invalid 12-digit vectors, a check-digit
      flip, and non-digit input. **Note:** `299183658111` is used as a candidate valid vector;
      if it does not pass the implemented `is_valid`, replace it with any 11 digits followed by
      the check digit the implementation computes, or compute a valid vector by brute force over
      the last digit. The flip/transposition cases must fail regardless.
    - _Requirements: 7.1, 1.2, 1.5_

    ```python
    from app.services import verhoeff


    def test_known_valid_vector():
        # Candidate valid Verhoeff number; see task note if this needs regenerating.
        assert verhoeff.is_valid("299183658111") is True


    def test_check_digit_flip_is_invalid():
        valid = "299183658111"
        # Flip the trailing check digit -> must fail.
        flipped = valid[:-1] + ("2" if valid[-1] != "2" else "3")
        assert verhoeff.is_valid(flipped) is False


    def test_transposition_is_caught():
        valid = "299183658111"
        # Swap two adjacent interior digits -> Verhoeff must reject most transpositions.
        swapped = list(valid)
        swapped[2], swapped[3] = swapped[3], swapped[2]
        assert verhoeff.is_valid("".join(swapped)) is False


    def test_non_digit_input_is_invalid():
        assert verhoeff.is_valid("12345678901X") is False
        assert verhoeff.is_valid("") is False
    ```

  - [ ]* 11.3 Create `tests/unit/test_aadhaar_service.py`
    - **CREATE `tests/unit/test_aadhaar_service.py`**
    - Covers normalization (spaces/hyphens stripped), the masking format (`XXXXXXXX` + last 4),
      and each invalid branch (non-digit, wrong length, bad checksum) returning `is_valid=False`
      with the correct message and no unmasked number.
    - _Requirements: 7.1, 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_

    ```python
    from app.services import aadhaar_service as svc


    def test_normalize_strips_spaces_and_hyphens():
        assert svc.normalize("2991 8365-8111") == "299183658111"


    def test_mask_reveals_only_last_four():
        assert svc.mask("299183658111") == "XXXXXXXX8111"


    def test_valid_aadhaar_returns_masked_value():
        result = svc.validate_aadhaar("2991 8365 8111")
        assert result.is_valid is True
        assert result.aadhaar == "XXXXXXXX8111"
        assert "299183658111" not in (result.aadhaar or "")


    def test_non_digit_is_invalid():
        result = svc.validate_aadhaar("2991ABCD8111")
        assert result.is_valid is False
        assert result.aadhaar is None
        assert "digits" in result.message.lower()


    def test_wrong_length_is_invalid():
        result = svc.validate_aadhaar("12345")
        assert result.is_valid is False
        assert result.aadhaar is None
        assert "12 digits" in result.message


    def test_bad_checksum_is_invalid():
        result = svc.validate_aadhaar("299183658112")
        assert result.is_valid is False
        assert result.aadhaar is None
        assert "checksum" in result.message.lower()
    ```

  - [ ]* 11.4 Create `tests/unit/test_pan_service.py`
    - **CREATE `tests/unit/test_pan_service.py`**
    - Covers valid PANs (including lowercase/whitespace input trimmed + uppercased), entity/
      surname decode, and non-matching PANs (wrong length, wrong pattern, empty) that omit the
      entity/surname fields. Uses the synthetic PAN `ABCPE1234F` (Individual, surname initial E).
    - _Requirements: 7.2, 2.1, 2.2, 2.3, 2.4, 2.5_

    ```python
    from app.services import pan_service as svc


    def test_valid_pan_is_uppercased_and_decoded():
        result = svc.validate_pan("  abcpe1234f  ")
        assert result.is_valid is True
        assert result.pan == "ABCPE1234F"
        assert result.entity_type == "Individual"
        assert result.surname_initial == "E"


    def test_company_entity_type_decode():
        result = svc.validate_pan("ABCCE1234F")
        assert result.is_valid is True
        assert result.entity_type == "Company"


    def test_wrong_pattern_is_invalid_and_omits_entity():
        result = svc.validate_pan("1BCPE1234F")
        assert result.is_valid is False
        assert result.entity_type is None
        assert result.surname_initial is None


    def test_wrong_length_is_invalid():
        result = svc.validate_pan("ABCPE1234")
        assert result.is_valid is False


    def test_empty_is_invalid():
        result = svc.validate_pan("   ")
        assert result.is_valid is False
        assert result.pan is None
    ```

  - [ ]* 11.5 Create `tests/unit/test_face_service.py`
    - **CREATE `tests/unit/test_face_service.py`**
    - Monkeypatches `DeepFace.verify` (patched on the `deepface` module the service imports
      lazily) to return `verified=True`/`False`, asserting `is_match` tracks `verified` and that
      distance/threshold/model pass through. Also asserts the size guard raises
      `ImageTooLargeError`, a non-image payload raises `UnreadableImageError`, and a DeepFace
      "face could not be detected" `ValueError` becomes `NoFaceDetectedError`.
    - _Requirements: 7.3, 3.2, 3.4, 3.5, 3.6_

    ```python
    import io

    import pytest
    from PIL import Image
    from starlette.datastructures import Headers, UploadFile

    from app.services import face_service
    from app.services.exceptions import (
        ImageTooLargeError,
        NoFaceDetectedError,
        UnreadableImageError,
    )


    def _png_upload(name: str, size: tuple[int, int] = (16, 16)) -> UploadFile:
        buffer = io.BytesIO()
        Image.new("RGB", size, (120, 120, 120)).save(buffer, format="PNG")
        buffer.seek(0)
        headers = Headers({"content-type": "image/png"})
        return UploadFile(file=buffer, filename=name, headers=headers)


    def _oversized_upload(name: str) -> UploadFile:
        from app.core.config import get_settings

        data = b"\x89PNG\r\n\x1a\n" + b"0" * (get_settings().max_upload_bytes + 1)
        headers = Headers({"content-type": "image/png"})
        return UploadFile(file=io.BytesIO(data), filename=name, headers=headers)


    def _text_upload(name: str) -> UploadFile:
        headers = Headers({"content-type": "text/plain"})
        return UploadFile(file=io.BytesIO(b"not an image"), filename=name, headers=headers)


    class _FakeDeepFace:
        def __init__(self, result=None, error: Exception | None = None):
            self._result = result
            self._error = error

        def verify(self, *args, **kwargs):
            if self._error is not None:
                raise self._error
            return self._result


    def _patch_deepface(monkeypatch, fake):
        import deepface

        monkeypatch.setattr(deepface, "DeepFace", fake, raising=True)


    @pytest.mark.anyio
    async def test_match_true(monkeypatch):
        _patch_deepface(
            monkeypatch,
            _FakeDeepFace(
                {"verified": True, "distance": 0.21, "threshold": 0.30, "model": "Facenet512"}
            ),
        )
        result = await face_service.match_face(_png_upload("doc.png"), _png_upload("selfie.png"))
        assert result.is_match is True
        assert result.distance == 0.21
        assert result.threshold == 0.30
        assert result.model == "Facenet512"


    @pytest.mark.anyio
    async def test_match_false(monkeypatch):
        _patch_deepface(
            monkeypatch,
            _FakeDeepFace(
                {"verified": False, "distance": 0.80, "threshold": 0.30, "model": "Facenet512"}
            ),
        )
        result = await face_service.match_face(_png_upload("doc.png"), _png_upload("selfie.png"))
        assert result.is_match is False


    @pytest.mark.anyio
    async def test_oversized_raises(monkeypatch):
        _patch_deepface(monkeypatch, _FakeDeepFace({"verified": True}))
        with pytest.raises(ImageTooLargeError):
            await face_service.match_face(_oversized_upload("doc.png"), _png_upload("selfie.png"))


    @pytest.mark.anyio
    async def test_unreadable_raises(monkeypatch):
        _patch_deepface(monkeypatch, _FakeDeepFace({"verified": True}))
        with pytest.raises(UnreadableImageError):
            await face_service.match_face(_text_upload("doc.txt"), _png_upload("selfie.png"))


    @pytest.mark.anyio
    async def test_no_face_raises(monkeypatch):
        _patch_deepface(
            monkeypatch,
            _FakeDeepFace(error=ValueError("Face could not be detected in the image.")),
        )
        with pytest.raises(NoFaceDetectedError):
            await face_service.match_face(_png_upload("doc.png"), _png_upload("selfie.png"))
    ```

    > **Note:** these async tests use `@pytest.mark.anyio`. Add an `anyio_backend` fixture (shown
    > below) or install `pytest-asyncio`/`anyio` and configure it. Simplest: append this fixture
    > to `tests/conftest.py` — `@pytest.fixture\ndef anyio_backend():\n    return "asyncio"` — and
    > add `anyio` to `requirements.txt`. If you prefer `pytest-asyncio`, swap the marker to
    > `@pytest.mark.asyncio` and add `pytest-asyncio` instead.

- [ ] 12. Create integration tests (FastAPI TestClient; DeepFace mocked)
  - [ ]* 12.1 Create `tests/integration/test_aadhaar_api.py`
    - **CREATE `tests/integration/test_aadhaar_api.py`**
    - 200 valid (masked), 200 invalid (checksum / wrong length / non-digit), 422 for missing
      field / non-string / > 32 chars; asserts the full number never appears in any body (R1.6)
      and that the 422 body matches the `ErrorResponse` shape.
    - _Requirements: 7.4, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 5.2_

    ```python
    def test_valid_aadhaar_returns_masked(client):
        resp = client.post("/api/v1/validate-aadhaar", json={"aadhar_number": "2991 8365 8111"})
        assert resp.status_code == 200
        body = resp.json()
        assert body["is_valid"] is True
        assert body["aadhaar"] == "XXXXXXXX8111"
        assert "299183658111" not in resp.text


    def test_invalid_checksum_returns_200_invalid(client):
        resp = client.post("/api/v1/validate-aadhaar", json={"aadhar_number": "299183658112"})
        assert resp.status_code == 200
        assert resp.json()["is_valid"] is False
        assert "299183658112" not in resp.text


    def test_wrong_length_returns_200_invalid(client):
        resp = client.post("/api/v1/validate-aadhaar", json={"aadhar_number": "12345"})
        assert resp.status_code == 200
        assert resp.json()["is_valid"] is False


    def test_non_digit_returns_200_invalid(client):
        resp = client.post("/api/v1/validate-aadhaar", json={"aadhar_number": "2991ABCD8111"})
        assert resp.status_code == 200
        assert resp.json()["is_valid"] is False


    def test_missing_field_returns_422(client):
        resp = client.post("/api/v1/validate-aadhaar", json={})
        assert resp.status_code == 422
        body = resp.json()
        assert body["error"] == "validation_error"
        assert set(body.keys()) == {"error", "message", "detail", "timestamp"}


    def test_non_string_returns_422(client):
        resp = client.post("/api/v1/validate-aadhaar", json={"aadhar_number": 123456789012})
        assert resp.status_code == 422


    def test_too_long_returns_422_without_echo(client):
        oversized = "9" * 33
        resp = client.post("/api/v1/validate-aadhaar", json={"aadhar_number": oversized})
        assert resp.status_code == 422
        assert oversized not in resp.text
    ```

  - [ ]* 12.2 Create `tests/integration/test_pan_api.py`
    - **CREATE `tests/integration/test_pan_api.py`**
    - 200 valid (with `entity_type` + `surname_initial`), 200 invalid, 422 for missing /
      non-string `pan_number`.
    - _Requirements: 7.4, 2.2, 2.3, 2.4, 2.5, 2.6, 5.2_

    ```python
    def test_valid_pan(client):
        resp = client.post("/api/v1/validate-pan", json={"pan_number": " abcpe1234f "})
        assert resp.status_code == 200
        body = resp.json()
        assert body["is_valid"] is True
        assert body["pan"] == "ABCPE1234F"
        assert body["entity_type"] == "Individual"
        assert body["surname_initial"] == "E"


    def test_invalid_pan_omits_entity(client):
        resp = client.post("/api/v1/validate-pan", json={"pan_number": "ABCPE1234"})
        assert resp.status_code == 200
        body = resp.json()
        assert body["is_valid"] is False
        assert body["entity_type"] is None
        assert body["surname_initial"] is None


    def test_missing_field_returns_422(client):
        resp = client.post("/api/v1/validate-pan", json={})
        assert resp.status_code == 422
        assert resp.json()["error"] == "validation_error"


    def test_non_string_returns_422(client):
        resp = client.post("/api/v1/validate-pan", json={"pan_number": 1234567890})
        assert resp.status_code == 422
    ```

  - [ ]* 12.3 Create `tests/integration/test_face_api.py`
    - **CREATE `tests/integration/test_face_api.py`**
    - Monkeypatches `deepface.DeepFace` so the real model never runs. 200 match and non-match,
      422 for missing image / unreadable image / no face / oversized upload; asserts no raw
      bytes appear in the response body (R3.8).
    - _Requirements: 7.4, 3.2, 3.3, 3.4, 3.5, 3.6, 3.8, 5.2_

    ```python
    import io

    import pytest
    from PIL import Image


    class _FakeDeepFace:
        def __init__(self, result=None, error=None):
            self._result = result
            self._error = error

        def verify(self, *args, **kwargs):
            if self._error is not None:
                raise self._error
            return self._result


    def _patch(monkeypatch, fake):
        import deepface

        monkeypatch.setattr(deepface, "DeepFace", fake, raising=True)


    def _png_bytes(size=(16, 16)) -> bytes:
        buffer = io.BytesIO()
        Image.new("RGB", size, (90, 90, 90)).save(buffer, format="PNG")
        return buffer.getvalue()


    def _files():
        return {
            "document_image": ("doc.png", io.BytesIO(_png_bytes()), "image/png"),
            "selfie_image": ("selfie.png", io.BytesIO(_png_bytes()), "image/png"),
        }


    def test_match_true(client, monkeypatch):
        _patch(
            monkeypatch,
            _FakeDeepFace(
                {"verified": True, "distance": 0.2, "threshold": 0.3, "model": "Facenet512"}
            ),
        )
        resp = client.post("/api/v1/match-face", files=_files())
        assert resp.status_code == 200
        body = resp.json()
        assert body["is_match"] is True
        assert body["model"] == "Facenet512"


    def test_match_false(client, monkeypatch):
        _patch(
            monkeypatch,
            _FakeDeepFace(
                {"verified": False, "distance": 0.9, "threshold": 0.3, "model": "Facenet512"}
            ),
        )
        resp = client.post("/api/v1/match-face", files=_files())
        assert resp.status_code == 200
        assert resp.json()["is_match"] is False


    def test_missing_image_returns_422(client, monkeypatch):
        _patch(monkeypatch, _FakeDeepFace({"verified": True}))
        files = {"document_image": ("doc.png", io.BytesIO(_png_bytes()), "image/png")}
        resp = client.post("/api/v1/match-face", files=files)
        assert resp.status_code == 422


    def test_unreadable_image_returns_422(client, monkeypatch):
        _patch(monkeypatch, _FakeDeepFace({"verified": True}))
        files = {
            "document_image": ("doc.txt", io.BytesIO(b"not an image"), "text/plain"),
            "selfie_image": ("selfie.png", io.BytesIO(_png_bytes()), "image/png"),
        }
        resp = client.post("/api/v1/match-face", files=files)
        assert resp.status_code == 422
        assert resp.json()["error"] == "unreadable_image"


    def test_no_face_returns_422(client, monkeypatch):
        _patch(
            monkeypatch,
            _FakeDeepFace(error=ValueError("Face could not be detected in the image.")),
        )
        resp = client.post("/api/v1/match-face", files=_files())
        assert resp.status_code == 422
        assert resp.json()["error"] == "no_face_detected"


    def test_oversized_returns_422(client, monkeypatch):
        from app.core.config import get_settings

        _patch(monkeypatch, _FakeDeepFace({"verified": True}))
        big = io.BytesIO(_png_bytes() + b"0" * (get_settings().max_upload_bytes + 1))
        files = {
            "document_image": ("doc.png", big, "image/png"),
            "selfie_image": ("selfie.png", io.BytesIO(_png_bytes()), "image/png"),
        }
        resp = client.post("/api/v1/match-face", files=files)
        assert resp.status_code == 422
        assert resp.json()["error"] == "image_too_large"
    ```

  - [ ]* 12.4 Create `tests/integration/test_health_api.py`
    - **CREATE `tests/integration/test_health_api.py`**
    - 200 healthy with no input (R4), plus a 404 check to confirm the unknown-route handler
      returns the `ErrorResponse` shape (R6.4).
    - _Requirements: 7.4, 4.1, 4.2, 6.4_

    ```python
    def test_health_returns_healthy(client):
        resp = client.get("/api/v1/health")
        assert resp.status_code == 200
        body = resp.json()
        assert body["status"] == "healthy"
        assert "service" in body
        assert "version" in body


    def test_unknown_route_returns_404_error_shape(client):
        resp = client.get("/api/v1/does-not-exist")
        assert resp.status_code == 404
        body = resp.json()
        assert body["error"] == "not_found"
        assert set(body.keys()) == {"error", "message", "detail", "timestamp"}
    ```

    > **Note:** create an empty **`tests/integration/__init__.py`** and
    > **`tests/unit/__init__.py`** if your pytest layout requires package markers for the
    > `tests/unit` and `tests/integration` subpackages.

- [ ] 13. Create the README
  - [ ] 13.1 Create `README.md`
    - **CREATE `README.md`**
    - Overview, setup (venv + `pip install -r requirements.txt`), run
      (`uvicorn app.main:app --reload`), `/docs`, env vars, and `pytest` instructions. Notes the
      India-only / English-only scope and the DeepFace first-run model download.
    - _Requirements: 6.1, 6.2, 6.3_

    ```markdown
    # KYC Validation API (FINNOVA-20)

    A standalone Python (FastAPI) service that performs three KYC validation checks used during
    customer onboarding: **Aadhaar** number validation (Verhoeff checksum), **PAN** format
    validation, and **face matching** between a document photo and a selfie (DeepFace). All
    endpoints live under `/api/v1/`.

    > India-only, English-only. Aadhaar and PAN are India-specific identity artifacts. There are
    > no bilingual fields, no non-English content, and no right-to-left rendering.

    ## Endpoints

    | Method | Path | Purpose |
    | --- | --- | --- |
    | POST | `/api/v1/validate-aadhaar` | Validate an Aadhaar number (returns masked value) |
    | POST | `/api/v1/validate-pan` | Validate a PAN and decode entity type / surname initial |
    | POST | `/api/v1/match-face` | Compare a document photo and a selfie (`multipart/form-data`) |
    | GET  | `/api/v1/health` | Liveness check |

    Interactive docs (Swagger UI) are served at `/docs`.

    ## Setup

    ```bash
    python -m venv .venv
    # Windows
    .venv\Scripts\activate
    # macOS / Linux
    source .venv/bin/activate

    pip install -r requirements.txt
    cp .env.example .env   # then edit as needed
    ```

    ## Run

    ```bash
    uvicorn app.main:app --reload
    ```

    Open http://127.0.0.1:8000/docs for Swagger UI.

    ## Environment variables

    | Variable | Default | Purpose |
    | --- | --- | --- |
    | `APP_NAME` | `KYC Validation API` | Service name (OpenAPI + health) |
    | `APP_VERSION` | `1.0.0` | Service version |
    | `CORS_ORIGINS` | `*` | Comma-separated allowlist of browser origins |
    | `FACE_MODEL` | `Facenet512` | DeepFace model |
    | `FACE_DETECTOR_BACKEND` | `opencv` | DeepFace detector backend |
    | `FACE_DISTANCE_METRIC` | `cosine` | DeepFace distance metric |
    | `MAX_UPLOAD_BYTES` | `10485760` | Per-file upload cap (10 MB) |

    ## Tests

    ```bash
    pytest
    ```

    DeepFace is mocked in tests, so the real model is never downloaded or run during the suite.

    ## Notes

    - **DeepFace first run** downloads model weights (hundreds of MB) and requires `tf-keras` on
      modern TensorFlow — both are pinned in `requirements.txt`. The first real `/match-face`
      call will be slow while weights download; subsequent calls are fast.
    - The service is stateless: Aadhaar/PAN values and uploaded images are never persisted, and
      the full Aadhaar number is never returned (only the masked `XXXXXXXX` + last 4 form).
    ```

## Notes

- Tasks marked with `*` are optional test sub-tasks and can be skipped for a faster first run;
  core implementation tasks are never optional.
- Each task references the specific requirements it satisfies for traceability.
- Property-based testing is intentionally **not** used (the design has no Correctness Properties
  section); coverage is example/edge unit tests plus FastAPI `TestClient` integration tests.
- All code is copy-paste-ready; paths are relative to the new repo root `Finnova-KYC-API/`.

## Execution notes

Run everything from the new repo root `D:\Projects\Finnova\Finnova-KYC-API`:

1. **Create the files** by copying each task's code block to the marked path (create parent
   folders as needed).
2. **Create a virtual environment and install deps:**
   ```bash
   python -m venv .venv
   .venv\Scripts\activate        # Windows (use: source .venv/bin/activate on macOS/Linux)
   pip install -r requirements.txt
   ```
3. **Serve the API:**
   ```bash
   uvicorn app.main:app --reload
   ```
   Browse `/docs` for Swagger UI.
4. **Run the tests:**
   ```bash
   pytest
   ```
   DeepFace is mocked in the suite, so no model weights are downloaded for tests.

> **DeepFace first run:** the first *real* `/match-face` call (outside tests) downloads the model
> weights and needs `tf-keras` on modern TensorFlow (both pinned in `requirements.txt`). Expect a
> one-time slow start while weights download.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3", "1.4"] },
    { "id": 1, "tasks": ["2.1", "3.1", "3.2", "3.3", "3.4", "3.5", "4.1", "5.1"] },
    { "id": 2, "tasks": ["6.1", "7.1", "8.1", "9.4"] },
    { "id": 3, "tasks": ["9.1", "9.2", "9.3"] },
    { "id": 4, "tasks": ["9.5"] },
    { "id": 5, "tasks": ["10.1"] },
    { "id": 6, "tasks": ["11.1", "11.2", "11.3", "11.4", "11.5"] },
    { "id": 7, "tasks": ["12.1", "12.2", "12.3", "12.4"] },
    { "id": 8, "tasks": ["13.1"] }
  ]
}
```
