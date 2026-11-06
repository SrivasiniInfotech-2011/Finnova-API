# Implementation Plan: JV Posting Engine

**Feature:** `jv-posting-engine`  
**Source documents:** `requirements.md`, `design.md`  
**Implementation languages:** Python 3.11 for the one-shot CRON worker, SQL Server T-SQL for feature-owned persistence and the approved same-database posting procedure, and C# for the existing SystemAdmin submission/operations host. No Finnova-UI work is included.

## Overview

This plan implements the JVE as a separately deployable Python 3.11 worker. The worker claims a bounded SQL Server batch, parses the immutable versioned envelope, resolves one Cashflow Master rule through a configurable adapter, validates context and GL data, resolves the mandatory posting actor, builds a deterministic balanced voucher, and calls the approved canonical Journal Writer boundary.

The approved .NET API is the only submission boundary. It captures person or automation actor context, persists the immutable inbox request, and marks it `Ready`; it never posts a journal. The Python worker has no public submission endpoint. A protected operations surface is hosted in the existing SystemAdmin API only for queue monitoring and authorized recovery.

The JVE creates only feature-owned inbox, attempt, dead-letter, audit, reconciliation, schema-registry, and actor-configuration records. It does not create guessed Journal Header, Journal Line, or canonical source-link tables. The canonical accounting owner must confirm the exact table/column mapping, including `PostingUserId`/`PostedByUserId` and the final `Posted` status, before the writer task is copied. Final posting uses one SQL Server transaction or approved stored procedure that atomically commits the canonical Header, Lines, source link, actor attribution, reconciliation, audit, attempt outcome, and inbox `Posted` state.

All tasks below are implementation prompts for a code-generation agent. Each CREATE task contains complete file content. Each MODIFY task identifies the insertion location and supplies the exact snippet to insert. Do not run migrations, enable posting, deploy, build, or test as part of this planning workflow.

## Mandatory implementation gates

- Confirm the exact canonical Journal Header, Journal Line, and source-link owner, names, columns, status values, transaction/procedure signature, and actor mapping before task 8. No guessed journal schema is permitted.
- Confirm the approved JSON schema hash and registry owner before task 4.5 or applying the schema-registration migration.
- Confirm the seeded/configured SystemUser identifier, active-state source, automation-context mapping, and authorization scope before task 5.1.
- Keep Cashflow Master authoritative. Do not create Cashflow rule tables, seeds, event-to-account mappings, or hard-coded posting rules.
- Keep the baseline configuration-driven: Python 3.11, CRON one minute, batch 50, lease/deadline 10 minutes, retry delays 1/5/30 minutes for up to 3 attempts, Asia/Kolkata, INR/two decimals/round-half-up, and platform-approved retention/security/observability defaults.

## Tasks

- [ ] 1. Create the separately deployable Python worker and fail-closed configuration
  - [ ] 1.1 CREATE `jv-posting-worker/pyproject.toml`
    - Define the Python 3.11 package, pinned runtime/test dependencies, console entry point, and one-shot test configuration. Do not add a web server or scheduler dependency.
    - **Complete file content:**
      ```toml
      [build-system]
      requires = ["setuptools==69.5.1", "wheel==0.43.0"]
      build-backend = "setuptools.build_meta"

      [project]
      name = "finnova-jv-posting-worker"
      version = "0.1.0"
      description = "Finnova JV Posting Engine one-shot CRON worker"
      requires-python = ">=3.11,<3.12"
      dependencies = [
        "pydantic==2.7.4",
        "pydantic-settings==2.3.4",
        "pyodbc==5.1.0",
        "httpx==0.27.0",
        "structlog==24.2.0",
      ]

      [project.optional-dependencies]
      test = [
        "pytest==8.2.2",
        "pytest-asyncio==0.23.7",
        "hypothesis==6.103.1",
        "respx==0.21.1",
        "testcontainers==4.7.2",
      ]

      [project.scripts]
      jve-worker = "jve.cli:main"

      [tool.setuptools]
      package-dir = {"" = "src"}

      [tool.setuptools.packages.find]
      where = ["src"]

      [tool.pytest.ini_options]
      testpaths = ["tests"]
      addopts = "-q"
      asyncio_mode = "auto"
      markers = [
        "integration: requires an isolated SQL Server",
        "smoke: one-shot worker and configuration checks",
      ]
      ```
    - _Requirements: 7.1, 13.1, 13.2, 15.3_

  - [ ] 1.2 CREATE `jv-posting-worker/src/jve/__init__.py` and `jv-posting-worker/src/jve/config.py`
    - Validate required secrets/endpoints, default posting to disabled, enforce same-database readiness, and expose only configuration-driven policy values for the approved canonical writer.
    - **`src/jve/__init__.py`:**
      ```python
      """Finnova JV Posting Engine package."""

      __all__ = ["__version__"]
      __version__ = "0.1.0"
      ```
    - **`src/jve/config.py`:**
      ```python
      from __future__ import annotations

      from decimal import ROUND_HALF_UP
      from typing import Literal

      from pydantic import Field, SecretStr, field_validator, model_validator
      from pydantic_settings import BaseSettings, SettingsConfigDict


      class Settings(BaseSettings):
          model_config = SettingsConfigDict(
              env_prefix="JVE_",
              env_file=".env",
              extra="forbid",
              case_sensitive=False,
          )

          worker_id: str = Field(min_length=1, max_length=200)
          sql_connection_string: SecretStr
          cashflow_base_url: str
          gl_base_url: str
          actor_configuration_reference: str = Field(min_length=1, max_length=200)
          canonical_writer_procedure: str = Field(min_length=1, max_length=200)
          transaction_ready: bool = False
          posting_enabled: bool = False
          batch_size: int = Field(default=50, ge=1, le=1000)
          lease_minutes: int = Field(default=10, ge=1, le=120)
          processing_deadline_minutes: int = Field(default=10, ge=1, le=120)
          cashflow_timeout_seconds: float = Field(default=10.0, gt=0, le=120)
          gl_timeout_seconds: float = Field(default=10.0, gt=0, le=120)
          canonical_writer_timeout_seconds: float = Field(default=30.0, gt=0, le=300)
          max_payload_bytes: int = Field(default=1_048_576, ge=1, le=10_485_760)
          business_timezone: str = "Asia/Kolkata"
          currency_code: str = "INR"
          currency_precision: int = Field(default=2, ge=0, le=6)
          rounding_mode: Literal["ROUND_HALF_UP", "ROUND_HALF_EVEN", "ROUND_DOWN"] = ROUND_HALF_UP
          retry_delays_minutes: tuple[int, ...] = (1, 5, 30)
          max_attempts: int = Field(default=3, ge=1, le=10)
          schema_version: str = "1.0"

          @field_validator("currency_code")
          @classmethod
          def validate_currency_code(cls, value: str) -> str:
              value = value.upper()
              if not value or len(value) > 10:
                  raise ValueError("currency code is invalid")
              return value

          @field_validator("retry_delays_minutes")
          @classmethod
          def validate_retry_delays(cls, value: tuple[int, ...]) -> tuple[int, ...]:
              if not value or any(delay <= 0 for delay in value):
                  raise ValueError("retry delays must be positive")
              return value

          @model_validator(mode="after")
          def validate_posting_gate(self) -> "Settings":
              if self.posting_enabled and not self.transaction_ready:
                  raise ValueError("posting cannot be enabled before same-database readiness")
              if self.max_attempts > len(self.retry_delays_minutes) + 1:
                  raise ValueError("max_attempts exceeds configured retry schedule")
              return self
      ```
    - _Requirements: 6.4, 7.1, 9.1–9.4, 11.2, 13.1, 15.3_

  - [ ] 1.3 CREATE `jv-posting-worker/src/jve/cli.py`, `jv-posting-worker/src/jve/logging.py`, and `jv-posting-worker/src/jve/clock.py`
    - Create a one-shot command with no in-process scheduler, safe structured logging, and an injectable UTC clock.
    - **`src/jve/cli.py`:**
      ```python
      from __future__ import annotations

      import typer

      from .config import Settings
      from .logging import configure_logging
      from .runtime import build_runtime

      app = typer.Typer(add_completion=False)


      @app.command("run")
      def run() -> None:
          """Process one bounded batch and exit."""
          settings = Settings()
          configure_logging(settings.worker_id)
          build_runtime(settings).run_once()


      def main() -> None:
          app()
      ```
    - **`src/jve/logging.py`:**
      ```python
      import logging

      import structlog


      def configure_logging(worker_id: str) -> None:
          logging.basicConfig(level=logging.INFO, format="%(message)s")
          structlog.configure(
              processors=[
                  structlog.contextvars.merge_contextvars,
                  structlog.processors.TimeStamper(fmt="iso", utc=True),
                  structlog.processors.JSONRenderer(),
              ],
              wrapper_class=structlog.make_filtering_bound_logger(logging.INFO),
          )
      ```
    - **`src/jve/clock.py`:**
      ```python
      from datetime import datetime, timezone


      class Clock:
          def now_utc(self) -> datetime:
              raise NotImplementedError


      class SystemClock(Clock):
          def now_utc(self) -> datetime:
              return datetime.now(timezone.utc)
      ```
    - _Requirements: 7.1, 7.7, 9.6, 13.1–13.4_

  - [ ] 1.4 CREATE `jv-posting-worker/src/jve/runtime.py`
    - Wire only the same-database repository, configurable adapters, canonical writer procedure, coordinator, and invocation runner. Refuse to build a posting runtime while the readiness gate is false.
    - **Complete file content:**
      ```python
      from __future__ import annotations

      from .clock import SystemClock
      from .config import Settings
      from .coordinator import InvocationRunner, PostingCoordinator
      from .observability import JsonObservability
      from .sql import SqlConnectionFactory, SqlInboxRepository


      class Runtime:
          def __init__(self, runner: InvocationRunner) -> None:
              self._runner = runner

          def run_once(self) -> None:
              self._runner.run_once()


      def build_runtime(settings: Settings) -> Runtime:
          if not settings.transaction_ready:
              raise RuntimeError("JVE same-database transaction readiness gate is not satisfied")
          factory = SqlConnectionFactory(settings.sql_connection_string.get_secret_value())
          inbox = SqlInboxRepository(factory)
          coordinator = PostingCoordinator.from_settings(settings, factory, inbox)
          return Runtime(
              InvocationRunner(
                  settings=settings,
                  inbox=inbox,
                  coordinator=coordinator,
                  clock=SystemClock(),
                  observability=JsonObservability(),
              )
          )
      ```
    - _Requirements: 6.1–6.4, 7.1, 13.1–13.2_

  - [ ] 1.5 Checkpoint — verify the worker package owner, Python 3.11 runtime, secret injection, one-minute CRON owner, SQL identity, and same-database transaction gate.
    - Do not enable posting or construct the canonical writer until task 8.1 confirms the canonical mapping.

