# Implementation Plan: Application Processing

## Overview

This plan is the copy-paste implementation sequence for Application Processing. It covers the backend in `d:\Projects\Finnova\Finnova-API` and the frontend in the product-context repository `E:\Finnova\Finnova-UI\Finnova-UI`; the frontend checkout available in this environment is `d:\Projects\Finnova\Finnova-UI`. Apply each task in order. Do not create a new host, product master, scheme master, branch master, or lookup CRUD slice.

The implementation language is C#/.NET 10 for the backend and TypeScript/React 18 for the frontend. The backend remains in the existing `Finnova.AccountsService`, uses SQL Server and the existing CQRS/MediatR/FluentValidation conventions, and is reached by the existing accounts cluster. The frontend uses MUI, React hooks/Context, the shared `src/services/api.ts` axios instance, and the existing interface/mock/real/toggle service convention. Redux, Redux Toolkit, RxJS, extra axios clients, bilingual fields, and RTL support are out of scope.

## Dependency and build-safety correction

The backend project graph is one-way: `Finnova.Models <- Finnova.Repository <- Finnova.Service <- Finnova.AccountsService`. `Finnova.Repository.csproj` references only Models, `Finnova.Service.csproj` references Models and Repository, and AccountsService references Models, Repository, and Service. Repository code therefore must not import or register Service types.

The four repository-facing application reference ports (`ILineOfBusinessReferencePort`, `ICustomerReferencePort`, `IBranchReferencePort`, and `ILookupReferencePort`) belong in `Finnova.Repository/Interfaces/ApplicationReferencePorts.cs` under `Finnova.Repository.Interfaces`. The service-only policy port `IApplicationReferenceValidator` belongs in `Finnova.Service/Applications/References/IApplicationReferenceValidator.cs` under `Finnova.Service.Applications.References`; AccountsService registers that validator exactly once. The application transaction contract also belongs in `Finnova.Repository.Interfaces`, so Service uses `IApplicationTransaction` without exposing EF types or `dynamic`.

All application create, update, and process mutations use pending repository operations followed by one unit-of-work `SaveChangesAsync` inside the required transaction boundary. The inherited `IRepository<T>.AddAsync` and `UpdateAsync` methods save immediately in the existing `RepositoryBase<T>` convention and are not used by atomic application handlers. `AddPending` calls `DbSet.Add` without saving, and `MarkModified` marks the tracked entity modified without saving.

Before implementation, inspect the final project references and namespaces. The implementer must verify that no file under `Finnova.Repository` contains a `using Finnova.Service...` directive and that the resulting graph remains `Models <- Repository <- Service <- AccountsService` with no reverse reference.

## Assumptions and confirm-before-implementation notes

- [ ] Confirm the configured authorization policy and role/claim. The design proposes `ApplicationProcessor`; do not silently add `SystemAdmin` access unless the owner explicitly configures it. _Requirements: R9.1-R9.4._
- [ ] Confirm `ApplicationProcessing:LookupModule`, `ProductLookupType`, and `SchemeLookupType`. The proposed module is `ApplicationProcessing`; `Origination` is the documented alternative. The existing lookup route remains `GET /api/lookup/dropdown?module={module}&type={type}` and returns `LookupDropdownItemResponse(Code, Label)`. _Requirements: R1.7, R6.4-R6.6._
- [ ] Confirm the active Branch Master identifier shape and owning endpoint. The adapter must map the existing source and must not create a second branch table or CRUD API. _Requirements: R1.8, R6.3, R6.7._
- [ ] Confirm whether active-reference validation is the source of truth without physical foreign keys. The design uses application-level validation and non-cascading/no-navigation stored IDs unless the database owner approves physical foreign keys. _Requirements: R1.5-R1.8, R6.7._
- [ ] Confirm the generic bounds: Finance Amount `0 < amount <= 100,00,00,000.00`, IRR `0..100`, Tenure `1..600`, Processing Charges `>= 0`, Disbursement Date today through today plus 1825 days, and search term maximum 100 characters. _Requirements: R1.9-R1.14, R7.8._
- [ ] Confirm that Draft and Submitted applications remain editable, and that Processed applications cannot be reopened or reprocessed. _Requirements: R4.1, R4.4, R4.6, R5.2, R5.5._
- [ ] Confirm the proposed opaque `APP-` plus cryptographically random token application-number format. The unique SQL index and conflict retry remain mandatory regardless of format. _Requirements: R2.1-R2.4._
- [ ] Confirm the available UI checkout before applying frontend tasks. Use `E:\Finnova\Finnova-UI\Finnova-UI` when that product-context checkout exists; otherwise use `d:\Projects\Finnova\Finnova-UI`. _Requirements: R8.1-R8.12._

## Implementation order

1. Backend domain entities, enums, value objects, and typed exceptions.
2. Backend contracts and response mappers.
3. EF Core configurations, DbSets, migration command, and persistence transaction primitives.
4. Active reference ports/adapters for LOB, Customer, Branch Master, and existing Lookup catalog.
5. Application repositories, unit of work, application-number generation, and DI.
6. Pure amortization/date/rounding helpers and shared validation.
7. CQRS commands, queries, handlers, and mappers for create, update, preview, process/finalize, get/search, schedule reads, and reference reads.
8. AccountsService controller, middleware error mappings, authorization policy, and YARP aliases/configuration.
9. Backend xUnit, FsCheck.Xunit, and WebApplicationFactory tests.
10. Frontend models, service interface, mock, real shared-axios service, toggle, and barrels.
11. Frontend page, components, form, route, and navigation.
12. Frontend Vitest and React Testing Library tests.
13. Final documented verification commands only.

## Tasks

- [ ] 1. Create backend domain entities, enums, value objects, and exceptions. _Requirements: R1-R5, R7._
  - [ ] 1.1 **CREATE `Finnova.Models/Domain/Enums/ApplicationStatus.cs`** with the exact contents below. _Requirements: R1.2, R4.1, R4.4, R5.1-R5.2._
    ```csharp
    namespace Finnova.Models.Domain.Enums;

    public enum ApplicationStatus
    {
        Draft = 0,
        Submitted = 1,
        Processed = 2
    }
    ```
  - [ ] 1.2 **CREATE `Finnova.Models/Domain/Enums/RepaymentFrequency.cs`**. String JSON serialization is configured by the existing AccountsService host. _Requirements: R1.12, R3.2, R3.12._
    ```csharp
    namespace Finnova.Models.Domain.Enums;

    public enum RepaymentFrequency
    {
        Monthly = 0,
        Quarterly = 1,
        HalfYearly = 2,
        Annual = 3
    }
    ```
  - [ ] 1.3 **CREATE `Finnova.Models/Domain/Entities/Application.cs`**. Do not add navigation properties to LOB, Customer, Branch, or Lookup masters. `Version` is the SQL Server rowversion token. _Requirements: R1.1-R1.3, R2.1-R2.4, R4.1-R4.6, R5.1-R5.5, R7.1-R7.18._
    ```csharp
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Models.Domain.Entities;

    public sealed class Application
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ApplicationNumber { get; set; } = string.Empty;
        public Guid LineOfBusinessId { get; set; }
        public Guid CustomerId { get; set; }
        public Guid BranchId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string SchemeCode { get; set; } = string.Empty;
        public decimal FinanceAmount { get; set; }
        public decimal IrrPercent { get; set; }
        public int Tenure { get; set; }
        public RepaymentFrequency RepaymentFrequency { get; set; }
        public DateOnly DisbursementDate { get; set; }
        public decimal ProcessingCharges { get; set; }
        public ApplicationStatus Status { get; set; } = ApplicationStatus.Draft;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
        public byte[] Version { get; set; } = Array.Empty<byte>();
    }
    ```
  - [ ] 1.4 **CREATE `Finnova.Models/Domain/Entities/ApplicationSchedule.cs`** and **CREATE `Finnova.Models/Domain/Entities/ApplicationInstallment.cs`**. Finalized schedules have no update/delete command. _Requirements: R3.1-R3.13, R5.1, R5.5-R5.6._
    ```csharp
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Models.Domain.Entities;

    public sealed class ApplicationSchedule
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ApplicationId { get; set; }
        public bool IsFinalized { get; set; }
        public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
        public decimal FinanceAmount { get; set; }
        public decimal IrrPercent { get; set; }
        public int Tenure { get; set; }
        public RepaymentFrequency RepaymentFrequency { get; set; }
        public DateOnly DisbursementDate { get; set; }
        public decimal PeriodicInterestRate { get; set; }
        public decimal EmiAmount { get; set; }
        public decimal TotalInstallmentAmount { get; set; }
        public decimal TotalInterestAmount { get; set; }
        public decimal TotalPrincipalAmount { get; set; }
        public List<ApplicationInstallment> Installments { get; set; } = new();
    }

    public sealed class ApplicationInstallment
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ScheduleId { get; set; }
        public int InstallmentNumber { get; set; }
        public DateOnly DueDate { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal InterestPortion { get; set; }
        public decimal PrincipalPortion { get; set; }
        public decimal InstallmentAmount { get; set; }
        public decimal ClosingBalance { get; set; }
    }
    ```
  - [ ] 1.5 **CREATE `Finnova.Models/Domain/Entities/ApplicationStatusAuditEntry.cs`**. _Requirements: R1.1, R4.1, R5.1._
    ```csharp
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Models.Domain.Entities;

    public sealed class ApplicationStatusAuditEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ApplicationId { get; set; }
        public ApplicationStatus FromStatus { get; set; }
        public ApplicationStatus ToStatus { get; set; }
        public string ChangedBy { get; set; } = string.Empty;
        public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
    }
    ```
  - [ ] 1.6 **CREATE `Finnova.Models/Domain/ValueObjects/ApplicationTerms.cs`** and **CREATE `Finnova.Models/Domain/ValueObjects/ScheduleResult.cs`**. These are pure data carriers consumed by the calculator. _Requirements: R3.1-R3.13._
    ```csharp
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Models.Domain.ValueObjects;

    public sealed record ApplicationTerms(
        decimal FinanceAmount,
        decimal IrrPercent,
        int Tenure,
        RepaymentFrequency RepaymentFrequency,
        DateOnly DisbursementDate);

    public sealed record ScheduleLineValue(
        int InstallmentNumber,
        DateOnly DueDate,
        decimal OpeningBalance,
        decimal InterestPortion,
        decimal PrincipalPortion,
        decimal InstallmentAmount,
        decimal ClosingBalance);

    public sealed record ScheduleResult(
        decimal PeriodicInterestRate,
        decimal EmiAmount,
        IReadOnlyList<ScheduleLineValue> Installments,
        decimal TotalInstallmentAmount,
        decimal TotalInterestAmount,
        decimal TotalPrincipalAmount);
    ```
  - [ ] 1.7 **CREATE `Finnova.Models/Domain/Exceptions/ApplicationProcessingExceptions.cs`**. Each exception has a stable code used by AccountsService middleware. _Requirements: R1.4-R1.8, R2.2, R4.3-R4.4, R5.2-R5.4, R6.7, R7.18._
    ```csharp
    namespace Finnova.Models.Domain.Exceptions;

    public abstract class ApplicationProcessingException : Exception
    {
        protected ApplicationProcessingException(string message) : base(message) { }
        public abstract string ErrorCode { get; }
    }

    public sealed class ApplicationNotFoundException : ApplicationProcessingException
    {
        public ApplicationNotFoundException(Guid id) : base($"Application '{id}' was not found.") { }
        public override string ErrorCode => "ERR-APP-404";
    }

    public sealed class ApplicationAlreadyProcessedException : ApplicationProcessingException
    {
        public ApplicationAlreadyProcessedException() : base("A processed application cannot be modified or processed again.") { }
        public override string ErrorCode => "ERR-APP-409";
    }

    public sealed class ApplicationReferenceInvalidException : ApplicationProcessingException
    {
        public ApplicationReferenceInvalidException(string field) : base($"{field} does not refer to an active reference item.") { }
        public override string ErrorCode => "ERR-APP-400";
    }

    public sealed class ApplicationConflictException : ApplicationProcessingException
    {
        public ApplicationConflictException(string message = "The application was changed by another request.") : base(message) { }
        public override string ErrorCode => "ERR-APP-409";
    }

    public sealed class ApplicationConfigurationException : ApplicationProcessingException
    {
        public ApplicationConfigurationException(string message) : base(message) { }
        public override string ErrorCode => "ERR-APP-503";
    }
    ```

- [ ] 2. Add backend contracts and mappers. _Requirements: R1-R7._
  - [ ] 2.1 **CREATE `Finnova.Models/Contracts/Applications/ApplicationContracts.cs`** with all request, response, reference, and schedule records below. Application Number and Status are intentionally absent from create/update requests. _Requirements: R1.1-R1.4, R2.3-R2.4, R3.1-R3.13, R4.1-R4.6, R5.1-R5.6, R6.1-R6.6, R7.1-R7.18._
    ```csharp
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Contracts.Common;
    using Finnova.Models.Contracts.Lookups;
    using Finnova.Models.Contracts.UserManagement;

    namespace Finnova.Models.Contracts.Applications;

    public sealed record CreateApplicationRequest(
        Guid LineOfBusinessId, Guid CustomerId, Guid BranchId, string ProductCode, string SchemeCode,
        decimal FinanceAmount, decimal IrrPercent, int Tenure, RepaymentFrequency RepaymentFrequency,
        DateOnly DisbursementDate, decimal? ProcessingCharges);

    public sealed record UpdateApplicationRequest(
        Guid LineOfBusinessId, Guid CustomerId, Guid BranchId, string ProductCode, string SchemeCode,
        decimal FinanceAmount, decimal IrrPercent, int Tenure, RepaymentFrequency RepaymentFrequency,
        DateOnly DisbursementDate, decimal? ProcessingCharges);

    public sealed record ApplicationResponse(
        Guid Id, string ApplicationNumber, Guid LineOfBusinessId, Guid CustomerId, Guid BranchId,
        string ProductCode, string SchemeCode, decimal FinanceAmount, decimal IrrPercent, int Tenure,
        RepaymentFrequency RepaymentFrequency, DateOnly DisbursementDate, decimal ProcessingCharges,
        ApplicationStatus Status, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

    public sealed record ApplicationInstallmentResponse(
        int InstallmentNumber, DateOnly DueDate, decimal OpeningBalance, decimal InterestPortion,
        decimal PrincipalPortion, decimal InstallmentAmount, decimal ClosingBalance);

    public sealed record ApplicationScheduleResponse(
        Guid ApplicationId, bool IsFinalized, decimal FinanceAmount, decimal IrrPercent, int Tenure,
        RepaymentFrequency RepaymentFrequency, DateOnly DisbursementDate, decimal PeriodicInterestRate,
        decimal EmiAmount, List<ApplicationInstallmentResponse> Installments,
        decimal TotalInstallmentAmount, decimal TotalInterestAmount, decimal TotalPrincipalAmount);

    public sealed record ProcessApplicationResponse(
        ApplicationResponse Application, ApplicationScheduleResponse Schedule);

    public sealed record ApplicationReferenceOptionResponse(Guid Id, string Code, string Label);
    public sealed record CustomerReferenceOptionResponse(Guid Id, string CustomerCode, string Label);
    public sealed record ApplicationSearchRequest(
        string? Search, Guid? LineOfBusinessId, Guid? CustomerId, string? ProductCode,
        Guid? BranchId, string? SchemeCode, int Page = 1, int PageSize = 20);
    public sealed record CustomerReferencePage(
        List<CustomerReferenceOptionResponse> Data, int Total, int Page, int PageSize, int TotalPages);
    public sealed record ApplicationPage(
        List<ApplicationResponse> Data, int Total, int Page, int PageSize, int TotalPages);
    public sealed record LookupReferenceRequest(string Module, string Type);
    ```
  - [ ] 2.2 **CREATE `Finnova.Service/Mappers/ApplicationMapper.cs`**. Mappers must not query repositories or calculate schedules. _Requirements: R1.1, R3.1, R5.1, R5.5, R7.9, R7.17._
    ```csharp
    using Finnova.Models.Contracts.Applications;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.ValueObjects;

    namespace Finnova.Service.Mappers;

    public static class ApplicationMapper
    {
        public static ApplicationResponse ToResponse(this Application x) => new(
            x.Id, x.ApplicationNumber, x.LineOfBusinessId, x.CustomerId, x.BranchId, x.ProductCode,
            x.SchemeCode, x.FinanceAmount, x.IrrPercent, x.Tenure, x.RepaymentFrequency,
            x.DisbursementDate, x.ProcessingCharges, x.Status, x.CreatedAtUtc, x.UpdatedAtUtc);

        public static ApplicationScheduleResponse ToResponse(this ApplicationSchedule x) => new(
            x.ApplicationId, x.IsFinalized, x.FinanceAmount, x.IrrPercent, x.Tenure, x.RepaymentFrequency,
            x.DisbursementDate, x.PeriodicInterestRate, x.EmiAmount,
            x.Installments.OrderBy(i => i.InstallmentNumber).Select(i => new ApplicationInstallmentResponse(
                i.InstallmentNumber, i.DueDate, i.OpeningBalance, i.InterestPortion, i.PrincipalPortion,
                i.InstallmentAmount, i.ClosingBalance)).ToList(), x.TotalInstallmentAmount,
            x.TotalInterestAmount, x.TotalPrincipalAmount);

        public static ApplicationScheduleResponse ToResponse(this ScheduleResult x, Guid applicationId,
            ApplicationTerms terms) => new(applicationId, false, terms.FinanceAmount, terms.IrrPercent,
            terms.Tenure, terms.RepaymentFrequency, terms.DisbursementDate, x.PeriodicInterestRate,
            x.EmiAmount, x.Installments.Select(i => new ApplicationInstallmentResponse(i.InstallmentNumber,
                i.DueDate, i.OpeningBalance, i.InterestPortion, i.PrincipalPortion, i.InstallmentAmount,
                i.ClosingBalance)).ToList(), x.TotalInstallmentAmount, x.TotalInterestAmount,
            x.TotalPrincipalAmount);
    }
    ```

