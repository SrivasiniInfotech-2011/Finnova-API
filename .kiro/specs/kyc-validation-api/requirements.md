# Requirements Document

## Introduction

The KYC Validation API feature (JIRA: FINNOVA-20, "Create Api for Aadhaar, PAN and Face Validation") provides a **standalone Python service** that performs three Know-Your-Customer (KYC) validation checks used during customer onboarding and verification: Aadhaar number validation, PAN number validation, and face matching between a document photo and a selfie. The service exposes a small, versioned HTTP API that consuming applications (including the Finnova platform) call to validate identity data without each caller re-implementing the validation rules.

Unlike the rest of the Finnova backend, this feature is delivered as a **new, standalone Python project** rather than a module inside the existing .NET solution. This is a deliberate, confirmed decision for FINNOVA-20: the required capabilities (Verhoeff checksum validation, PAN format rules, and deep-learning face matching via the DeepFace library) are most naturally expressed in the Python ecosystem. The project is built with **FastAPI + Pydantic** and organized in a conventional layered layout (`app/` with routers, services, schemas, and core/config; `tests/` with pytest). All endpoints live under the `/api/v1/` prefix.

The three validation capabilities are:

- **Aadhaar validation** — normalizes the input (strips spaces and hyphens), confirms the value is exactly 12 digits, and validates the trailing check digit using the **Verhoeff checksum algorithm**. The response masks the Aadhaar number so the full value is never echoed back. No OCR, QR parsing, DigiLocker, or image processing is performed for Aadhaar.
- **PAN validation** — validates the 10-character PAN against the format pattern `[A-Z]{5}[0-9]{4}[A-Z]`, accepting case-insensitive input and uppercasing it. Optionally, the decoded 4th-character entity type and the 5th-character surname initial are returned as informational fields. The core rule is format validity. No OCR is performed.
- **Face matching** — accepts two uploaded images (a document photo and a selfie) over `multipart/form-data`, runs face matching using the **DeepFace** library's verification, and returns whether the faces match, a distance/similarity score, and the model and threshold used. No live webcam capture is performed; this is a server-side API.

This document captures the requirements derived from the ticket and the confirmed lean scope. The reference implementations (`D:\Projects\Finnova\Finnova-KYC-Verifier` for FastAPI/face-match structure and the Verhoeff-based `github.com/toamitrawat/aadhar-validator`) inform the shape of the solution; the webcam-based capture in the reference is explicitly replaced with two uploaded images because live capture is unsuitable for a server API.

### Dependencies and Cross-Cutting Assumptions

- **Decision (Standalone project):** This feature is a new, independent Python project intended to live at the sibling path `D:\Projects\Finnova\Finnova-KYC-API`. It is NOT added to the `Finnova.Backend.slnx` .NET solution and NOT placed in the Finnova-UI repository. This is a firm, confirmed decision for this ticket, not an open assumption.
- **Decision (Framework and layout):** The project uses FastAPI, Pydantic, and pytest, laid out as `app/` (routers, services, schemas, `core/config`) and `tests/`. Interactive API documentation is served by FastAPI at `/docs` (Swagger UI) and generated automatically from the route and schema definitions.
- **Decision (Face-matching library):** Face matching uses the **DeepFace** library's verification function. The specific model and distance metric are selected in design; the response always reports which model and threshold produced the result.
- **Decision (No live capture):** The service performs no webcam or camera capture. Face matching operates only on the two images supplied in the request. This replaces the `cv2.VideoCapture` approach seen in the reference implementation.
- **Decision (Consumer and auth scope):** FINNOVA-20 scope is limited to the three validation capabilities plus a health check. Request authentication/authorization (API keys, JWT) is NOT part of this ticket; it is flagged for confirmation in design. CORS is enabled so browser-based callers can reach the API.
- **Note (Localization):** Finnova targets the **India** market and is **English-only**. All field names, error messages, and documentation are in English. There are no bilingual fields, no Arabic or other non-English content, and no right-to-left rendering. Aadhaar and PAN are India-specific identity artifacts.
- **Note (Data handling):** The service validates data in-request and does not persist Aadhaar numbers, PAN numbers, or uploaded images beyond the lifetime of the request. The full Aadhaar number is never returned in a response; only a masked form is returned.