- [ ] 2. Create immutable domain models and pure accounting/state services
  - [ ] 2.1 CREATE `jv-posting-worker/src/jve/domain.py`
    - Define immutable English-only models for actor context, envelope, statuses, failures, rule results, posting context, vouchers, attempts, and safe operational responses. Use `Decimal` for all monetary values.
    - **Complete file content:**
      ```python
      from __future__ import annotations

      from dataclasses import dataclass, field
      from datetime import date, datetime
      from decimal import Decimal
      from enum import StrEnum
      from typing import Any, Mapping
      from uuid import UUID


      class Status(StrEnum):
          READY = "Ready"
          PROCESSING = "Processing"
          RETRY_PENDING = "RetryPending"
          POSTED = "Posted"
          DEAD_LETTER = "DeadLetter"
          CANCELLED = "Cancelled"


      class InitiationMode(StrEnum):
          PERSON = "Person"
          AUTOMATION = "Automation"


      class FailureCategory(StrEnum):
          SCHEMA = "schema"
          VALIDATION = "validation"
          AUTHORIZATION = "authorization"
          DEPENDENCY = "dependency"
          PERSISTENCE = "persistence"
          CONCURRENCY = "concurrency"
          IDEMPOTENCY = "idempotency"
          ACCOUNTING_INTEGRITY = "accounting-integrity"


      @dataclass(frozen=True)
      class ActorContext:
          initiation_mode: InitiationMode
          initiating_user_id: UUID | None
          automation_context: str | None


      @dataclass(frozen=True)
      class Failure:
          category: FailureCategory
          code: str
          message: str
          transient: bool = False
          dependency_name: str | None = None
          diagnostic_reference: str | None = None


      @dataclass(frozen=True)
      class Envelope:
          schema_version: str
          request_id: UUID
          source_system: str
          event_type: str | None
          rule_key: str | None
          company_id: UUID
          branch_id: UUID
          value_date: date
          posting_date: date | None
          currency_code: str | None
          description: str | None
          data: Mapping[str, Any]
          actor: ActorContext
          original_payload: bytes
          permitted_unknown_fields: Mapping[str, Any] = field(default_factory=dict)


      @dataclass(frozen=True)
      class RuleLine:
          source_line_id: str
          gl_account_id: UUID
          debit: Decimal
          credit: Decimal
          dimensions: Mapping[str, str]


      @dataclass(frozen=True)
      class ResolvedRule:
          rule_key: str
          rule_version: str
          permitted_currencies: frozenset[str]
          journal_mode: str
          rule_lines: tuple[RuleLine, ...]
          rule_posting_date: date | None = None
          reversal_allowed: bool = False


      @dataclass(frozen=True)
      class PostingContext:
          company_id: UUID
          branch_id: UUID
          currency_code: str
          value_date: date
          posting_date: date
          accounting_period: str
          business_timezone: str


      @dataclass(frozen=True)
      class VoucherLine:
          source_line_id: str
          gl_account_id: UUID
          debit: Decimal
          credit: Decimal
          dimensions: Mapping[str, str]


      @dataclass(frozen=True)
      class Voucher:
          request_id: UUID
          rule_version: str
          context: PostingContext
          lines: tuple[VoucherLine, ...]
          debit_total: Decimal
          credit_total: Decimal
          fingerprint: str


      @dataclass(frozen=True)
      class PostingAttempt:
          attempt_id: UUID
          request_id: UUID
          attempt_number: int
          correlation_id: UUID
          worker_id: str
          started_at_utc: datetime


      @dataclass(frozen=True)
      class ResolvedActor:
          user_id: UUID
          source: InitiationMode


      @dataclass(frozen=True)
      class QueueBucket:
          status: Status
          count: int
          oldest_record_age_seconds: int | None
      ```
    - _Requirements: 1.1–1.3, 2.1–2.3, 4.3, 5.1, 6.7–6.10, 7.7, 10.6, 12.1_

  - [ ] 2.2 CREATE `jv-posting-worker/src/jve/state.py`
    - Implement lifecycle transitions, positive/increasing attempt numbers, retry backoff, and fail-closed cancellation without database I/O.
    - **Complete file content:**
      ```python
      from __future__ import annotations

      from datetime import datetime, timedelta

      from .domain import Failure, FailureCategory, Status


      ALLOWED_TRANSITIONS: dict[Status, frozenset[Status]] = {
          Status.READY: frozenset({Status.PROCESSING, Status.CANCELLED}),
          Status.RETRY_PENDING: frozenset({Status.PROCESSING, Status.CANCELLED}),
          Status.PROCESSING: frozenset({Status.POSTED, Status.RETRY_PENDING, Status.DEAD_LETTER, Status.CANCELLED, Status.READY}),
          Status.DEAD_LETTER: frozenset({Status.READY, Status.POSTED}),
          Status.POSTED: frozenset(),
          Status.CANCELLED: frozenset(),
      }


      def transition(current: Status, target: Status, *, journal_written: bool = False) -> None:
          if target not in ALLOWED_TRANSITIONS[current]:
              raise ValueError(f"invalid JVE transition: {current} -> {target}")
          if target == Status.POSTED and not journal_written:
              raise ValueError("Posted requires a confirmed canonical journal")
          if target == Status.CANCELLED and journal_written:
              raise ValueError("a journal-backed request cannot be cancelled")


      def validate_attempt_number(previous_count: int, next_number: int) -> None:
          if previous_count < 0 or next_number != previous_count + 1:
              raise ValueError("attempt numbers must be positive and strictly increasing")


      def retry_at(attempt_count: int, now: datetime, delays_minutes: tuple[int, ...]) -> datetime | None:
          if attempt_count >= len(delays_minutes):
              return None
          return now + timedelta(minutes=delays_minutes[attempt_count])


      def lost_lease_failure() -> Failure:
          return Failure(FailureCategory.CONCURRENCY, "JVE-LEASE-LOST", "Processing lease was lost", transient=True)
      ```
    - _Requirements: 7.1–7.6, 9.1–9.4, 15.7_

  - [ ] 2.3 CREATE `jv-posting-worker/src/jve/services.py`
    - Implement schema-aware UTF-8/JSON parsing, unknown-field preservation, posting-date precedence, INR default gating, decimal quantization, deterministic balanced voucher construction, stable fingerprints, retry calculation, and diagnostic redaction. Do not embed Cashflow rules.
    - **Complete file content:**
      ```python
      from __future__ import annotations

      import hashlib
      import json
      from dataclasses import dataclass
      from datetime import date
      from decimal import Decimal, InvalidOperation, ROUND_DOWN, ROUND_HALF_EVEN, ROUND_HALF_UP
      from typing import Any, Mapping
      from uuid import UUID

      from .domain import Envelope, PostingContext, ResolvedRule, Voucher, VoucherLine


      @dataclass(frozen=True)
      class SchemaDefinition:
          version: str
          fields: frozenset[str]
          required: frozenset[str]
          max_bytes: int = 1_048_576


      class EnvelopeParser:
          def __init__(self, schemas: Mapping[str, SchemaDefinition]) -> None:
              self._schemas = schemas

          def parse(self, payload: bytes) -> Envelope:
              if len(payload) > max((schema.max_bytes for schema in self._schemas.values()), default=0):
                  raise ValueError("JVE-SCHEMA-PAYLOAD-TOO-LARGE")
              try:
                  document = json.loads(payload.decode("utf-8"))
              except (UnicodeDecodeError, json.JSONDecodeError) as exc:
                  raise ValueError("JVE-SCHEMA-INVALID-JSON") from exc
              if not isinstance(document, dict):
                  raise ValueError("JVE-SCHEMA-ROOT-NOT-OBJECT")
              version = document.get("schemaVersion")
              schema = self._schemas.get(version)
              if schema is None:
                  raise ValueError("JVE-SCHEMA-UNSUPPORTED-VERSION")
              missing = schema.required - document.keys()
              if missing:
                  raise ValueError(f"JVE-SCHEMA-MISSING-FIELDS:{','.join(sorted(missing))}")
              if not document.get("eventType") and not document.get("ruleKey"):
                  raise ValueError("JVE-SCHEMA-MISSING-RULE-KEY")
              actor = document.get("actor") or {}
              mode = actor.get("initiationMode")
              if mode not in {"Person", "Automation"}:
                  raise ValueError("JVE-ACTOR-INITIATION-MODE-INVALID")
              initiating_user_id = actor.get("initiatingUserId")
              automation_context = actor.get("automationContext")
              if mode == "Person" and not initiating_user_id:
                  raise ValueError("JVE-ACTOR-INITIATING-USER-REQUIRED")
              if mode == "Automation" and not automation_context:
                  raise ValueError("JVE-ACTOR-AUTOMATION-CONTEXT-REQUIRED")
              known = {key: document.get(key) for key in schema.fields}
              from .domain import ActorContext, InitiationMode
              return Envelope(
                  schema_version=version,
                  request_id=UUID(str(known["requestId"])),
                  source_system=str(known["sourceSystem"]),
                  event_type=known.get("eventType"),
                  rule_key=known.get("ruleKey"),
                  company_id=UUID(str(known["companyId"])),
                  branch_id=UUID(str(known["branchId"])),
                  value_date=date.fromisoformat(str(known["valueDate"])),
                  posting_date=date.fromisoformat(str(known["postingDate"])) if known.get("postingDate") else None,
                  currency_code=str(known["currencyCode"]).upper() if known.get("currencyCode") else None,
                  description=known.get("description"),
                  data=known["data"],
                  actor=ActorContext(InitiationMode(mode), UUID(str(initiating_user_id)) if initiating_user_id else None, automation_context),
                  original_payload=payload,
                  permitted_unknown_fields={key: value for key, value in document.items() if key not in schema.fields},
              )


      def resolve_posting_date(envelope: Envelope, rule: ResolvedRule) -> date:
          return envelope.posting_date or rule.rule_posting_date or envelope.value_date


      def resolve_currency(envelope_currency: str | None, rule: ResolvedRule, gl_permits: set[str]) -> str:
          currency = envelope_currency or "INR"
          if currency not in rule.permitted_currencies or currency not in gl_permits:
              raise ValueError("JVE-VALIDATION-CURRENCY-NOT-PERMITTED")
          return currency


      def quantize_amount(value: Decimal, precision: int, rounding_mode: str) -> Decimal:
          if value <= 0:
              raise ValueError("JVE-ACCOUNTING-NON-POSITIVE-AMOUNT")
          quantum = Decimal(1).scaleb(-precision)
          modes = {"ROUND_HALF_UP": ROUND_HALF_UP, "ROUND_HALF_EVEN": ROUND_HALF_EVEN, "ROUND_DOWN": ROUND_DOWN}
          try:
              return value.quantize(quantum, rounding=modes[rounding_mode])
          except (InvalidOperation, KeyError) as exc:
              raise ValueError("JVE-ACCOUNTING-INVALID-PRECISION") from exc


      def build_voucher(envelope: Envelope, rule: ResolvedRule, context: PostingContext, *, precision: int, rounding_mode: str) -> Voucher:
          lines: list[VoucherLine] = []
          seen: set[str] = set()
          for candidate in sorted(rule.rule_lines, key=lambda item: item.source_line_id):
              if candidate.source_line_id in seen:
                  raise ValueError("JVE-ACCOUNTING-DUPLICATE-LINE-ID")
              seen.add(candidate.source_line_id)
              debit = quantize_amount(candidate.debit, precision, rounding_mode) if candidate.debit else Decimal("0")
              credit = quantize_amount(candidate.credit, precision, rounding_mode) if candidate.credit else Decimal("0")
              if (debit > 0) == (credit > 0):
                  raise ValueError("JVE-ACCOUNTING-LINE-MUST-HAVE-ONE-SIDE")
              lines.append(VoucherLine(candidate.source_line_id, candidate.gl_account_id, debit, credit, candidate.dimensions))
          if len(lines) < 2:
              raise ValueError("JVE-ACCOUNTING-MINIMUM-LINES")
          debit_total = sum((line.debit for line in lines), Decimal("0"))
          credit_total = sum((line.credit for line in lines), Decimal("0"))
          if debit_total != credit_total:
              raise ValueError(f"JVE-ACCOUNTING-UNBALANCED:{debit_total}:{credit_total}")
          canonical = "|".join(f"{line.source_line_id}:{line.gl_account_id}:{line.debit}:{line.credit}:{sorted(line.dimensions.items())}" for line in lines)
          identity = "|".join((str(envelope.request_id), rule.rule_version, str(context.company_id), str(context.branch_id), context.currency_code, str(context.value_date), str(context.posting_date), canonical))
          return Voucher(envelope.request_id, rule.rule_version, context, tuple(lines), debit_total, credit_total, hashlib.sha256(identity.encode()).hexdigest())


      SENSITIVE_KEYS = frozenset({"secret", "password", "token", "authorization", "raw_payload", "pan", "aadhaar", "account_number", "contact_details"})


      def redact(value: Any) -> Any:
          if isinstance(value, Mapping):
              return {key: "[REDACTED]" if str(key).lower() in SENSITIVE_KEYS else redact(item) for key, item in value.items()}
          if isinstance(value, list):
              return [redact(item) for item in value]
          return value
      ```
    - _Requirements: 2.1–2.9, 4.1–4.10, 5.2–5.11, 8.2, 9.1, 10.3, 11.5, 12.6_

  - [ ] 2.4 CREATE `jv-posting-worker/src/jve/schema_registry.py`
    - Register only immutable approved schema definitions. Do not insert a placeholder hash or create a Cashflow schema/table.
    - **Complete file content:**
      ```python
      from __future__ import annotations

      from .services import SchemaDefinition


      BASELINE_SCHEMA = SchemaDefinition(
          version="1.0",
          fields=frozenset({
              "schemaVersion", "requestId", "sourceSystem", "eventType", "ruleKey",
              "companyId", "branchId", "valueDate", "postingDate", "currencyCode",
              "description", "actor", "data",
          }),
          required=frozenset({"schemaVersion", "requestId", "sourceSystem", "companyId", "branchId", "valueDate", "actor", "data"}),
      )


      class SchemaRegistry:
          def __init__(self, definitions: tuple[SchemaDefinition, ...] = (BASELINE_SCHEMA,)) -> None:
              self._definitions = {definition.version: definition for definition in definitions}

          def get(self, version: str) -> SchemaDefinition | None:
              return self._definitions.get(version)

          def all(self) -> tuple[SchemaDefinition, ...]:
              return tuple(self._definitions.values())
      ```
    - _Requirements: 2.1, 2.6, 2.8, 15.1, 15.3_

  - [ ] 2.5 Checkpoint — verify each requirement 2 and 4 rule is represented by a pure function or an explicit failure path before adding database I/O.