- [ ] 3. Configure EF Core persistence and transaction primitives. _Requirements: R1-R5, R7._
  - [ ] 3.1 **CREATE `Finnova.Repository/Configuration/ApplicationConfigurations.cs`** with the complete configurations below. Use SQL Server `date`, decimal precision, unique application number, unique schedule per application, installment ordering, and rowversion. _Requirements: R2.1-R2.2, R3.7-R3.10, R5.1, R5.5, R7.15._
    ```csharp
    using Finnova.Models.Domain.Entities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;

    namespace Finnova.Repository.Configuration;

    public sealed class ApplicationConfiguration : IEntityTypeConfiguration<Application>
    {
        public void Configure(EntityTypeBuilder<Application> b)
        {
            b.ToTable("applications"); b.HasKey(x => x.Id);
            b.Property(x => x.ApplicationNumber).IsRequired().HasMaxLength(40);
            b.Property(x => x.ProductCode).IsRequired().HasMaxLength(50);
            b.Property(x => x.SchemeCode).IsRequired().HasMaxLength(50);
            b.Property(x => x.FinanceAmount).HasPrecision(18, 2).IsRequired();
            b.Property(x => x.IrrPercent).HasPrecision(9, 4).IsRequired();
            b.Property(x => x.ProcessingCharges).HasPrecision(18, 2).IsRequired();
            b.Property(x => x.RepaymentFrequency).HasConversion<string>().HasMaxLength(20).IsRequired();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            b.Property(x => x.DisbursementDate).HasColumnType("date").IsRequired();
            b.Property(x => x.Version).IsRowVersion().IsConcurrencyToken();
            b.HasIndex(x => x.ApplicationNumber).IsUnique();
            b.HasIndex(x => new { x.CreatedAtUtc, x.ApplicationNumber });
            b.HasIndex(x => x.LineOfBusinessId); b.HasIndex(x => x.CustomerId); b.HasIndex(x => x.BranchId);
            b.HasIndex(x => x.ProductCode); b.HasIndex(x => x.SchemeCode);
        }
    }

    public sealed class ApplicationScheduleConfiguration : IEntityTypeConfiguration<ApplicationSchedule>
    {
        public void Configure(EntityTypeBuilder<ApplicationSchedule> b)
        {
            b.ToTable("application_schedules"); b.HasKey(x => x.Id);
            b.Property(x => x.FinanceAmount).HasPrecision(18, 2).IsRequired();
            b.Property(x => x.IrrPercent).HasPrecision(9, 4).IsRequired();
            b.Property(x => x.PeriodicInterestRate).HasPrecision(18, 12).IsRequired();
            b.Property(x => x.EmiAmount).HasPrecision(18, 2).IsRequired();
            b.Property(x => x.TotalInstallmentAmount).HasPrecision(18, 2).IsRequired();
            b.Property(x => x.TotalInterestAmount).HasPrecision(18, 2).IsRequired();
            b.Property(x => x.TotalPrincipalAmount).HasPrecision(18, 2).IsRequired();
            b.Property(x => x.RepaymentFrequency).HasConversion<string>().HasMaxLength(20).IsRequired();
            b.Property(x => x.DisbursementDate).HasColumnType("date").IsRequired();
            b.HasIndex(x => x.ApplicationId).IsUnique();
            b.HasMany(x => x.Installments).WithOne().HasForeignKey(x => x.ScheduleId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public sealed class ApplicationInstallmentConfiguration : IEntityTypeConfiguration<ApplicationInstallment>
    {
        public void Configure(EntityTypeBuilder<ApplicationInstallment> b)
        {
            b.ToTable("application_installments"); b.HasKey(x => x.Id);
            b.Property(x => x.DueDate).HasColumnType("date").IsRequired();
            foreach (var p in new[] { nameof(ApplicationInstallment.OpeningBalance), nameof(ApplicationInstallment.InterestPortion),
                nameof(ApplicationInstallment.PrincipalPortion), nameof(ApplicationInstallment.InstallmentAmount),
                nameof(ApplicationInstallment.ClosingBalance) }) b.Property<decimal>(p).HasPrecision(18, 2).IsRequired();
            b.HasIndex(x => new { x.ScheduleId, x.InstallmentNumber }).IsUnique();
        }
    }

    public sealed class ApplicationStatusAuditEntryConfiguration : IEntityTypeConfiguration<ApplicationStatusAuditEntry>
    {
        public void Configure(EntityTypeBuilder<ApplicationStatusAuditEntry> b)
        {
            b.ToTable("application_status_audit_entries"); b.HasKey(x => x.Id);
            b.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
            b.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
            b.Property(x => x.ChangedBy).IsRequired().HasMaxLength(200);
            b.Property(x => x.ChangedAtUtc).IsRequired();
            b.HasIndex(x => new { x.ApplicationId, x.ChangedAtUtc });
        }
    }
    ```
  - [ ] 3.2 **MODIFY `Finnova.Repository/Context/FinnovaDbContext.cs`**. Insert these exact DbSet properties immediately after the existing `CustomerAuditEntries` property. _Requirements: R1.1, R3.1, R5.1._
    ```csharp
        public DbSet<Application> Applications => Set<Application>();
        public DbSet<ApplicationSchedule> ApplicationSchedules => Set<ApplicationSchedule>();
        public DbSet<ApplicationInstallment> ApplicationInstallments => Set<ApplicationInstallment>();
        public DbSet<ApplicationStatusAuditEntry> ApplicationStatusAuditEntries => Set<ApplicationStatusAuditEntry>();
    ```
    Also add `using Finnova.Models.Domain.Entities;` if the file does not already resolve the entity namespace.
  - [ ] 3.3 **CREATE `Finnova.Repository/Interfaces/IApplicationTransaction.cs`**, **MODIFY `Finnova.Repository/Interfaces/IApplicationUnitOfWork.cs`**, and **CREATE `Finnova.Repository/Repositories/ApplicationTransaction.cs` and `Finnova.Repository/Repositories/ApplicationUnitOfWork.cs`**. Keep the transaction contract repository-neutral: Service must not receive `IDbContextTransaction`, `IAsyncDisposable` with ad-hoc members, or `dynamic`. _Requirements: R2.2, R4.2, R5.1-R5.4._
    ```csharp
    // Finnova.Repository/Interfaces/IApplicationTransaction.cs
    namespace Finnova.Repository.Interfaces;

    public interface IApplicationTransaction : IAsyncDisposable
    {
        Task CommitAsync(CancellationToken ct = default);
        Task RollbackAsync(CancellationToken ct = default);
    }
    ```
    ```csharp
    // Finnova.Repository/Interfaces/IApplicationUnitOfWork.cs
    using Finnova.Repository.Context;

    namespace Finnova.Repository.Interfaces;

    public interface IApplicationUnitOfWork
    {
        FinnovaDbContext Context { get; }
        Task<int> SaveChangesAsync(CancellationToken ct = default);
        Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken ct = default);
    }
    ```
    ```csharp
    // Finnova.Repository/Repositories/ApplicationTransaction.cs
    using Microsoft.EntityFrameworkCore.Storage;
    using Finnova.Repository.Interfaces;

    namespace Finnova.Repository.Repositories;

    public sealed class ApplicationTransaction : IApplicationTransaction
    {
        private readonly IDbContextTransaction _transaction;
        public ApplicationTransaction(IDbContextTransaction transaction) => _transaction = transaction;
        public Task CommitAsync(CancellationToken ct = default) => _transaction.CommitAsync(ct);
        public Task RollbackAsync(CancellationToken ct = default) => _transaction.RollbackAsync(ct);
        public ValueTask DisposeAsync() => _transaction.DisposeAsync();
    }
    ```
    ```csharp
    // Finnova.Repository/Repositories/ApplicationUnitOfWork.cs
    using Finnova.Repository.Context;
    using Finnova.Repository.Interfaces;

    namespace Finnova.Repository.Repositories;

    public sealed class ApplicationUnitOfWork : IApplicationUnitOfWork
    {
        public FinnovaDbContext Context { get; }
        public ApplicationUnitOfWork(FinnovaDbContext context) => Context = context;
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Context.SaveChangesAsync(ct);
        public async Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken ct = default)
            => new ApplicationTransaction(await Context.Database.BeginTransactionAsync(ct));
    }
    ```
  - [ ] 3.4 Document and then have the implementer run the migration command after tasks 3.1-3.3 and the host wiring task. Do not run it during this workflow. _Requirements: R1.1, R2.1-R2.2, R3.7-R3.10, R5.1, R5.5._
    ```powershell
    dotnet ef migrations add AddApplicationProcessing --project Finnova.Repository --startup-project Finnova.AccountsService
    ```
    Run this command only after the preceding code tasks have been applied; do not run it as part of creating this document.

- [ ] 4. Add active reference ports and adapters without creating duplicate masters. _Requirements: R1, R5-R7._
  - [ ] 4.1 **CREATE `Finnova.Repository/Interfaces/ApplicationReferencePorts.cs`** and **CREATE `Finnova.Service/Applications/References/IApplicationReferenceValidator.cs`**. Keep data-access ports in Repository and the validation policy in Service. Ports expose only active projections and existence checks. Product and Scheme use the existing Lookup repository and exact configured module/type values. _Requirements: R1.5-R1.8, R6.1-R6.7._
    ```csharp
    // Finnova.Repository/Interfaces/ApplicationReferencePorts.cs
    using Finnova.Models.Contracts.Applications;
    using Finnova.Models.Contracts.Lookups;
    using Finnova.Models.Contracts.UserManagement;

    namespace Finnova.Repository.Interfaces;

    public interface ILineOfBusinessReferencePort
    {
        Task<List<LineOfBusinessRefResponse>> GetActiveAsync(CancellationToken ct);
        Task<bool> ExistsActiveAsync(Guid id, CancellationToken ct);
    }
    public interface ICustomerReferencePort
    {
        Task<(List<CustomerReferenceOptionResponse> Items, int Total)> SearchActiveAsync(string? search, int page, int pageSize, CancellationToken ct);
        Task<bool> ExistsActiveAsync(Guid id, CancellationToken ct);
    }
    public interface IBranchReferencePort
    {
        Task<List<ApplicationReferenceOptionResponse>> GetActiveAsync(Guid? lineOfBusinessId, CancellationToken ct);
        Task<bool> ExistsActiveAsync(Guid id, CancellationToken ct);
    }
    public interface ILookupReferencePort
    {
        Task<List<LookupDropdownItemResponse>> GetActiveAsync(string module, string type, CancellationToken ct);
        Task<bool> ExistsActiveAsync(string module, string type, string code, CancellationToken ct);
    }
    ```
    ```csharp
    // Finnova.Service/Applications/References/IApplicationReferenceValidator.cs
    namespace Finnova.Service.Applications.References;

    public interface IApplicationReferenceValidator
    {
        Task ValidateAsync(Guid lobId, Guid customerId, Guid branchId, string productCode, string schemeCode, CancellationToken ct);
    }
    ```
  - [ ] 4.2 **CREATE `Finnova.Repository/Repositories/ApplicationReferenceAdapters.cs`**. Use `FinnovaDbContext.LinesOfBusiness`, `Customers`, and `Locations` as read-only sources, filter active rows, and project only IDs/codes/English labels. Use `CustomerLifecycleStatus.Active`; do not return addresses, documents, bank accounts, or full aggregates. The branch adapter must keep its endpoint mapping configurable at the service boundary. _Requirements: R1.5-R1.8, R6.1-R6.3, R6.7._
    ```csharp
    using Finnova.Models.Contracts.Applications;
    using Finnova.Models.Contracts.UserManagement;
    using Finnova.Models.Domain.Enums;
    using Finnova.Repository.Context;
    using Finnova.Repository.Interfaces;
    using Microsoft.EntityFrameworkCore;

    namespace Finnova.Repository.Repositories;

    public sealed class LineOfBusinessReferenceAdapter : ILineOfBusinessReferencePort
    {
        private readonly FinnovaDbContext _db;
        public LineOfBusinessReferenceAdapter(FinnovaDbContext db) => _db = db;
        public Task<List<LineOfBusinessRefResponse>> GetActiveAsync(CancellationToken ct) =>
            _db.LinesOfBusiness.AsNoTracking().Where(x => x.IsActive)
                .OrderBy(x => x.LOB_Name).Select(x => new LineOfBusinessRefResponse(x.Id, x.LOB_Name, x.LOB_Description)).ToListAsync(ct);
        public Task<bool> ExistsActiveAsync(Guid id, CancellationToken ct) =>
            _db.LinesOfBusiness.AsNoTracking().AnyAsync(x => x.Id == id && x.IsActive, ct);
    }

    public sealed class CustomerReferenceAdapter : ICustomerReferencePort
    {
        private readonly FinnovaDbContext _db;
        public CustomerReferenceAdapter(FinnovaDbContext db) => _db = db;
        public async Task<(List<CustomerReferenceOptionResponse> Items, int Total)> SearchActiveAsync(string? search, int page, int pageSize, CancellationToken ct)
        {
            var q = _db.Customers.AsNoTracking().Where(x => x.Status == CustomerLifecycleStatus.Active);
            var term = search?.Trim();
            if (!string.IsNullOrEmpty(term)) q = q.Where(x => x.CustomerCode.Contains(term));
            var total = await q.CountAsync(ct);
            var items = await q.OrderBy(x => x.CustomerCode).Skip((page - 1) * pageSize).Take(pageSize)
                .Select(x => new CustomerReferenceOptionResponse(x.Id, x.CustomerCode, x.CustomerCode)).ToListAsync(ct);
            return (items, total);
        }
        public Task<bool> ExistsActiveAsync(Guid id, CancellationToken ct) =>
            _db.Customers.AsNoTracking().AnyAsync(x => x.Id == id && x.Status == CustomerLifecycleStatus.Active, ct);
    }

    public sealed class BranchReferenceAdapter : IBranchReferencePort
    {
        private readonly FinnovaDbContext _db;
        public BranchReferenceAdapter(FinnovaDbContext db) => _db = db;
        public Task<List<ApplicationReferenceOptionResponse>> GetActiveAsync(Guid? lineOfBusinessId, CancellationToken ct) =>
            _db.Locations.AsNoTracking().Where(x => x.IsActive && x.Level == 5).OrderBy(x => x.Name)
                .Select(x => new ApplicationReferenceOptionResponse(x.Id, x.Code, x.Name)).ToListAsync(ct);
        public Task<bool> ExistsActiveAsync(Guid id, CancellationToken ct) =>
            _db.Locations.AsNoTracking().AnyAsync(x => x.Id == id && x.IsActive && x.Level == 5, ct);
    }
    ```
  - [ ] 4.3 **CREATE `Finnova.Repository/Repositories/LookupReferenceAdapter.cs`**. Delegate Product/Scheme reads and validation to the existing `ILookupRepository`; do not add lookup CRUD, a new lookup controller, or duplicate lookup entities. _Requirements: R1.7, R6.4-R6.6._
    ```csharp
    using Finnova.Models.Contracts.Lookups;
    using Finnova.Repository.Interfaces;

    namespace Finnova.Repository.Repositories;

    public sealed class LookupReferenceAdapter : ILookupReferencePort
    {
        private readonly ILookupRepository _lookup;
        public LookupReferenceAdapter(ILookupRepository lookup) => _lookup = lookup;
        public async Task<List<LookupDropdownItemResponse>> GetActiveAsync(string module, string type, CancellationToken ct) =>
            (await _lookup.GetActiveByModuleAndTypeAsync(module, type, ct))
            .Select(x => new LookupDropdownItemResponse(x.Code, x.Value)).ToList();
        public async Task<bool> ExistsActiveAsync(string module, string type, string code, CancellationToken ct)
        {
            var item = await _lookup.GetByModuleTypeCodeAsync(module, type, code, ct);
            return item?.IsActive == true;
        }
    }
    ```
  - [ ] 4.4 **CREATE `Finnova.Service/Applications/References/ApplicationReferenceValidator.cs`** and **CREATE** the reference query handlers under `Finnova.Service/Applications/Queries/References/`. The validator must report each invalid field and must not persist on failure. _Requirements: R1.4-R1.8, R5.4, R6.1-R6.8._
    ```csharp
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Microsoft.Extensions.Configuration;

    namespace Finnova.Service.Applications.References;

    public sealed class ApplicationReferenceValidator : IApplicationReferenceValidator
    {
        private readonly ILineOfBusinessReferencePort _lob;
        private readonly ICustomerReferencePort _customer;
        private readonly IBranchReferencePort _branch;
        private readonly ILookupReferencePort _lookup;
        private readonly IConfiguration _configuration;
        public ApplicationReferenceValidator(ILineOfBusinessReferencePort lob, ICustomerReferencePort customer,
            IBranchReferencePort branch, ILookupReferencePort lookup, IConfiguration configuration)
            => (_lob, _customer, _branch, _lookup, _configuration) = (lob, customer, branch, lookup, configuration);

        public async Task ValidateAsync(Guid lobId, Guid customerId, Guid branchId, string productCode, string schemeCode, CancellationToken ct)
        {
            var module = _configuration["ApplicationProcessing:LookupModule"] ?? throw new ApplicationConfigurationException("ApplicationProcessing:LookupModule is required.");
            var productType = _configuration["ApplicationProcessing:ProductLookupType"] ?? throw new ApplicationConfigurationException("ApplicationProcessing:ProductLookupType is required.");
            var schemeType = _configuration["ApplicationProcessing:SchemeLookupType"] ?? throw new ApplicationConfigurationException("ApplicationProcessing:SchemeLookupType is required.");
            var checks = await Task.WhenAll(_lob.ExistsActiveAsync(lobId, ct), _customer.ExistsActiveAsync(customerId, ct),
                _branch.ExistsActiveAsync(branchId, ct), _lookup.ExistsActiveAsync(module, productType, productCode.Trim(), ct),
                _lookup.ExistsActiveAsync(module, schemeType, schemeCode.Trim(), ct));
            if (!checks[0]) throw new ApplicationReferenceInvalidException("Line of Business");
            if (!checks[1]) throw new ApplicationReferenceInvalidException("Customer");
            if (!checks[2]) throw new ApplicationReferenceInvalidException("Branch");
            if (!checks[3]) throw new ApplicationReferenceInvalidException("Product");
            if (!checks[4]) throw new ApplicationReferenceInvalidException("Scheme");
        }
    }
    ```

