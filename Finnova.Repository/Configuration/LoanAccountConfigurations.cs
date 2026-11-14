using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finnova.Repository.Configuration;

public sealed class AccountCreationRequestConfiguration : IEntityTypeConfiguration<AccountCreationRequest>
{
    public void Configure(EntityTypeBuilder<AccountCreationRequest> b)
    {
        b.ToTable("account_creation_requests", t =>
        {
            t.HasCheckConstraint("CK_acr_attempts", "[AttemptCount] >= 0 AND [AttemptCount] <= 10");
            t.HasCheckConstraint("CK_acr_retry_time", "[Status] <> 'Retry' OR [NextAttemptAtUtc] IS NOT NULL");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.ApplicationNumber).IsRequired().HasMaxLength(50);
        b.Property(x => x.CorrelationId).IsRequired().HasMaxLength(64);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.ClaimedBy).HasMaxLength(100);
        b.Property(x => x.FailureReason).HasMaxLength(100);
        b.HasIndex(x => x.ApplicationId).IsUnique();
        b.HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
        b.HasIndex(x => new { x.Status, x.LeaseExpiresAtUtc });
    }
}

public sealed class AccountKycJobConfiguration : IEntityTypeConfiguration<AccountKycJob>
{
    public void Configure(EntityTypeBuilder<AccountKycJob> b)
    {
        b.ToTable("account_kyc_jobs", t => t.HasCheckConstraint("CK_akj_attempts", "[AttemptCount] >= 0 AND [AttemptCount] <= 10"));
        b.HasKey(x => x.AccountId);
        b.Property(x => x.CorrelationId).IsRequired().HasMaxLength(64);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.ClaimedBy).HasMaxLength(100);
        b.Property(x => x.FailureReason).HasMaxLength(100);
        b.Property(x => x.RecheckRequestedBy).HasMaxLength(200);
        b.Property(x => x.RecheckReason).HasMaxLength(500);
        b.HasOne<LoanAccount>().WithOne().HasForeignKey<AccountKycJob>(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
        b.HasIndex(x => new { x.Status, x.LeaseExpiresAtUtc });
    }
}

public sealed class AccountKycHistoryEntryConfiguration : IEntityTypeConfiguration<AccountKycHistoryEntry>
{
    public void Configure(EntityTypeBuilder<AccountKycHistoryEntry> b)
    {
        b.ToTable("account_kyc_history");
        b.HasKey(x => x.Id);
        b.Property(x => x.Source).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.KycStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
        b.Property(x => x.PreviousStatus).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.PanValidation).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.PanNameMatch).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.AadhaarValidation).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.AadhaarAddressMatch).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.ProviderReference).HasMaxLength(100);
        b.Property(x => x.Reason).HasMaxLength(500);
        b.Property(x => x.Comment).HasMaxLength(500);
        b.Property(x => x.Actor).IsRequired().HasMaxLength(200);
        b.Property(x => x.CorrelationId).IsRequired().HasMaxLength(64);
        b.HasOne<LoanAccount>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.AccountId, x.OccurredAtUtc });
    }
}

public sealed class AccountKycResolutionRequestConfiguration : IEntityTypeConfiguration<AccountKycResolutionRequest>
{
    public void Configure(EntityTypeBuilder<AccountKycResolutionRequest> b)
    {
        b.ToTable("account_kyc_resolution_requests", t =>
        {
            t.HasCheckConstraint("CK_akrr_reason_len", "LEN(LTRIM(RTRIM([Reason]))) BETWEEN 10 AND 500");
            t.HasCheckConstraint("CK_akrr_four_eyes", "[CheckerUserId] IS NULL OR [CheckerUserId] <> [MakerUserId]");
            t.HasCheckConstraint("CK_akrr_decision", "[Status] = 'Pending' OR ([CheckerUserId] IS NOT NULL AND [DecisionComment] IS NOT NULL AND [DecidedAtUtc] IS NOT NULL)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.PreviousKycStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
        b.Property(x => x.MakerName).IsRequired().HasMaxLength(200);
        b.Property(x => x.CheckerName).HasMaxLength(200);
        b.Property(x => x.Reason).IsRequired().HasMaxLength(500);
        b.Property(x => x.DecisionComment).HasMaxLength(500);
        b.Property(x => x.CorrelationId).IsRequired().HasMaxLength(64);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne<LoanAccount>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        // At most one Pending request per Account (R9.9, R9.13).
        b.HasIndex(x => x.AccountId).IsUnique().HasFilter("[Status] = 'Pending'").HasDatabaseName("UX_account_kyc_resolution_pending");
        b.HasIndex(x => new { x.AccountId, x.SubmittedAtUtc });
        b.HasIndex(x => new { x.Status, x.SubmittedAtUtc });
    }
}

public sealed class AccountAuditEntryConfiguration : IEntityTypeConfiguration<AccountAuditEntry>
{
    public void Configure(EntityTypeBuilder<AccountAuditEntry> b)
    {
        b.ToTable("account_audit_entries");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).IsRequired().HasMaxLength(40);
        b.Property(x => x.Source).IsRequired().HasMaxLength(10);
        b.Property(x => x.Actor).IsRequired().HasMaxLength(200);
        b.Property(x => x.CorrelationId).IsRequired().HasMaxLength(64);
        b.Property(x => x.ChangeSummary).IsRequired().HasMaxLength(1000);
        b.HasIndex(x => new { x.AccountId, x.OccurredAtUtc });
    }
}

public sealed class AppRoleConfiguration : IEntityTypeConfiguration<AppRole>
{
    private static readonly DateTime SeedDate = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<AppRole> b)
    {
        b.ToTable("app_roles");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(50);
        b.Property(x => x.Description).IsRequired().HasMaxLength(200);
        b.HasIndex(x => x.Name).IsUnique();
        // HasData with fixed keys is idempotent across migrations and re-runs (R18.3).
        b.HasData(AppRoleCatalog.Roles.Select(r => new AppRole
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.Description,
            IsSystem = true,
            CreatedAt = SeedDate
        }));
    }
}

public sealed class UserAccountRoleConfiguration : IEntityTypeConfiguration<UserAccountRole>
{
    public void Configure(EntityTypeBuilder<UserAccountRole> b)
    {
        b.ToTable("user_account_roles");
        b.HasKey(x => new { x.UserAccountId, x.AppRoleId });
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AppRole>().WithMany().HasForeignKey(x => x.AppRoleId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ServiceCredentialConfiguration : IEntityTypeConfiguration<ServiceCredential>
{
    public void Configure(EntityTypeBuilder<ServiceCredential> b)
    {
        b.ToTable("service_credentials");
        b.HasKey(x => x.ClientId);
        b.Property(x => x.SecretHash).IsRequired().HasMaxLength(400);
    }
}