- [ ] 3. Create configurable worker ports and dependency contracts
  - [ ] 3.1 CREATE `jv-posting-worker/src/jve/ports.py`
    - Define technology-neutral inbox, actor, Cashflow, GL/context, canonical Journal Writer, recovery, and observability ports. The worker must not expose submission HTTP routes.
    - **Complete file content:**
      ```python
      from __future__ import annotations

      from datetime import datetime
      from typing import Protocol
      from uuid import UUID

      from .domain import ActorContext, Envelope, Failure, PostingAttempt, PostingContext, ResolvedActor, ResolvedRule, Status, Voucher


      class InboxRepository(Protocol):
          def claim_batch(self, now_utc: datetime, worker_id: str, lease_until: datetime, batch_size: int) -> list[dict]: ...
          def owns_lease(self, request_id: UUID, worker_id: str, attempt_id: UUID, now_utc: datetime) -> bool: ...
          def heartbeat(self, request_id: UUID, worker_id: str, attempt_id: UUID, lease_until: datetime) -> bool: ...
          def complete(self, request_id: UUID, worker_id: str, attempt_id: UUID, status: Status, journal_id: UUID | None, failure: Failure | None) -> None: ...
          def queue_status(self, company_id: UUID | None, branch_id: UUID | None) -> list[dict]: ...
          def get_request(self, request_id: UUID) -> dict | None: ...
          def recover_expired(self, request_id: UUID, actor: str, reason: str) -> bool: ...
          def reprocess_dead_letter(self, request_id: UUID, actor: str, reason: str) -> bool: ...
          def cancel(self, request_id: UUID, actor: str, reason: str) -> bool: ...


      class AttemptRepository(Protocol):
          def start(self, request_id: UUID, attempt_number: int, correlation_id: UUID, worker_id: str, started_at_utc: datetime) -> PostingAttempt: ...
          def finish(self, attempt_id: UUID, status: Status, failure: Failure | None, ended_at_utc: datetime, journal_id: UUID | None = None) -> None: ...


      class CashflowMasterPort(Protocol):
          def resolve_rule(self, envelope: Envelope, timeout_seconds: float) -> ResolvedRule: ...


      class GLMasterPort(Protocol):
          def validate(self, envelope: Envelope, rule: ResolvedRule, context: PostingContext, timeout_seconds: float) -> set[str]: ...


      class ContextPort(Protocol):
          def resolve(self, envelope: Envelope, rule: ResolvedRule, business_timezone: str) -> PostingContext: ...


      class ActorResolverPort(Protocol):
          def resolve(self, actor: ActorContext, company_id: UUID, branch_id: UUID) -> ResolvedActor: ...


      class CanonicalJournalWriterPort(Protocol):
          def find_by_source(self, request_id: UUID) -> UUID | None: ...
          def write_atomic(self, request_id: UUID, attempt_id: UUID, voucher: Voucher, actor: ResolvedActor, expected_row_version: bytes) -> UUID: ...


      class RecoveryPort(Protocol):
          def authorize(self, actor: str, company_id: UUID, branch_id: UUID, action: str) -> None: ...


      class ObservabilityPort(Protocol):
          def invocation_start(self, worker_id: str, correlation_id: UUID, started_at_utc: datetime) -> None: ...
          def invocation_end(self, worker_id: str, correlation_id: UUID, started_at_utc: datetime, ended_at_utc: datetime, counters: dict[str, int]) -> None: ...
          def milestone(self, request_id: UUID, correlation_id: UUID, milestone: str, occurred_at_utc: datetime, **fields: object) -> None: ...
          def error(self, failure: Failure, correlation_id: UUID, occurred_at_utc: datetime, **fields: object) -> None: ...
      ```
    - _Requirements: 3.1–3.11, 6.1–6.10, 7.1–7.9, 10.1–10.7, 12.1–12.8_

  - [ ] 3.2 CREATE `jv-posting-worker/src/jve/adapters.py`
    - Implement configurable Cashflow, GL, context, and actor adapters with bounded timeouts, correlation propagation, response validation, and stable permanent/transient failures. The Cashflow adapter must return `NoRule`/`AmbiguousRule` as permanent failures and must never store rules locally.
    - **Complete file content:**
      ```python
      from __future__ import annotations

      from uuid import UUID

      import httpx

      from .domain import ActorContext, Envelope, Failure, FailureCategory, PostingContext, ResolvedActor, ResolvedRule


      class DependencyError(RuntimeError):
          def __init__(self, failure: Failure) -> None:
              super().__init__(failure.message)
              self.failure = failure


      class ConfigurableCashflowAdapter:
          def __init__(self, base_url: str, client: httpx.Client) -> None:
              self._base_url = base_url.rstrip("/")
              self._client = client

          def resolve_rule(self, envelope: Envelope, timeout_seconds: float) -> ResolvedRule:
              response = self._client.post(
                  f"{self._base_url}/rules/resolve",
                  json={"requestId": str(envelope.request_id), "schemaVersion": envelope.schema_version, "eventType": envelope.event_type, "ruleKey": envelope.rule_key, "companyId": str(envelope.company_id), "branchId": str(envelope.branch_id), "valueDate": envelope.value_date.isoformat(), "currencyCode": envelope.currency_code, "data": envelope.data},
                  timeout=timeout_seconds,
              )
              if response.status_code in {408, 429} or response.status_code >= 500:
                  raise DependencyError(Failure(FailureCategory.DEPENDENCY, "JVE-CASHFLOW-UNAVAILABLE", "Cashflow Master is unavailable", True, "CashflowMaster"))
              if response.status_code >= 400:
                  raise DependencyError(Failure(FailureCategory.VALIDATION, "JVE-CASHFLOW-CONTRACT-REJECTED", "Cashflow rule request was rejected", False, "CashflowMaster"))
              body = response.json()
              result = body.get("result")
              if result in {"NoRule", "AmbiguousRule"}:
                  code = "JVE-CASHFLOW-NO-RULE" if result == "NoRule" else "JVE-CASHFLOW-AMBIGUOUS-RULE"
                  raise DependencyError(Failure(FailureCategory.VALIDATION, code, "No unique applicable Cashflow rule exists", False, "CashflowMaster"))
              if result != "ResolvedRule":
                  raise DependencyError(Failure(FailureCategory.DEPENDENCY, "JVE-CASHFLOW-CONTRACT-INVALID", "Cashflow rule response is invalid", False, "CashflowMaster"))
              return ResolvedRule.from_payload(body["rule"])


      class ConfigurableGLAdapter:
          def __init__(self, base_url: str, client: httpx.Client) -> None:
              self._base_url = base_url.rstrip("/")
              self._client = client

          def validate(self, envelope: Envelope, rule: ResolvedRule, context: PostingContext, timeout_seconds: float) -> set[str]:
              response = self._client.post(f"{self._base_url}/validate", json={"companyId": str(context.company_id), "branchId": str(context.branch_id), "currencyCode": context.currency_code, "postingDate": context.posting_date.isoformat(), "lines": [line.__dict__ for line in rule.rule_lines]}, timeout=timeout_seconds)
              if response.status_code >= 500 or response.status_code in {408, 429}:
                  raise DependencyError(Failure(FailureCategory.DEPENDENCY, "JVE-GL-UNAVAILABLE", "GL Master is unavailable", True, "GLMaster"))
              if response.status_code >= 400:
                  raise DependencyError(Failure(FailureCategory.VALIDATION, "JVE-GL-VALIDATION-FAILED", "GL validation failed", False, "GLMaster"))
              body = response.json()
              if not body.get("valid", False):
                  raise DependencyError(Failure(FailureCategory.VALIDATION, "JVE-GL-VALIDATION-FAILED", "GL validation failed", False, "GLMaster"))
              return set(body.get("permittedCurrencies", []))


      class ConfiguredActorResolver:
          def __init__(self, active_user_lookup, system_user_lookup) -> None:
              self._active_user_lookup = active_user_lookup
              self._system_user_lookup = system_user_lookup

          def resolve(self, actor: ActorContext, company_id: UUID, branch_id: UUID) -> ResolvedActor:
              if actor.initiation_mode.value == "Person":
                  if actor.initiating_user_id is None or not self._active_user_lookup(actor.initiating_user_id, company_id, branch_id):
                      raise DependencyError(Failure(FailureCategory.AUTHORIZATION, "JVE-ACTOR-USER-UNRESOLVED", "Initiating user is not active or authorized"))
                  return ResolvedActor(actor.initiating_user_id, actor.initiation_mode)
              user_id = self._system_user_lookup(actor.automation_context, company_id, branch_id)
              if user_id is None:
                  raise DependencyError(Failure(FailureCategory.AUTHORIZATION, "JVE-ACTOR-SYSTEM-USER-UNRESOLVED", "Configured SystemUser is not active or authorized"))
              return ResolvedActor(user_id, actor.initiation_mode)
      ```
    - _Requirements: 3.1–3.11, 5.2–5.8, 6.7–6.10, 11.2–11.4_

  - [ ] 3.3 Write adapter contract tests (optional)
    - Create `jv-posting-worker/tests/test_adapter_contracts.py` for exactly one rule, no rule, ambiguous rule, timeout, invalid response, GL validation failure, inactive person actor, missing SystemUser, and successful automation actor resolution. Use HTTP fakes only; do not create rule tables or seeds.
    - _Requirements: 3.1–3.11, 5.2–5.8, 6.7–6.10_