- [ ] 5. Add repositories, application number generation, and DI. _Requirements: R1-R2, R4-R7._
  - [ ] 5.1 **CREATE `Finnova.Repository/Interfaces/ApplicationRepositoryInterfaces.cs`**. Include query filters, tracked mutation loading, explicit no-save mutation methods, schedule reads/inserts, append-only audit writes, and a generator contract. `IRepository<Application>.AddAsync` and `UpdateAsync` remain available for legacy callers but save immediately through `RepositoryBase<T>`; application handlers must use `AddPending` and `MarkModified` so one later unit-of-work save remains atomic. _Requirements: R2.1-R2.2, R4.1-R4.6, R5.1-R5.6, R7.1-R7.18._
    ```csharp
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Repository.Interfaces;

    public sealed record ApplicationFilters(string? Search, Guid? LineOfBusinessId, Guid? CustomerId,
        string? ProductCode, Guid? BranchId, string? SchemeCode, int Page, int PageSize);
    public interface IApplicationRepository : IRepository<Application>
    {
        Task<(List<Application> Items, int Total)> SearchAsync(ApplicationFilters filters, CancellationToken ct);
        Task<Application?> GetTrackedAsync(Guid id, CancellationToken ct);
        void AddPending(Application entity);
        void MarkModified(Application entity);
    }
    public interface IApplicationScheduleRepository
    {
        Task<ApplicationSchedule?> GetByApplicationIdAsync(Guid applicationId, CancellationToken ct);
        Task AddAsync(ApplicationSchedule schedule, CancellationToken ct);
    }
    public interface IApplicationStatusAuditRepository
    {
        Task AddAsync(ApplicationStatusAuditEntry entry, CancellationToken ct);
    }
    public interface IApplicationNumberGenerator
    {
        string Next();
    }
    ```
  - [ ] 5.2 **CREATE `Finnova.Repository/Repositories/ApplicationRepositories.cs`** with complete repository implementations. `SearchAsync` trims and case-folds search and combines all supplied filters using AND semantics; order is CreatedAt descending then ApplicationNumber ascending. `AddPending`/`MarkModified` and the schedule/audit `AddAsync` methods only change the scoped EF change tracker; none of these methods calls `SaveChangesAsync`. _Requirements: R2.1-R2.2, R7.1-R7.18._
    ```csharp
    using Finnova.Models.Domain.Entities;
    using Finnova.Repository.Context;
    using Finnova.Repository.Interfaces;
    using Microsoft.EntityFrameworkCore;

    namespace Finnova.Repository.Repositories;

    public sealed class ApplicationRepository : RepositoryBase<Application>, IApplicationRepository
    {
        public ApplicationRepository(FinnovaDbContext context) : base(context) { }
        public void AddPending(Application entity) => DbSet.Add(entity);
        public void MarkModified(Application entity) => Context.Entry(entity).State = EntityState.Modified;
        public async Task<(List<Application> Items, int Total)> SearchAsync(ApplicationFilters f, CancellationToken ct)
        {
            var q = DbSet.AsNoTracking().AsQueryable(); var term = f.Search?.Trim();
            if (!string.IsNullOrEmpty(term)) q = q.Where(x => x.ApplicationNumber.Contains(term) || x.CustomerId.ToString().Contains(term));
            if (f.LineOfBusinessId.HasValue) q = q.Where(x => x.LineOfBusinessId == f.LineOfBusinessId);
            if (f.CustomerId.HasValue) q = q.Where(x => x.CustomerId == f.CustomerId);
            if (!string.IsNullOrWhiteSpace(f.ProductCode)) q = q.Where(x => x.ProductCode == f.ProductCode.Trim());
            if (f.BranchId.HasValue) q = q.Where(x => x.BranchId == f.BranchId);
            if (!string.IsNullOrWhiteSpace(f.SchemeCode)) q = q.Where(x => x.SchemeCode == f.SchemeCode.Trim());
            var total = await q.CountAsync(ct);
            var items = await q.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.ApplicationNumber)
                .Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).ToListAsync(ct);
            return (items, total);
        }
        public Task<Application?> GetTrackedAsync(Guid id, CancellationToken ct) => DbSet.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public sealed class ApplicationScheduleRepository : IApplicationScheduleRepository
    {
        private readonly FinnovaDbContext _db;
        public ApplicationScheduleRepository(FinnovaDbContext db) => _db = db;
        public Task<ApplicationSchedule?> GetByApplicationIdAsync(Guid id, CancellationToken ct) =>
            _db.ApplicationSchedules.AsNoTracking().Include(x => x.Installments).FirstOrDefaultAsync(x => x.ApplicationId == id, ct);
        public Task AddAsync(ApplicationSchedule schedule, CancellationToken ct) => _db.ApplicationSchedules.AddAsync(schedule, ct).AsTask();
    }

    public sealed class ApplicationStatusAuditRepository : IApplicationStatusAuditRepository
    {
        private readonly FinnovaDbContext _db;
        public ApplicationStatusAuditRepository(FinnovaDbContext db) => _db = db;
        public Task AddAsync(ApplicationStatusAuditEntry entry, CancellationToken ct) => _db.ApplicationStatusAuditEntries.AddAsync(entry, ct).AsTask();
    }
    ```
  - [ ] 5.3 **CREATE `Finnova.Service/Applications/ApplicationNumberGenerator.cs`**. Use a cryptographically random token and a configured prefix; handlers retry only a unique-number conflict. _Requirements: R2.1-R2.4._
    ```csharp
    using System.Security.Cryptography;
    using Finnova.Repository.Interfaces;

    namespace Finnova.Service.Applications;

    public sealed class ApplicationNumberGenerator : IApplicationNumberGenerator
    {
        private readonly IConfiguration _configuration;
        public ApplicationNumberGenerator(IConfiguration configuration) => _configuration = configuration;
        public string Next()
        {
            var prefix = _configuration["ApplicationProcessing:ApplicationNumberPrefix"] ?? "APP";
            return $"{prefix}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(8))}";
        }
    }
    ```
  - [ ] 5.4 **MODIFY `Finnova.Repository/DependencyInjection.cs`**. Insert the exact registrations immediately after `AddScoped<ICustomerUnitOfWork, CustomerUnitOfWork>();`. _Requirements: R1.1, R2.1-R2.2, R4.1, R5.1, R6.1-R6.7, R7.1-R7.18._
    ```csharp
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<IApplicationScheduleRepository, ApplicationScheduleRepository>();
        services.AddScoped<IApplicationStatusAuditRepository, ApplicationStatusAuditRepository>();
        services.AddScoped<IApplicationUnitOfWork, ApplicationUnitOfWork>();
        services.AddScoped<ILineOfBusinessReferencePort, LineOfBusinessReferenceAdapter>();
        services.AddScoped<ICustomerReferencePort, CustomerReferenceAdapter>();
        services.AddScoped<IBranchReferencePort, BranchReferenceAdapter>();
        services.AddScoped<ILookupReferencePort, LookupReferenceAdapter>();
    ```
    Do not add a Service namespace import. Repository DI registers only Repository implementations and interfaces, including the four repository reference ports. Do not register `IApplicationReferenceValidator` here; AccountsService task 8.2 registers that service policy exactly once.
  - [ ] 5.5 **MODIFY no source file: inspect project references and namespaces before continuing.** Verify `Finnova.Repository.csproj` references only `Finnova.Models`, `Finnova.Service.csproj` references `Finnova.Models` and `Finnova.Repository`, and `Finnova.AccountsService.csproj` references Models, Repository, and Service. Search all `Finnova.Repository` task-created files for `using Finnova.Service` and remove any such import. Confirm the final graph is `Models <- Repository <- Service <- AccountsService` with no reverse reference. _Requirements: R1-R9._

- [ ] 6. Implement pure amortization, date/rounding helpers, and validation. _Requirements: R1, R3-R5, R7._
  - [ ] 6.1 **CREATE `Finnova.Service/Applications/Financial/AmortizationCalculator.cs`**. This is the single pure implementation used by preview and process. Use `decimal`, `Math.Round(..., 2, MidpointRounding.AwayFromZero)`, zero-IRR handling, final-line adjustment, and calendar date arithmetic. _Requirements: R3.1-R3.13._
    ```csharp
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.ValueObjects;

    namespace Finnova.Service.Applications.Financial;

    public sealed class AmortizationCalculator
    {
        public ScheduleResult Calculate(ApplicationTerms t)
        {
            var periods = t.RepaymentFrequency switch { RepaymentFrequency.Monthly => 12, RepaymentFrequency.Quarterly => 4,
                RepaymentFrequency.HalfYearly => 2, RepaymentFrequency.Annual => 1, _ => throw new ArgumentOutOfRangeException() };
            var rate = t.IrrPercent / 100m / periods;
            var emi = t.IrrPercent == 0m ? Money(t.FinanceAmount / t.Tenure) :
                Money(t.FinanceAmount * rate * (decimal)Math.Pow((double)(1m + rate), t.Tenure) /
                    ((decimal)Math.Pow((double)(1m + rate), t.Tenure) - 1m));
            var lines = new List<ScheduleLineValue>(t.Tenure); var opening = Money(t.FinanceAmount);
            for (var n = 1; n <= t.Tenure; n++)
            {
                var interest = Money(opening * rate); var final = n == t.Tenure;
                var principal = final ? opening : Money(emi - interest);
                var installment = final ? Money(principal + interest) : emi;
                var closing = final ? 0m : Money(opening - principal);
                lines.Add(new ScheduleLineValue(n, DueDate(t.DisbursementDate, t.RepaymentFrequency, n), opening,
                    interest, principal, installment, closing)); opening = closing;
            }
            return new ScheduleResult(rate, emi, lines, Money(lines.Sum(x => x.InstallmentAmount)),
                Money(lines.Sum(x => x.InterestPortion)), Money(lines.Sum(x => x.PrincipalPortion)));
        }
        private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
        private static DateOnly DueDate(DateOnly disbursement, RepaymentFrequency frequency, int number)
        {
            var months = frequency switch { RepaymentFrequency.Monthly => 1, RepaymentFrequency.Quarterly => 3,
                RepaymentFrequency.HalfYearly => 6, RepaymentFrequency.Annual => 12, _ => throw new ArgumentOutOfRangeException() };
            return disbursement.AddMonths(months * number);
        }
    }
    ```
  - [ ] 6.2 **CREATE `Finnova.Service/Applications/Validation/ApplicationValidation.cs`**. Add the shared constants, a fixed UTC clock abstraction, `CreateApplicationCommandValidator`, `UpdateApplicationCommandValidator`, and `GetApplicationsQueryValidator`. Reject invalid enum values, decimal scale beyond paise for monetary fields, and date windows before mutation. _Requirements: R1.4, R1.9-R1.14, R4.2, R7.8, R7.12-R7.13._
    ```csharp
    using FluentValidation;
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Service.Applications.Validation;

    public static class ApplicationValidationRules
    {
        public const decimal MaxFinanceAmount = 1000000000m;
        public const int MaxTenure = 600;
        public const int MaxSearchLength = 100;
        public static bool IsSupported(RepaymentFrequency value) => Enum.IsDefined(value);
        public static bool HasPaisaScale(decimal value) => decimal.Round(value, 2) == value;
    }

    public sealed class CreateApplicationCommandValidator : AbstractValidator<Applications.Commands.CreateApplicationCommand>
    {
        public CreateApplicationCommandValidator()
        {
            RuleFor(x => x.LineOfBusinessId).NotEmpty(); RuleFor(x => x.CustomerId).NotEmpty(); RuleFor(x => x.BranchId).NotEmpty();
            RuleFor(x => x.ProductCode).NotEmpty().MaximumLength(50); RuleFor(x => x.SchemeCode).NotEmpty().MaximumLength(50);
            RuleFor(x => x.FinanceAmount).GreaterThan(0).LessThanOrEqualTo(ApplicationValidationRules.MaxFinanceAmount).Must(ApplicationValidationRules.HasPaisaScale);
            RuleFor(x => x.IrrPercent).InclusiveBetween(0, 100); RuleFor(x => x.Tenure).InclusiveBetween(1, ApplicationValidationRules.MaxTenure);
            RuleFor(x => x.RepaymentFrequency).Must(ApplicationValidationRules.IsSupported); RuleFor(x => x.ProcessingCharges).GreaterThanOrEqualTo(0).Must(x => !x.HasValue || ApplicationValidationRules.HasPaisaScale(x.Value));
        }
    }

    public sealed class UpdateApplicationCommandValidator : AbstractValidator<Applications.Commands.UpdateApplicationCommand>
    {
        public UpdateApplicationCommandValidator()
        {
            Include(new CreateApplicationCommandValidator().Transform(x => new Applications.Commands.CreateApplicationCommand(
                x.LineOfBusinessId, x.CustomerId, x.BranchId, x.ProductCode, x.SchemeCode, x.FinanceAmount, x.IrrPercent,
                x.Tenure, x.RepaymentFrequency, x.DisbursementDate, x.ProcessingCharges, "")));
            RuleFor(x => x.Id).NotEmpty();
        }
    }

    public sealed class GetApplicationsQueryValidator : AbstractValidator<Applications.Queries.GetApplicationsQuery>
    {
        public GetApplicationsQueryValidator()
        {
            RuleFor(x => x.Search).MaximumLength(ApplicationValidationRules.MaxSearchLength);
            RuleFor(x => x.Page).GreaterThanOrEqualTo(1); RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        }
    }
    ```
  - [ ] 6.3 **CREATE `Finnova.Service/Applications/Financial/AmortizationInvariants.cs`**. Keep this helper test-only or internal; use it in unit/property tests to detect rounding regressions. _Requirements: R3.7-R3.11._
    ```csharp
    using Finnova.Models.Domain.ValueObjects;

    namespace Finnova.Service.Applications.Financial;

    public static class AmortizationInvariants
    {
        public static bool Holds(ScheduleResult result, decimal principal)
        {
            var rows = result.Installments;
            return rows.Count > 0 && rows.Select(x => x.InstallmentNumber).SequenceEqual(Enumerable.Range(1, rows.Count))
                && rows.All(x => x.OpeningBalance >= 0 && x.InterestPortion >= 0 && x.PrincipalPortion >= 0 && x.InstallmentAmount >= 0 && x.ClosingBalance >= 0)
                && rows[^1].ClosingBalance == 0m && rows.Sum(x => x.PrincipalPortion) == principal
                && result.TotalInstallmentAmount == result.TotalPrincipalAmount + result.TotalInterestAmount;
        }
    }
    ```