## Glossary

- **KYC**: Know Your Customer; the set of identity-verification checks performed during customer onboarding.
- **KYC_API**: The standalone Python (FastAPI) service that exposes the Aadhaar, PAN, face-match, and health endpoints described in this document.
- **Aadhaar Number**: A 12-digit identification number issued by the Unique Identification Authority of India (UIDAI). In requests it may include separating spaces or hyphens.
- **Verhoeff Checksum**: The checksum algorithm (based on the dihedral group D5) used to validate the trailing check digit of an Aadhaar number.
- **Normalized Aadhaar**: The Aadhaar Number after removing all spaces and hyphens, leaving only digit characters.
- **Masked Aadhaar**: The Aadhaar Number with all but the last four digits replaced by a mask character (for example, `XXXXXXXX9012`).
- **PAN**: Permanent Account Number; a 10-character alphanumeric identifier issued by the Indian Income Tax Department, matching the pattern `[A-Z]{5}[0-9]{4}[A-Z]`.
- **PAN Entity Type**: The category of PAN holder encoded by the 4th character of a PAN (for example, `P` for an individual person).
- **Surname Initial**: The first letter of the PAN holder's surname, encoded by the 5th character of a PAN.
- **Document Photo**: An uploaded image of the face printed on an identity document, supplied to the face-match endpoint.
- **Selfie**: An uploaded image of a live-captured face supplied by the caller to the face-match endpoint for comparison against the Document Photo.
- **DeepFace**: The Python face-recognition library used to perform face verification between two images.
- **Face Match Result**: The outcome of a face comparison, consisting of a match boolean, a distance/similarity score, and the model and threshold used.
- **Distance Score**: The numeric distance between two face embeddings produced by DeepFace; a lower value indicates greater similarity.
- **Threshold**: The maximum Distance Score at or below which two faces are considered a match for the chosen model.
- **Validation Error Response**: A structured JSON error body returned for a failed request, containing an error type, a human-readable message, a detail field, and a timestamp.
- **EARS**: Easy Approach to Requirements Syntax; the structured patterns (Ubiquitous, Event-driven, State-driven, Unwanted-event, Optional-feature, Complex) used to write clear, testable requirements.

## Requirements

### Requirement 1: Validate Aadhaar Number

**User Story:** As a consuming application, I want to submit an Aadhaar number and learn whether it is valid, so that I can accept only well-formed, checksum-valid Aadhaar numbers during onboarding.

#### Acceptance Criteria

1. WHEN a caller sends a POST request to `/api/v1/validate-aadhaar` with a JSON body containing an `aadhar_number` field whose string value is at most 32 characters, THE KYC_API SHALL normalize the value by removing all space and hyphen characters before applying validation.
2. WHEN the Normalized Aadhaar consists of exactly 12 digit characters and its trailing check digit satisfies the Verhoeff Checksum, THE KYC_API SHALL respond with HTTP 200 and a body reporting the Aadhaar as valid and including the Masked Aadhaar, where the Masked Aadhaar replaces the first 8 digits with a fixed mask character and reveals only the last 4 digits (for example, `XXXXXXXX9012`).
3. IF the Normalized Aadhaar contains any non-digit character after normalization, THEN THE KYC_API SHALL respond with HTTP 200 and a body reporting the Aadhaar as invalid and a message identifying that only digits (optionally separated by spaces or hyphens) are permitted.
4. IF the Normalized Aadhaar has a length other than 12 digits, THEN THE KYC_API SHALL respond with HTTP 200 and a body reporting the Aadhaar as invalid and a message identifying that an Aadhaar number must contain exactly 12 digits.
5. IF the Normalized Aadhaar is exactly 12 digits but fails the Verhoeff Checksum, THEN THE KYC_API SHALL respond with HTTP 200 and a body reporting the Aadhaar as invalid and a message identifying that the Aadhaar checksum is invalid.
6. THE KYC_API SHALL exclude the full, unmasked Aadhaar Number from every response body, including valid, invalid, and Validation Error Response bodies, returning the Aadhaar only in its Masked Aadhaar form.
7. IF the request body omits the `aadhar_number` field, provides it as a non-string value, or provides a string value longer than 32 characters, THEN THE KYC_API SHALL respond with HTTP 422 and a Validation Error Response identifying the offending field without echoing the submitted Aadhaar value.
   - **Assumption:** The JSON field is named `aadhar_number` (matching the ticket and reference spelling); the response may use the spelling `aadhaar`. Field naming is confirmed in design.