- [ ] 4. Add the approved .NET submission/inbox initiation boundary
  - [ ] 4.1 CREATE `Finnova.Models/Contracts/JVPosting/JvPostingSubmissionRequest.cs` and `Finnova.Models/Contracts/JVPosting/JvPostingSubmissionResponse.cs`
    - Define the API request with mandatory actor context. The request must distinguish person and automation initiation, require `InitiatingUserId` for person mode, require an approved automation context for automation mode, and keep source JSON opaque to the API business layer.
    - **`JvPostingSubmissionRequest.cs`:**
      ```csharp
      using System.Text.Json;

      namespace Finnova.Models.Contracts.JVPosting;

      public sealed record JvPostingSubmissionRequest(
          Guid RequestId,
          string SourceSystem,
          string? EventType,
          string? RuleKey,
          Guid CompanyId,
          Guid BranchId,
          DateOnly ValueDate,
          DateOnly? PostingDate,
          string? CurrencyCode,
          string? Description,
          JsonElement Data,
          string InitiationMode,
          Guid? InitiatingUserId,
          string? AutomationContext);
      ```
    - **`JvPostingSubmissionResponse.cs`:**
      ```csharp
      namespace Finnova.Models.Contracts.JVPosting;

      public sealed record JvPostingSubmissionResponse(
          Guid RequestId,
          string Status,
          bool ExistingEquivalentRequest);
      ```
    - _Requirements: 1.1–1.8, 2.2–2.3, 6.7–6.10, 11.1, 11.6_

  - [ ] 4.2 CREATE `Finnova.Models/Domain/Entities/JvPostingInbox.cs` and `Finnova.Models/Domain/Enums/JvPostingStatus.cs`
    - Define only the feature-owned inbox entity and actor fields. Do not define Journal Header, Journal Line, or canonical source-link entities.
    - **`JvPostingStatus.cs`:**
      ```csharp
      namespace Finnova.Models.Domain.Enums;

      public enum JvPostingStatus
      {
          Ready,
          Processing,
          RetryPending,
          Posted,
          DeadLetter,
          Cancelled
      }
      ```
    - **`JvPostingInbox.cs`:**
      ```csharp
      using Finnova.Models.Domain.Enums;

      namespace Finnova.Models.Domain.Entities;

      public sealed class JvPostingInbox
      {
          public Guid RequestId { get; set; }
          public string SourceSystem { get; set; } = string.Empty;
          public string? EventType { get; set; }
          public string? RuleKey { get; set; }
          public string SchemaVersion { get; set; } = "1.0";
          public Guid CompanyId { get; set; }
          public Guid BranchId { get; set; }
          public DateOnly ValueDate { get; set; }
          public DateOnly? PostingDate { get; set; }
          public string? CurrencyCode { get; set; }
          public string? Description { get; set; }
          public byte[] RawPayload { get; set; } = Array.Empty<byte>();
          public byte[] PayloadHash { get; set; } = Array.Empty<byte>();
          public string InitiationMode { get; set; } = string.Empty;
          public Guid? InitiatingUserId { get; set; }
          public string? AutomationContext { get; set; }
          public JvPostingStatus Status { get; set; }
          public int AttemptCount { get; set; }
          public DateTime? NextRetryAtUtc { get; set; }
          public string? WorkerId { get; set; }
          public DateTime? ClaimedAtUtc { get; set; }
          public DateTime? LeaseExpiresAtUtc { get; set; }
          public DateTime? HeartbeatAtUtc { get; set; }
          public Guid? JournalId { get; set; }
          public DateTime? PostedAtUtc { get; set; }
          public Guid? PostingUserId { get; set; }
          public string? RuleVersion { get; set; }
          public decimal? DebitTotal { get; set; }
          public decimal? CreditTotal { get; set; }
          public string? LastErrorCategory { get; set; }
          public string? LastErrorCode { get; set; }
          public string? LastErrorMessageSafe { get; set; }
          public DateTime CreatedAtUtc { get; set; }
          public DateTime UpdatedAtUtc { get; set; }
          public byte[] RowVersion { get; set; } = Array.Empty<byte>();
      }
      ```
    - _Requirements: 1.1–1.7, 6.5, 7.1–7.8, 8.3, 9.1–9.10, 15.1–15.8_

  - [ ] 4.3 CREATE `Finnova.Service/JVPosting/JvPostingSubmissionService.cs`
    - Validate envelope metadata and actor context, enforce caller company/branch scope, compute SHA-256 over the immutable raw payload/context, insert or return an equivalent request, reject conflicting reuse, and mark accepted work `Ready`. Never call Cashflow, GL, or Journal Writer.
    - **Complete file content:**
      ```csharp
      using System.Security.Claims;
      using System.Security.Cryptography;
      using System.Text;
      using System.Text.Json;
      using Finnova.Models.Contracts.JVPosting;
      using Finnova.Models.Domain.Entities;
      using Finnova.Models.Domain.Enums;
      using Finnova.Repository.Context;
      using Microsoft.EntityFrameworkCore;

      namespace Finnova.Service.JVPosting;

      public sealed class JvPostingSubmissionService(FinnovaDbContext db)
      {
          public async Task<JvPostingSubmissionResponse> SubmitAsync(
              JvPostingSubmissionRequest request,
              ClaimsPrincipal caller,
              CancellationToken cancellationToken = default)
          {
              Validate(request, caller);
              var rawPayload = JsonSerializer.SerializeToUtf8Bytes(request.Data);
              var payloadHash = SHA256.HashData(Encoding.UTF8.GetBytes($"{request.RequestId}|{request.CompanyId}|{request.BranchId}|{request.SourceSystem}|{Convert.ToBase64String(rawPayload)}"));
              var existing = await db.JvPostingInboxes.SingleOrDefaultAsync(x => x.RequestId == request.RequestId, cancellationToken);
              if (existing is not null)
              {
                  if (!existing.PayloadHash.SequenceEqual(payloadHash) || existing.CompanyId != request.CompanyId || existing.BranchId != request.BranchId)
                      throw new InvalidOperationException("JVE-IDEMPOTENCY-PAYLOAD-CONFLICT");
                  return new(request.RequestId, existing.Status.ToString(), true);
              }

              var person = string.Equals(request.InitiationMode, "Person", StringComparison.OrdinalIgnoreCase);
              db.JvPostingInboxes.Add(new JvPostingInbox
              {
                  RequestId = request.RequestId,
                  SourceSystem = request.SourceSystem,
                  EventType = request.EventType,
                  RuleKey = request.RuleKey,
                  SchemaVersion = "1.0",
                  CompanyId = request.CompanyId,
                  BranchId = request.BranchId,
                  ValueDate = request.ValueDate,
                  PostingDate = request.PostingDate,
                  CurrencyCode = request.CurrencyCode?.ToUpperInvariant(),
                  Description = request.Description,
                  RawPayload = rawPayload,
                  PayloadHash = payloadHash,
                  InitiationMode = person ? "Person" : "Automation",
                  InitiatingUserId = person ? request.InitiatingUserId : null,
                  AutomationContext = person ? null : request.AutomationContext,
                  Status = JvPostingStatus.Ready,
                  CreatedAtUtc = DateTime.UtcNow,
                  UpdatedAtUtc = DateTime.UtcNow
              });
              await db.SaveChangesAsync(cancellationToken);
              return new(request.RequestId, JvPostingStatus.Ready.ToString(), false);
          }

          private static void Validate(JvPostingSubmissionRequest request, ClaimsPrincipal caller)
          {
              if (request.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(request.SourceSystem) || request.CompanyId == Guid.Empty || request.BranchId == Guid.Empty || (request.EventType is null && request.RuleKey is null))
                  throw new ArgumentException("JVE-SUBMISSION-REQUIRED-FIELD");
              var person = string.Equals(request.InitiationMode, "Person", StringComparison.OrdinalIgnoreCase);
              var automation = string.Equals(request.InitiationMode, "Automation", StringComparison.OrdinalIgnoreCase);
              if (!person && !automation) throw new ArgumentException("JVE-ACTOR-INITIATION-MODE-INVALID");
              if (person && request.InitiatingUserId is null) throw new ArgumentException("JVE-ACTOR-INITIATING-USER-REQUIRED");
              if (automation && string.IsNullOrWhiteSpace(request.AutomationContext)) throw new ArgumentException("JVE-ACTOR-AUTOMATION-CONTEXT-REQUIRED");
              if (person && request.InitiatingUserId is not null && request.InitiatingUserId != Guid.Parse(caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? Guid.Empty.ToString()))
                  throw new UnauthorizedAccessException("JVE-ACTOR-INITIATING-USER-MISMATCH");
          }
      }
      ```
    - **Required completion:** replace `DateTime.UtcNow` with the repository's injected clock, use the existing scope/authorization abstraction, and serialize the approved full envelope rather than only `Data` once the schema contract is confirmed. The snippet is the exact service shape, not permission to bypass existing auth.
    - _Requirements: 1.1–1.9, 8.3, 11.1, 11.6_

  - [ ] 4.4 CREATE `Finnova.SystemAdminService/Controllers/JvPostingController.cs`
    - Add only the authenticated submission endpoint to the existing SystemAdmin host. Do not add worker posting endpoints or a public Python submission route.
    - **Complete file content:**
      ```csharp
      using Finnova.Models.Contracts.JVPosting;
      using Finnova.Service.JVPosting;
      using Microsoft.AspNetCore.Authorization;
      using Microsoft.AspNetCore.Mvc;

      namespace Finnova.SystemAdminService.Controllers;

      [ApiController]
      [Route("api/jv-posting")]
      [Authorize]
      public sealed class JvPostingController(JvPostingSubmissionService service) : ControllerBase
      {
          [HttpPost("requests")]
          public async Task<ActionResult<JvPostingSubmissionResponse>> Submit(
              [FromBody] JvPostingSubmissionRequest request,
              CancellationToken cancellationToken)
              => Ok(await service.SubmitAsync(request, User, cancellationToken));
      }
      ```
    - _Requirements: 1.1–1.9, 11.1, 11.6_

  - [ ] 4.5 MODIFY `Finnova.Repository/Context/FinnovaDbContext.cs`, `Finnova.Repository/DependencyInjection.cs`, and `Finnova.SystemAdminService/Program.cs`
    - Register the inbox DbSet, EF configuration/service, MediatR/service assembly if needed, and the existing authorization/scope service. Preserve all existing registrations.
    - **Insertion in `FinnovaDbContext.cs`:** immediately after the last existing `DbSet` and before `OnModelCreating`:
      ```csharp
      public DbSet<JvPostingInbox> JvPostingInboxes => Set<JvPostingInbox>();
      ```
    - **Insertion in `DependencyInjection.cs`:** after the existing repository registrations:
      ```csharp
      services.AddScoped<JvPostingSubmissionService>();
      ```
    - **Insertion in `Program.cs`:** after the existing SystemAdmin service registrations and before `builder.Build()`:
      ```csharp
      // JVE submission only. The Python worker owns processing and canonical posting.
      builder.Services.AddScoped<Finnova.Service.JVPosting.JvPostingSubmissionService>();
      ```
    - Add only the policy/claim registration needed by the existing auth model for scoped submission. Do not add a worker posting policy or a UI route.
    - _Requirements: 1.1–1.9, 11.1–11.3, 15.1_

  - [ ] 4.6 Checkpoint — verify the API writes only feature-owned inbox data, captures actor context, marks `Ready`, and cannot invoke Cashflow, GL, or Journal Writer.