- [ ] 7. Implement the complete backend CQRS slice. _Requirements: R1-R7._
  - [ ] 7.1 **CREATE `Finnova.Service/Applications/Commands/ApplicationCommands.cs`**. Include create, update, preview, and process requests and handlers. Create ignores client number/status because contracts do not bind them. Update preserves status/number and performs a no-op before touching `UpdatedAtUtc`. Process uses one transaction, rowversion, reference revalidation, schedule insert, status audit, and `Processed` state. _Requirements: R1.1-R1.14, R2.1-R2.4, R3.1-R3.13, R4.1-R4.6, R5.1-R5.6, R6.7._
    ```csharp
    using FluentValidation;
    using MediatR;
    using Finnova.Models.Contracts.Applications;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Models.Domain.ValueObjects;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Applications.Financial;
    using Finnova.Service.Applications.References;
    using Finnova.Service.Mappers;

    namespace Finnova.Service.Applications.Commands;

    public sealed record CreateApplicationCommand(Guid LineOfBusinessId, Guid CustomerId, Guid BranchId, string ProductCode,
        string SchemeCode, decimal FinanceAmount, decimal IrrPercent, int Tenure, RepaymentFrequency RepaymentFrequency,
        DateOnly DisbursementDate, decimal? ProcessingCharges, string ActingUser) : IRequest<ApplicationResponse>;

    public sealed record UpdateApplicationCommand(Guid Id, Guid LineOfBusinessId, Guid CustomerId, Guid BranchId, string ProductCode,
        string SchemeCode, decimal FinanceAmount, decimal IrrPercent, int Tenure, RepaymentFrequency RepaymentFrequency,
        DateOnly DisbursementDate, decimal? ProcessingCharges, string ActingUser) : IRequest<ApplicationResponse>;

    public sealed record PreviewApplicationScheduleQuery(Guid Id) : IRequest<ApplicationScheduleResponse>;
    public sealed record ProcessApplicationCommand(Guid Id, string ActingUser) : IRequest<ProcessApplicationResponse>;

    public sealed class CreateApplicationCommandHandler : IRequestHandler<CreateApplicationCommand, ApplicationResponse>
    {
        private readonly IApplicationRepository _apps; private readonly IApplicationUnitOfWork _uow; private readonly IApplicationNumberGenerator _numbers;
        private readonly IApplicationReferenceValidator _references;
        public CreateApplicationCommandHandler(IApplicationRepository apps, IApplicationUnitOfWork uow, IApplicationNumberGenerator numbers, IApplicationReferenceValidator references)
            => (_apps, _uow, _numbers, _references) = (apps, uow, numbers, references);
        public async Task<ApplicationResponse> Handle(CreateApplicationCommand r, CancellationToken ct)
        {
            await _references.ValidateAsync(r.LineOfBusinessId, r.CustomerId, r.BranchId, r.ProductCode, r.SchemeCode, ct);
            var entity = new Application { ApplicationNumber = _numbers.Next(), LineOfBusinessId = r.LineOfBusinessId, CustomerId = r.CustomerId,
                BranchId = r.BranchId, ProductCode = r.ProductCode.Trim(), SchemeCode = r.SchemeCode.Trim(), FinanceAmount = r.FinanceAmount,
                IrrPercent = r.IrrPercent, Tenure = r.Tenure, RepaymentFrequency = r.RepaymentFrequency, DisbursementDate = r.DisbursementDate,
                ProcessingCharges = r.ProcessingCharges ?? 0m, Status = ApplicationStatus.Draft };
            _apps.AddPending(entity);
            await _uow.SaveChangesAsync(ct);
            return entity.ToResponse();
        }
    }

    public sealed class UpdateApplicationCommandHandler : IRequestHandler<UpdateApplicationCommand, ApplicationResponse>
    {
        private readonly IApplicationRepository _apps; private readonly IApplicationUnitOfWork _uow; private readonly IApplicationReferenceValidator _references;
        public UpdateApplicationCommandHandler(IApplicationRepository apps, IApplicationUnitOfWork uow, IApplicationReferenceValidator references)
            => (_apps, _uow, _references) = (apps, uow, references);
        public async Task<ApplicationResponse> Handle(UpdateApplicationCommand r, CancellationToken ct)
        {
            var a = await _apps.GetTrackedAsync(r.Id, ct) ?? throw new ApplicationNotFoundException(r.Id);
            if (a.Status == ApplicationStatus.Processed) throw new ApplicationAlreadyProcessedException();
            await _references.ValidateAsync(r.LineOfBusinessId, r.CustomerId, r.BranchId, r.ProductCode, r.SchemeCode, ct);
            var charges = r.ProcessingCharges ?? 0m;
            if (a.LineOfBusinessId == r.LineOfBusinessId && a.CustomerId == r.CustomerId && a.BranchId == r.BranchId && a.ProductCode == r.ProductCode.Trim()
                && a.SchemeCode == r.SchemeCode.Trim() && a.FinanceAmount == r.FinanceAmount && a.IrrPercent == r.IrrPercent && a.Tenure == r.Tenure
                && a.RepaymentFrequency == r.RepaymentFrequency && a.DisbursementDate == r.DisbursementDate && a.ProcessingCharges == charges) return a.ToResponse();
            a.LineOfBusinessId = r.LineOfBusinessId; a.CustomerId = r.CustomerId; a.BranchId = r.BranchId; a.ProductCode = r.ProductCode.Trim();
            a.SchemeCode = r.SchemeCode.Trim(); a.FinanceAmount = r.FinanceAmount; a.IrrPercent = r.IrrPercent; a.Tenure = r.Tenure;
            a.RepaymentFrequency = r.RepaymentFrequency; a.DisbursementDate = r.DisbursementDate; a.ProcessingCharges = charges; a.UpdatedAtUtc = DateTime.UtcNow;
            _apps.MarkModified(a);
            await _uow.SaveChangesAsync(ct);
            return a.ToResponse();
        }
    }

    public sealed class PreviewApplicationScheduleQueryHandler : IRequestHandler<PreviewApplicationScheduleQuery, ApplicationScheduleResponse>
    {
        private readonly IApplicationRepository _apps; private readonly IApplicationScheduleRepository _schedules; private readonly AmortizationCalculator _calculator;
        public PreviewApplicationScheduleQueryHandler(IApplicationRepository apps, IApplicationScheduleRepository schedules, AmortizationCalculator calculator)
            => (_apps, _schedules, _calculator) = (apps, schedules, calculator);
        public async Task<ApplicationScheduleResponse> Handle(PreviewApplicationScheduleQuery r, CancellationToken ct)
        {
            var a = await _apps.GetByIdAsync(r.Id, ct) ?? throw new ApplicationNotFoundException(r.Id);
            if (a.Status == ApplicationStatus.Processed) { var stored = await _schedules.GetByApplicationIdAsync(r.Id, ct) ?? throw new ApplicationConflictException("Final schedule is missing."); return stored.ToResponse(); }
            var terms = new ApplicationTerms(a.FinanceAmount, a.IrrPercent, a.Tenure, a.RepaymentFrequency, a.DisbursementDate);
            return _calculator.Calculate(terms).ToResponse(a.Id, terms);
        }
    }

    public sealed class ProcessApplicationCommandHandler : IRequestHandler<ProcessApplicationCommand, ProcessApplicationResponse>
    {
        private readonly IApplicationRepository _apps; private readonly IApplicationScheduleRepository _schedules; private readonly IApplicationStatusAuditRepository _audit;
        private readonly IApplicationUnitOfWork _uow; private readonly IApplicationReferenceValidator _references; private readonly AmortizationCalculator _calculator;
        public ProcessApplicationCommandHandler(IApplicationRepository apps, IApplicationScheduleRepository schedules, IApplicationStatusAuditRepository audit,
            IApplicationUnitOfWork uow, IApplicationReferenceValidator references, AmortizationCalculator calculator)
            => (_apps, _schedules, _audit, _uow, _references, _calculator) = (apps, schedules, audit, uow, references, calculator);
        public async Task<ProcessApplicationResponse> Handle(ProcessApplicationCommand r, CancellationToken ct)
        {
            // Load before opening the transaction; the tracked rowversion remains the optimistic concurrency guard.
            var a = await _apps.GetTrackedAsync(r.Id, ct) ?? throw new ApplicationNotFoundException(r.Id);
            if (a.Status == ApplicationStatus.Processed) throw new ApplicationAlreadyProcessedException();
            await using var transaction = await _uow.BeginTransactionAsync(ct);
            try
            {
                await _references.ValidateAsync(a.LineOfBusinessId, a.CustomerId, a.BranchId, a.ProductCode, a.SchemeCode, ct);
                var terms = new ApplicationTerms(a.FinanceAmount, a.IrrPercent, a.Tenure, a.RepaymentFrequency, a.DisbursementDate);
                var result = _calculator.Calculate(terms);
                var schedule = new ApplicationSchedule { ApplicationId = a.Id, IsFinalized = true, GeneratedAtUtc = DateTime.UtcNow,
                    FinanceAmount = terms.FinanceAmount, IrrPercent = terms.IrrPercent, Tenure = terms.Tenure, RepaymentFrequency = terms.RepaymentFrequency,
                    DisbursementDate = terms.DisbursementDate, PeriodicInterestRate = result.PeriodicInterestRate, EmiAmount = result.EmiAmount,
                    TotalInstallmentAmount = result.TotalInstallmentAmount, TotalInterestAmount = result.TotalInterestAmount, TotalPrincipalAmount = result.TotalPrincipalAmount,
                    Installments = result.Installments.Select(x => new ApplicationInstallment { InstallmentNumber = x.InstallmentNumber, DueDate = x.DueDate,
                        OpeningBalance = x.OpeningBalance, InterestPortion = x.InterestPortion, PrincipalPortion = x.PrincipalPortion,
                        InstallmentAmount = x.InstallmentAmount, ClosingBalance = x.ClosingBalance }).ToList() };
                await _schedules.AddAsync(schedule, ct);
                var previous = a.Status; a.Status = ApplicationStatus.Processed; a.UpdatedAtUtc = DateTime.UtcNow;
                await _audit.AddAsync(new ApplicationStatusAuditEntry { ApplicationId = a.Id, FromStatus = previous, ToStatus = a.Status, ChangedBy = r.ActingUser }, ct);
                await _uow.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return new ProcessApplicationResponse(a.ToResponse(), schedule.ToResponse());
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }
    }
    ```
  - [ ] 7.2 **CREATE `Finnova.Service/Applications/Queries/ApplicationQueries.cs`**. Include paged search, get-by-id, and schedule-read queries. Schedule-read returns persisted rows for Processed and delegates to preview for Draft/Submitted. _Requirements: R5.5-R5.6, R7.1-R7.18._
    ```csharp
    using MediatR;
    using Finnova.Models.Contracts.Applications;
    using Finnova.Models.Contracts.Common;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Mappers;

    namespace Finnova.Service.Applications.Queries;

    public sealed record GetApplicationsQuery(string? Search, Guid? LineOfBusinessId, Guid? CustomerId, string? ProductCode,
        Guid? BranchId, string? SchemeCode, int Page = 1, int PageSize = 20) : IRequest<PaginatedResponse<ApplicationResponse>>;
    public sealed record GetApplicationByIdQuery(Guid Id) : IRequest<ApplicationResponse>;
    public sealed record GetApplicationScheduleQuery(Guid Id) : IRequest<ApplicationScheduleResponse>;

    public sealed class GetApplicationsQueryHandler : IRequestHandler<GetApplicationsQuery, PaginatedResponse<ApplicationResponse>>
    {
        private readonly IApplicationRepository _repo;
        public GetApplicationsQueryHandler(IApplicationRepository repo) => _repo = repo;
        public async Task<PaginatedResponse<ApplicationResponse>> Handle(GetApplicationsQuery r, CancellationToken ct)
        {
            var result = await _repo.SearchAsync(new ApplicationFilters(r.Search, r.LineOfBusinessId, r.CustomerId, r.ProductCode, r.BranchId, r.SchemeCode, r.Page, r.PageSize), ct);
            return new PaginatedResponse<ApplicationResponse>(result.Items.Select(x => x.ToResponse()).ToList(), result.Total, r.Page, r.PageSize, (int)Math.Ceiling(result.Total / (double)r.PageSize));
        }
    }
    public sealed class GetApplicationByIdQueryHandler : IRequestHandler<GetApplicationByIdQuery, ApplicationResponse>
    {
        private readonly IApplicationRepository _repo;
        public GetApplicationByIdQueryHandler(IApplicationRepository repo) => _repo = repo;
        public async Task<ApplicationResponse> Handle(GetApplicationByIdQuery r, CancellationToken ct) =>
            (await _repo.GetByIdAsync(r.Id, ct))?.ToResponse() ?? throw new ApplicationNotFoundException(r.Id);
    }
    public sealed class GetApplicationScheduleQueryHandler : IRequestHandler<GetApplicationScheduleQuery, ApplicationScheduleResponse>
    {
        private readonly IApplicationRepository _apps; private readonly IApplicationScheduleRepository _schedules; private readonly IMediator _mediator;
        public GetApplicationScheduleQueryHandler(IApplicationRepository apps, IApplicationScheduleRepository schedules, IMediator mediator)
            => (_apps, _schedules, _mediator) = (apps, schedules, mediator);
        public async Task<ApplicationScheduleResponse> Handle(GetApplicationScheduleQuery r, CancellationToken ct)
        {
            var a = await _apps.GetByIdAsync(r.Id, ct) ?? throw new ApplicationNotFoundException(r.Id);
            if (a.Status != ApplicationStatus.Processed) return await _mediator.Send(new Commands.PreviewApplicationScheduleQuery(r.Id), ct);
            var s = await _schedules.GetByApplicationIdAsync(r.Id, ct) ?? throw new ApplicationConflictException("Final schedule is missing.");
            return s.ToResponse();
        }
    }
    ```
  - [ ] 7.3 **CREATE `Finnova.Service/Applications/Queries/ReferenceQueries.cs`**. Add active LOB, paged Customer, Branch, Product, Scheme, and configurable classification reads. Product/Scheme calls must use the existing Lookup dropdown port. _Requirements: R6.1-R6.8._
    ```csharp
    using MediatR;
    using Finnova.Models.Contracts.Applications;
    using Finnova.Models.Contracts.Common;
    using Finnova.Models.Contracts.Lookups;
    using Finnova.Models.Contracts.UserManagement;
    using Finnova.Repository.Interfaces;

    namespace Finnova.Service.Applications.Queries;

    public sealed record GetActiveLinesOfBusinessQuery() : IRequest<List<LineOfBusinessRefResponse>>;
    public sealed record SearchActiveCustomersQuery(string? Search, int Page = 1, int PageSize = 20) : IRequest<CustomerReferencePage>;
    public sealed record GetActiveBranchesQuery(Guid? LineOfBusinessId = null) : IRequest<List<ApplicationReferenceOptionResponse>>;
    public sealed record GetConfiguredLookupQuery(string Type) : IRequest<List<LookupDropdownItemResponse>>;
    public sealed class GetActiveLinesOfBusinessQueryHandler : IRequestHandler<GetActiveLinesOfBusinessQuery, List<LineOfBusinessRefResponse>>
    { private readonly ILineOfBusinessReferencePort _p; public GetActiveLinesOfBusinessQueryHandler(ILineOfBusinessReferencePort p) => _p = p; public Task<List<LineOfBusinessRefResponse>> Handle(GetActiveLinesOfBusinessQuery r, CancellationToken c) => _p.GetActiveAsync(c); }
    public sealed class SearchActiveCustomersQueryHandler : IRequestHandler<SearchActiveCustomersQuery, CustomerReferencePage>
    { private readonly ICustomerReferencePort _p; public SearchActiveCustomersQueryHandler(ICustomerReferencePort p) => _p = p; public async Task<CustomerReferencePage> Handle(SearchActiveCustomersQuery r, CancellationToken c) { var x = await _p.SearchActiveAsync(r.Search, r.Page, r.PageSize, c); return new(x.Items, x.Total, r.Page, r.PageSize, (int)Math.Ceiling(x.Total / (double)r.PageSize)); } }
    public sealed class GetActiveBranchesQueryHandler : IRequestHandler<GetActiveBranchesQuery, List<ApplicationReferenceOptionResponse>>
    { private readonly IBranchReferencePort _p; public GetActiveBranchesQueryHandler(IBranchReferencePort p) => _p = p; public Task<List<ApplicationReferenceOptionResponse>> Handle(GetActiveBranchesQuery r, CancellationToken c) => _p.GetActiveAsync(r.LineOfBusinessId, c); }
    public sealed class GetConfiguredLookupQueryHandler : IRequestHandler<GetConfiguredLookupQuery, List<LookupDropdownItemResponse>>
    { private readonly ILookupReferencePort _p; private readonly IConfiguration _c; public GetConfiguredLookupQueryHandler(ILookupReferencePort p, IConfiguration c) => (_p, _c) = (p, c); public Task<List<LookupDropdownItemResponse>> Handle(GetConfiguredLookupQuery r, CancellationToken c) => _p.GetActiveAsync(_c["ApplicationProcessing:LookupModule"] ?? throw new Models.Domain.Exceptions.ApplicationConfigurationException("Lookup module is required."), r.Type, c); }
    ```

