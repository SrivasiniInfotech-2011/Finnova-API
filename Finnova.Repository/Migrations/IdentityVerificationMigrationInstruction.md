# Identity Verification Migration Instructions

## Prerequisites

Apply tasks 4.1-4.4 (EF configurations and DbContext updates) before creating this migration.

## Step 1: Create the migration

From the `Finnova-API` repository root, run:

```bash
dotnet ef migrations add AddIdentityVerification --project Finnova.Repository --startup-project Finnova.SystemAdminService
```

## Step 2: Review the generated migration

Open the generated migration file under `Finnova.Repository\Migrations\` (e.g., `YYYYMMDDHHMMSS_AddIdentityVerification.cs`) and verify:

- Three tables are created:
  - `verification_sessions`
  - `validation_records`
  - `protected_document_references`
- Restricted foreign keys are configured (OnDelete(DeleteBehavior.Restrict)):
  - `ValidationRecord.SessionId` → `VerificationSession.Id`
  - `ValidationRecord.ProtectedDocumentReferenceId` → `ProtectedDocumentReference.Id`
- Owned type columns for `SourceSummary` are inlined in `validation_records`:
  - `SourceType`, `MappingProfile`, `DocumentContentType`, `DocumentSizeBytes`, `ProtectedDocumentReferenceId`, `ExtractedValueKeysJson`, `ExtractionCompleted`
- Indexes are created as configured:
  - `IX_validation_records (CreatedUtc DESC, Id DESC)`
  - `IX_validation_records (OperationType, ValidationStatus, ComparisonOutcome, Method)`
  - `IX_validation_records (SessionId, CreatedUtc)`
  - `IX_validation_records (ProtectedSearchReference)`
  - `IX_verification_sessions (CorrelationId)`
  - `IX_protected_document_references (ContentHash)`
- No provider payload tables or columns are added

## Step 3: Apply the migration

Run the following command to update the database:

```bash
dotnet ef database update --project Finnova.Repository --startup-project Finnova.SystemAdminService
```

## Step 4: Verify database state

After applying the migration, confirm:

- All three tables exist with the correct column names and data types.
- The `SourceSummary` owned type columns appear directly under `validation_records`.
- Foreign key constraints exist but do not cascade delete.
- Indexes are present and cover the required filter/sort fields.

## Notes

- The migration does not seed any data. The feature is ready to use once the migration is applied and the service host is restarted with the appropriate provider configuration.
- If the migration generation fails due to configuration issues, run `dotnet ef migrations remove` and verify EF configuration classes are correctly registered in `FinnovaDbContext` before retrying.