- [ ] 5. Create the feature-owned SQL Server schema and actor configuration
  - [ ] 5.1 CREATE `jv-posting-worker/migrations/001_jve_schema_and_inbox.sql`
    - Create the schema registry and inbox only. Include immutable actor fields, payload hash, UTC timestamps, rowversion, status/retry/lease/Posted constraints, and indexes. Do not create any canonical journal table.
    - **Complete file content:**
      ```sql
      IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'jve') EXEC('CREATE SCHEMA jve AUTHORIZATION dbo');
      GO

      CREATE TABLE jve.schema_registry (
          schema_version nvarchar(40) NOT NULL CONSTRAINT pk_jve_schema_registry PRIMARY KEY,
          schema_identifier nvarchar(200) NOT NULL,
          schema_hash binary(32) NOT NULL,
          max_payload_bytes int NOT NULL,
          unknown_field_policy nvarchar(30) NOT NULL,
          status nvarchar(20) NOT NULL,
          created_at_utc datetime2(7) NOT NULL CONSTRAINT df_jve_schema_created DEFAULT SYSUTCDATETIME(),
          retired_at_utc datetime2(7) NULL,
          CONSTRAINT ck_jve_schema_payload CHECK (max_payload_bytes > 0),
          CONSTRAINT ck_jve_schema_status CHECK (status IN ('Active','Retired')),
          CONSTRAINT ck_jve_schema_unknown CHECK (unknown_field_policy IN ('Retain','Reject'))
      );
      GO

      CREATE TABLE jve.posting_inbox (
          request_id uniqueidentifier NOT NULL CONSTRAINT pk_jve_inbox PRIMARY KEY,
          source_system nvarchar(100) NOT NULL,
          event_type nvarchar(150) NULL,
          rule_key nvarchar(150) NULL,
          schema_version nvarchar(40) NOT NULL,
          company_id uniqueidentifier NOT NULL,
          branch_id uniqueidentifier NOT NULL,
          value_date date NOT NULL,
          posting_date date NULL,
          currency_code nvarchar(10) NULL,
          description nvarchar(500) NULL,
          raw_payload varbinary(max) NOT NULL,
          payload_hash binary(32) NOT NULL,
          initiation_mode nvarchar(20) NOT NULL,
          initiating_user_id uniqueidentifier NULL,
          automation_context nvarchar(200) NULL,
          status nvarchar(20) NOT NULL,
          attempt_count int NOT NULL CONSTRAINT df_jve_inbox_attempts DEFAULT 0,
          next_retry_at_utc datetime2(7) NULL,
          worker_id nvarchar(200) NULL,
          claimed_at_utc datetime2(7) NULL,
          lease_expires_at_utc datetime2(7) NULL,
          heartbeat_at_utc datetime2(7) NULL,
          journal_id uniqueidentifier NULL,
          posted_at_utc datetime2(7) NULL,
          posting_user_id uniqueidentifier NULL,
          rule_version nvarchar(100) NULL,
          debit_total decimal(28,8) NULL,
          credit_total decimal(28,8) NULL,
          last_error_category nvarchar(30) NULL,
          last_error_code nvarchar(100) NULL,
          last_error_message_safe nvarchar(1000) NULL,
          created_at_utc datetime2(7) NOT NULL CONSTRAINT df_jve_inbox_created DEFAULT SYSUTCDATETIME(),
          updated_at_utc datetime2(7) NOT NULL CONSTRAINT df_jve_inbox_updated DEFAULT SYSUTCDATETIME(),
          row_version rowversion NOT NULL,
          CONSTRAINT fk_jve_inbox_schema FOREIGN KEY (schema_version) REFERENCES jve.schema_registry(schema_version),
          CONSTRAINT ck_jve_inbox_rule CHECK (event_type IS NOT NULL OR rule_key IS NOT NULL),
          CONSTRAINT ck_jve_inbox_actor CHECK ((initiation_mode = 'Person' AND initiating_user_id IS NOT NULL AND automation_context IS NULL) OR (initiation_mode = 'Automation' AND initiating_user_id IS NULL AND automation_context IS NOT NULL)),
          CONSTRAINT ck_jve_inbox_status CHECK (status IN ('Ready','Processing','RetryPending','Posted','DeadLetter','Cancelled')),
          CONSTRAINT ck_jve_inbox_attempt_count CHECK (attempt_count >= 0),
          CONSTRAINT ck_jve_inbox_retry CHECK ((status = 'RetryPending' AND next_retry_at_utc IS NOT NULL) OR (status <> 'RetryPending' AND next_retry_at_utc IS NULL)),
          CONSTRAINT ck_jve_inbox_posted CHECK ((status = 'Posted' AND journal_id IS NOT NULL AND posted_at_utc IS NOT NULL) OR (status <> 'Posted' AND journal_id IS NULL AND posted_at_utc IS NULL)),
          CONSTRAINT ck_jve_inbox_lease CHECK ((status = 'Processing' AND worker_id IS NOT NULL AND claimed_at_utc IS NOT NULL AND lease_expires_at_utc IS NOT NULL) OR status <> 'Processing')
      );
      GO

      CREATE UNIQUE INDEX ux_jve_inbox_identity ON jve.posting_inbox(request_id, payload_hash);
      CREATE INDEX ix_jve_inbox_eligible ON jve.posting_inbox(status, next_retry_at_utc, created_at_utc, request_id);
      CREATE INDEX ix_jve_inbox_stale ON jve.posting_inbox(status, lease_expires_at_utc) WHERE status = 'Processing';
      CREATE INDEX ix_jve_inbox_scope ON jve.posting_inbox(company_id, branch_id, status, created_at_utc);
      CREATE INDEX ix_jve_inbox_journal ON jve.posting_inbox(journal_id) WHERE journal_id IS NOT NULL;
      GO
      ```
    - _Requirements: 1.1–1.7, 6.5, 7.1–7.4, 8.3, 8.6, 15.1–15.8_

  - [ ] 5.2 CREATE `jv-posting-worker/migrations/002_jve_history.sql`
    - Create append-only attempts, dead-letter snapshots, lifecycle audit, and reconciliation records. Reconciliation stores the canonical journal ID and actor fields but does not define a competing canonical source-link table.
    - **Complete file content:**
      ```sql
      CREATE TABLE jve.posting_attempts (
          attempt_id uniqueidentifier NOT NULL CONSTRAINT pk_jve_attempts PRIMARY KEY,
          request_id uniqueidentifier NOT NULL,
          attempt_number int NOT NULL,
          attempt_correlation_id uniqueidentifier NOT NULL,
          worker_id nvarchar(200) NOT NULL,
          started_at_utc datetime2(7) NOT NULL,
          ended_at_utc datetime2(7) NULL,
          outcome nvarchar(20) NOT NULL,
          error_category nvarchar(30) NULL,
          error_code nvarchar(100) NULL,
          safe_error_message nvarchar(1000) NULL,
          dependency_name nvarchar(100) NULL,
          journal_id uniqueidentifier NULL,
          diagnostic_reference nvarchar(200) NULL,
          CONSTRAINT fk_jve_attempt_request FOREIGN KEY (request_id) REFERENCES jve.posting_inbox(request_id),
          CONSTRAINT uq_jve_attempt_number UNIQUE (request_id, attempt_number),
          CONSTRAINT ck_jve_attempt_number CHECK (attempt_number > 0),
          CONSTRAINT ck_jve_attempt_outcome CHECK (outcome IN ('Processing','Posted','RetryPending','DeadLetter','Cancelled','Reconciled'))
      );

      CREATE TABLE jve.dead_letters (
          dead_letter_id uniqueidentifier NOT NULL CONSTRAINT pk_jve_dead_letters PRIMARY KEY,
          request_id uniqueidentifier NOT NULL,
          failure_category nvarchar(30) NOT NULL,
          failure_code nvarchar(100) NOT NULL,
          safe_message nvarchar(1000) NOT NULL,
          attempt_count int NOT NULL,
          last_worker_id nvarchar(200) NULL,
          dead_lettered_at_utc datetime2(7) NOT NULL,
          reprocessed_by nvarchar(200) NULL,
          reprocess_reason nvarchar(1000) NULL,
          CONSTRAINT fk_jve_dead_request FOREIGN KEY (request_id) REFERENCES jve.posting_inbox(request_id)
      );

      CREATE TABLE jve.lifecycle_audit (
          audit_id uniqueidentifier NOT NULL CONSTRAINT pk_jve_audit PRIMARY KEY,
          request_id uniqueidentifier NOT NULL,
          prior_status nvarchar(20) NULL,
          new_status nvarchar(20) NOT NULL,
          action nvarchar(40) NOT NULL,
          actor_or_worker nvarchar(200) NOT NULL,
          correlation_id uniqueidentifier NOT NULL,
          reason_code nvarchar(100) NULL,
          details_json nvarchar(max) NULL,
          occurred_at_utc datetime2(7) NOT NULL,
          CONSTRAINT fk_jve_audit_request FOREIGN KEY (request_id) REFERENCES jve.posting_inbox(request_id)
      );

      CREATE TABLE jve.reconciliation (
          reconciliation_id uniqueidentifier NOT NULL CONSTRAINT pk_jve_reconciliation PRIMARY KEY,
          request_id uniqueidentifier NOT NULL,
          source_system nvarchar(100) NOT NULL,
          rule_key nvarchar(150) NOT NULL,
          rule_version nvarchar(100) NOT NULL,
          company_id uniqueidentifier NOT NULL,
          branch_id uniqueidentifier NOT NULL,
          value_date date NOT NULL,
          posting_date date NOT NULL,
          accounting_period nvarchar(40) NOT NULL,
          currency_code nvarchar(10) NOT NULL,
          debit_total decimal(28,8) NOT NULL,
          credit_total decimal(28,8) NOT NULL,
          initiating_user_id uniqueidentifier NULL,
          posting_user_id uniqueidentifier NOT NULL,
          canonical_journal_id uniqueidentifier NOT NULL,
          original_request_id uniqueidentifier NULL,
          original_journal_id uniqueidentifier NULL,
          created_at_utc datetime2(7) NOT NULL,
          CONSTRAINT fk_jve_recon_request FOREIGN KEY (request_id) REFERENCES jve.posting_inbox(request_id),
          CONSTRAINT uq_jve_recon_request UNIQUE (request_id),
          CONSTRAINT ck_jve_recon_balanced CHECK (debit_total = credit_total)
      );
      GO

      CREATE INDEX ix_jve_attempt_request ON jve.posting_attempts(request_id, started_at_utc DESC);
      CREATE INDEX ix_jve_dead_request ON jve.dead_letters(request_id, dead_lettered_at_utc DESC);
      CREATE INDEX ix_jve_audit_request ON jve.lifecycle_audit(request_id, occurred_at_utc, audit_id);
      CREATE INDEX ix_jve_recon_journal ON jve.reconciliation(canonical_journal_id);
      GO
      ```
    - _Requirements: 6.1–6.10, 9.3–9.10, 10.1–10.7, 14.3, 15.4–15.8_

  - [ ] 5.3 CREATE `jv-posting-worker/migrations/003_jve_permissions.sql`
    - Create least-privilege roles and append-only grants. The worker cannot modify master data, identities, actor configuration, or historical rows.
    - **Complete file content:**
      ```sql
      CREATE ROLE jve_worker_role;
      CREATE ROLE jve_submission_role;
      CREATE ROLE jve_operations_read_role;
      CREATE ROLE jve_operations_recovery_role;
      GO

      GRANT SELECT, INSERT, UPDATE ON jve.posting_inbox TO jve_worker_role;
      GRANT SELECT, INSERT ON jve.posting_attempts, jve.dead_letters, jve.lifecycle_audit, jve.reconciliation TO jve_worker_role;
      GRANT SELECT ON jve.schema_registry TO jve_worker_role;
      GRANT SELECT, INSERT ON jve.posting_inbox TO jve_submission_role;
      GRANT SELECT ON jve.posting_inbox, jve.posting_attempts, jve.dead_letters, jve.lifecycle_audit, jve.reconciliation TO jve_operations_read_role;
      GRANT UPDATE ON jve.posting_inbox TO jve_operations_recovery_role;
      DENY UPDATE, DELETE ON jve.posting_attempts, jve.dead_letters, jve.lifecycle_audit, jve.reconciliation TO jve_worker_role, jve_operations_recovery_role;
      DENY DELETE ON jve.posting_inbox TO jve_worker_role, jve_submission_role, jve_operations_read_role, jve_operations_recovery_role;
      ```
    - _Requirements: 10.5, 11.2–11.5, 15.6_

  - [ ] 5.4 CREATE `jv-posting-worker/migrations/004_jve_schema_1_0.sql` only after the approved schema hash is known
    - Register the immutable approved schema. Replace every value with the stakeholder-approved identifier/hash before applying; never apply a placeholder hash.
    - **Complete file content template:**
      ```sql
      INSERT INTO jve.schema_registry
          (schema_version, schema_identifier, schema_hash, max_payload_bytes, unknown_field_policy, status)
      VALUES
          ('1.0', 'REPLACE_WITH_APPROVED_SCHEMA_IDENTIFIER', 0xREPLACE_WITH_64_HEX_CHAR_HASH, 1048576, 'Retain', 'Active');
      ```
    - _Requirements: 2.1, 2.6, 2.8, 15.1, 15.3_

  - [ ] 5.5 CREATE `jv-posting-worker/src/jve/actor_config.py`
    - Load read-only automation-context-to-SystemUser configuration and reject missing, inactive, ambiguous, or out-of-scope identities. This task owns configuration/seed shape only; it must not create users in the worker.
    - **Complete file content:**
      ```python
      from __future__ import annotations

      from dataclasses import dataclass
      from uuid import UUID


      @dataclass(frozen=True)
      class SystemUserBinding:
          automation_context: str
          system_user_id: UUID
          active: bool
          company_ids: frozenset[UUID]
          branch_ids: frozenset[UUID]


      class SystemUserConfiguration:
          def __init__(self, bindings: tuple[SystemUserBinding, ...]) -> None:
              self._bindings = bindings

          def resolve(self, automation_context: str | None, company_id: UUID, branch_id: UUID) -> UUID:
              matches = [binding for binding in self._bindings if binding.automation_context == automation_context and binding.active and company_id in binding.company_ids and branch_id in binding.branch_ids]
              if len(matches) != 1:
                  raise ValueError("JVE-ACTOR-SYSTEM-USER-UNRESOLVED")
              return matches[0].system_user_id
      ```
    - Add the approved SystemUser seed/configuration migration in the owning identity module only after the identifier and ownership are confirmed. Do not invent a user GUID in this task.
    - _Requirements: 6.8–6.10, 11.2–11.4, 15.1–15.2_

  - [ ] 5.6 Checkpoint — confirm actor seed/configuration ownership, person/automation scope checks, and that no worker path can substitute worker ID or database principal as Posting User.

- [ ] 6. Implement SQL claim, lease, retry, detail, and recovery repositories
  - [ ] 6.1 CREATE `jv-posting-worker/src/jve/sql.py`
    - Use parameterized SQL Server commands, explicit UTC values, ordered atomic claim with `UPDLOCK`/`READPAST`, row-version/CAS ownership checks, and append-only attempt/audit writes. Submission remains in the approved .NET API; this repository must not expose a public submit endpoint.
    - **Complete file content:**
      ```python
      from __future__ import annotations

      from contextlib import contextmanager
      from datetime import datetime
      from uuid import UUID

      import pyodbc

      from .domain import Failure, Status


      class SqlConnectionFactory:
          def __init__(self, connection_string: str) -> None:
              self._connection_string = connection_string

          @contextmanager
          def connection(self):
              connection = pyodbc.connect(self._connection_string, autocommit=False)
              try:
                  yield connection
                  connection.commit()
              except Exception:
                  connection.rollback()
                  raise
              finally:
                  connection.close()


      class SqlInboxRepository:
          def __init__(self, factory: SqlConnectionFactory) -> None:
              self._factory = factory

          def claim_batch(self, now_utc: datetime, worker_id: str, lease_until: datetime, batch_size: int) -> list[dict]:
              with self._factory.connection() as connection:
                  cursor = connection.cursor()
                  cursor.execute(""";WITH candidates AS (
                      SELECT TOP (?) request_id
                      FROM jve.posting_inbox WITH (UPDLOCK, READPAST, ROWLOCK)
                      WHERE status = 'Ready' OR (status = 'RetryPending' AND next_retry_at_utc <= ?)
                      ORDER BY created_at_utc ASC, request_id ASC
                  )
                  UPDATE inbox SET status = 'Processing', worker_id = ?, claimed_at_utc = ?, lease_expires_at_utc = ?, heartbeat_at_utc = ?, updated_at_utc = SYSUTCDATETIME()
                  OUTPUT inserted.request_id, inserted.attempt_count, inserted.row_version, inserted.raw_payload, inserted.schema_version, inserted.company_id, inserted.branch_id
                  FROM jve.posting_inbox inbox INNER JOIN candidates ON candidates.request_id = inbox.request_id;""", batch_size, now_utc, worker_id, now_utc, lease_until, now_utc)
                  return [{"request_id": row.request_id, "attempt_count": row.attempt_count, "row_version": bytes(row.row_version), "raw_payload": bytes(row.raw_payload), "schema_version": row.schema_version, "company_id": row.company_id, "branch_id": row.branch_id} for row in cursor.fetchall()]

          def owns_lease(self, request_id: UUID, worker_id: str, attempt_id: UUID, now_utc: datetime) -> bool:
              with self._factory.connection() as connection:
                  row = connection.execute("SELECT 1 FROM jve.posting_inbox WHERE request_id = ? AND status = 'Processing' AND worker_id = ? AND lease_expires_at_utc > ?", str(request_id), worker_id, now_utc).fetchone()
                  return row is not None

          def heartbeat(self, request_id: UUID, worker_id: str, attempt_id: UUID, lease_until: datetime) -> bool:
              with self._factory.connection() as connection:
                  cursor = connection.execute("UPDATE jve.posting_inbox SET lease_expires_at_utc = ?, heartbeat_at_utc = SYSUTCDATETIME(), updated_at_utc = SYSUTCDATETIME() WHERE request_id = ? AND status = 'Processing' AND worker_id = ? AND lease_expires_at_utc > SYSUTCDATETIME()", lease_until, str(request_id), worker_id)
                  return cursor.rowcount == 1

          def complete(self, request_id: UUID, worker_id: str, attempt_id: UUID, status: Status, journal_id: UUID | None, failure: Failure | None) -> None:
              if status == Status.POSTED and journal_id is None:
                  raise ValueError("JVE-POSTED-JOURNAL-ID-REQUIRED")
              with self._factory.connection() as connection:
                  cursor = connection.execute("""UPDATE jve.posting_inbox SET status = ?, journal_id = ?, posted_at_utc = CASE WHEN ? = 'Posted' THEN SYSUTCDATETIME() ELSE NULL END, next_retry_at_utc = CASE WHEN ? = 'RetryPending' THEN DATEADD(MINUTE, 1, SYSUTCDATETIME()) ELSE NULL END, last_error_category = ?, last_error_code = ?, last_error_message_safe = ?, updated_at_utc = SYSUTCDATETIME() WHERE request_id = ? AND status = 'Processing' AND worker_id = ?""", status.value, str(journal_id) if journal_id else None, status.value, status.value, failure.category.value if failure else None, failure.code if failure else None, failure.message if failure else None, str(request_id), worker_id)
                  if cursor.rowcount != 1:
                      raise RuntimeError("JVE-CONCURRENCY-COMPLETE-LOST")

          def queue_status(self, company_id: UUID | None, branch_id: UUID | None) -> list[dict]:
              with self._factory.connection() as connection:
                  cursor = connection.execute("""SELECT status, COUNT_BIG(*) AS count, CASE WHEN COUNT_BIG(*) = 0 THEN NULL ELSE DATEDIFF_BIG(SECOND, MIN(created_at_utc), SYSUTCDATETIME()) END AS oldest_record_age_seconds FROM jve.posting_inbox WHERE (? IS NULL OR company_id = ?) AND (? IS NULL OR branch_id = ?) GROUP BY status""", str(company_id) if company_id else None, str(company_id) if company_id else None, str(branch_id) if branch_id else None, str(branch_id) if branch_id else None)
                  return [dict(zip([column[0] for column in cursor.description], row)) for row in cursor.fetchall()]

          def get_request(self, request_id: UUID) -> dict | None:
              with self._factory.connection() as connection:
                  cursor = connection.execute("SELECT request_id, source_system, company_id, branch_id, status, attempt_count, journal_id, posting_user_id, created_at_utc, updated_at_utc, last_error_category, last_error_code, last_error_message_safe FROM jve.posting_inbox WHERE request_id = ?", str(request_id))
                  row = cursor.fetchone()
                  return dict(zip([column[0] for column in cursor.description], row)) if row else None

          def recover_expired(self, request_id: UUID, actor: str, reason: str) -> bool:
              with self._factory.connection() as connection:
                  cursor = connection.execute("UPDATE jve.posting_inbox SET status = 'Ready', worker_id = NULL, claimed_at_utc = NULL, lease_expires_at_utc = NULL, heartbeat_at_utc = NULL, updated_at_utc = SYSUTCDATETIME() WHERE request_id = ? AND status = 'Processing' AND lease_expires_at_utc <= SYSUTCDATETIME()", str(request_id))
                  return cursor.rowcount == 1

          def reprocess_dead_letter(self, request_id: UUID, actor: str, reason: str) -> bool:
              with self._factory.connection() as connection:
                  cursor = connection.execute("UPDATE jve.posting_inbox SET status = 'Ready', next_retry_at_utc = NULL, updated_at_utc = SYSUTCDATETIME() WHERE request_id = ? AND status = 'DeadLetter'", str(request_id))
                  return cursor.rowcount == 1

          def cancel(self, request_id: UUID, actor: str, reason: str) -> bool:
              with self._factory.connection() as connection:
                  cursor = connection.execute("UPDATE jve.posting_inbox SET status = 'Cancelled', updated_at_utc = SYSUTCDATETIME() WHERE request_id = ? AND status IN ('Ready','RetryPending') AND journal_id IS NULL", str(request_id))
                  return cursor.rowcount == 1
      ```
    - **Required completion:** transactionally insert the attempt and lifecycle audit with each claim/recovery, preserve every recovery reason, and replace the fixed retry minute with the injected policy. The code must use the confirmed SQL Server isolation/locking policy.
    - _Requirements: 7.1–7.9, 8.3–8.6, 9.1–9.10, 10.1, 12.1–12.5, 15.7_

  - [ ] 6.2 Write SQL claim/recovery integration tests (optional)
    - Create `jv-posting-worker/tests/integration/test_claim_and_recovery.py` against isolated SQL Server. Verify ordered single-winner claims, lease expiry, heartbeat ownership, CAS recovery, duplicate submission behavior through the .NET boundary, retry eligibility, and immutable history.
    - _Requirements: 1.4–1.7, 7.1–7.9, 8.3–8.6, 9.1–9.10_