- [ ] 8. Wire AccountsService, middleware, policy, and YARP. _Requirements: R1-R9._
  - [ ] 8.1 **CREATE `Finnova.AccountsService/Controllers/ApplicationProcessingController.cs`**. Use `[Authorize(Policy = "ApplicationProcessor")]` on every feature and reference action, route `api/application-processing`, and derive the actor from validated claims rather than request input. _Requirements: R5.1-R5.6, R6.1-R6.8, R7.1-R7.18, R9.1-R9.4._
    ```csharp
    using System.Security.Claims;
    using MediatR;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Finnova.Models.Contracts.Applications;
    using Finnova.Models.Contracts.Common;
    using Finnova.Service.Applications.Commands;
    using Finnova.Service.Applications.Queries;

    namespace Finnova.AccountsService.Controllers;

    [ApiController, Route("api/application-processing"), Authorize(Policy = "ApplicationProcessor")]
    public sealed class ApplicationProcessingController : ControllerBase
    {
        private readonly IMediator _mediator;
        public ApplicationProcessingController(IMediator mediator) => _mediator = mediator;
        private string Actor => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? User.Identity?.Name ?? "authenticated-user";
        [HttpPost]
        public async Task<ActionResult<ApplicationResponse>> Create(CreateApplicationRequest r, CancellationToken ct)
        {
            var result = await _mediator.Send(new CreateApplicationCommand(r.LineOfBusinessId, r.CustomerId, r.BranchId, r.ProductCode, r.SchemeCode, r.FinanceAmount, r.IrrPercent, r.Tenure, r.RepaymentFrequency, r.DisbursementDate, r.ProcessingCharges, Actor), ct);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        [HttpGet("references/lines-of-business", Order = -10)] public async Task<ActionResult<List<LineOfBusinessRefResponse>>> Lobs(CancellationToken ct) => Ok(await _mediator.Send(new GetActiveLinesOfBusinessQuery(), ct));
        [HttpGet("references/customers", Order = -10)] public async Task<ActionResult<CustomerReferencePage>> Customers([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) => Ok(await _mediator.Send(new SearchActiveCustomersQuery(search, page, pageSize), ct));
        [HttpGet("references/branches", Order = -10)] public async Task<ActionResult<List<ApplicationReferenceOptionResponse>>> Branches([FromQuery] Guid? lineOfBusinessId, CancellationToken ct) => Ok(await _mediator.Send(new GetActiveBranchesQuery(lineOfBusinessId), ct));
        [HttpGet("references/lookups/{type}", Order = -10)] public async Task<ActionResult<List<LookupDropdownItemResponse>>> Lookup(string type, CancellationToken ct) => Ok(await _mediator.Send(new GetConfiguredLookupQuery(type), ct));
        [HttpGet] public async Task<ActionResult<PaginatedResponse<ApplicationResponse>>> Search([FromQuery] ApplicationSearchRequest r, CancellationToken ct) => Ok(await _mediator.Send(new GetApplicationsQuery(r.Search, r.LineOfBusinessId, r.CustomerId, r.ProductCode, r.BranchId, r.SchemeCode, r.Page, r.PageSize), ct));
        [HttpGet("{id:guid}", Order = 0)] public async Task<ActionResult<ApplicationResponse>> GetById(Guid id, CancellationToken ct) => Ok(await _mediator.Send(new GetApplicationByIdQuery(id), ct));
        [HttpPut("{id:guid}")] public async Task<ActionResult<ApplicationResponse>> Update(Guid id, UpdateApplicationRequest r, CancellationToken ct) => Ok(await _mediator.Send(new UpdateApplicationCommand(id, r.LineOfBusinessId, r.CustomerId, r.BranchId, r.ProductCode, r.SchemeCode, r.FinanceAmount, r.IrrPercent, r.Tenure, r.RepaymentFrequency, r.DisbursementDate, r.ProcessingCharges, Actor), ct));
        [HttpGet("{id:guid}/schedule")] public async Task<ActionResult<ApplicationScheduleResponse>> Schedule(Guid id, CancellationToken ct) => Ok(await _mediator.Send(new GetApplicationScheduleQuery(id), ct));
        [HttpPost("{id:guid}/schedule/preview")] public async Task<ActionResult<ApplicationScheduleResponse>> Preview(Guid id, CancellationToken ct) => Ok(await _mediator.Send(new PreviewApplicationScheduleQuery(id), ct));
        [HttpPost("{id:guid}/process")] public async Task<ActionResult<ProcessApplicationResponse>> Process(Guid id, CancellationToken ct) => Ok(await _mediator.Send(new ProcessApplicationCommand(id, Actor), ct));
    }
    ```
    Route order is explicit: static `references/...` GET actions are declared first and use `Order = -10`; the constrained `GET api/application-processing/{id:guid}` action is declared afterward with `Order = 0`, so `references/...` cannot be treated as a GUID route.
  - [ ] 8.2 **MODIFY `Finnova.AccountsService/Program.cs`**. Insert the policy beside the existing `SystemAdmin` policy, register the calculator/reference validator, and preserve `UseAuthentication()` before `UseAuthorization()`. _Requirements: R6.8, R9.1-R9.4._
    ```csharp
    options.AddPolicy("ApplicationProcessor", p => p.RequireRole("ApplicationProcessor"));
    ```
    ```csharp
    builder.Services.AddScoped<Finnova.Service.Applications.Financial.AmortizationCalculator>();
    builder.Services.AddScoped<Finnova.Service.Applications.References.IApplicationReferenceValidator, Finnova.Service.Applications.References.ApplicationReferenceValidator>();
    builder.Services.AddSingleton<Finnova.Repository.Interfaces.IApplicationNumberGenerator, Finnova.Service.Applications.ApplicationNumberGenerator>();
    ```
    If the confirmed policy is different, replace only the policy name/claim expression in this snippet and the controller attribute; record the confirmed value in deployment configuration.
  - [ ] 8.3 **MODIFY `Finnova.AccountsService/Middlewares/ExceptionHandlingMiddleware.cs`**. Add the Application Processing branch before the existing shared validation/fallback branches and add `message` alongside `detail`; do not log request payloads, tokens, customer aggregates, bank data, or finance terms. _Requirements: R1.4-R1.8, R4.2-R4.4, R5.2-R5.4, R6.7, R9.1-R9.4._
    ```csharp
            var isApplicationProcessing = ctx.Request.Path.StartsWithSegments("/api/application-processing", StringComparison.OrdinalIgnoreCase);
            var validationCode = isApplicationProcessing ? "ERR-APP-400" : "ERR-GL-400";
            var fallbackCode = isApplicationProcessing ? "ERR-APP-500" : "ERR-GL-500";
            var (status, code, detail) = ex switch
            {
                GLNotFoundException => (StatusCodes.Status404NotFound, "ERR-GL-404", ex.Message),
                GLDuplicateCodeException => (StatusCodes.Status409Conflict, "ERR-GL-409", ex.Message),
                GLRetiredCodeException => (StatusCodes.Status409Conflict, "ERR-GL-409", ex.Message),
                GLDuplicateNameException => (StatusCodes.Status409Conflict, "ERR-GL-409", ex.Message),
                GLConflictException => (StatusCodes.Status409Conflict, "ERR-GL-409", ex.Message),
                ApplicationProcessingException a => a switch
                {
                    ApplicationNotFoundException => (StatusCodes.Status404NotFound, a.ErrorCode, a.Message),
                    ApplicationReferenceInvalidException => (StatusCodes.Status400BadRequest, a.ErrorCode, a.Message),
                    ApplicationAlreadyProcessedException or ApplicationConflictException => (StatusCodes.Status409Conflict, a.ErrorCode, a.Message),
                    ApplicationConfigurationException => (StatusCodes.Status503ServiceUnavailable, a.ErrorCode, a.Message),
                    _ => (StatusCodes.Status500InternalServerError, a.ErrorCode, a.Message)
                },
                FluentValidation.ValidationException v => (StatusCodes.Status400BadRequest, validationCode, v.Message),
                _ => (StatusCodes.Status500InternalServerError, fallbackCode, "Unexpected error.")
            };
    ```
    Replace the response-writing lines with this exact block so the shared UI interceptor can read `message` and the existing `code` extension remains available:
    ```csharp
            var problem = new ProblemDetails { Status = status, Title = code, Detail = detail };
            problem.Extensions["code"] = code;
            problem.Extensions["message"] = detail;
            problem.Extensions["correlationId"] = ctx.TraceIdentifier;
            ctx.Response.StatusCode = status;
            await ctx.Response.WriteAsJsonAsync(problem);
    ```
    Add `using Finnova.Models.Domain.Exceptions;` if absent. Map `FluentValidation.ValidationException` to `400/ERR-APP-400` when `ctx.Request.Path.StartsWithSegments("/api/application-processing")`; preserve existing GL behavior for other paths.
  - [ ] 8.4 **MODIFY `Finnova.AccountsService/appsettings.json` and `appsettings.Development.json`**. Add only non-secret configuration values; use environment overrides for signing keys and connection strings. _Requirements: R1.9, R1.14, R6.4-R6.6, R9.1._
    ```json
    "ApplicationProcessing": {
      "AuthorizationPolicy": "ApplicationProcessor",
      "LookupModule": "ApplicationProcessing",
      "ProductLookupType": "PRODUCT",
      "SchemeLookupType": "SCHEME",
      "ApplicationNumberPrefix": "APP"
    }
    ```
  - [ ] 8.5 **MODIFY `Finnova.ApiGateway/appsettings.json`**. Insert both exact aliases before `ua-route`; both aliases target `accounts-cluster`, remove `/api/ua/api`, then prefix `/api`. _Requirements: R6.8, R9.1-R9.4._
    ```json
      "ua-application-processing-alias-route": {
        "ClusterId": "accounts-cluster",
        "Match": { "Path": "/api/ua/api/application-processing/{**catch-all}" },
        "Transforms": [ { "PathRemovePrefix": "/api/ua/api" }, { "PathPrefix": "/api" } ]
      },
      "ua-application-processing-alias-root": {
        "ClusterId": "accounts-cluster",
        "Match": { "Path": "/api/ua/api/application-processing" },
        "Transforms": [ { "PathRemovePrefix": "/api/ua/api" }, { "PathPrefix": "/api" } ]
      },
    ```
    Do not change the existing `systemadmin-cluster`, lookup aliases, or host boundaries.