8. THE KYC_API SHALL accept the Aadhaar input only as the textual `aadhar_number` JSON field and SHALL NOT accept image, file, or scanned-document uploads for Aadhaar validation.

### Requirement 2: Validate PAN Number

**User Story:** As a consuming application, I want to submit a PAN and learn whether its format is valid, so that I can accept only well-formed PAN values during onboarding.

#### Acceptance Criteria

1. WHEN a caller sends a POST request to `/api/v1/validate-pan` with a JSON body containing a string `pan_number` field, THE KYC_API SHALL remove all leading and trailing whitespace characters from the value and convert every alphabetic character to uppercase before applying validation.
2. WHEN the trimmed, uppercased PAN is exactly 10 characters and matches the pattern `[A-Z]{5}[0-9]{4}[A-Z]`, THE KYC_API SHALL respond with HTTP 200 and a body reporting the PAN as valid (validity indicator true) and including the trimmed, uppercased PAN value.
3. IF the trimmed, uppercased PAN is not exactly 10 characters or does not match the pattern `[A-Z]{5}[0-9]{4}[A-Z]` (including an empty or whitespace-only value), THEN THE KYC_API SHALL respond with HTTP 200 and a body reporting the PAN as invalid (validity indicator false) and a message identifying that a PAN must be five letters, then four digits, then one letter.
4. WHERE the trimmed, uppercased PAN is valid, THE KYC_API SHALL include in the response, as informational fields, the PAN Entity Type derived from the 4th character and the Surname Initial derived from the 5th character.
5. IF the submitted PAN is invalid, THEN THE KYC_API SHALL omit the PAN Entity Type and Surname Initial informational fields from the response.
6. IF the request body omits the `pan_number` field or provides it as a non-string value, THEN THE KYC_API SHALL respond with HTTP 422 and a Validation Error Response identifying the offending field.
   - **Assumption:** The JSON request field is named `pan_number` (matching the ticket and reference spelling); the uppercased PAN is echoed back in the response. Field naming and the exact decoded PAN Entity Type labels are confirmed in design.

### Requirement 3: Match Face Between Document Photo and Selfie

**User Story:** As a consuming application, I want to submit a document photo and a selfie and learn whether they show the same person, so that I can verify that the person onboarding matches the identity document.

#### Acceptance Criteria

1. WHEN a caller sends a POST request to `/api/v1/match-face` as `multipart/form-data` containing a Document Photo image and a Selfie image, THE KYC_API SHALL run face verification between the two images using the DeepFace library.
2. WHEN DeepFace verification produces a Distance Score for two images in which exactly one face is detected in the Document Photo and exactly one face is detected in the Selfie, THE KYC_API SHALL respond with HTTP 200 and a Face Match Result containing a match boolean (`is_match`) set to true when the Distance Score is at or below the Threshold and false otherwise, the numeric Distance Score, and the model name and numeric Threshold used to produce the result.
3. IF either the Document Photo or the Selfie is missing from the request, THEN THE KYC_API SHALL respond with HTTP 422 and a Validation Error Response identifying which image is required.
4. IF an uploaded file cannot be decoded as an image in a supported format, THEN THE KYC_API SHALL respond with HTTP 422 and a Validation Error Response identifying that the uploaded file could not be read as a supported image, and SHALL NOT return a Face Match Result.
5. IF no face can be detected in the Document Photo or in the Selfie, THEN THE KYC_API SHALL respond with HTTP 422 and a Validation Error Response identifying that a face could not be detected in the affected image, and SHALL NOT return a Face Match Result.
6. IF an uploaded Document Photo or Selfie exceeds the maximum accepted upload size of 10 megabytes, THEN THE KYC_API SHALL respond with HTTP 422 and a Validation Error Response identifying that the uploaded image exceeds the maximum accepted size, and SHALL NOT return a Face Match Result.
7. THE KYC_API SHALL perform face matching only on the two images supplied in the request and SHALL NOT capture images from any camera or webcam.
8. THE KYC_API SHALL exclude the raw uploaded image bytes from every response body.
   - **Assumption:** The multipart field names are `document_image` and `selfie_image`; exact field names and accepted image formats (for example JPEG and PNG) are confirmed in design.
   - **Assumption:** The maximum accepted per-file upload size is 10 megabytes; the exact limit is confirmed in design.