- [ ] 7. Configure actor resolution and Cashflow/GL/context integration
  - [ ] 7.1 CREATE `jv-posting-worker/src/jve/schema_registry.py` integration and adapter factory changes
    - Register the approved schema registry, construct the configurable Cashflow adapter, GL/context adapters, and `ConfiguredActorResolver` from environment/configuration. Keep all transport URLs, timeouts, and actor references configuration-driven.
    - **Exact insertion in `runtime.py`:** replace the `PostingCoordinator.from_settings` construction with:
      ```python
      coordinator = PostingCoordinator.from_settings(
          settings=settings,
          factory=factory,
          inbox=inbox,
          cashflow_base_url=settings.cashflow_base_url,
          gl_base_url=settings.gl_base_url,
          actor_configuration_reference=settings.actor_configuration_reference,
          canonical_writer_procedure=settings.canonical_writer_procedure,
      )
      ```
    - _Requirements: 2.1, 3.1–3.11, 5.2–5.8, 6.7–6.10, 11.2–11.4, 15.3_

  - [ ] 7.2 Write actor and dependency failure tests (optional)
    - Verify the active initiating UserId is passed unchanged, the configured SystemUser is used for automation, worker/database identity is never used, no-rule and ambiguous-rule failures do not write journals, and dependency timeouts retry without changing raw payload.
    - _Requirements: 3.3–3.5, 3.10–3.11, 6.7–6.10, 8.4, 9.1–9.4_

- [ ] 8. Map and implement the canonical same-database Journal Writer
  - [ ] 8.1 Checkpoint — record the confirmed canonical mapping before writing posting SQL
    - Confirm and record the exact existing accounting/GL owner, Journal Header table/key, Journal Line table/foreign key, canonical source-link table/unique key, actor column (`PostingUserId` or `PostedByUserId`), final Posted status, transaction owner/procedure name, and required grants.
    - **Required mapping artifact:** create or update `jv-posting-worker/config/canonical-journal-mapping.json` only after confirmation:
      ```json
      {
        "headerTable": "CONFIRMED_EXISTING_TABLE",
        "headerKeyColumn": "CONFIRMED_EXISTING_KEY",
        "lineTable": "CONFIRMED_EXISTING_TABLE",
        "lineHeaderForeignKey": "CONFIRMED_EXISTING_FOREIGN_KEY",
        "sourceLinkTable": "CONFIRMED_EXISTING_SOURCE_LINK_TABLE",
        "sourceLinkRequestColumn": "CONFIRMED_EXISTING_REQUEST_COLUMN",
        "sourceLinkJournalColumn": "CONFIRMED_EXISTING_JOURNAL_COLUMN",
        "postingActorColumn": "PostingUserId_OR_PostedByUserId",
        "postedStatusValue": "CONFIRMED_EXISTING_POSTED_VALUE",
        "atomicProcedure": "CONFIRMED_EXISTING_OR_APPROVED_PROCEDURE"
      }
      ```
    - Do not copy the next task until every `CONFIRMED_*` value is replaced. Do not create a second journal model or table.
    - _Requirements: 4.3, 6.1–6.6, 6.7–6.10, 8.1, 8.5, 8.6, 10.2, 15.5, 15.8_

  - [ ] 8.2 CREATE `jv-posting-worker/src/jve/canonical_journal.py`
    - Define the canonical writer boundary and procedure parameter contract without naming guessed accounting tables. The implementation must use the confirmed procedure/mapping and the same SQL connection/transaction.
    - **Complete file content:**
      ```python
      from __future__ import annotations

      from uuid import UUID

      from .domain import ResolvedActor, Voucher


      class CanonicalJournalWriter:
          def __init__(self, connection_factory, procedure_name: str) -> None:
              self._factory = connection_factory
              self._procedure_name = procedure_name

          def find_by_source(self, request_id: UUID) -> UUID | None:
              with self._factory.connection() as connection:
                  row = connection.execute("EXEC jve_find_canonical_source_link @RequestId = ?", str(request_id)).fetchone()
                  return UUID(str(row[0])) if row else None

          def write_atomic(self, request_id: UUID, attempt_id: UUID, voucher: Voucher, actor: ResolvedActor, expected_row_version: bytes) -> UUID:
              with self._factory.connection() as connection:
                  cursor = connection.cursor()
                  cursor.execute(f"EXEC {self._procedure_name} @RequestId = ?, @AttemptId = ?, @ExpectedRowVersion = ?, @PostingUserId = ?, @VoucherJson = ?", str(request_id), str(attempt_id), expected_row_version, str(actor.user_id), _voucher_json(voucher))
                  row = cursor.fetchone()
                  if row is None:
                      raise RuntimeError("JVE-JOURNAL-WRITER-NO-JOURNAL-ID")
                  return UUID(str(row[0]))


      def _voucher_json(voucher: Voucher) -> str:
          import json
          return json.dumps({
              "requestId": str(voucher.request_id),
              "ruleVersion": voucher.rule_version,
              "context": {
                  "companyId": str(voucher.context.company_id),
                  "branchId": str(voucher.context.branch_id),
                  "currencyCode": voucher.context.currency_code,
                  "valueDate": voucher.context.value_date.isoformat(),
                  "postingDate": voucher.context.posting_date.isoformat(),
                  "accountingPeriod": voucher.context.accounting_period,
              },
              "lines": [line.__dict__ for line in voucher.lines],
              "debitTotal": str(voucher.debit_total),
              "creditTotal": str(voucher.credit_total),
              "fingerprint": voucher.fingerprint,
          }, default=str, sort_keys=True)
      ```
    - **Required completion:** replace `jve_find_canonical_source_link` with the confirmed lookup boundary; never interpolate untrusted table names; execute the approved atomic procedure in the same transaction that rechecks ownership, rowversion, immutable payload hash, existing source link, and idempotency. The procedure must write Header, Lines, canonical source link, actor attribution, reconciliation, audit, attempt result, and inbox `Posted` state together, and return the existing journal on duplicate source link.
    - _Requirements: 6.1–6.6, 8.1–8.6, 10.2, 10.7, 15.5, 15.8_

  - [ ] 8.3 Write canonical writer contract tests (optional)
    - Test the confirmed procedure contract with an isolated SQL Server integration fixture. Cover actor column mapping, balanced Header/Lines, source-link uniqueness, expected-rowversion conflict, rollback of every participating record, uncertain response lookup, and duplicate reconciliation. Do not assert guessed table names.
    - _Requirements: 6.1–6.10, 8.1–8.6, 10.2, 10.7_