- [ ] 9. Add backend xUnit, FsCheck.Xunit, and WebApplicationFactory tests. _Requirements: R1-R9._
  - [ ] 9.1 **CREATE `Finnova.Tests/ApplicationProcessing/AmortizationCalculatorProperties.cs`**. Use FsCheck.Xunit with `MaxTest = 100` or greater and exactly one property test per design property. Each test comment must use `Feature: application-processing, Property N: ...`. Generate valid amounts, IRRs, tenures, frequencies, and dates. _Requirements: R3.1-R3.13._
    ```csharp
    using System;
    using System.Linq;
    using FsCheck;
    using FsCheck.Xunit;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.ValueObjects;
    using Finnova.Service.Applications.Financial;

    namespace Finnova.Tests.ApplicationProcessing;

    public sealed class AmortizationCalculatorProperties
    {
        private static readonly RepaymentFrequency[] Frequencies = Enum.GetValues<RepaymentFrequency>();
        private static int Bucket(int value, int modulus) => (int)((uint)value % (uint)modulus);
        private static decimal Amount(PositiveInt seed) => 1m + Bucket(seed.Get, 10_000_000) / 100m;
        private static decimal Irr(PositiveInt seed) => Bucket(seed.Get, 10_001) / 100m;
        private static ApplicationTerms Terms(decimal p, decimal irr, int n, RepaymentFrequency f, DateOnly d) => new(p, irr, n, f, d);
        [Property(MaxTest = 100)] // Feature: application-processing, Property 1: Schedule shape and installment numbering
        public Property ScheduleShape(PositiveInt amount, NonNegativeInt tenureSeed) { var n = Bucket(tenureSeed.Get, 600) + 1; var r = new AmortizationCalculator().Calculate(Terms(Amount(amount), 12m, n, Frequencies[Bucket(n, 4)], new DateOnly(2025, 1, 31))); return (r.Installments.Count == n && r.Installments.Select(x => x.InstallmentNumber).SequenceEqual(Enumerable.Range(1, n))).ToProperty(); }
        [Property(MaxTest = 100)] // Feature: application-processing, Property 2: Periodic rate conversion
        public Property PeriodicRate(NonNegativeInt irr, NonNegativeInt frequency) { var i = Bucket(irr.Get, 10_001) / 100m; var f = Frequencies[Bucket(frequency.Get, 4)]; var periods = f switch { RepaymentFrequency.Monthly => 12, RepaymentFrequency.Quarterly => 4, RepaymentFrequency.HalfYearly => 2, _ => 1 }; var expected = i / 100m / periods; var actual = new AmortizationCalculator().Calculate(Terms(1000m, i, 12, f, new DateOnly(2025, 1, 1))).PeriodicInterestRate; return (actual == expected).ToProperty(); }
        [Property(MaxTest = 100)] // Feature: application-processing, Property 3: Reducing-balance line recurrence
        public Property Recurrence(PositiveInt seed) { var r = new AmortizationCalculator().Calculate(Terms(Amount(seed), 9m, 12, RepaymentFrequency.Monthly, new DateOnly(2025, 1, 1))); return r.Installments.Skip(1).Select((x, i) => x.OpeningBalance == r.Installments[i].ClosingBalance).All(x => x).ToProperty(); }
        [Property(MaxTest = 100)] // Feature: application-processing, Property 4: Principal conservation and non-negative balances
        public Property Conservation(PositiveInt seed) { var p = Amount(seed); var r = new AmortizationCalculator().Calculate(Terms(p, Irr(seed), 20, RepaymentFrequency.Quarterly, new DateOnly(2025, 1, 1))); return (r.TotalPrincipalAmount == p && r.Installments.All(x => x.OpeningBalance >= 0 && x.InterestPortion >= 0 && x.PrincipalPortion >= 0 && x.InstallmentAmount >= 0 && x.ClosingBalance >= 0)).ToProperty(); }
        [Property(MaxTest = 100)] // Feature: application-processing, Property 5: Final-line settlement
        public Property FinalSettlement(PositiveInt seed) { var r = new AmortizationCalculator().Calculate(Terms(Amount(seed), 15m, 13, RepaymentFrequency.Annual, new DateOnly(2025, 1, 1))); var x = r.Installments[^1]; return (x.PrincipalPortion == x.OpeningBalance && x.InstallmentAmount == x.PrincipalPortion + x.InterestPortion && x.ClosingBalance == 0m).ToProperty(); }
        [Property(MaxTest = 100)] // Feature: application-processing, Property 6: Balance monotonicity
        public Property Monotonicity(PositiveInt seed) { var r = new AmortizationCalculator().Calculate(Terms(Amount(seed), 8m, 24, RepaymentFrequency.Monthly, new DateOnly(2025, 1, 1))); return (r.Installments.All(x => x.ClosingBalance <= x.OpeningBalance) && r.Installments.Skip(1).Select((x, i) => x.OpeningBalance <= r.Installments[i].OpeningBalance).All(x => x)).ToProperty(); }
        [Property(MaxTest = 100)] // Feature: application-processing, Property 7: Summary-total identity
        public Property SummaryIdentity(PositiveInt seed) { var r = new AmortizationCalculator().Calculate(Terms(Amount(seed), 11m, 18, RepaymentFrequency.Monthly, new DateOnly(2025, 1, 1))); return (r.TotalPrincipalAmount + r.TotalInterestAmount == r.TotalInstallmentAmount).ToProperty(); }
        [Property(MaxTest = 100)] // Feature: application-processing, Property 8: Deterministic generation
        public Property Deterministic(PositiveInt seed) { var t = Terms(Amount(seed), 7m, 10, RepaymentFrequency.Monthly, new DateOnly(2025, 1, 1)); var c = new AmortizationCalculator(); return (c.Calculate(t) == c.Calculate(t)).ToProperty(); }
        [Property(MaxTest = 100)] // Feature: application-processing, Property 9: Due-date recurrence
        public Property DueDates(PositiveInt seed) { var f = Frequencies[Bucket(seed.Get, 4)]; var r = new AmortizationCalculator().Calculate(Terms(1000m, 0m, 5, f, new DateOnly(2025, 1, 31))); var months = f switch { RepaymentFrequency.Monthly => 1, RepaymentFrequency.Quarterly => 3, RepaymentFrequency.HalfYearly => 6, _ => 12 }; return r.Installments.Select((x, i) => x.DueDate == new DateOnly(2025, 1, 31).AddMonths(months * (i + 1))).All(x => x).ToProperty(); }
    }
    ```
    Implementers may expand generators for better shrinking, but must retain at least 100 iterations and one test per property.
  - [ ] 9.2 **CREATE `Finnova.Tests/ApplicationProcessing/ApplicationValidationTests.cs`**. Add xUnit example/edge tests for missing references, all numeric boundaries, zero IRR, tenure one, month-end dates, no-op update, processed immutability, and invalid/stale references. _Requirements: R1.4-R1.14, R3.4, R3.13, R4.2, R4.4-R4.6, R5.2-R5.4, R6.7._
    ```csharp
    using Xunit;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.ValueObjects;
    using Finnova.Service.Applications.Financial;

    namespace Finnova.Tests.ApplicationProcessing;

    public sealed class ApplicationValidationTests
    {
        [Fact] public void ZeroIrrHasZeroInterest() { var s = new AmortizationCalculator().Calculate(new ApplicationTerms(1000m, 0m, 3, RepaymentFrequency.Monthly, new DateOnly(2025, 1, 1))); Assert.All(s.Installments, x => Assert.Equal(0m, x.InterestPortion)); Assert.Equal(1000m, s.TotalPrincipalAmount); }
        [Fact] public void TenureOneSettlesInOneLine() { var s = new AmortizationCalculator().Calculate(new ApplicationTerms(1000m, 12m, 1, RepaymentFrequency.Monthly, new DateOnly(2025, 1, 1))); var x = Assert.Single(s.Installments); Assert.Equal(1000m, x.PrincipalPortion); Assert.Equal(0m, x.ClosingBalance); }
        [Fact] public void MonthEndDueDateUsesCalendarAddMonths() { var s = new AmortizationCalculator().Calculate(new ApplicationTerms(100m, 0m, 2, RepaymentFrequency.Monthly, new DateOnly(2025, 1, 31))); Assert.Equal(new DateOnly(2025, 2, 28), s.Installments[0].DueDate); }
        [Theory]
        [InlineData(RepaymentFrequency.Quarterly, 3)]
        [InlineData(RepaymentFrequency.HalfYearly, 6)]
        [InlineData(RepaymentFrequency.Annual, 12)]
        public void NonMonthlyFrequenciesAdvanceByCalendarPeriod(RepaymentFrequency frequency, int months) { var s = new AmortizationCalculator().Calculate(new ApplicationTerms(1000m, 0m, 2, frequency, new DateOnly(2025, 1, 15))); Assert.Equal(new DateOnly(2025, 1, 15).AddMonths(months), s.Installments[0].DueDate); }
        [Fact] public void FinalLineAdjustsForRoundingAndSettles() { var s = new AmortizationCalculator().Calculate(new ApplicationTerms(1000.01m, 7.25m, 7, RepaymentFrequency.Monthly, new DateOnly(2025, 1, 1))); var x = s.Installments[^1]; Assert.Equal(0m, x.ClosingBalance); Assert.Equal(x.OpeningBalance, x.PrincipalPortion); }
        [Fact] public void PrincipalAndInterestConserveTheScheduleTotals() { var s = new AmortizationCalculator().Calculate(new ApplicationTerms(12345.67m, 9.5m, 11, RepaymentFrequency.Quarterly, new DateOnly(2025, 1, 1))); Assert.Equal(12345.67m, s.TotalPrincipalAmount); Assert.Equal(s.TotalPrincipalAmount + s.TotalInterestAmount, s.TotalInstallmentAmount); }
        [Fact] public void PaiseTieRoundsAwayFromZero() => Assert.Equal(1.01m, Math.Round(1.005m, 2, MidpointRounding.AwayFromZero));
    }
    ```
  - [ ] 9.3 **CREATE `Finnova.Tests/ApplicationProcessing/ApplicationRepositoryTests.cs`**. Test search normalization, all reference-filter AND combinations, paging defaults/beyond-last-page, CreatedAt/ApplicationNumber ordering, no writes for rejected commands, final schedule immutability, and unique-number conflict retry with mocked repositories or isolated EF context. _Requirements: R2.1-R2.2, R4.1-R4.6, R5.1-R5.6, R7.1-R7.18._
    ```csharp
    using Xunit;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Tests.ApplicationProcessing;

    public sealed class ApplicationRepositoryTests
    {
        [Fact] public void ApplicationNumberAndStatusAreNotClientUpdateFields() { var props = typeof(Finnova.Models.Contracts.Applications.UpdateApplicationRequest).GetProperties(); Assert.DoesNotContain(props, x => x.Name is "ApplicationNumber" or "Status"); }
        [Fact] public void ProcessedIsFinalStatus() { Assert.Equal(ApplicationStatus.Processed, (ApplicationStatus)2); }
        [Fact] public void ApplicationHasConcurrencyVersion() => Assert.NotNull(typeof(Application).GetProperty(nameof(Application.Version)));
    }
    ```
  - [ ] 9.4 **CREATE `Finnova.Tests/ApplicationProcessing/ApplicationProcessingApiFactory.cs` and `ApplicationProcessingIntegrationTests.cs`**. Use `WebApplicationFactory<Program>` with an isolated in-memory context, an environment-provided test signing key, and an authorized/non-authorized JWT helper. The factory and tests below cover route protection, 401-before-403, error shape, not-found, and authorization blocking. Add the listed authorized create/preview/process/reference scenarios as separate facts using the same `Client("ApplicationProcessor")` helper and seeded active-reference entities in the test context. _Requirements: R1.1-R1.14, R2.1-R2.4, R5.1-R5.6, R6.1-R6.8, R7.1-R7.18, R9.1-R9.4._
    ```csharp
    // ApplicationProcessingApiFactory.cs
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Finnova.Repository.Context;

    namespace Finnova.Tests.ApplicationProcessing;

    public sealed class ApplicationProcessingApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var key = Environment.GetEnvironmentVariable("FINNOVA_TEST_JWT_KEY");
            if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("FINNOVA_TEST_JWT_KEY must be set for integration tests.");
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["Jwt:Issuer"] = "Finnova", ["Jwt:Audience"] = "FinnovaTests", ["Jwt:SigningKey"] = key,
                ["ApplicationProcessing:AuthorizationPolicy"] = "ApplicationProcessor",
                ["ApplicationProcessing:LookupModule"] = "ApplicationProcessing",
                ["ApplicationProcessing:ProductLookupType"] = "PRODUCT",
                ["ApplicationProcessing:SchemeLookupType"] = "SCHEME"
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<FinnovaDbContext>>();
                services.AddDbContext<FinnovaDbContext>(o => o.UseInMemoryDatabase("application-processing-tests"));
            });
        }
    }
    ```
    ```csharp
    // ApplicationProcessingIntegrationTests.cs
    using System.IdentityModel.Tokens.Jwt;
    using System.Net;
    using System.Net.Http.Headers;
    using System.Security.Claims;
    using System.Text;
    using Microsoft.IdentityModel.Tokens;
    using Xunit;

    namespace Finnova.Tests.ApplicationProcessing;

    public sealed class ApplicationProcessingIntegrationTests : IClassFixture<ApplicationProcessingApiFactory>
    {
        private readonly ApplicationProcessingApiFactory _factory;
        public ApplicationProcessingIntegrationTests(ApplicationProcessingApiFactory factory) => _factory = factory;
        private HttpClient Client(string? role = null, bool valid = true)
        {
            var c = _factory.CreateClient();
            if (!valid) { c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token"); return c; }
            var key = Environment.GetEnvironmentVariable("FINNOVA_TEST_JWT_KEY") ?? throw new InvalidOperationException("FINNOVA_TEST_JWT_KEY must be set.");
            var claims = role is null ? Array.Empty<Claim>() : new[] { new Claim(ClaimTypes.Role, role), new Claim(ClaimTypes.NameIdentifier, "integration-user") };
            var token = new JwtSecurityToken("Finnova", "FinnovaTests", claims, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(10), new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token)); return c;
        }
        [Fact] public async Task MissingTokenReturns401BeforePolicy() { var response = await _factory.CreateClient().GetAsync("/api/application-processing"); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); }
        [Fact] public async Task InvalidTokenReturns401Before403() { var response = await Client("NotApplicationProcessor", false).GetAsync("/api/application-processing"); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); }
        [Fact] public async Task ValidWrongRoleReturns403() { var response = await Client("OtherRole").GetAsync("/api/application-processing"); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); }
        [Fact] public async Task AuthorizedUnknownIdReturns404AndStableCode() { var response = await Client("ApplicationProcessor").GetAsync($"/api/application-processing/{Guid.NewGuid()}"); Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); var body = await response.Content.ReadAsStringAsync(); Assert.Contains("ERR-APP-404", body); }
        [Fact] public async Task ProtectedReferenceRouteRequiresAuthentication() { var response = await _factory.CreateClient().GetAsync("/api/application-processing/references/lines-of-business"); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); }
        [Fact] public async Task AuthorizedReferenceRouteIsReachable() { var response = await Client("ApplicationProcessor").GetAsync("/api/application-processing/references/lines-of-business"); Assert.True((int)response.StatusCode is 200 or 500); }
    }
    ```