### Requirement 4: Report Service Health

**User Story:** As an operator, I want a health endpoint, so that I can confirm the service is running before routing traffic to it.

#### Acceptance Criteria

1. WHEN a caller sends a GET request to `/api/v1/health`, THE KYC_API SHALL respond with HTTP 200 and a body reporting the service status as healthy.
2. THE KYC_API SHALL respond to the health request without requiring any Aadhaar, PAN, or image input.

### Requirement 5: Structured Error Handling

**User Story:** As a consuming application, I want errors returned in a consistent, structured form, so that I can handle failures programmatically.

#### Acceptance Criteria

1. IF a request body fails Pydantic schema validation (for example, a missing or wrong-typed field), THEN THE KYC_API SHALL respond with HTTP 422 and a Validation Error Response.
2. WHEN the KYC_API returns any error response, THE KYC_API SHALL format the body as JSON containing an error type, a human-readable message, a detail field, and a timestamp in Coordinated Universal Time (UTC).
3. IF an unexpected internal error occurs while processing a request, THEN THE KYC_API SHALL respond with HTTP 500 and a Validation Error Response whose message indicates an internal error, and SHALL exclude stack traces and internal file paths from the response body.
4. WHEN the KYC_API produces any user-facing message, THE KYC_API SHALL express the message in English only, without bilingual, Arabic, or right-to-left content.

### Requirement 6: API Surface, Documentation, and Cross-Origin Access

**User Story:** As a developer integrating with the service, I want a versioned, documented, browser-reachable API, so that I can discover and call the endpoints reliably.

#### Acceptance Criteria

1. THE KYC_API SHALL expose its validation endpoints under the `/api/v1/` path prefix, namely `POST /api/v1/validate-aadhaar`, `POST /api/v1/validate-pan`, `POST /api/v1/match-face`, and `GET /api/v1/health`.
2. THE KYC_API SHALL serve interactive API documentation (Swagger UI) at `/docs`, generated from the route and schema definitions.
3. WHEN a browser-based caller from a configured allowed origin sends a cross-origin request, THE KYC_API SHALL include the cross-origin resource sharing (CORS) response headers that permit the request.
4. IF a caller sends a request to a path under `/api/v1/` that does not correspond to a defined route, or uses an HTTP method not supported by a defined route, THEN THE KYC_API SHALL respond with the appropriate not-found (404) or method-not-allowed (405) status and a Validation Error Response.
   - **Assumption:** The set of allowed CORS origins is configurable and defaults to permissive for development; the production origin allow-list is confirmed in design.

### Requirement 7: Automated Test Coverage

**User Story:** As a maintainer, I want automated tests for each validation rule and endpoint, so that regressions are caught before release.

#### Acceptance Criteria

1. THE KYC_API test suite SHALL include unit tests that assert Verhoeff Checksum validation against known-valid and known-invalid 12-digit vectors.
2. THE KYC_API test suite SHALL include unit tests that assert PAN format validation against matching and non-matching PAN strings, including case-insensitive input.
3. THE KYC_API test suite SHALL include unit tests for face matching in which the DeepFace verification call is replaced by a test double (mock), asserting both the match and non-match outcomes without invoking the real model.
4. THE KYC_API test suite SHALL include integration tests using the FastAPI test client that exercise each of the four endpoints and assert the success and error response shapes.
5. WHEN the test suite is run with pytest, THE KYC_API SHALL have every test pass.