- [ ] 9. Implement posting coordination, retry/dead-letter handling, and observability
  - [ ] 9.1 CREATE `jv-posting-worker/src/jve/observability.py`
    - Emit exactly one invocation start/end event, request milestones, stable category/code errors, numeric counters, and redacted fields. Use UTC technical timestamps and no raw payloads or sensitive values.
    - **Complete file content:**
      ```python
      from __future__ import annotations

      from datetime import datetime
      from uuid import UUID

      import structlog

      from .domain import Failure
      from .services import redact


      class JsonObservability:
          def __init__(self) -> None:
              self._log = structlog.get_logger("jve")

          def invocation_start(self, worker_id: str, correlation_id: UUID, started_at_utc: datetime) -> None:
              self._log.info("invocation_start", worker_id=worker_id, invocation_correlation_id=str(correlation_id), occurred_at_utc=started_at_utc.isoformat())

          def invocation_end(self, worker_id: str, correlation_id: UUID, started_at_utc: datetime, ended_at_utc: datetime, counters: dict[str, int]) -> None:
              self._log.info("invocation_end", worker_id=worker_id, invocation_correlation_id=str(correlation_id), started_at_utc=started_at_utc.isoformat(), ended_at_utc=ended_at_utc.isoformat(), **{key: max(0, int(value)) for key, value in counters.items()})

          def milestone(self, request_id: UUID, correlation_id: UUID, milestone: str, occurred_at_utc: datetime, **fields: object) -> None:
              self._log.info("request_milestone", request_id=str(request_id), invocation_correlation_id=str(correlation_id), milestone=milestone, occurred_at_utc=occurred_at_utc.isoformat(), **redact(fields))

          def error(self, failure: Failure, correlation_id: UUID, occurred_at_utc: datetime, **fields: object) -> None:
              self._log.error("jve_error", category=failure.category.value, code=failure.code, dependency_name=failure.dependency_name, correlation_id=str(correlation_id), occurred_at_utc=occurred_at_utc.isoformat(), **redact(fields))
      ```
    - _Requirements: 10.3, 11.5, 12.5–12.6, 13.1–13.9_

  - [ ] 9.2 CREATE `jv-posting-worker/src/jve/coordinator.py`
    - Orchestrate claim records, parse/validate, Cashflow/GL/context calls, actor resolution, deterministic voucher creation, ownership checks, canonical writer lookup/write, attempt outcomes, and request-level continue-on-error. Enforce the processing deadline before every external or final write.
    - **Complete file content:**
      ```python
      from __future__ import annotations

      from datetime import timedelta
      from uuid import UUID, uuid4

      from .domain import Failure, FailureCategory, Status
      from .services import EnvelopeParser, build_voucher, resolve_currency, resolve_posting_date
      from .state import retry_at


      class PostingCoordinator:
          def __init__(self, settings, inbox, attempts, parser, cashflow, context, gl, actor, writer, clock, observability) -> None:
              self.settings, self.inbox, self.attempts = settings, inbox, attempts
              self.parser, self.cashflow, self.context, self.gl = parser, cashflow, context, gl
              self.actor, self.writer, self.clock, self.observability = actor, writer, clock, observability

          @classmethod
          def from_settings(cls, settings, factory, inbox, **adapters):
              raise NotImplementedError("Construct confirmed adapters and canonical writer in the implementation task")

          def process(self, record: dict, invocation_id: UUID) -> str:
              request_id = UUID(str(record["request_id"]))
              started = self.clock.now_utc()
              attempt = self.attempts.start(request_id, int(record["attempt_count"]) + 1, uuid4(), self.settings.worker_id, started)
              try:
                  envelope = self.parser.parse(record["raw_payload"])
                  self.observability.milestone(request_id, invocation_id, "validation", self.clock.now_utc())
                  rule = self.cashflow.resolve_rule(envelope, self.settings.cashflow_timeout_seconds)
                  context = self.context.resolve(envelope, rule, self.settings.business_timezone)
                  permitted = self.gl.validate(envelope, rule, context, self.settings.gl_timeout_seconds)
                  currency = resolve_currency(envelope.currency_code, rule, permitted)
                  if currency != context.currency_code or resolve_posting_date(envelope, rule) != context.posting_date:
                      raise ValueError("JVE-CONSISTENCY-CONTEXT-MISMATCH")
                  actor = self.actor.resolve(envelope.actor, envelope.company_id, envelope.branch_id)
                  voucher = build_voucher(envelope, rule, context, precision=self.settings.currency_precision, rounding_mode=self.settings.rounding_mode)
                  if self.clock.now_utc() - started >= timedelta(minutes=self.settings.processing_deadline_minutes):
                      raise RuntimeError("JVE-PROCESSING-DEADLINE")
                  if not self.inbox.owns_lease(request_id, self.settings.worker_id, attempt.attempt_id, self.clock.now_utc()):
                      raise RuntimeError("JVE-LEASE-LOST")
                  existing = self.writer.find_by_source(request_id)
                  journal_id = existing or self.writer.write_atomic(request_id, attempt.attempt_id, voucher, actor, record["row_version"])
                  self.attempts.finish(attempt.attempt_id, Status.POSTED, None, self.clock.now_utc(), journal_id)
                  return "posted"
              except Exception as error:
                  failure = getattr(error, "failure", Failure(FailureCategory.VALIDATION, str(error).split(":", 1)[0], "Posting request failed", False))
                  next_retry = retry_at(int(record["attempt_count"]), self.clock.now_utc(), self.settings.retry_delays_minutes) if failure.transient else None
                  target = Status.RETRY_PENDING if next_retry and int(record["attempt_count"]) + 1 < self.settings.max_attempts else Status.DEAD_LETTER
                  self.attempts.finish(attempt.attempt_id, target, failure, self.clock.now_utc())
                  self.inbox.complete(request_id, self.settings.worker_id, attempt.attempt_id, target, None, failure)
                  self.observability.error(failure, invocation_id, self.clock.now_utc(), request_id=str(request_id))
                  return "retried" if target == Status.RETRY_PENDING else "dead_lettered"


      class InvocationRunner:
          def __init__(self, settings, inbox, coordinator, clock, observability) -> None:
              self.settings, self.inbox, self.coordinator, self.clock, self.observability = settings, inbox, coordinator, clock, observability

          def run_once(self) -> None:
              invocation_id = uuid4()
              started = self.clock.now_utc()
              self.observability.invocation_start(self.settings.worker_id, invocation_id, started)
              counters = {"claimed": 0, "posted": 0, "retried": 0, "dead_lettered": 0, "failed": 0}
              try:
                  records = self.inbox.claim_batch(started, self.settings.worker_id, started + timedelta(minutes=self.settings.lease_minutes), self.settings.batch_size)
                  counters["claimed"] = len(records)
                  for record in records:
                      try:
                          counters[self.coordinator.process(record, invocation_id)] += 1
                      except Exception:
                          counters["failed"] += 1
              finally:
                  self.observability.invocation_end(self.settings.worker_id, invocation_id, started, self.clock.now_utc(), counters)
      ```
    - **Required completion:** replace the factory `NotImplementedError`, classify every exception into one stable failure category/code, persist `RuleVersion`/actor/totals, write dead-letter history, and ensure the canonical transaction owns the final inbox `Posted` update. The worker must never use its own identity as Posting User.
    - _Requirements: 2.1–2.9, 3.1–3.11, 4.1–4.10, 5.1–5.11, 6.1–6.10, 7.1–7.9, 8.1–8.8, 9.1–9.10, 13.1–13.9, 14.1–14.7_

  - [ ] 9.3 CREATE `jv-posting-worker/src/jve/recovery.py`
    - Implement authorized expired-lease recovery, dead-letter reprocessing, and pre-journal cancellation. Require a reason, compare-and-set the state, preserve all history, and never permit payload correction in the baseline.
    - **Complete file content:**
      ```python
      from uuid import UUID

      from .ports import InboxRepository, ObservabilityPort, RecoveryPort


      class RecoveryService:
          def __init__(self, inbox: InboxRepository, authorization: RecoveryPort, observability: ObservabilityPort) -> None:
              self._inbox, self._authorization, self._observability = inbox, authorization, observability

          def recover_expired_lease(self, request_id: UUID, actor: str, company_id: UUID, branch_id: UUID, reason: str) -> bool:
              self._authorize(actor, company_id, branch_id, "recover-expired-lease", reason)
              return self._inbox.recover_expired(request_id, actor, reason)

          def reprocess_dead_letter(self, request_id: UUID, actor: str, company_id: UUID, branch_id: UUID, reason: str) -> bool:
              self._authorize(actor, company_id, branch_id, "reprocess", reason)
              return self._inbox.reprocess_dead_letter(request_id, actor, reason)

          def cancel(self, request_id: UUID, actor: str, company_id: UUID, branch_id: UUID, reason: str) -> bool:
              self._authorize(actor, company_id, branch_id, "cancel", reason)
              return self._inbox.cancel(request_id, actor, reason)

          def _authorize(self, actor: str, company_id: UUID, branch_id: UUID, action: str, reason: str) -> None:
              if not reason.strip():
                  raise ValueError("JVE-RECOVERY-REASON-REQUIRED")
              self._authorization.authorize(actor, company_id, branch_id, action)
      ```
    - _Requirements: 9.7–9.10, 12.3–12.6, 14.5_

  - [ ] 9.4 Write coordinator/retry/reversal/observability tests (optional)
    - Cover no-rule/no-write, actor failures, retry boundaries, max-attempt dead letter, request-level continue-on-error, lost lease, uncertain writer response, existing-source reconciliation, reversal original-link checks, start/end counts, and redaction.
    - _Requirements: 8.1–8.8, 9.1–9.10, 10.1–10.7, 13.1–13.9, 14.1–14.7_

- [ ] 10. Add the protected operational API in the existing SystemAdmin host
  - [ ] 10.1 CREATE `Finnova.Models/Contracts/JVPosting/JvPostingOperationsContracts.cs`
    - Define safe queue/detail/recovery contracts. Do not return raw payloads, credentials, tokens, customer identity numbers, contact details, or financial account details by default.
    - **Complete file content:**
      ```csharp
      namespace Finnova.Models.Contracts.JVPosting;

      public sealed record JvPostingQueueBucket(string Status, long Count, long? OldestRecordAgeSeconds);
      public sealed record JvPostingRecoveryRequest(string Reason);
      public sealed record JvPostingDetailResponse(Guid RequestId, string SourceSystem, Guid CompanyId, Guid BranchId, string Status, int AttemptCount, Guid? JournalId, Guid? PostingUserId, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, string? LastErrorCategory, string? LastErrorCode);
      ```
    - _Requirements: 10.6, 11.5, 11.7–11.10, 12.1–12.8_

  - [ ] 10.2 CREATE `Finnova.Service/JVPosting/JvPostingOperationsService.cs`
    - Enforce read/recovery/cancel policy and company/branch scope before every operation. Use the Python/SQL repository boundary; do not expose a worker submission endpoint.
    - **Complete file content:**
      ```csharp
      using Finnova.Models.Contracts.JVPosting;
      using Finnova.Repository.Context;
      using Microsoft.EntityFrameworkCore;

      namespace Finnova.Service.JVPosting;

      public sealed class JvPostingOperationsService(FinnovaDbContext db)
      {
          public async Task<IReadOnlyList<JvPostingQueueBucket>> QueueStatusAsync(Guid? companyId, Guid? branchId, CancellationToken ct)
          {
              var query = db.JvPostingInboxes.AsNoTracking().AsQueryable();
              if (companyId.HasValue) query = query.Where(x => x.CompanyId == companyId.Value);
              if (branchId.HasValue) query = query.Where(x => x.BranchId == branchId.Value);
              var rows = await query.GroupBy(x => x.Status).Select(g => new JvPostingQueueBucket(g.Key.ToString(), g.LongCount(), g.LongCount() == 0 ? null : (long?)EF.Functions.DateDiffSecond(g.Min(x => x.CreatedAtUtc), DateTime.UtcNow))).ToListAsync(ct);
              return rows;
          }

          public async Task<JvPostingDetailResponse?> DetailAsync(Guid requestId, CancellationToken ct)
          {
              return await db.JvPostingInboxes.AsNoTracking().Where(x => x.RequestId == requestId).Select(x => new JvPostingDetailResponse(x.RequestId, x.SourceSystem, x.CompanyId, x.BranchId, x.Status.ToString(), x.AttemptCount, x.JournalId, x.PostingUserId, x.CreatedAtUtc, x.UpdatedAtUtc, x.LastErrorCategory, x.LastErrorCode)).SingleOrDefaultAsync(ct);
          }
      }
      ```
    - **Required completion:** bind recovery/cancel methods to the compare-and-set repository/recovery service, use the existing authorization/scope abstraction, and add attempts/failure details only through safe projections. Do not authorize by query parameters alone.
    - _Requirements: 9.7–9.10, 10.6, 11.7–11.10, 12.1–12.8_

  - [ ] 10.3 CREATE `Finnova.SystemAdminService/Controllers/JvPostingOperationsController.cs`
    - Add protected queue status, safe request detail, expired-lease recovery, dead-letter reprocess, and pre-journal cancellation to the existing SystemAdmin host.
    - **Complete file content:**
      ```csharp
      using Finnova.Models.Contracts.JVPosting;
      using Finnova.Service.JVPosting;
      using Microsoft.AspNetCore.Authorization;
      using Microsoft.AspNetCore.Mvc;

      namespace Finnova.SystemAdminService.Controllers;

      [ApiController]
      [Route("api/jv-posting/operations")]
      [Authorize(Policy = "JveOperationsRead")]
      public sealed class JvPostingOperationsController(JvPostingOperationsService service) : ControllerBase
      {
          [HttpGet("queue-status")]
          public Task<IReadOnlyList<JvPostingQueueBucket>> QueueStatus(Guid? companyId, Guid? branchId, CancellationToken ct) => service.QueueStatusAsync(companyId, branchId, ct);

          [HttpGet("requests/{requestId:guid}")]
          public Task<JvPostingDetailResponse?> Detail(Guid requestId, CancellationToken ct) => service.DetailAsync(requestId, ct);

          [HttpPost("requests/{requestId:guid}/recover-expired-lease")]
          [Authorize(Policy = "JveOperationsRecovery")]
          public Task<IActionResult> RecoverExpiredLease(Guid requestId, [FromBody] JvPostingRecoveryRequest request, CancellationToken ct) => throw new NotImplementedException("Bind to compare-and-set JVE recovery service.");

          [HttpPost("requests/{requestId:guid}/reprocess")]
          [Authorize(Policy = "JveOperationsRecovery")]
          public Task<IActionResult> Reprocess(Guid requestId, [FromBody] JvPostingRecoveryRequest request, CancellationToken ct) => throw new NotImplementedException("Bind to compare-and-set JVE recovery service.");

          [HttpPost("requests/{requestId:guid}/cancel")]
          [Authorize(Policy = "JveOperationsRecovery")]
          public Task<IActionResult> Cancel(Guid requestId, [FromBody] JvPostingRecoveryRequest request, CancellationToken ct) => throw new NotImplementedException("Bind to pre-journal cancellation service.");
      }
      ```
    - **Required completion:** replace each `NotImplementedException` with the approved recovery service, return 404 for missing requests, return safe RFC 7807 errors, and preserve existing controller conventions. This API is operational only; it cannot submit or post a journal.
    - _Requirements: 11.7–11.10, 12.1–12.8_

  - [ ] 10.4 MODIFY `Finnova.SystemAdminService/Program.cs`, `Finnova.SystemAdminService/Middleware/ExceptionHandlingMiddleware.cs`, and `Finnova.ApiGateway/appsettings.json`
    - Register the two operation policies using confirmed role/claim names, register services, add the JVE exception branch without replacing existing branches, and add only a gateway alias to the existing SystemAdmin cluster.
    - **Insertion in `Program.cs` inside `AddAuthorization`:**
      ```csharp
      options.AddPolicy("JveOperationsRead", policy => policy.RequireClaim("permission", "jve.operations.read"));
      options.AddPolicy("JveOperationsRecovery", policy => policy.RequireClaim("permission", "jve.operations.recovery"));
      ```
    - **Insertion in `Program.cs` before `builder.Build()`:**
      ```csharp
      builder.Services.AddScoped<Finnova.Service.JVPosting.JvPostingOperationsService>();
      ```
    - **Insertion in `ExceptionHandlingMiddleware.cs` inside the existing `ex switch` before the shared fallback:**
      ```csharp
      JveOperationException jve => (jve.StatusCode, jve.Code, jve.SafeMessage),
      ```
      Create the typed exception in the existing domain-exception location with `StatusCode`, `Code`, and `SafeMessage`; do not return SQL text or raw exception messages.
    - **Insertion in `Finnova.ApiGateway/appsettings.json` under `ReverseProxy.Routes`, adjacent to `systemadmin-route`:**
      ```json
      "jve-operations-alias-route": {
        "ClusterId": "systemadmin-cluster",
        "Match": { "Path": "/api/systemadmin/jv-posting/{**catch-all}" },
        "Transforms": [ { "PathRemovePrefix": "/api/systemadmin" } ]
      }
      ```
    - Do not add a new host/cluster and do not add any UI route.
    - _Requirements: 11.1, 11.7–11.10, 12.1–12.8_

  - [ ] 10.5 Checkpoint — verify operational endpoints are authenticated, scope checked, redacted, compare-and-set protected, and separate from submission and worker posting.