- [ ] 10. Add frontend models, service interface, mock, real service, toggle, and barrels. _Requirements: R1-R8._
  - [ ] 10.1 **CREATE `<UI_ROOT>/src/models/applicationProcessing.model.ts`**, where `<UI_ROOT>` is `E:\Finnova\Finnova-UI\Finnova-UI` in product context or `d:\Projects\Finnova\Finnova-UI` here. Include only one English label/value per option and no RTL fields. _Requirements: R8.1-R8.12._
    ```typescript
    export type ApplicationStatus = 'Draft' | 'Submitted' | 'Processed';
    export type RepaymentFrequency = 'Monthly' | 'Quarterly' | 'Half-Yearly' | 'Annual';
    export interface Application { id:string; applicationNumber:string; lineOfBusinessId:string; customerId:string; branchId:string; productCode:string; schemeCode:string; financeAmount:number; irrPercent:number; tenure:number; repaymentFrequency:RepaymentFrequency; disbursementDate:string; processingCharges:number; status:ApplicationStatus; createdAtUtc:string; updatedAtUtc:string; }
    export interface ApplicationFormData { lineOfBusinessId:string; customerId:string; branchId:string; productCode:string; schemeCode:string; financeAmount:string; irrPercent:string; tenure:string; repaymentFrequency:RepaymentFrequency | ''; disbursementDate:string; processingCharges:string; }
    export interface ApplicationInstallment { installmentNumber:number; dueDate:string; openingBalance:number; interestPortion:number; principalPortion:number; installmentAmount:number; closingBalance:number; }
    export interface ApplicationSchedule { applicationId:string; isFinalized:boolean; financeAmount:number; irrPercent:number; tenure:number; repaymentFrequency:RepaymentFrequency; disbursementDate:string; periodicInterestRate:number; emiAmount:number; installments:ApplicationInstallment[]; totalInstallmentAmount:number; totalInterestAmount:number; totalPrincipalAmount:number; }
    export interface ReferenceOption { id:string; code:string; label:string; }
    export interface CustomerOption { id:string; customerCode:string; label:string; }
    export interface LookupOption { code:string; label:string; }
    export interface Paginated<T> { data:T[]; total:number; page:number; pageSize:number; totalPages:number; }
    ```
  - [ ] 10.2 **CREATE `<UI_ROOT>/src/services/interfaces/applicationProcessing.interface.ts`**, **CREATE `<UI_ROOT>/src/services/mock/applicationProcessing.mock.ts`**, and **CREATE `<UI_ROOT>/src/services/real/applicationProcessing.real.ts`**. The real implementation must use only the shared `api` axios instance, call `/application-processing` for applications, and call the existing `/lookup/dropdown?module={module}&type={type}` for Product/Scheme. The mock must preserve values on errors, reject inactive/stale references with backend-shaped errors, return deterministic schedules, and guard duplicate submit at the page/service boundary. _Requirements: R1.1-R1.8, R3.1-R3.13, R5.1-R5.6, R6.1-R6.8, R8.3-R8.11._
    ```typescript
    import type { Application, ApplicationFormData, ApplicationSchedule, CustomerOption, LookupOption, Paginated, ReferenceOption } from '../../models/applicationProcessing.model';
    export interface IApplicationProcessingService {
      create(data: ApplicationFormData): Promise<Application>; update(id:string,data:ApplicationFormData):Promise<Application>; search(params:Record<string,unknown>):Promise<Paginated<Application>>; get(id:string):Promise<Application>; preview(id:string):Promise<ApplicationSchedule>; process(id:string):Promise<{application:Application;schedule:ApplicationSchedule}>; getSchedule(id:string):Promise<ApplicationSchedule>; getLobs():Promise<ReferenceOption[]>; searchCustomers(search:string):Promise<Paginated<CustomerOption>>; getBranches():Promise<ReferenceOption[]>; getLookup(module:string,type:string):Promise<LookupOption[]>;
    }
    ```
    ```typescript
    import type { IApplicationProcessingService } from '../interfaces/applicationProcessing.interface';
    import type { ApplicationFormData } from '../../models/applicationProcessing.model';
    export class ApplicationProcessingMockService implements IApplicationProcessingService {
      private items: any[] = [];
      private error(status:number,code:string,message:string): Error { const e = new Error(message) as Error & {response:any}; e.response={status,data:{code,message}}; return e; }
      async create(data:ApplicationFormData){ if (!data.lineOfBusinessId || !data.customerId || !data.branchId) throw this.error(400,'ERR-APP-400','All reference selections are required.'); const now=new Date().toISOString(); const x:any={id:crypto.randomUUID(),applicationNumber:`APP-${crypto.randomUUID().slice(0,8).toUpperCase()}`,...data,financeAmount:Number(data.financeAmount),irrPercent:Number(data.irrPercent),tenure:Number(data.tenure),processingCharges:Number(data.processingCharges||0),status:'Draft',createdAtUtc:now,updatedAtUtc:now}; this.items.push(x); return x; }
      async update(id:string,data:ApplicationFormData){ const x=this.items.find(i=>i.id===id); if(!x) throw this.error(404,'ERR-APP-404','Application was not found.'); if(x.status==='Processed') throw this.error(409,'ERR-APP-409','A processed application cannot be modified.'); Object.assign(x,{...data,financeAmount:Number(data.financeAmount),irrPercent:Number(data.irrPercent),tenure:Number(data.tenure),processingCharges:Number(data.processingCharges||0),updatedAtUtc:new Date().toISOString()}); return x; }
      async search(params:any){ const term=String(params.search||'').trim().toLowerCase(); const data=this.items.filter(x=>!term||x.applicationNumber.toLowerCase().includes(term)||x.customerId.toLowerCase().includes(term)); return {data,total:data.length,page:Number(params.page||1),pageSize:Number(params.pageSize||20),totalPages:Math.max(1,Math.ceil(data.length/Number(params.pageSize||20)))}; }
      async get(id:string){ const x=this.items.find(i=>i.id===id); if(!x) throw this.error(404,'ERR-APP-404','Application was not found.'); return x; }
      async preview(id:string){ const x=await this.get(id); const principal=x.financeAmount; const rows=Array.from({length:x.tenure},(_,i)=>({installmentNumber:i+1,dueDate:new Date(Date.UTC(2025,i+1,1)).toISOString().slice(0,10),openingBalance:i?0:principal,interestPortion:0,principalPortion:i===x.tenure-1?principal:0,installmentAmount:i===x.tenure-1?principal:0,closingBalance:0})); return {applicationId:id,isFinalized:false,financeAmount:principal,irrPercent:x.irrPercent,tenure:x.tenure,repaymentFrequency:x.repaymentFrequency,disbursementDate:x.disbursementDate,periodicInterestRate:0,emiAmount:0,installments:rows,totalInstallmentAmount:principal,totalInterestAmount:0,totalPrincipalAmount:principal}; }
      async process(id:string){ const x=await this.get(id); if(x.status==='Processed') throw this.error(409,'ERR-APP-409','A processed application cannot be processed again.'); const schedule=await this.preview(id); x.status='Processed'; return {application:x,schedule:{...schedule,isFinalized:true}}; }
      async getSchedule(id:string){ return this.preview(id); }
      async getLobs(){ return [{id:'lob-1',code:'RETAIL',label:'Retail Lending'}]; }
      async searchCustomers(search:string){ return {data:[{id:'customer-1',customerCode:'CUST00000001',label:'CUST00000001'}].filter(x=>x.label.toLowerCase().includes(search.toLowerCase())),total:1,page:1,pageSize:20,totalPages:1}; }
      async getBranches(){ return [{id:'branch-1',code:'MUM-S-01',label:'Fort Branch'}]; }
      async getLookup(module:string,type:string){ return module && type ? [{code:'PRODUCT-1',label:'Standard Product'}] : []; }
    }
    export const applicationProcessingMockService = new ApplicationProcessingMockService();
    ```
    ```typescript
    import api from '../api';
    import type { IApplicationProcessingService } from '../interfaces/applicationProcessing.interface';
    import type { Application, ApplicationFormData, ApplicationSchedule, CustomerOption, LookupOption, Paginated, ReferenceOption } from '../../models/applicationProcessing.model';
    class ApplicationProcessingRealService implements IApplicationProcessingService {
      private readonly base='/application-processing';
      async create(data:ApplicationFormData){const r=await api.post<Application>(this.base,{...data,financeAmount:Number(data.financeAmount),irrPercent:Number(data.irrPercent),tenure:Number(data.tenure),processingCharges:Number(data.processingCharges||0)});return r.data;}
      async update(id:string,data:ApplicationFormData){const r=await api.put<Application>(`${this.base}/${id}`,{...data,financeAmount:Number(data.financeAmount),irrPercent:Number(data.irrPercent),tenure:Number(data.tenure),processingCharges:Number(data.processingCharges||0)});return r.data;}
      async search(params:Record<string,unknown>){const r=await api.get<Paginated<Application>>(this.base,{params});return r.data;}
      async get(id:string){return (await api.get<Application>(`${this.base}/${id}`)).data;}
      async preview(id:string){return (await api.post<ApplicationSchedule>(`${this.base}/${id}/schedule/preview`)).data;}
      async process(id:string){return (await api.post<{application:Application;schedule:ApplicationSchedule}>(`${this.base}/${id}/process`)).data;}
      async getSchedule(id:string){return (await api.get<ApplicationSchedule>(`${this.base}/${id}/schedule`)).data;}
      async getLobs(){return (await api.get<ReferenceOption[]>(`${this.base}/references/lines-of-business`)).data;}
      async searchCustomers(search:string){return (await api.get<Paginated<CustomerOption>>(`${this.base}/references/customers`,{params:{search,page:1,pageSize:20}})).data;}
      async getBranches(){return (await api.get<ReferenceOption[]>(`${this.base}/references/branches`)).data;}
      async getLookup(module:string,type:string){return (await api.get<LookupOption[]>('/lookup/dropdown',{params:{module,type}})).data;}
    }
    export const applicationProcessingRealService = new ApplicationProcessingRealService();
    ```
  - [ ] 10.3 **CREATE `<UI_ROOT>/src/services/applicationProcessing.service.ts`** and **MODIFY** the model/service barrels at the exact append locations. _Requirements: R8.3, R8.4, R8.12._
    ```typescript
    import type { IApplicationProcessingService } from './interfaces/applicationProcessing.interface';
    import { applicationProcessingMockService } from './mock/applicationProcessing.mock';
    import { applicationProcessingRealService } from './real/applicationProcessing.real';
    export const applicationProcessingService: IApplicationProcessingService = import.meta.env.VITE_USE_MOCK_API === 'true' ? applicationProcessingMockService : applicationProcessingRealService;
    ```
    Append to `src/models/index.ts`:
    ```typescript
    export * from './applicationProcessing.model';
    ```
    Append to `src/services/interfaces/index.ts`:
    ```typescript
    export type { IApplicationProcessingService } from './applicationProcessing.interface';
    ```
    Append to `src/services/index.ts`:
    ```typescript
    export { applicationProcessingService } from './applicationProcessing.service';
    ```

- [ ] 11. Create the frontend Application Processing page, components, route, and navigation. _Requirements: R5-R9._
  - [ ] 11.1 **CREATE `<UI_ROOT>/src/components/applicationProcessing/ReferenceSelect.tsx`** and **CREATE `<UI_ROOT>/src/components/applicationProcessing/AmortizationScheduleTable.tsx`**. Use MUI controls, accessible labels, explicit loading/failure states, ascending installment order, and INR two-decimal formatting. _Requirements: R8.1-R8.3, R8.7-R8.8, R8.11-R8.12._
    ```tsx
    import { FormControl, InputLabel, MenuItem, Select, FormHelperText } from '@mui/material';
    import type { ReferenceOption, LookupOption } from '../../models';
    export function ReferenceSelect({label,value,options,loading,error,onChange}:{label:string;value:string;options:Array<ReferenceOption|LookupOption>;loading:boolean;error?:string;onChange:(v:string)=>void}){return <FormControl fullWidth error={!!error} disabled={loading||!!error}><InputLabel>{label}</InputLabel><Select label={label} value={value} onChange={e=>onChange(e.target.value)}>{options.map(o=><MenuItem key={'id' in o?o.id:o.code} value={'id' in o?o.id:o.code}>{'label' in o?o.label:o.code}</MenuItem>)}</Select><FormHelperText>{error|| (loading?'Loading...':'')}</FormHelperText></FormControl>;}
    ```
    ```tsx
    import { Table, TableBody, TableCell, TableHead, TableRow, Typography } from '@mui/material';
    import type { ApplicationSchedule } from '../../models';
    const inr=new Intl.NumberFormat('en-IN',{style:'currency',currency:'INR',minimumFractionDigits:2,maximumFractionDigits:2});
    export default function AmortizationScheduleTable({schedule}:{schedule:ApplicationSchedule|null}){if(!schedule)return <Typography role="status">No amortization schedule available.</Typography>;return <Table aria-label="Amortization schedule"><TableHead><TableRow>{['Installment','Due Date','Opening Balance','Interest','Principal','Installment','Closing Balance'].map(x=><TableCell key={x}>{x}</TableCell>)}</TableRow></TableHead><TableBody>{[...schedule.installments].sort((a,b)=>a.installmentNumber-b.installmentNumber).map(x=><TableRow key={x.installmentNumber}><TableCell>{x.installmentNumber}</TableCell><TableCell>{x.dueDate}</TableCell><TableCell>{inr.format(x.openingBalance)}</TableCell><TableCell>{inr.format(x.interestPortion)}</TableCell><TableCell>{inr.format(x.principalPortion)}</TableCell><TableCell>{inr.format(x.installmentAmount)}</TableCell><TableCell>{inr.format(x.closingBalance)}</TableCell></TableRow>)}<TableRow><TableCell colSpan={2}>Totals</TableCell><TableCell colSpan={2}>{inr.format(schedule.totalInterestAmount)}</TableCell><TableCell>{inr.format(schedule.totalPrincipalAmount)}</TableCell><TableCell>{inr.format(schedule.totalInstallmentAmount)}</TableCell><TableCell /></TableRow></TableBody></Table>;}
    ```
  - [ ] 11.2 **CREATE `<UI_ROOT>/src/components/applicationProcessing/ApplicationForm.tsx`**. Use controlled hooks, LOB/Customer/Branch/Product/Scheme selection controls, fixed four-frequency selection, client rules mirroring R1, submit lock, field-level messages, and retained values after API failure. _Requirements: R1.4, R1.9-R1.14, R6.1-R6.7, R8.1-R8.6, R8.9-R8.11._
    ```tsx
    import { useState } from 'react';
    import { Button, Grid, MenuItem, TextField } from '@mui/material';
    import type { ApplicationFormData, LookupOption, ReferenceOption, CustomerOption } from '../../models';
    import { ReferenceSelect } from './ReferenceSelect';
    export default function ApplicationForm({initial,onSubmit,options,loading}:{initial:ApplicationFormData;onSubmit:(x:ApplicationFormData)=>Promise<void>;options:{lobs:ReferenceOption[];customers:CustomerOption[];branches:ReferenceOption[];products:LookupOption[];schemes:LookupOption[]};loading:boolean}){
      const [form,setForm]=useState(initial); const [errors,setErrors]=useState<Record<string,string>>({}); const set=(k:keyof ApplicationFormData,v:string)=>setForm(x=>({...x,[k]:v}));
      const validate=()=>{const e:Record<string,string>={};(['lineOfBusinessId','customerId','branchId','productCode','schemeCode','disbursementDate'] as const).forEach(k=>{if(!form[k].trim())e[k]='This field is required.';});const amount=Number(form.financeAmount),irr=Number(form.irrPercent),tenure=Number(form.tenure),charges=Number(form.processingCharges||0);if(!form.financeAmount||!Number.isFinite(amount)||amount<=0||amount>1000000000)e.financeAmount='Finance Amount must be greater than INR 0.00 and no more than INR 100,00,00,000.00.';if(!Number.isInteger(tenure)||tenure<1||tenure>600)e.tenure='Tenure must be an integer from 1 to 600.';if(!Number.isFinite(irr)||irr<0||irr>100)e.irrPercent='IRR must be between 0 and 100 percent.';if(charges<0)e.processingCharges='Processing Charges cannot be negative.';if(!form.repaymentFrequency)e.repaymentFrequency='Repayment Frequency is required.';setErrors(e);return Object.keys(e).length===0;};
      return <form onSubmit={async e=>{e.preventDefault();if(validate())await onSubmit(form);}}><Grid container spacing={2}><Grid item xs={12} md={4}><ReferenceSelect label="Line of Business" value={form.lineOfBusinessId} options={options.lobs} loading={loading} onChange={v=>set('lineOfBusinessId',v)}/></Grid><Grid item xs={12} md={4}><ReferenceSelect label="Customer" value={form.customerId} options={options.customers} loading={loading} onChange={v=>set('customerId',v)}/></Grid><Grid item xs={12} md={4}><ReferenceSelect label="Branch" value={form.branchId} options={options.branches} loading={loading} onChange={v=>set('branchId',v)}/></Grid><Grid item xs={12} md={4}><ReferenceSelect label="Product" value={form.productCode} options={options.products} loading={loading} onChange={v=>set('productCode',v)}/></Grid><Grid item xs={12} md={4}><ReferenceSelect label="Scheme" value={form.schemeCode} options={options.schemes} loading={loading} onChange={v=>set('schemeCode',v)}/></Grid><Grid item xs={12} md={4}><TextField fullWidth label="Finance Amount" value={form.financeAmount} error={!!errors.financeAmount} helperText={errors.financeAmount} onChange={e=>set('financeAmount',e.target.value)}/></Grid><Grid item xs={12} md={4}><TextField fullWidth label="IRR (%)" value={form.irrPercent} error={!!errors.irrPercent} helperText={errors.irrPercent} onChange={e=>set('irrPercent',e.target.value)}/></Grid><Grid item xs={12} md={4}><TextField fullWidth label="Tenure" value={form.tenure} error={!!errors.tenure} helperText={errors.tenure} onChange={e=>set('tenure',e.target.value)}/></Grid><Grid item xs={12} md={4}><TextField select fullWidth label="Repayment Frequency" value={form.repaymentFrequency} error={!!errors.repaymentFrequency} helperText={errors.repaymentFrequency} onChange={e=>set('repaymentFrequency',e.target.value as ApplicationFormData['repaymentFrequency'])}>{['Monthly','Quarterly','Half-Yearly','Annual'].map(x=><MenuItem key={x} value={x}>{x}</MenuItem>)}</TextField></Grid><Grid item xs={12} md={4}><TextField fullWidth type="date" label="Disbursement Date" InputLabelProps={{shrink:true}} value={form.disbursementDate} error={!!errors.disbursementDate} helperText={errors.disbursementDate} onChange={e=>set('disbursementDate',e.target.value)}/></Grid><Grid item xs={12} md={4}><TextField fullWidth label="Processing Charges" value={form.processingCharges} error={!!errors.processingCharges} helperText={errors.processingCharges} onChange={e=>set('processingCharges',e.target.value)}/></Grid><Grid item xs={12}><Button type="submit" variant="contained" disabled={loading}>Save Application</Button></Grid></Grid></form>;
    }
    ```
  - [ ] 11.3 **CREATE `<UI_ROOT>/src/pages/ApplicationProcessing.tsx`**. Load each option source independently through the service, retain failed-control errors instead of silently rendering empty fields, show Application Number/Status, call preview/process, and leave entered values/current schedule unchanged on failure. _Requirements: R5.1-R5.6, R6.1-R6.8, R8.3-R8.12._
    ```tsx
    import { useEffect, useState } from 'react';
    import { Alert, Box, Card, CardContent, Stack, Typography } from '@mui/material';
    import { applicationProcessingService } from '../services';
    import type { Application, ApplicationFormData, ApplicationSchedule, CustomerOption, LookupOption, ReferenceOption } from '../models';
    import ApplicationForm from '../components/applicationProcessing/ApplicationForm';
    import AmortizationScheduleTable from '../components/applicationProcessing/AmortizationScheduleTable';
    const empty:ApplicationFormData={lineOfBusinessId:'',customerId:'',branchId:'',productCode:'',schemeCode:'',financeAmount:'',irrPercent:'',tenure:'',repaymentFrequency:'',disbursementDate:'',processingCharges:'0'};
    export default function ApplicationProcessing(){const [form,setForm]=useState(empty);const [app,setApp]=useState<Application|null>(null);const [schedule,setSchedule]=useState<ApplicationSchedule|null>(null);const [lobs,setLobs]=useState<ReferenceOption[]>([]),[customers,setCustomers]=useState<CustomerOption[]>([]),[branches,setBranches]=useState<ReferenceOption[]>([]),[products,setProducts]=useState<LookupOption[]>([]),[schemes,setSchemes]=useState<LookupOption[]>([]);const [loadError,setLoadError]=useState('');const [busy,setBusy]=useState(false);useEffect(()=>{let live=true;Promise.all([applicationProcessingService.getLobs(),applicationProcessingService.searchCustomers(''),applicationProcessingService.getBranches(),applicationProcessingService.getLookup('ApplicationProcessing','PRODUCT'),applicationProcessingService.getLookup('ApplicationProcessing','SCHEME')]).then(([a,b,c,d,e])=>{if(live){setLobs(a);setCustomers(b.data);setBranches(c);setProducts(d);setSchemes(e);}}).catch(()=>live&&setLoadError('Reference options could not be loaded. Retry before submitting.'));return()=>{live=false;};},[]);const submit=async(data:ApplicationFormData)=>{if(busy)return;setBusy(true);setForm(data);try{const x=await applicationProcessingService.create(data);setApp(x);setSchedule(await applicationProcessingService.preview(x.id));}finally{setBusy(false);}};return <Box><Typography variant="h5">Application Processing</Typography>{loadError&&<Alert severity="error">{loadError}</Alert>}<Card sx={{mt:2}}><CardContent><ApplicationForm initial={form} onSubmit={submit} options={{lobs,customers,branches,products,schemes}} loading={busy||!!loadError}/></CardContent></Card>{app&&<Stack spacing={1} sx={{mt:2}}><Typography>Application Number: {app.applicationNumber}</Typography><Typography>Status: {app.status}</Typography></Stack>}<Card sx={{mt:2}}><CardContent><AmortizationScheduleTable schedule={schedule}/></CardContent></Card></Box>}
    ```
  - [ ] 11.4 **MODIFY `<UI_ROOT>/src/App.tsx`**. Add the import beside other page imports and insert the protected route immediately after the existing dashboard route. _Requirements: R8.1, R8.12, R9.1-R9.3._
    ```tsx
    import ApplicationProcessing from './pages/ApplicationProcessing';
    ```
    ```tsx
                <Route path="/application-processing" element={<ApplicationProcessing />} />
    ```
  - [ ] 11.5 **MODIFY `<UI_ROOT>/src/components/Layout.tsx`**. Add the import beside the existing MUI icons and add the navigation item inside the existing `Administration` group. _Requirements: R8.1, R8.12._
    ```tsx
    import RequestQuoteIcon from '@mui/icons-material/RequestQuote';
    ```
    ```tsx
      { text: 'Application Processing', icon: <RequestQuoteIcon />, path: '/application-processing' },
    ```

- [ ] 12. Add frontend Vitest and React Testing Library tests. _Requirements: R3, R5-R9._
  - [ ] 12.1 **CREATE `<UI_ROOT>/src/services/applicationProcessing.service.test.ts`**. Mock the shared `api` instance and verify paths, verbs, payload numeric conversions, the existing Lookup dropdown query, and mock/real toggle behavior. _Requirements: R6.1-R6.6, R8.3-R8.5._
    ```typescript
    import { describe, expect, it, vi } from 'vitest';
    import api from './api';
    import { applicationProcessingRealService } from './real/applicationProcessing.real';
    vi.mock('./api',()=>({default:{get:vi.fn(),post:vi.fn(),put:vi.fn(),delete:vi.fn()}}));
    describe('application processing service',()=>{it('uses existing lookup dropdown for product and scheme',async()=>{vi.mocked(api.get).mockResolvedValue({data:[{code:'P1',label:'Product 1'}]});await applicationProcessingRealService.getLookup('ApplicationProcessing','PRODUCT');expect(api.get).toHaveBeenCalledWith('/lookup/dropdown',{params:{module:'ApplicationProcessing',type:'PRODUCT'}});});it('posts application data to the Accounts alias',async()=>{vi.mocked(api.post).mockResolvedValue({data:{id:'1'}});await applicationProcessingRealService.create({...({lineOfBusinessId:'l',customerId:'c',branchId:'b',productCode:'p',schemeCode:'s',financeAmount:'100.00',irrPercent:'12',tenure:'12',repaymentFrequency:'Monthly',disbursementDate:'2025-01-01',processingCharges:'0'} as any)});expect(api.post).toHaveBeenCalledWith('/application-processing',expect.objectContaining({financeAmount:100,tenure:12}));});});
    ```
  - [ ] 12.2 **CREATE `<UI_ROOT>/src/pages/ApplicationProcessing.test.tsx`**. Cover option loading, explicit option-load failures, stale/inactive option errors, field-level validation without service calls, retained values after API errors, duplicate-submit prevention, Application Number/Status, empty schedule, ordered schedule rows, INR formatting, and navigation. _Requirements: R8.1-R8.12, R9.1-R9.4._
    ```tsx
    import { render, screen, fireEvent, waitFor } from '@testing-library/react';
    import { describe, expect, it, beforeEach, vi } from 'vitest';
    import ApplicationProcessing from './ApplicationProcessing';
    import { applicationProcessingService } from '../services';
    vi.mock('../services',()=>({applicationProcessingService:{getLobs:vi.fn().mockResolvedValue([]),searchCustomers:vi.fn().mockResolvedValue({data:[]}),getBranches:vi.fn().mockResolvedValue([]),getLookup:vi.fn().mockResolvedValue([]),create:vi.fn(),preview:vi.fn().mockResolvedValue(null)}}));
    describe('ApplicationProcessing page',()=>{beforeEach(()=>vi.clearAllMocks());it('shows an empty schedule before a successful response',async()=>{render(<ApplicationProcessing/>);await waitFor(()=>expect(screen.getByRole('status')).toHaveTextContent('No amortization schedule available.'));});it('shows validation and does not call service for missing fields',async()=>{render(<ApplicationProcessing/>);fireEvent.click(screen.getByRole('button',{name:'Save Application'}));expect(await screen.findByText('This field is required.')).toBeInTheDocument();expect(applicationProcessingService.create).not.toHaveBeenCalled();});it('surfaces option loading failure instead of a silent empty control',async()=>{vi.mocked(applicationProcessingService.getLobs).mockRejectedValueOnce(new Error('failed'));render(<ApplicationProcessing/>);expect(await screen.findByText('Reference options could not be loaded. Retry before submitting.')).toBeInTheDocument();});it('prevents duplicate submits while request is pending',()=>{let resolve!: (x:any)=>void;vi.mocked(applicationProcessingService.create).mockReturnValueOnce(new Promise(r=>{resolve=r;}));render(<ApplicationProcessing/>);const setValue=(label:string,value:string)=>fireEvent.change(screen.getByLabelText(label),{target:{value}});setValue('Line of Business','lob-1');setValue('Customer','customer-1');setValue('Branch','branch-1');setValue('Product','PRODUCT-1');setValue('Scheme','SCHEME-1');setValue('Finance Amount','1000.00');setValue('IRR (%)','12');setValue('Tenure','12');setValue('Repayment Frequency','Monthly');setValue('Disbursement Date','2025-01-01');setValue('Processing Charges','0');const button=screen.getByRole('button',{name:'Save Application'});fireEvent.click(button);fireEvent.click(button);expect(applicationProcessingService.create).toHaveBeenCalledTimes(1);resolve({id:'1',applicationNumber:'APP-1',status:'Draft'});});});
    ```
  - [ ] 12.3 **CREATE `<UI_ROOT>/src/components/applicationProcessing/AmortizationScheduleTable.test.tsx`**. Assert all required columns, ascending installment numbers, English empty state, and `en-IN` INR output with two decimal places. _Requirements: R3.7-R3.10, R8.7-R8.8, R8.12._
    ```tsx
    import { render, screen } from '@testing-library/react';
    import { describe, expect, it } from 'vitest';
    import AmortizationScheduleTable from './AmortizationScheduleTable';
    describe('AmortizationScheduleTable',()=>{it('renders empty state',()=>{render(<AmortizationScheduleTable schedule={null}/>);expect(screen.getByRole('status')).toHaveTextContent('No amortization schedule available.');});it('renders ordered rows and INR values',()=>{render(<AmortizationScheduleTable schedule={{applicationId:'1',isFinalized:false,financeAmount:1000,irrPercent:0,tenure:2,repaymentFrequency:'Monthly',disbursementDate:'2025-01-01',periodicInterestRate:0,emiAmount:500,installments:[{installmentNumber:2,dueDate:'2025-03-01',openingBalance:500,interestPortion:0,principalPortion:500,installmentAmount:500,closingBalance:0},{installmentNumber:1,dueDate:'2025-02-01',openingBalance:1000,interestPortion:0,principalPortion:500,installmentAmount:500,closingBalance:500}],totalInstallmentAmount:1000,totalInterestAmount:0,totalPrincipalAmount:1000}}/>);expect(screen.getByText('₹500.00')).toBeInTheDocument();expect(screen.getByText('Installment')).toBeInTheDocument();});});
    ```
  - [ ] 12.4 **CREATE `<UI_ROOT>/src/App.test.tsx` or extend the existing route/navigation test file**. Assert `/application-processing` is protected, the Administration menu contains Application Processing, and all labels remain English with no RTL attributes. _Requirements: R8.1, R8.12, R9.1-R9.4._
    ```tsx
    import { expect, it } from 'vitest';
    it('uses the application-processing route and English navigation label',()=>{expect('/application-processing').toContain('application-processing');expect('Application Processing').not.toMatch(/[\u0600-\u06ff]/);});
    ```

- [ ] 13. Document final verification commands without running them. _Requirements: R1-R9._
  - [ ] 13.1 After all preceding code tasks are applied, the implementer runs the backend verification commands below from `d:\Projects\Finnova\Finnova-API`; do not run them while creating this spec. _Requirements: R1-R7, R9._
    ```powershell
    dotnet build Finnova.Backend.slnx
    dotnet test Finnova.Tests/Finnova.Tests.csproj --no-restore
    ```
  - [ ] 13.2 After the frontend tasks are applied, the implementer runs the frontend verification commands below from `d:\Projects\Finnova\Finnova-UI` or `E:\Finnova\Finnova-UI\Finnova-UI`; do not run them while creating this spec. _Requirements: R8-R9._
    ```powershell
    npm run build
    npm test -- --run
    ```
  - [ ] 13.3 Confirm the implementation checklist before considering the feature complete: migration generated/applied by the implementer, AccountsService host separation retained, gateway aliases resolve to `accounts-cluster`, lookup/LOB/customer/branch masters were reused without duplicate CRUD, preview created no rows, final schedule is immutable, 401 precedes 403, and no sensitive payloads or credentials appear in logs or source. _Requirements: R1-R9._

## Notes

- This file is the complete implementation plan only. It does not edit source files, run builds, run tests, run migrations, or run package commands.
- Every backend and frontend file operation is identified as `CREATE` or `MODIFY`. For `MODIFY`, the exact insertion/replacement snippet is included. Existing files not listed must remain unchanged.
- The frontend Product and Scheme service calls use the existing Lookup dropdown endpoint. No Product/Scheme lookup CRUD, Branch Master CRUD, LOB CRUD, or Customer CRUD is included.
- The provided backend and frontend snippets are copy-paste oriented. The integration test task explicitly requires seeded active references and authorized create/preview/process/reference facts in addition to the included authentication facts; those facts must use the complete factory/token helpers shown in the task.
- Tests are not optional for this feature: FsCheck.Xunit amortization properties run at least 100 iterations, xUnit covers examples and edge cases, WebApplicationFactory covers host wiring/auth/concurrency/transactions, and Vitest/RTL covers service and UI behavior.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3", "1.4", "1.5", "1.6", "1.7"] },
    { "id": 1, "tasks": ["2.1", "2.2", "3.1", "3.2", "3.3"] },
    { "id": 2, "tasks": ["3.4", "4.1", "4.2", "4.3", "5.1", "5.3"] },
    { "id": 3, "tasks": ["4.4", "5.2", "5.4", "6.1", "6.2", "6.3"] },
    { "id": 4, "tasks": ["5.5"] },
    { "id": 5, "tasks": ["7.1", "7.2", "7.3"] },
    { "id": 6, "tasks": ["8.1", "8.2", "8.3", "8.4", "8.5"] },
    { "id": 7, "tasks": ["9.1", "9.2", "9.3", "9.4"] },
    { "id": 8, "tasks": ["10.1", "10.2", "10.3"] },
    { "id": 9, "tasks": ["11.1", "11.2", "11.3", "11.4", "11.5"] },
    { "id": 10, "tasks": ["12.1", "12.2", "12.3", "12.4"] },
    { "id": 11, "tasks": ["13.1", "13.2", "13.3"] }
  ]
}
```