- [ ] 11. Add backend, worker, SQL, and host tests
  - [ ] 11.1 CREATE `jv-posting-worker/tests/conftest.py` and `jv-posting-worker/tests/fakes.py`
    - Supply a fixed UTC clock, fake actor/Cashflow/GL/writer ports, and valid English-only INR fixtures. Keep fakes in-memory except integration tests.
    - **`tests/conftest.py`:**
      ```python
      from datetime import datetime, timezone

      import pytest

      from jve.clock import Clock


      class FixedClock(Clock):
          def __init__(self, value: datetime) -> None:
              self.value = value

          def now_utc(self) -> datetime:
              return self.value


      @pytest.fixture
      def fixed_clock() -> FixedClock:
          return FixedClock(datetime(2024, 1, 1, tzinfo=timezone.utc))
      ```
    - **`tests/fakes.py`:**
      ```python
      from dataclasses import dataclass, field
      from uuid import UUID

      from jve.domain import ResolvedActor


      @dataclass
      class FakeWriter:
          existing: UUID | None = None
          writes: list[UUID] = field(default_factory=list)

          def find_by_source(self, request_id: UUID) -> UUID | None:
              return self.existing

          def write_atomic(self, request_id, attempt_id, voucher, actor: ResolvedActor, expected_row_version):
              journal_id = self.existing or UUID("00000000-0000-0000-0000-000000000001")
              self.writes.append(journal_id)
              return journal_id
      ```
    - _Requirements: 2.1–2.9, 4.1–4.10, 6.7–6.10, 8.1–8.8, 13.1–13.9_

  - [ ] 11.2 CREATE `jv-posting-worker/tests/test_properties.py`
    - Implement one Hypothesis test for each universal design property, with at least 100 examples per property and comments naming the property and requirement clause. Cover canonical ordering/fingerprint, balance, actor stability, idempotency, retry limits, claim order, redaction, and state transitions.
    - **Complete test pattern to expand into the eleven required property tests:**
      ```python
      from hypothesis import given, settings, strategies as st


      # Feature: jv-posting-engine, Property 1: Canonical line ordering is input-order independent
      @settings(max_examples=100)
      @given(st.lists(st.integers(min_value=1, max_value=100), min_size=2, unique=True))
      def test_property_1_canonical_order_is_input_order_independent(values):
          assert sorted(values) == sorted(list(reversed(values)))
      ```
    - Replace the pattern with domain strategies and assertions from `design.md`; do not leave the integer placeholder test as the final implementation.
    - _Requirements: 4.5–4.6, 6.7–6.10, 7.8–7.9, 8.1–8.8, 9.1–9.4, 10.3, 11.5, 15.7_

  - [ ] 11.3 CREATE `jv-posting-worker/tests/test_examples.py`
    - Add plain pytest examples for malformed UTF-8/JSON, unsupported schema, required-field errors, actor-mode errors, no/ambiguous Cashflow rule, closed period, currency rejection, one-sided/two-sided/duplicate/zero/negative/over-precision lines, lease loss, redaction, retry boundaries, reversal reference failures, and terminal lifecycle transitions.
    - **Complete file content:**
      ```python
      import pytest

      from jve.domain import Failure, FailureCategory, Status
      from jve.state import transition


      @pytest.mark.parametrize(("current", "target"), [(Status.POSTED, Status.READY), (Status.CANCELLED, Status.POSTED)])
      def test_terminal_states_cannot_reopen(current, target):
          with pytest.raises(ValueError):
              transition(current, target)


      def test_schema_failure_is_permanent():
          failure = Failure(FailureCategory.SCHEMA, "JVE-SCHEMA-INVALID-JSON", "Invalid JSON", transient=False)
          assert failure.transient is False
      ```
    - _Requirements: 2.4–2.7, 3.3–3.11, 4.7–4.10, 5.7, 7.5–7.6, 9.1–9.4, 14.2, 15.7_

  - [ ] 11.4 CREATE `jv-posting-worker/tests/integration/test_transactions_and_claims.py`
    - Use an isolated SQL Server fixture to test migrations, constraints, atomic claims, CAS, rollback, source-link uniqueness through the confirmed canonical procedure, append-only history, retention gates, and integrity checks.
    - **Complete file content:**
      ```python
      import pytest


      @pytest.mark.integration
      def test_transaction_suite_requires_isolated_sql_server(sql_server_database):
          assert sql_server_database.isolated is True
          assert sql_server_database.applied_migrations == ["001", "002", "003", "004"]
      ```
    - _Requirements: 6.1–6.6, 7.1–7.9, 8.3–8.6, 9.1–9.10, 15.5–15.10_

  - [ ] 11.5 CREATE `Finnova.Tests/JVPosting/JvPostingApiTests.cs`
    - Use the existing `WebApplicationFactory<Program>` and auth helpers to verify unauthenticated/unauthorized submission, actor capture, scope rejection, equivalent duplicate behavior, conflicting request rejection, `Ready` initiation, operational read/recovery permissions, safe RFC 7807 errors, and gateway alias behavior. Do not test a Python public submit endpoint because none exists.
    - **Complete file content:**
      ```csharp
      using System.Net;
      using Xunit;

      namespace Finnova.Tests.JVPosting;

      public sealed class JvPostingApiTests
      {
          [Fact]
          public async Task Submission_requires_authentication()
          {
              using var factory = TestApplicationFactory.Create();
              using var client = factory.CreateClient();
              var response = await client.PostAsync("/api/jv-posting/requests", content: null);
              Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
          }
      }
      ```
    - **Required completion:** replace `TestApplicationFactory.Create()` with the repository's existing factory/helper and add the authenticated request cases; do not invent a second test host.
    - _Requirements: 1.1–1.9, 11.1, 11.6–11.10, 12.1–12.8_

  - [ ] 11.6 CREATE `jv-posting-worker/tests/integration/test_cron_smoke.py`
    - Verify configuration validation, disabled-posting readiness gate, bounded one-shot invocation, exact start/end signals, exit behavior, overlap lock, and no-write behavior when posting is disabled.
    - **Complete file content:**
      ```python
      import pytest


      def test_posting_requires_same_database_readiness(settings_factory):
          settings = settings_factory(transaction_ready=False, posting_enabled=False)
          assert settings.posting_enabled is False
          with pytest.raises(ValueError):
              settings_factory(transaction_ready=False, posting_enabled=True)
      ```
    - _Requirements: 6.3–6.4, 7.4–7.6, 13.1–13.9, 15.3_

  - [ ] 11.7 Checkpoint — confirm all requirements have tests or an explicit non-testable configuration/gate, all actor paths are covered, and no test uses an unapproved journal schema.

- [ ] 12. Package the one-shot worker and operational database scripts
  - [ ] 12.1 CREATE `jv-posting-worker/Dockerfile`
    - Run one invocation as a non-root user. The image must not contain a scheduler or HTTP submission server.
    - **Complete file content:**
      ```dockerfile
      FROM python:3.11-slim

      ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1
      WORKDIR /opt/jve

      RUN addgroup --system jve && adduser --system --ingroup jve jve
      COPY pyproject.toml ./
      COPY src ./src
      RUN pip install --no-cache-dir . && chown -R jve:jve /opt/jve

      USER jve
      ENTRYPOINT ["jve-worker", "run"]
      ```
    - _Requirements: 7.1, 11.2, 13.1–13.2, 15.3_

  - [ ] 12.2 CREATE `jv-posting-worker/deploy/jve.env.example` and `jv-posting-worker/deploy/jve.cron.example`
    - Document secret-manager injection, safe defaults, same-database procedure configuration, and the proposed one-minute CRON schedule. Keep posting disabled until all gates pass.
    - **`deploy/jve.env.example`:**
      ```dotenv
      JVE_WORKER_ID=jve-worker-REPLACE
      JVE_SQL_CONNECTION_STRING=REPLACE_WITH_SECRET_MANAGER_REFERENCE
      JVE_CASHFLOW_BASE_URL=https://REPLACE/cashflow
      JVE_GL_BASE_URL=https://REPLACE/gl
      JVE_ACTOR_CONFIGURATION_REFERENCE=REPLACE_WITH_APPROVED_REFERENCE
      JVE_CANONICAL_WRITER_PROCEDURE=REPLACE_WITH_APPROVED_PROCEDURE
      JVE_TRANSACTION_READY=false
      JVE_POSTING_ENABLED=false
      JVE_BATCH_SIZE=50
      JVE_LEASE_MINUTES=10
      JVE_PROCESSING_DEADLINE_MINUTES=10
      JVE_RETRY_DELAYS_MINUTES=1,5,30
      JVE_MAX_ATTEMPTS=3
      JVE_BUSINESS_TIMEZONE=Asia/Kolkata
      JVE_CURRENCY_CODE=INR
      JVE_CURRENCY_PRECISION=2
      JVE_ROUNDING_MODE=ROUND_HALF_UP
      ```
    - **`deploy/jve.cron.example`:**
      ```cron
      # Confirm host, environment path, overlap lock, and timeout before deployment.
      * * * * * flock -n /var/run/jve-worker.lock timeout 55s /opt/jve/.venv/bin/jve-worker run >> /var/log/finnova/jve-worker.log 2>&1
      ```
    - _Requirements: 7.1–7.6, 11.2, 13.1–13.9_

  - [ ] 12.3 CREATE `jv-posting-worker/scripts/integrity_checks.sql`
    - Add read-only orphan/duplicate/unbalanced/status-without-attempt checks and a retention/legal-hold gate. Never auto-repair financial data.
    - **Complete file content:**
      ```sql
      SELECT request_id, status
      FROM jve.posting_inbox
      WHERE status = 'Posted' AND journal_id IS NULL;

      SELECT request_id, COUNT(*) AS reconciliation_count
      FROM jve.reconciliation
      GROUP BY request_id
      HAVING COUNT(*) > 1;

      SELECT i.request_id, i.status
      FROM jve.posting_inbox i
      LEFT JOIN jve.posting_attempts a ON a.request_id = i.request_id
      WHERE a.request_id IS NULL AND i.status <> 'Ready';

      SELECT request_id, debit_total, credit_total
      FROM jve.reconciliation
      WHERE debit_total <> credit_total;

      THROW 51000, 'JVE retention purge requires approved retention and legal-hold procedure', 1;
      ```
    - _Requirements: 10.7, 15.5–15.10_

  - [ ] 12.4 CREATE `jv-posting-worker/README-DEPLOYMENT.md`
    - Document migration order, schema hash gate, canonical mapping gate, SystemUser gate, disabled-posting rehearsal, actor/security permissions, one-minute CRON, recovery, observability, retention, and the prohibition on UI/public worker submission.
    - **Complete file content:**
      ```markdown
      # JVE Deployment Checklist

      1. Confirm Python 3.11 ownership, CRON host, worker identity, and secret injection.
      2. Confirm the approved .NET API is the only submission boundary and captures actor context.
      3. Apply feature-owned migrations 001–004 only in an isolated rehearsal.
      4. Register the approved schema 1.0 with its real SHA-256 hash.
      5. Confirm the seeded/configured SystemUser and person/automation scope rules.
      6. Confirm the canonical Journal Header/Line/source-link mapping, actor column, Posted status, and same-database procedure.
      7. Verify Cashflow/GL adapter contracts, redaction, least privilege, and observability.
      8. Run with `JVE_TRANSACTION_READY=false` and `JVE_POSTING_ENABLED=false` for configuration checks.
      9. Enable posting only after atomicity, actor, idempotency, rollback, and recovery tests pass.
      10. Retain historical inbox, attempt, audit, reconciliation, actor, and journal-link records under the approved financial-record policy.
      ```
    - _Requirements: 6.3–6.4, 11.2–11.5, 13.1–13.9, 15.3–15.10_

## Notes

- Every task is limited to coding, configuration, migration-script authoring, or automated testing. This plan does not authorize deployment, migration execution, manual acceptance testing, or source implementation during spec creation.
- The plan includes no public Python submission endpoint or browser work. Browser journal viewing and browser JVE administration remain deferred.
- The feature-owned SQL migrations deliberately do not create canonical Journal Header, Journal Line, or source-link tables. Task 8 cannot proceed until the existing accounting/GL owner confirms exact names, columns, actor mapping, final Posted value, and atomic procedure.
- Cashflow Master remains the authoritative rule owner. No Cashflow rule table, seed data, event-to-account mapping, or hard-coded posting rule may be added.
- All user-facing values, statuses, error messages, and fixtures are English-only. No bilingual fields or RTL handling are introduced.
- Test sub-tasks marked `*` are optional under the task workflow; core implementation tasks are not optional.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3", "2.1", "2.2", "3.1"] },
    { "id": 1, "tasks": ["1.4", "2.3", "2.4", "4.1", "4.2", "5.1", "5.2", "5.3", "5.5"] },
    { "id": 2, "tasks": ["3.2", "4.3", "4.4", "4.5", "5.4", "6.1", "7.1", "10.1", "10.2"] },
    { "id": 3, "tasks": ["3.3", "6.2", "7.2", "8.2", "9.1", "9.2", "9.3", "10.3"] },
    { "id": 4, "tasks": ["8.3", "9.4", "10.4", "11.1", "11.2", "11.3", "11.4", "11.5", "11.6"] },
    { "id": 5, "tasks": ["12.1", "12.2", "12.3", "12.4"] }
  ]
}
```