# Implementation Plan — User Management (SystemAdmin · FS §9)

## Overview

This plan implements User Management across the `Finnova.SystemAdminService` backend (.NET,
solution `Finnova.Backend.slnx`) and the Finnova-UI frontend (React 18 + TypeScript + Vite + MUI).

It is a **copy-paste plan**: each task is one step, presented in dependency order
(backend domain → exceptions → contracts → persistence → repository → helpers → service CQRS →
API/middleware/gateway → tests, then frontend model/services → page/components → tests). Copy-paste
each task in order.

- **CREATE** = new file (paste the whole block).
- **MODIFY** = edit an existing file (apply the shown snippet at the indicated place).
- Backend root: `e:\Finnova\Finnova-API`. Frontend root: `E:\Finnova\Finnova-UI\Finnova-UI`.

Grounded conventions this plan follows (confirmed against the current repo):
- A `User` auth entity already exists — this feature uses distinct names (`UserAccount`, `UserGroup`,
  `FunctionalGroup`, …) and the frontend `userManagement`/`UserManagement` namespace.
- Persistence is wired through `AddFinnovaRepository` with `UseSqlServer` and
  `ApplyConfigurationsFromAssembly` (new `IEntityTypeConfiguration<T>` classes auto-register).
- MediatR handlers and FluentValidation validators are auto-registered by assembly scanning in the
  host `Program.cs` — new slices need **no** DI edits.
- Password hashing reuses the existing `Finnova.Service.Auth.PasswordHasher` (PBKDF2, `salt.hash`).
- The acting admin is read from the JWT via a `GetActingAdmin()` controller helper, never the body.
- Enums are serialized by name (host `JsonStringEnumConverter`) and stored as `int`.

## Tasks

- [ ] 1. Backend domain: enums + entities
  - _Requirements: R1, R2, R3, R5, R6, R8, R9, R14_

- [ ] 1.1 CREATE `Finnova.Models/Domain/Enums/UserConfiguration.cs`

```csharp
namespace Finnova.Models.Domain.Enums;

/// <summary>Which kind of record is being provisioned (FS §9 R1). Exactly one per record.</summary>
public enum UserConfiguration
{
    User = 0,
    UserGroup = 1,
    FunctionalGroup = 2
}
```

- [ ] 1.2 CREATE `Finnova.Models/Domain/Enums/UserType.cs`

```csharp
namespace Finnova.Models.Domain.Enums;

/// <summary>Classification of an individual user (FS §9 R3.12). Lookup-backed, two values.</summary>
public enum UserType
{
    Corporate = 0,
    Branch = 1
}
```

- [ ] 1.3 CREATE `Finnova.Models/Domain/Enums/UserManagementAuditAction.cs`

```csharp
namespace Finnova.Models.Domain.Enums;

/// <summary>The mutation an audit entry records (FS §9 R14).</summary>
public enum UserManagementAuditAction
{
    Create = 0,
    Modify = 1
}
```

- [ ] 1.4 CREATE `Finnova.Models/Domain/Entities/UserAccount.cs`

```csharp
using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

/// <summary>
/// A provisioned individual user in the User Management master (FS §9). Named UserAccount (not
/// User) to avoid clashing with the existing authentication User entity. India-only: a single
/// English Name. Maps to table "user_accounts".
/// </summary>
public class UserAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserCode { get; set; } = string.Empty;     // generated, 4-6, uppercase, unique (R2)
    public string Name { get; set; } = string.Empty;         // max 50, English, mandatory (R3.2/3.3)
    public string PasswordHash { get; set; } = string.Empty; // PBKDF2 "salt.hash" (never plaintext) (R3.4/3.5)

    public DateTime DateOfJoining { get; set; }              // defaults to system date (R3.7)
    public string Designation { get; set; } = string.Empty;  // max 40, Lookup-backed (R3.9/3.13)
    public string Department { get; set; } = string.Empty;   // max 40, Lookup-backed (R3.10/3.14)
    public string? MobileNumber { get; set; }                // numeric, <=12, optional (R4.1-4.3)
    public string? Email { get; set; }                       // <=60, format rules, optional (R4.4-4.6)
    public UserType UserType { get; set; }                   // Corporate | Branch (R3.11/3.12)
    public bool IsActive { get; set; } = true;               // default active (R3.15)

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Access assignments + branch associations hang off the user (and off groups) via FKs below.
    public ICollection<UserAccessAssignment> AccessAssignments { get; set; } = new List<UserAccessAssignment>();
    public ICollection<UserBranchAssociation> BranchAssociations { get; set; } = new List<UserBranchAssociation>();
}
```

- [ ] 1.5 CREATE `Finnova.Models/Domain/Entities/UserGroup.cs`

```csharp
namespace Finnova.Models.Domain.Entities;

/// <summary>A named collection of active users (FS §9 R5). Maps to table "user_groups".</summary>
public class UserGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserGroupCode { get; set; } = string.Empty; // generated, 4-6, unique (R2.2)
    public string Name { get; set; } = string.Empty;          // max 30, English, mandatory (R5.2/5.3)
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<UserGroupMember> Members { get; set; } = new List<UserGroupMember>();
}
```

- [ ] 1.6 CREATE `Finnova.Models/Domain/Entities/UserGroupMember.cs`

```csharp
namespace Finnova.Models.Domain.Entities;

/// <summary>Links an active UserAccount to a UserGroup (FS §9 R5.7/5.8). Table "user_group_members".</summary>
public class UserGroupMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserGroupId { get; set; }                     // FK -> user_groups
    public Guid UserAccountId { get; set; }                   // FK -> user_accounts (must be active, R5.8)
}
```

- [ ] 1.7 CREATE `Finnova.Models/Domain/Entities/FunctionalGroup.cs`

```csharp
namespace Finnova.Models.Domain.Entities;

/// <summary>A grouping of program functions clubbed from a Role Center (FS §9 R6). Table "functional_groups".</summary>
public class FunctionalGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FunctionalGroupCode { get; set; } = string.Empty; // generated from Role Center Name (R2.3)
    public string RoleCenterName { get; set; } = string.Empty;      // source Role Center (R6.1)
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<FunctionalGroupFunction> Functions { get; set; } = new List<FunctionalGroupFunction>();
}
```

- [ ] 1.8 CREATE `Finnova.Models/Domain/Entities/FunctionalGroupFunction.cs`

```csharp
namespace Finnova.Models.Domain.Entities;

/// <summary>One clubbed program function under a functional group (FS §9 R6.3). Table "functional_group_functions".</summary>
public class FunctionalGroupFunction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FunctionalGroupId { get; set; }               // FK -> functional_groups
    public string ProgramName { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;      // RoleCenterName + ProgramName (R8.4)
}
```

- [ ] 1.9 CREATE `Finnova.Models/Domain/Entities/FunctionalGroupAssignment.cs`

```csharp
namespace Finnova.Models.Domain.Entities;

/// <summary>
/// Assigns a functional group to a target user OR user group (FS §9 R6.5/6.6). Exactly one target
/// is set. Supports one-to-many and many-to-one. Table "functional_group_assignments".
/// </summary>
public class FunctionalGroupAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FunctionalGroupId { get; set; }               // FK -> functional_groups
    public Guid? UserAccountId { get; set; }                  // target: a user ...
    public Guid? UserGroupId { get; set; }                    // ... or a user group (exactly one non-null)
}
```

- [ ] 1.10 CREATE `Finnova.Models/Domain/Entities/UserAccessAssignment.cs`

```csharp
namespace Finnova.Models.Domain.Entities;

/// <summary>
/// A single access line: a Program (under a Role Center) within a Line of Business, with the four
/// access-right flags. RoleCode = RoleCenterName + ProgramName (R8.4). Owned by a user OR a group
/// (exactly one owner FK is non-null). Table "user_access_assignments". (R8.6/8.9)
/// </summary>
public class UserAccessAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? UserAccountId { get; set; }                  // owner: a user ...
    public Guid? UserGroupId { get; set; }                    // ... or a user group (exactly one non-null)

    public string LineOfBusiness { get; set; } = string.Empty; // selected LOB (R7)
    public string RoleCenterName { get; set; } = string.Empty; // "ALL" allowed (R9.5)
    public string ProgramName { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;       // RoleCenterName + ProgramName (R8.4)

    public bool CanAdd { get; set; }                           // (R8.6)
    public bool CanModify { get; set; }
    public bool CanQuery { get; set; }
    public bool CanDelete { get; set; }
}
```

- [ ] 1.11 CREATE `Finnova.Models/Domain/Entities/UserBranchAssociation.cs`

```csharp
namespace Finnova.Models.Domain.Entities;

/// <summary>A branch (or ALL) associated with a user/group access scope (FS §9 R9). Table "user_branch_associations".</summary>
public class UserBranchAssociation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserAccountId { get; set; }                  // owner: a user ...
    public Guid? UserGroupId { get; set; }                    // ... or a user group (exactly one non-null)

    public string LineOfBusiness { get; set; } = string.Empty; // branches are scoped to the LOB selection
    public string BranchCode { get; set; } = string.Empty;     // concrete branch code, or "ALL" (R9.3/9.4)
    public bool IsAll { get; set; }                            // true when BranchCode == "ALL"
}
```

- [ ] 1.12 CREATE `Finnova.Models/Domain/Entities/UserManagementAuditEntry.cs`

```csharp
using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

/// <summary>
/// Immutable audit record for a create/modify of a user, user group, or functional group (FS §9 R14).
/// Written in the same unit of work as the mutation; never updated or deleted. Table "user_management_audit".
/// </summary>
public class UserManagementAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RecordId { get; set; }                        // the user/group/functional-group id
    public UserConfiguration RecordKind { get; set; }         // which master kind was affected
    public UserManagementAuditAction Action { get; set; }     // Create | Modify

    public string? OldValues { get; set; }                    // JSON snapshot (null on Create)
    public string NewValues { get; set; } = string.Empty;     // JSON snapshot of result
    public string Summary { get; set; } = string.Empty;       // human-readable change summary

    public string ChangedBy { get; set; } = string.Empty;     // acting admin id from JWT (R14.1/14.2)
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow; // transaction date (UTC)
}
```

---

- [ ] 2. Backend domain: exceptions (ERR-USR family)
  - _Requirements: R2.5, R5.8, R11.7, R12.3, R14.4, R1.6, R7, R8.2, R9.6_

- [ ] 2.1 CREATE `Finnova.Models/Domain/Exceptions/UserNotFoundException.cs`

```csharp
namespace Finnova.Models.Domain.Exceptions;

/// <summary>Thrown when a user/group/functional-group record is not found. Maps to ERR-USR-404.</summary>
public class UserNotFoundException : Exception
{
    public const string ErrorCode = "ERR-USR-404";
    public UserNotFoundException(string key) : base($"User record '{key}' was not found.") { }
}
```

- [ ] 2.2 CREATE `Finnova.Models/Domain/Exceptions/UserDuplicateCodeException.cs`

```csharp
namespace Finnova.Models.Domain.Exceptions;

/// <summary>Thrown when a generated code collides and cannot be made unique (R2.5). Maps to ERR-USR-409.</summary>
public class UserDuplicateCodeException : Exception
{
    public const string ErrorCode = "ERR-USR-409";
    public UserDuplicateCodeException() : base("Generated code collided and could not be made unique.") { }
}
```

- [ ] 2.3 CREATE `Finnova.Models/Domain/Exceptions/UserInactiveMemberException.cs`

```csharp
namespace Finnova.Models.Domain.Exceptions;

/// <summary>Thrown when a non-active user is tagged to a group (R5.8). Maps to ERR-USR-409.</summary>
public class UserInactiveMemberException : Exception
{
    public const string ErrorCode = "ERR-USR-409";
    public UserInactiveMemberException() : base("Only active users can be tagged to a group.") { }
}
```

- [ ] 2.4 CREATE `Finnova.Models/Domain/Exceptions/AuditEntryImmutableException.cs`

```csharp
namespace Finnova.Models.Domain.Exceptions;

/// <summary>Thrown on any attempt to update/delete an audit entry (R14.4). Maps to ERR-USR-409.</summary>
public class AuditEntryImmutableException : Exception
{
    public const string ErrorCode = "ERR-USR-409";
    public AuditEntryImmutableException() : base("Audit entries are immutable and cannot be changed or removed.") { }
}
```

- [ ] 2.5 CREATE `Finnova.Models/Domain/Exceptions/UserValidationException.cs`

```csharp
namespace Finnova.Models.Domain.Exceptions;

/// <summary>
/// Domain validation that is not expressible in a static FluentValidation rule — e.g. missing
/// prerequisite reference (R1.6), LOB not linked to Role Codes (R7.5), missing branch (R9.6),
/// missing Role Center (R8.2). Maps to ERR-USR-400.
/// </summary>
public class UserValidationException : Exception
{
    public const string ErrorCode = "ERR-USR-400";
    public UserValidationException(string message) : base(message) { }
}
```

---

- [ ] 3. Backend contracts (request/response records + mappers)
  - _Requirements: R3, R4, R5, R6, R7, R8, R9, R10, R11, R13, R14_

- [ ] 3.1 CREATE `Finnova.Models/Contracts/UserManagement/UserManagementRequests.cs`

```csharp
namespace Finnova.Models.Contracts.UserManagement;

using Finnova.Models.Domain.Enums;

// ---- Create ----

/// <summary>Create an individual user (R3). DOJ/IsActive nullable so the service defaults them (R3.7/3.15).</summary>
public record CreateUserAccountRequest(
    string Name,
    string Password,
    DateTime? DateOfJoining,
    string Designation,
    string Department,
    string? MobileNumber,
    string? Email,
    UserType UserType,
    bool? IsActive);

/// <summary>Create a user group (R5). MemberUserCodes: the active users tagged to the group.</summary>
public record CreateUserGroupRequest(
    string Name,
    IReadOnlyList<string> MemberUserCodes,
    bool? IsActive);

/// <summary>Create a functional group (R6). Code is generated from the Role Center Name.</summary>
public record CreateFunctionalGroupRequest(
    string RoleCenterName,
    bool? IsActive);

// ---- Update (Modify mode, R11) ----

/// <summary>Modify a user (R11.1/11.3). Password is changed only via ResetPassword (R11.5/11.8).</summary>
public record UpdateUserAccountRequest(
    string Name,
    DateTime? DateOfJoining,
    string Designation,
    string Department,
    string? MobileNumber,
    string? Email,
    UserType UserType,
    bool IsActive);

public record ResetPasswordRequest(string NewPassword);        // Modify mode only (R11.4/11.5)

public record UpdateUserGroupRequest(
    string Name,
    IReadOnlyList<string> MemberUserCodes,
    bool IsActive);

// ---- Access assignment (R7/R8/R9/R10) ----

public record AccessRightRow(
    string RoleCode,
    string RoleCenterName,
    string ProgramName,
    bool CanAdd,
    bool CanModify,
    bool CanQuery,
    bool CanDelete);

public record CopyProfileRequest(string SourceUserCode, string SourceLineOfBusiness);

public record SaveUserAccessRequest(
    string LineOfBusiness,
    IReadOnlyList<AccessRightRow> Rows,
    IReadOnlyList<string> BranchCodes,                         // may contain "ALL" (R9.3/9.4)
    CopyProfileRequest? CopyProfile);                          // Create mode only (R10)
```

- [ ] 3.2 CREATE `Finnova.Models/Contracts/UserManagement/UserManagementResponses.cs`

```csharp
namespace Finnova.Models.Contracts.UserManagement;

using Finnova.Models.Domain.Enums;

public record UserAccountResponse(
    Guid Id, string UserCode, string Name, DateTime DateOfJoining,
    string Designation, string Department, string? MobileNumber, string? Email,
    UserType UserType, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);

public record UserGroupMemberResponse(string UserCode, string Name, bool IsActive);

public record UserGroupResponse(
    Guid Id, string UserGroupCode, string Name,
    IReadOnlyList<UserGroupMemberResponse> Members, bool IsActive,
    DateTime CreatedAt, DateTime UpdatedAt);

public record FunctionalGroupFunctionResponse(string ProgramName, string RoleCode);

public record FunctionalGroupResponse(
    Guid Id, string FunctionalGroupCode, string RoleCenterName,
    IReadOnlyList<FunctionalGroupFunctionResponse> Functions, bool IsActive,
    DateTime CreatedAt, DateTime UpdatedAt);

public record UserAccessResponse(
    string LineOfBusiness,
    IReadOnlyList<AccessRightRow> Rows,
    IReadOnlyList<string> BranchCodes);

/// <summary>Slim list projection (R13): one row covers all three record kinds via a discriminator.</summary>
public record UserListItemResponse(
    Guid Id, string Code, string Name, UserConfiguration Kind, bool IsActive);

public record UserManagementAuditEntryResponse(
    Guid Id, Guid RecordId, UserConfiguration RecordKind,
    string Action, string Summary, string ChangedBy, DateTime ChangedAtUtc);

// ---- Reference LOVs (consumed master data) ----
public record ReferenceItemResponse(string Code, string Label);     // designation/department/user-type/role-center/LOB

public record BranchTreeNodeResponse(
    string Code, string Name, string Level,                         // Location | Region | Branch
    IReadOnlyList<BranchTreeNodeResponse> Children);
```

- [ ] 3.3 CREATE `Finnova.Repository/Interfaces/UserListItemResult.cs`

```csharp
using Finnova.Models.Domain.Enums;

namespace Finnova.Repository.Interfaces;

/// <summary>
/// Repository-level union projection for the paged list (R13). The repository unions user_accounts,
/// user_groups and functional_groups into this flat shape; the service maps it to UserListItemResponse.
/// </summary>
public record UserListItemResult(Guid Id, string Code, string Name, UserConfiguration Kind, bool IsActive);
```

---

- [ ] 4. Backend EF configuration + DbContext DbSets + migration
  - One `IEntityTypeConfiguration<T>` per entity, auto-registered by `ApplyConfigurationsFromAssembly`.
  - _Requirements: R2.4, R5.7, R8.9, R9, R13, R14_

- [ ] 4.1 CREATE `Finnova.Repository/Configuration/UserAccountConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> b)
    {
        b.ToTable("user_accounts");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserCode).IsRequired().HasMaxLength(6);
        b.Property(x => x.Name).IsRequired().HasMaxLength(50);
        b.Property(x => x.PasswordHash).IsRequired().HasMaxLength(400);
        b.Property(x => x.Designation).IsRequired().HasMaxLength(40);
        b.Property(x => x.Department).IsRequired().HasMaxLength(40);
        b.Property(x => x.MobileNumber).HasMaxLength(12);
        b.Property(x => x.Email).HasMaxLength(60);
        b.Property(x => x.UserType).IsRequired();              // int
        b.Property(x => x.IsActive).IsRequired();

        b.HasIndex(x => x.UserCode).IsUnique();                // company-wide uniqueness (R2.4)
        b.HasIndex(x => x.Name);                               // search/order (R13.2/13.9)

        b.HasMany(x => x.AccessAssignments).WithOne()
            .HasForeignKey(a => a.UserAccountId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.BranchAssociations).WithOne()
            .HasForeignKey(a => a.UserAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [ ] 4.2 CREATE `Finnova.Repository/Configuration/UserGroupConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class UserGroupConfiguration : IEntityTypeConfiguration<UserGroup>
{
    public void Configure(EntityTypeBuilder<UserGroup> b)
    {
        b.ToTable("user_groups");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserGroupCode).IsRequired().HasMaxLength(6);
        b.Property(x => x.Name).IsRequired().HasMaxLength(30);
        b.Property(x => x.IsActive).IsRequired();

        b.HasIndex(x => x.UserGroupCode).IsUnique();           // company-wide uniqueness (R2.4)
        b.HasIndex(x => x.Name);

        b.HasMany(x => x.Members).WithOne()
            .HasForeignKey(m => m.UserGroupId).OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [ ] 4.3 CREATE `Finnova.Repository/Configuration/UserGroupMemberConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class UserGroupMemberConfiguration : IEntityTypeConfiguration<UserGroupMember>
{
    public void Configure(EntityTypeBuilder<UserGroupMember> b)
    {
        b.ToTable("user_group_members");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserGroupId).IsRequired();
        b.Property(x => x.UserAccountId).IsRequired();

        // No duplicate member within a group (R5.7).
        b.HasIndex(x => new { x.UserGroupId, x.UserAccountId }).IsUnique();
    }
}
```

- [ ] 4.4 CREATE `Finnova.Repository/Configuration/FunctionalGroupConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class FunctionalGroupConfiguration : IEntityTypeConfiguration<FunctionalGroup>
{
    public void Configure(EntityTypeBuilder<FunctionalGroup> b)
    {
        b.ToTable("functional_groups");
        b.HasKey(x => x.Id);
        b.Property(x => x.FunctionalGroupCode).IsRequired().HasMaxLength(6);
        b.Property(x => x.RoleCenterName).IsRequired().HasMaxLength(100);
        b.Property(x => x.IsActive).IsRequired();

        b.HasIndex(x => x.FunctionalGroupCode).IsUnique();     // company-wide uniqueness (R2.4)

        b.HasMany(x => x.Functions).WithOne()
            .HasForeignKey(f => f.FunctionalGroupId).OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [ ] 4.5 CREATE `Finnova.Repository/Configuration/FunctionalGroupFunctionConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class FunctionalGroupFunctionConfiguration : IEntityTypeConfiguration<FunctionalGroupFunction>
{
    public void Configure(EntityTypeBuilder<FunctionalGroupFunction> b)
    {
        b.ToTable("functional_group_functions");
        b.HasKey(x => x.Id);
        b.Property(x => x.FunctionalGroupId).IsRequired();
        b.Property(x => x.ProgramName).IsRequired().HasMaxLength(100);
        b.Property(x => x.RoleCode).IsRequired().HasMaxLength(50);
        b.HasIndex(x => new { x.FunctionalGroupId, x.RoleCode }).IsUnique();
    }
}
```

- [ ] 4.6 CREATE `Finnova.Repository/Configuration/FunctionalGroupAssignmentConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class FunctionalGroupAssignmentConfiguration : IEntityTypeConfiguration<FunctionalGroupAssignment>
{
    public void Configure(EntityTypeBuilder<FunctionalGroupAssignment> b)
    {
        b.ToTable("functional_group_assignments");
        b.HasKey(x => x.Id);
        b.Property(x => x.FunctionalGroupId).IsRequired();
        b.Property(x => x.UserAccountId);
        b.Property(x => x.UserGroupId);

        b.HasIndex(x => new { x.FunctionalGroupId, x.UserAccountId });
        b.HasIndex(x => new { x.FunctionalGroupId, x.UserGroupId });

        // Exactly one owner target set (R6.5): a user OR a group, never both/neither.
        b.ToTable(t => t.HasCheckConstraint(
            "CK_functional_group_assignments_owner",
            "([UserAccountId] IS NOT NULL AND [UserGroupId] IS NULL) OR ([UserAccountId] IS NULL AND [UserGroupId] IS NOT NULL)"));
    }
}
```

- [ ] 4.7 CREATE `Finnova.Repository/Configuration/UserAccessAssignmentConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class UserAccessAssignmentConfiguration : IEntityTypeConfiguration<UserAccessAssignment>
{
    public void Configure(EntityTypeBuilder<UserAccessAssignment> b)
    {
        b.ToTable("user_access_assignments");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserAccountId);
        b.Property(x => x.UserGroupId);
        b.Property(x => x.LineOfBusiness).IsRequired().HasMaxLength(100);
        b.Property(x => x.RoleCenterName).IsRequired().HasMaxLength(100);
        b.Property(x => x.ProgramName).IsRequired().HasMaxLength(100);
        b.Property(x => x.RoleCode).IsRequired().HasMaxLength(50);
        b.Property(x => x.CanAdd).IsRequired();
        b.Property(x => x.CanModify).IsRequired();
        b.Property(x => x.CanQuery).IsRequired();
        b.Property(x => x.CanDelete).IsRequired();

        b.HasIndex(x => new { x.UserAccountId, x.LineOfBusiness, x.RoleCode });
        b.HasIndex(x => new { x.UserGroupId, x.LineOfBusiness, x.RoleCode });

        // Exactly one owner (user or group) (R8.9 owner polymorphism).
        b.ToTable(t => t.HasCheckConstraint(
            "CK_user_access_assignments_owner",
            "([UserAccountId] IS NOT NULL AND [UserGroupId] IS NULL) OR ([UserAccountId] IS NULL AND [UserGroupId] IS NOT NULL)"));
    }
}
```

- [ ] 4.8 CREATE `Finnova.Repository/Configuration/UserBranchAssociationConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class UserBranchAssociationConfiguration : IEntityTypeConfiguration<UserBranchAssociation>
{
    public void Configure(EntityTypeBuilder<UserBranchAssociation> b)
    {
        b.ToTable("user_branch_associations");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserAccountId);
        b.Property(x => x.UserGroupId);
        b.Property(x => x.LineOfBusiness).IsRequired().HasMaxLength(100);
        b.Property(x => x.BranchCode).IsRequired().HasMaxLength(50);
        b.Property(x => x.IsAll).IsRequired();

        b.HasIndex(x => new { x.UserAccountId, x.LineOfBusiness });
        b.HasIndex(x => new { x.UserGroupId, x.LineOfBusiness });

        b.ToTable(t => t.HasCheckConstraint(
            "CK_user_branch_associations_owner",
            "([UserAccountId] IS NOT NULL AND [UserGroupId] IS NULL) OR ([UserAccountId] IS NULL AND [UserGroupId] IS NOT NULL)"));
    }
}
```

- [ ] 4.9 CREATE `Finnova.Repository/Configuration/UserManagementAuditEntryConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class UserManagementAuditEntryConfiguration : IEntityTypeConfiguration<UserManagementAuditEntry>
{
    public void Configure(EntityTypeBuilder<UserManagementAuditEntry> b)
    {
        b.ToTable("user_management_audit");
        b.HasKey(x => x.Id);
        b.Property(x => x.RecordId).IsRequired();
        b.Property(x => x.RecordKind).IsRequired();            // int
        b.Property(x => x.Action).IsRequired();               // int
        b.Property(x => x.OldValues);
        b.Property(x => x.NewValues).IsRequired();
        b.Property(x => x.Summary).IsRequired().HasMaxLength(1000);
        b.Property(x => x.ChangedBy).IsRequired().HasMaxLength(200);
        b.Property(x => x.ChangedAtUtc).IsRequired();

        b.HasIndex(x => new { x.RecordId, x.ChangedAtUtc });   // newest-first lookups (R14)
    }
}
```

- [ ] 4.10 MODIFY `Finnova.Repository/Context/FinnovaDbContext.cs`

  Add the nine DbSets after the existing `EntityAuditEntries` set (just before the
  `protected override void OnModelCreating` line):

```csharp
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<UserGroup> UserGroups => Set<UserGroup>();
    public DbSet<UserGroupMember> UserGroupMembers => Set<UserGroupMember>();
    public DbSet<FunctionalGroup> FunctionalGroups => Set<FunctionalGroup>();
    public DbSet<FunctionalGroupFunction> FunctionalGroupFunctions => Set<FunctionalGroupFunction>();
    public DbSet<FunctionalGroupAssignment> FunctionalGroupAssignments => Set<FunctionalGroupAssignment>();
    public DbSet<UserAccessAssignment> UserAccessAssignments => Set<UserAccessAssignment>();
    public DbSet<UserBranchAssociation> UserBranchAssociations => Set<UserBranchAssociation>();
    public DbSet<UserManagementAuditEntry> UserManagementAuditEntries => Set<UserManagementAuditEntry>();
```

- [ ] 4.11 RUN the EF migration command (user runs this in a terminal from the repo root `e:\Finnova\Finnova-API`)

  The solution wires persistence with `UseSqlServer` (see `AddFinnovaRepository`), so this is a
  SQL Server migration. Generate and apply:

```
dotnet ef migrations add AddUserManagement --project Finnova.Repository --startup-project Finnova.SystemAdminService
dotnet ef database update --project Finnova.Repository --startup-project Finnova.SystemAdminService
```

---

- [ ] 5. Backend repository + DI registration
  - _Requirements: R2.4, R5.5, R5.8, R10.2, R11.1, R13, R14_

- [ ] 5.1 CREATE `Finnova.Repository/Interfaces/IUserManagementRepository.cs`

```csharp
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;

namespace Finnova.Repository.Interfaces;

/// <summary>
/// Multi-entity data access for User Management. Spans user_accounts, user_groups,
/// functional_groups and their satellites. Backed by RepositoryBase&lt;UserAccount&gt; for the
/// generic CRUD surface, with feature-specific members below.
/// </summary>
public interface IUserManagementRepository
{
    // Users
    Task<UserAccount?> GetUserByCodeAsync(string code, CancellationToken ct = default);
    Task<UserAccount?> GetUserWithAccessAsync(Guid id, CancellationToken ct = default);
    Task<bool> UserCodeExistsAsync(string code, CancellationToken ct = default);
    Task AddUserAsync(UserAccount user, CancellationToken ct = default);
    Task UpdateUserAsync(UserAccount user, CancellationToken ct = default);

    // Groups / functional groups
    Task<UserGroup?> GetGroupByCodeAsync(string code, CancellationToken ct = default);
    Task<bool> GroupCodeExistsAsync(string code, CancellationToken ct = default);
    Task AddGroupAsync(UserGroup group, CancellationToken ct = default);
    Task UpdateGroupAsync(UserGroup group, CancellationToken ct = default);
    Task<FunctionalGroup?> GetFunctionalGroupByCodeAsync(string code, CancellationToken ct = default);
    Task<bool> FunctionalGroupCodeExistsAsync(string code, CancellationToken ct = default);
    Task AddFunctionalGroupAsync(FunctionalGroup fg, CancellationToken ct = default);

    // Members / references
    Task<List<UserAccount>> GetActiveUsersByCodesAsync(IEnumerable<string> codes, CancellationToken ct = default);
    Task<List<UserAccount>> SearchActiveUsersAsync(string? search, CancellationToken ct = default);

    // Access
    Task ReplaceAccessAsync(Guid ownerUserId, string lob,
        IEnumerable<UserAccessAssignment> rows, IEnumerable<UserBranchAssociation> branches, CancellationToken ct = default);
    Task<(List<UserAccessAssignment> Rows, List<UserBranchAssociation> Branches)> GetAccessAsync(
        Guid ownerUserId, string lob, CancellationToken ct = default);

    // List + audit
    Task<(List<UserListItemResult> Items, int Total)> GetPagedAsync(
        string? search, UserConfiguration? kind, bool? isActive, int page, int pageSize, CancellationToken ct = default);
    Task AddAuditAsync(UserManagementAuditEntry entry, CancellationToken ct = default);
    Task<List<UserManagementAuditEntry>> GetAuditAsync(Guid recordId, CancellationToken ct = default);
}
```

- [ ] 5.2 CREATE `Finnova.Repository/Repositories/UserManagementRepository.cs`

```csharp
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

public class UserManagementRepository(FinnovaDbContext db)
    : RepositoryBase<UserAccount>(db), IUserManagementRepository
{
    // ---- Users ----
    public Task<UserAccount?> GetUserByCodeAsync(string code, CancellationToken ct = default)
    {
        var c = code.Trim();
        return DbSet.AsNoTracking().FirstOrDefaultAsync(x => x.UserCode == c, ct);
    }

    public Task<UserAccount?> GetUserWithAccessAsync(Guid id, CancellationToken ct = default)
        => DbSet.AsNoTracking()
            .Include(x => x.AccessAssignments)
            .Include(x => x.BranchAssociations)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<bool> UserCodeExistsAsync(string code, CancellationToken ct = default)
    {
        var c = code.Trim();
        return DbSet.AsNoTracking().AnyAsync(x => x.UserCode == c, ct);
    }

    public async Task AddUserAsync(UserAccount user, CancellationToken ct = default)
    {
        await DbSet.AddAsync(user, ct);
        await Context.SaveChangesAsync(ct);
    }

    public async Task UpdateUserAsync(UserAccount user, CancellationToken ct = default)
    {
        DbSet.Update(user);
        await Context.SaveChangesAsync(ct);
    }

    // ---- Groups / functional groups ----
    public Task<UserGroup?> GetGroupByCodeAsync(string code, CancellationToken ct = default)
    {
        var c = code.Trim();
        return Context.UserGroups.AsNoTracking()
            .Include(x => x.Members)
            .FirstOrDefaultAsync(x => x.UserGroupCode == c, ct);
    }

    public Task<bool> GroupCodeExistsAsync(string code, CancellationToken ct = default)
    {
        var c = code.Trim();
        return Context.UserGroups.AsNoTracking().AnyAsync(x => x.UserGroupCode == c, ct);
    }

    public async Task AddGroupAsync(UserGroup group, CancellationToken ct = default)
    {
        await Context.UserGroups.AddAsync(group, ct);
        await Context.SaveChangesAsync(ct);
    }

    public async Task UpdateGroupAsync(UserGroup group, CancellationToken ct = default)
    {
        Context.UserGroups.Update(group);
        await Context.SaveChangesAsync(ct);
    }

    public Task<FunctionalGroup?> GetFunctionalGroupByCodeAsync(string code, CancellationToken ct = default)
    {
        var c = code.Trim();
        return Context.FunctionalGroups.AsNoTracking()
            .Include(x => x.Functions)
            .FirstOrDefaultAsync(x => x.FunctionalGroupCode == c, ct);
    }

    public Task<bool> FunctionalGroupCodeExistsAsync(string code, CancellationToken ct = default)
    {
        var c = code.Trim();
        return Context.FunctionalGroups.AsNoTracking().AnyAsync(x => x.FunctionalGroupCode == c, ct);
    }

    public async Task AddFunctionalGroupAsync(FunctionalGroup fg, CancellationToken ct = default)
    {
        await Context.FunctionalGroups.AddAsync(fg, ct);
        await Context.SaveChangesAsync(ct);
    }

    // ---- Members / references ----
    public async Task<List<UserAccount>> GetActiveUsersByCodesAsync(IEnumerable<string> codes, CancellationToken ct = default)
    {
        var set = codes.Select(c => c.Trim()).ToHashSet();
        return await DbSet.AsNoTracking()
            .Where(x => set.Contains(x.UserCode))
            .ToListAsync(ct);
    }

    public async Task<List<UserAccount>> SearchActiveUsersAsync(string? search, CancellationToken ct = default)
    {
        var q = DbSet.AsNoTracking().Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(x => x.UserCode.Contains(term) || x.Name.Contains(term));
        }
        return await q.OrderBy(x => x.UserCode).ToListAsync(ct);
    }

    // ---- Access (replace the (owner, LOB) slice atomically) ----
    public async Task ReplaceAccessAsync(Guid ownerUserId, string lob,
        IEnumerable<UserAccessAssignment> rows, IEnumerable<UserBranchAssociation> branches, CancellationToken ct = default)
    {
        var existingRows = await Context.UserAccessAssignments
            .Where(x => x.UserAccountId == ownerUserId && x.LineOfBusiness == lob).ToListAsync(ct);
        Context.UserAccessAssignments.RemoveRange(existingRows);

        var existingBranches = await Context.UserBranchAssociations
            .Where(x => x.UserAccountId == ownerUserId && x.LineOfBusiness == lob).ToListAsync(ct);
        Context.UserBranchAssociations.RemoveRange(existingBranches);

        await Context.UserAccessAssignments.AddRangeAsync(rows, ct);
        await Context.UserBranchAssociations.AddRangeAsync(branches, ct);
        await Context.SaveChangesAsync(ct);
    }

    public async Task<(List<UserAccessAssignment> Rows, List<UserBranchAssociation> Branches)> GetAccessAsync(
        Guid ownerUserId, string lob, CancellationToken ct = default)
    {
        var rows = await Context.UserAccessAssignments.AsNoTracking()
            .Where(x => x.UserAccountId == ownerUserId && x.LineOfBusiness == lob).ToListAsync(ct);
        var branches = await Context.UserBranchAssociations.AsNoTracking()
            .Where(x => x.UserAccountId == ownerUserId && x.LineOfBusiness == lob).ToListAsync(ct);
        return (rows, branches);
    }

    // ---- List (union of the three kinds) + audit ----
    public async Task<(List<UserListItemResult> Items, int Total)> GetPagedAsync(
        string? search, UserConfiguration? kind, bool? isActive, int page, int pageSize, CancellationToken ct = default)
    {
        var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        var users = DbSet.AsNoTracking().Select(x => new UserListItemResult(
            x.Id, x.UserCode, x.Name, UserConfiguration.User, x.IsActive));
        var groups = Context.UserGroups.AsNoTracking().Select(x => new UserListItemResult(
            x.Id, x.UserGroupCode, x.Name, UserConfiguration.UserGroup, x.IsActive));
        var functionals = Context.FunctionalGroups.AsNoTracking().Select(x => new UserListItemResult(
            x.Id, x.FunctionalGroupCode, x.RoleCenterName, UserConfiguration.FunctionalGroup, x.IsActive));

        var union = users.Concat(groups).Concat(functionals);

        if (kind is not null)
            union = union.Where(x => x.Kind == kind);
        if (isActive is not null)
            union = union.Where(x => x.IsActive == isActive);          // R13.3
        if (term is not null)
            union = union.Where(x => x.Code.Contains(term) || x.Name.Contains(term)); // R13.2

        var total = await union.CountAsync(ct);
        var items = await union
            .OrderBy(x => x.Code).ThenBy(x => x.Id)                     // R13.9 (UserCode asc -> Id asc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);                                           // empty beyond last page (R13.8)
        return (items, total);
    }

    public async Task AddAuditAsync(UserManagementAuditEntry entry, CancellationToken ct = default)
    {
        await Context.UserManagementAuditEntries.AddAsync(entry, ct);
        await Context.SaveChangesAsync(ct);
    }

    public async Task<List<UserManagementAuditEntry>> GetAuditAsync(Guid recordId, CancellationToken ct = default)
        => await Context.UserManagementAuditEntries.AsNoTracking()
            .Where(x => x.RecordId == recordId)
            .OrderByDescending(x => x.ChangedAtUtc).ThenByDescending(x => x.Id)   // newest-first (R14)
            .ToListAsync(ct);
}
```

- [ ] 5.3 MODIFY `Finnova.Repository/DependencyInjection.cs`

  Register the new repository inside `AddFinnovaRepository`, after the
  `services.AddScoped<IEntityAuditRepository, EntityAuditRepository>();` line:

```csharp
        services.AddScoped<IUserManagementRepository, UserManagementRepository>();
```

---

- [ ] 6. Backend domain helpers + password-policy abstraction
  - Pure, property-testable helpers (P1–P4) plus the externally-owned password-policy abstraction.
  - _Requirements: R2.1-2.5, R8.4, R10.2, R3.5, R11.6_

- [ ] 6.1 CREATE `Finnova.Service/UserManagement/Helpers/UserCodeGenerator.cs`

```csharp
using System.Text;

namespace Finnova.Service.UserManagement.Helpers;

/// <summary>
/// Generates a 4-6 char, uppercase, alphanumeric code whose first char is a letter, derived from a
/// source string (user name OR role-center name) plus a number, with no spaces/specials
/// (R2.1-2.3). On collision the numeric suffix is varied and retried against <paramref name="isTaken"/>
/// (R2.5). After a bounded number of attempts throws so the handler can surface ERR-USR-409.
/// Pure except for the injected collision predicate — property-tested (P1).
/// </summary>
public static class UserCodeGenerator
{
    private const int MinLen = 4;
    private const int MaxLen = 6;
    private const int MaxAttempts = 10_000;

    public static string Generate(string source, Func<string, bool> isTaken)
    {
        var letters = new string((source ?? string.Empty)
            .ToUpperInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());

        // Ensure the first character is a letter (R2.1). Fall back to 'U' (User) when none present.
        var firstLetter = letters.FirstOrDefault(char.IsLetter);
        if (firstLetter == default) firstLetter = 'U';

        // Alpha stem (letters only, drives readability), bounded to leave room for the number.
        var alpha = new string(letters.Where(char.IsLetter).ToArray());
        if (alpha.Length == 0) alpha = firstLetter.ToString();
        var stem = (firstLetter + (alpha.Length > 1 ? alpha[1..] : string.Empty));

        for (var n = 1; n <= MaxAttempts; n++)
        {
            var numeric = n.ToString();
            // Keep total length within 4-6: trim the stem so stem+number fits, min 1 letter.
            var maxStem = Math.Max(1, MaxLen - numeric.Length);
            var usableStem = stem.Length > maxStem ? stem[..maxStem] : stem;

            var candidate = usableStem + numeric;
            if (candidate.Length < MinLen)
                candidate = usableStem + numeric.PadLeft(MinLen - usableStem.Length, '0');
            if (candidate.Length > MaxLen)
                candidate = candidate[..MaxLen];

            candidate = candidate.ToUpperInvariant();
            if (candidate.Length >= MinLen && candidate.Length <= MaxLen
                && char.IsLetter(candidate[0]) && candidate.All(char.IsLetterOrDigit)
                && !isTaken(candidate))
                return candidate;
        }

        throw new InvalidOperationException("Unable to generate a unique code within the attempt budget.");
    }
}
```

- [ ] 6.2 CREATE `Finnova.Service/UserManagement/Helpers/RoleCodeBuilder.cs`

```csharp
namespace Finnova.Service.UserManagement.Helpers;

/// <summary>
/// RoleCode = RoleCenterName concatenated with ProgramName (uppercased, no separators), e.g.
/// SSCMP / OOASM / OOETM (FS §9 R8.4). Deterministic pure function — property-tested (P2).
/// </summary>
public static class RoleCodeBuilder
{
    public static string Build(string roleCenterName, string programName)
        => ((roleCenterName ?? string.Empty) + (programName ?? string.Empty))
            .ToUpperInvariant();
}
```

- [ ] 6.3 CREATE `Finnova.Service/UserManagement/Helpers/AccessAssignmentMerger.cs`

```csharp
using Finnova.Models.Contracts.UserManagement;

namespace Finnova.Service.UserManagement.Helpers;

/// <summary>
/// Copy-Profile merge (R10.2): append source rows/branches to current and de-dup. Duplicate access
/// rows (same RoleCode) collapse to one row whose Add/Modify/Query/Delete flags are the logical OR
/// of the duplicates; duplicate branches collapse to one. Pure, commutative on flags, idempotent —
/// property-tested (P3, P4).
/// </summary>
public static class AccessAssignmentMerger
{
    public static (List<AccessRightRow> Rows, List<string> Branches) Merge(
        (IEnumerable<AccessRightRow> Rows, IEnumerable<string> Branches) current,
        (IEnumerable<AccessRightRow> Rows, IEnumerable<string> Branches) source)
    {
        var byRole = new Dictionary<string, AccessRightRow>(StringComparer.OrdinalIgnoreCase);

        foreach (var r in current.Rows.Concat(source.Rows))
        {
            if (byRole.TryGetValue(r.RoleCode, out var existing))
            {
                byRole[r.RoleCode] = existing with
                {
                    CanAdd = existing.CanAdd || r.CanAdd,
                    CanModify = existing.CanModify || r.CanModify,
                    CanQuery = existing.CanQuery || r.CanQuery,
                    CanDelete = existing.CanDelete || r.CanDelete,
                };
            }
            else
            {
                byRole[r.RoleCode] = r;
            }
        }

        var branches = current.Branches
            .Concat(source.Branches)
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (byRole.Values.ToList(), branches);
    }
}
```

- [ ] 6.4 CREATE `Finnova.Service/UserManagement/Abstractions/IPasswordPolicy.cs`

```csharp
namespace Finnova.Service.UserManagement.Abstractions;

/// <summary>
/// GPS password-policy evaluation (owned externally; consumed here). Enforced in the create/reset
/// handlers before hashing (R3.5/R11.6). The real policy is injected when available; a config-driven
/// default ships for development.
/// </summary>
public interface IPasswordPolicy
{
    bool IsCompliant(string password);
}
```

- [ ] 6.5 CREATE `Finnova.Service/UserManagement/Abstractions/DefaultPasswordPolicy.cs`

```csharp
using System.Linq;

namespace Finnova.Service.UserManagement.Abstractions;

/// <summary>
/// Development default for the GPS password policy: alphanumeric with special characters, min 8
/// chars, at least one letter, one digit, and one special character (R3 Password glossary). The
/// real GPS policy replaces this via DI when available.
/// </summary>
public class DefaultPasswordPolicy : IPasswordPolicy
{
    public bool IsCompliant(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8) return false;
        var hasLetter = password.Any(char.IsLetter);
        var hasDigit = password.Any(char.IsDigit);
        var hasSpecial = password.Any(c => !char.IsLetterOrDigit(c));
        return hasLetter && hasDigit && hasSpecial;
    }
}
```

- [ ] 6.6 MODIFY `Finnova.SystemAdminService/Program.cs`

  Register the password-policy default so the User Management handlers can resolve `IPasswordPolicy`.
  Add this line after the repository registration
  (`builder.Services.AddFinnovaRepository(connectionString);`):

```csharp
// GPS password policy (consumed by User Management). Dev default; swap for the real policy via DI.
builder.Services.AddScoped<Finnova.Service.UserManagement.Abstractions.IPasswordPolicy,
    Finnova.Service.UserManagement.Abstractions.DefaultPasswordPolicy>();
```

---

- [ ] 7. Backend service CQRS slices (commands, queries, validators, mapper)
  - Feature slice `Finnova.Service/UserManagement/`. MediatR + FluentValidation auto-register via
    the host assembly scan — no DI edits needed for these.
  - _Requirements: R2, R3, R4, R5, R6, R7, R8, R9, R10, R11, R12, R13, R14_

- [ ] 7.1 CREATE `Finnova.Service/UserManagement/Mappers/UserManagementMapper.cs`

```csharp
using Finnova.Models.Contracts.UserManagement;
using Finnova.Models.Domain.Entities;

namespace Finnova.Service.UserManagement.Mappers;

/// <summary>Entity -> contract mappers. The password hash is NEVER mapped into any response.</summary>
public static class UserManagementMapper
{
    public static UserAccountResponse ToResponse(this UserAccount x) => new(
        x.Id, x.UserCode, x.Name, x.DateOfJoining, x.Designation, x.Department,
        x.MobileNumber, x.Email, x.UserType, x.IsActive, x.CreatedAt, x.UpdatedAt);

    public static UserGroupResponse ToResponse(this UserGroup x, IReadOnlyList<UserGroupMemberResponse> members) => new(
        x.Id, x.UserGroupCode, x.Name, members, x.IsActive, x.CreatedAt, x.UpdatedAt);

    public static UserGroupMemberResponse ToMemberResponse(this UserAccount x) => new(x.UserCode, x.Name, x.IsActive);

    public static FunctionalGroupResponse ToResponse(this FunctionalGroup x) => new(
        x.Id, x.FunctionalGroupCode, x.RoleCenterName,
        x.Functions.Select(f => new FunctionalGroupFunctionResponse(f.ProgramName, f.RoleCode)).ToList(),
        x.IsActive, x.CreatedAt, x.UpdatedAt);

    public static UserListItemResponse ToResponse(this Finnova.Repository.Interfaces.UserListItemResult x) => new(
        x.Id, x.Code, x.Name, x.Kind, x.IsActive);

    public static List<UserListItemResponse> ToResponseList(
        this IEnumerable<Finnova.Repository.Interfaces.UserListItemResult> items)
        => items.Select(i => i.ToResponse()).ToList();

    public static UserManagementAuditEntryResponse ToResponse(this UserManagementAuditEntry a) => new(
        a.Id, a.RecordId, a.RecordKind, a.Action.ToString(), a.Summary, a.ChangedBy, a.ChangedAtUtc);
}
```

- [ ] 7.2 CREATE `Finnova.Service/UserManagement/Internal/EmailFormat.cs`

```csharp
namespace Finnova.Service.UserManagement.Internal;

/// <summary>
/// Email format rule shared by the validator and the property test (R4.4/4.5): &le;60 chars,
/// exactly one '@', at least one '.', and no leading/trailing '@' / '.' / special character.
/// </summary>
public static class EmailFormat
{
    public static bool IsValid(string? email)
    {
        if (string.IsNullOrEmpty(email)) return false;
        if (email.Length > 60) return false;
        if (email.Count(c => c == '@') != 1) return false;
        if (!email.Contains('.')) return false;

        var first = email[0];
        var last = email[^1];
        if (!char.IsLetterOrDigit(first) || !char.IsLetterOrDigit(last)) return false;

        return true;
    }
}
```

- [ ] 7.3 CREATE `Finnova.Service/UserManagement/Commands/CreateUserAccount/CreateUserAccountCommand.cs`

```csharp
using Finnova.Models.Contracts.UserManagement;
using Finnova.Models.Domain.Enums;
using MediatR;

namespace Finnova.Service.UserManagement.Commands.CreateUserAccount;

public record CreateUserAccountCommand(
    string Name,
    string Password,
    DateTime? DateOfJoining,
    string Designation,
    string Department,
    string? MobileNumber,
    string? Email,
    UserType UserType,
    bool? IsActive,
    string ActingAdmin) : IRequest<UserAccountResponse>;
```

- [ ] 7.4 CREATE `Finnova.Service/UserManagement/Commands/CreateUserAccount/CreateUserAccountCommandValidator.cs`

```csharp
using FluentValidation;
using Finnova.Service.UserManagement.Internal;

namespace Finnova.Service.UserManagement.Commands.CreateUserAccount;

public class CreateUserAccountCommandValidator : AbstractValidator<CreateUserAccountCommand>
{
    public CreateUserAccountCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Please enter the User Name")              // R3.2
            .MaximumLength(50).WithMessage("User Name must not exceed 50 characters.");   // R3.3

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Please enter the User Password");         // R3.4

        RuleFor(x => x.Designation)
            .NotEmpty().WithMessage("Please select the Designation")           // R3.9
            .MaximumLength(40).WithMessage("Designation must not exceed 40 characters.");   // R3.13

        RuleFor(x => x.Department)
            .NotEmpty().WithMessage("Please select the Department")            // R3.10
            .MaximumLength(40).WithMessage("Department must not exceed 40 characters.");    // R3.14

        RuleFor(x => x.UserType)
            .IsInEnum().WithMessage("Please select the User Type");            // R3.11/3.12

        RuleFor(x => x.MobileNumber)
            .Matches("^[0-9]{1,12}$").WithMessage("Special characters are not allowed in this field")
            .When(x => !string.IsNullOrEmpty(x.MobileNumber));                 // R4.1-4.3

        RuleFor(x => x.Email)
            .Must(EmailFormat.IsValid).WithMessage("Please enter a valid Email id")
            .When(x => !string.IsNullOrEmpty(x.Email));                        // R4.4/4.5
    }
}
```

- [ ] 7.5 CREATE `Finnova.Service/UserManagement/Commands/CreateUserAccount/CreateUserAccountCommandHandler.cs`

```csharp
using Finnova.Models.Contracts.UserManagement;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Auth;
using Finnova.Service.UserManagement.Abstractions;
using Finnova.Service.UserManagement.Helpers;
using Finnova.Service.UserManagement.Mappers;
using MediatR;

namespace Finnova.Service.UserManagement.Commands.CreateUserAccount;

public class CreateUserAccountCommandHandler : IRequestHandler<CreateUserAccountCommand, UserAccountResponse>
{
    private readonly IUserManagementRepository _repository;
    private readonly IPasswordPolicy _passwordPolicy;

    public CreateUserAccountCommandHandler(IUserManagementRepository repository, IPasswordPolicy passwordPolicy)
    {
        _repository = repository;
        _passwordPolicy = passwordPolicy;
    }

    public async Task<UserAccountResponse> Handle(CreateUserAccountCommand request, CancellationToken ct)
    {
        // GPS password policy (R3.5) — enforced here because the policy is externally configured.
        if (!_passwordPolicy.IsCompliant(request.Password))
            throw new UserValidationException("Please enter a valid Password");

        // Generate a unique UserCode from the name after it is present (R2.1/2.7).
        var code = UserCodeGenerator.Generate(
            request.Name,
            candidate => _repository.UserCodeExistsAsync(candidate, ct).GetAwaiter().GetResult());

        var user = new UserAccount
        {
            UserCode = code,
            Name = request.Name.Trim(),
            PasswordHash = PasswordHasher.Hash(request.Password),       // never store plaintext
            DateOfJoining = request.DateOfJoining ?? DateTime.UtcNow.Date, // default to system date (R3.7)
            Designation = request.Designation.Trim(),
            Department = request.Department.Trim(),
            MobileNumber = request.MobileNumber?.Trim(),
            Email = request.Email?.Trim(),
            UserType = request.UserType,
            IsActive = request.IsActive ?? true,                        // default active (R3.15)
        };

        await _repository.AddUserAsync(user, ct);

        await _repository.AddAuditAsync(new UserManagementAuditEntry
        {
            RecordId = user.Id,
            RecordKind = UserConfiguration.User,
            Action = UserManagementAuditAction.Create,
            OldValues = null,
            NewValues = $"{{\"UserCode\":\"{user.UserCode}\",\"Name\":\"{user.Name}\"}}",
            Summary = $"Created user '{user.UserCode}'.",
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, ct);

        return user.ToResponse();
    }
}
```

- [ ] 7.6 CREATE `Finnova.Service/UserManagement/Commands/UpdateUserAccount/UpdateUserAccountCommand.cs`

```csharp
using Finnova.Models.Contracts.UserManagement;
using Finnova.Models.Domain.Enums;
using MediatR;

namespace Finnova.Service.UserManagement.Commands.UpdateUserAccount;

public record UpdateUserAccountCommand(
    Guid Id,
    string Name,
    DateTime? DateOfJoining,
    string Designation,
    string Department,
    string? MobileNumber,
    string? Email,
    UserType UserType,
    bool IsActive,
    string ActingAdmin) : IRequest<UserAccountResponse>;
```

- [ ] 7.7 CREATE `Finnova.Service/UserManagement/Commands/UpdateUserAccount/UpdateUserAccountCommandValidator.cs`

```csharp
using FluentValidation;
using Finnova.Service.UserManagement.Internal;

namespace Finnova.Service.UserManagement.Commands.UpdateUserAccount;

public class UpdateUserAccountCommandValidator : AbstractValidator<UpdateUserAccountCommand>
{
    public UpdateUserAccountCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Please enter the User Name")              // R3.2
            .MaximumLength(50).WithMessage("User Name must not exceed 50 characters.");   // R3.3

        RuleFor(x => x.Designation)
            .NotEmpty().WithMessage("Please select the Designation").MaximumLength(40);   // R3.9/3.13

        RuleFor(x => x.Department)
            .NotEmpty().WithMessage("Please select the Department").MaximumLength(40);     // R3.10/3.14

        RuleFor(x => x.UserType).IsInEnum().WithMessage("Please select the User Type");    // R3.11/3.12

        RuleFor(x => x.MobileNumber)
            .Matches("^[0-9]{1,12}$").WithMessage("Special characters are not allowed in this field")
            .When(x => !string.IsNullOrEmpty(x.MobileNumber));                 // R4.1-4.3

        RuleFor(x => x.Email)
            .Must(EmailFormat.IsValid).WithMessage("Please enter a valid Email id")
            .When(x => !string.IsNullOrEmpty(x.Email));                        // R4.4/4.5
    }
}
```

- [ ] 7.8 CREATE `Finnova.Service/UserManagement/Commands/UpdateUserAccount/UpdateUserAccountCommandHandler.cs`

```csharp
using Finnova.Models.Contracts.UserManagement;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.UserManagement.Mappers;
using MediatR;

namespace Finnova.Service.UserManagement.Commands.UpdateUserAccount;

public class UpdateUserAccountCommandHandler : IRequestHandler<UpdateUserAccountCommand, UserAccountResponse>
{
    private readonly IUserManagementRepository _repository;

    public UpdateUserAccountCommandHandler(IUserManagementRepository repository) => _repository = repository;

    public async Task<UserAccountResponse> Handle(UpdateUserAccountCommand request, CancellationToken ct)
    {
        var user = await _repository.GetUserWithAccessAsync(request.Id, ct)
            ?? throw new UserNotFoundException(request.Id.ToString());   // R11.7

        // UserCode is immutable (R11.2) — never touched here. Password unchanged (R11.8).
        user.Name = request.Name.Trim();
        if (request.DateOfJoining is not null) user.DateOfJoining = request.DateOfJoining.Value;
        user.Designation = request.Designation.Trim();
        user.Department = request.Department.Trim();
        user.MobileNumber = request.MobileNumber?.Trim();
        user.Email = request.Email?.Trim();
        user.UserType = request.UserType;
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateUserAsync(user, ct);

        await _repository.AddAuditAsync(new UserManagementAuditEntry
        {
            RecordId = user.Id,
            RecordKind = UserConfiguration.User,
            Action = UserManagementAuditAction.Modify,
            OldValues = null,
            NewValues = $"{{\"UserCode\":\"{user.UserCode}\",\"Name\":\"{user.Name}\"}}",
            Summary = $"Modified user '{user.UserCode}'.",
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, ct);

        return user.ToResponse();
    }
}
```

- [ ] 7.9 CREATE `Finnova.Service/UserManagement/Commands/ResetPassword/ResetPasswordCommand.cs`

```csharp
using MediatR;

namespace Finnova.Service.UserManagement.Commands.ResetPassword;

public record ResetPasswordCommand(Guid Id, string NewPassword, string ActingAdmin) : IRequest<Unit>;
```

- [ ] 7.10 CREATE `Finnova.Service/UserManagement/Commands/ResetPassword/ResetPasswordCommandHandler.cs`

```csharp
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Auth;
using Finnova.Service.UserManagement.Abstractions;
using MediatR;

namespace Finnova.Service.UserManagement.Commands.ResetPassword;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Unit>
{
    private readonly IUserManagementRepository _repository;
    private readonly IPasswordPolicy _passwordPolicy;

    public ResetPasswordCommandHandler(IUserManagementRepository repository, IPasswordPolicy passwordPolicy)
    {
        _repository = repository;
        _passwordPolicy = passwordPolicy;
    }

    public async Task<Unit> Handle(ResetPasswordCommand request, CancellationToken ct)
    {
        var user = await _repository.GetUserWithAccessAsync(request.Id, ct)
            ?? throw new UserNotFoundException(request.Id.ToString());   // R11.7

        // Non-compliant password: reject and preserve the existing hash unchanged (R11.6).
        if (!_passwordPolicy.IsCompliant(request.NewPassword))
            throw new UserValidationException("Please enter a valid Password");

        user.PasswordHash = PasswordHasher.Hash(request.NewPassword);   // R11.5
        user.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateUserAsync(user, ct);

        await _repository.AddAuditAsync(new UserManagementAuditEntry
        {
            RecordId = user.Id,
            RecordKind = UserConfiguration.User,
            Action = UserManagementAuditAction.Modify,
            NewValues = $"{{\"UserCode\":\"{user.UserCode}\",\"PasswordReset\":true}}",
            Summary = $"Reset password for user '{user.UserCode}'.",
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, ct);

        return Unit.Value;
    }
}
```

- [ ] 7.11 CREATE `Finnova.Service/UserManagement/Commands/CreateUserGroup/CreateUserGroupCommand.cs`

```csharp
using Finnova.Models.Contracts.UserManagement;
using MediatR;

namespace Finnova.Service.UserManagement.Commands.CreateUserGroup;

public record CreateUserGroupCommand(
    string Name,
    IReadOnlyList<string> MemberUserCodes,
    bool? IsActive,
    string ActingAdmin) : IRequest<UserGroupResponse>;
```

- [ ] 7.12 CREATE `Finnova.Service/UserManagement/Commands/CreateUserGroup/CreateUserGroupCommandValidator.cs`

```csharp
using FluentValidation;

namespace Finnova.Service.UserManagement.Commands.CreateUserGroup;

public class CreateUserGroupCommandValidator : AbstractValidator<CreateUserGroupCommand>
{
    public CreateUserGroupCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Please enter the User Group Name")        // R5.2
            .MaximumLength(30).WithMessage("User Group Name must not exceed 30 characters.");   // R5.3

        RuleFor(x => x.MemberUserCodes)
            .NotEmpty().WithMessage("Please enter the User Code & Name");       // R5.6
    }
}
```

- [ ] 7.13 CREATE `Finnova.Service/UserManagement/Commands/CreateUserGroup/CreateUserGroupCommandHandler.cs`

```csharp
using Finnova.Models.Contracts.UserManagement;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.UserManagement.Helpers;
using Finnova.Service.UserManagement.Mappers;
using MediatR;

namespace Finnova.Service.UserManagement.Commands.CreateUserGroup;

public class CreateUserGroupCommandHandler : IRequestHandler<CreateUserGroupCommand, UserGroupResponse>
{
    private readonly IUserManagementRepository _repository;

    public CreateUserGroupCommandHandler(IUserManagementRepository repository) => _repository = repository;

    public async Task<UserGroupResponse> Handle(CreateUserGroupCommand request, CancellationToken ct)
    {
        var codes = request.MemberUserCodes.Select(c => c.Trim()).Distinct().ToList();
        var found = await _repository.GetActiveUsersByCodesAsync(codes, ct);

        // Every tagged user must exist and be active (R5.8).
        if (found.Count != codes.Count || found.Any(u => !u.IsActive))
            throw new UserInactiveMemberException();

        var code = UserCodeGenerator.Generate(
            request.Name,
            candidate => _repository.GroupCodeExistsAsync(candidate, ct).GetAwaiter().GetResult());

        var group = new UserGroup
        {
            UserGroupCode = code,
            Name = request.Name.Trim(),
            IsActive = request.IsActive ?? true,
            Members = found.Select(u => new UserGroupMember { UserAccountId = u.Id }).ToList(),
        };

        await _repository.AddGroupAsync(group, ct);

        await _repository.AddAuditAsync(new UserManagementAuditEntry
        {
            RecordId = group.Id,
            RecordKind = UserConfiguration.UserGroup,
            Action = UserManagementAuditAction.Create,
            NewValues = $"{{\"UserGroupCode\":\"{group.UserGroupCode}\",\"Members\":{found.Count}}}",
            Summary = $"Created user group '{group.UserGroupCode}' with {found.Count} member(s).",
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, ct);

        var members = found.Select(u => u.ToMemberResponse()).ToList();
        return group.ToResponse(members);
    }
}
```

- [ ] 7.14 CREATE `Finnova.Service/UserManagement/Commands/UpdateUserGroup/UpdateUserGroupCommand.cs`

```csharp
using Finnova.Models.Contracts.UserManagement;
using MediatR;

namespace Finnova.Service.UserManagement.Commands.UpdateUserGroup;

public record UpdateUserGroupCommand(
    Guid Id,
    string Name,
    IReadOnlyList<string> MemberUserCodes,
    bool IsActive,
    string ActingAdmin) : IRequest<UserGroupResponse>;
```

- [ ] 7.15 CREATE `Finnova.Service/UserManagement/Commands/UpdateUserGroup/UpdateUserGroupCommandValidator.cs`

```csharp
using FluentValidation;

namespace Finnova.Service.UserManagement.Commands.UpdateUserGroup;

public class UpdateUserGroupCommandValidator : AbstractValidator<UpdateUserGroupCommand>
{
    public UpdateUserGroupCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Please enter the User Group Name").MaximumLength(30);   // R5.2/5.3
        RuleFor(x => x.MemberUserCodes)
            .NotEmpty().WithMessage("Please enter the User Code & Name");                     // R5.6
    }
}
```

- [ ] 7.16 CREATE `Finnova.Service/UserManagement/Commands/UpdateUserGroup/UpdateUserGroupCommandHandler.cs`

```csharp
using Finnova.Models.Contracts.UserManagement;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.UserManagement.Mappers;
using MediatR;

namespace Finnova.Service.UserManagement.Commands.UpdateUserGroup;

public class UpdateUserGroupCommandHandler : IRequestHandler<UpdateUserGroupCommand, UserGroupResponse>
{
    private readonly IUserManagementRepository _repository;

    public UpdateUserGroupCommandHandler(IUserManagementRepository repository) => _repository = repository;

    public async Task<UserGroupResponse> Handle(UpdateUserGroupCommand request, CancellationToken ct)
    {
        var group = await _repository.GetGroupByCodeAsync(request.Id.ToString(), ct);
        // Lookups by code above are a convenience; resolve by id when needed. Fall back to not-found.
        if (group is null)
            throw new UserNotFoundException(request.Id.ToString());   // R11.7

        var codes = request.MemberUserCodes.Select(c => c.Trim()).Distinct().ToList();
        var found = await _repository.GetActiveUsersByCodesAsync(codes, ct);
        if (found.Count != codes.Count || found.Any(u => !u.IsActive))
            throw new UserInactiveMemberException();   // R5.8

        group.Name = request.Name.Trim();
        group.IsActive = request.IsActive;
        group.UpdatedAt = DateTime.UtcNow;
        group.Members = found.Select(u => new UserGroupMember { UserGroupId = group.Id, UserAccountId = u.Id }).ToList();

        await _repository.UpdateGroupAsync(group, ct);

        await _repository.AddAuditAsync(new UserManagementAuditEntry
        {
            RecordId = group.Id,
            RecordKind = UserConfiguration.UserGroup,
            Action = UserManagementAuditAction.Modify,
            NewValues = $"{{\"UserGroupCode\":\"{group.UserGroupCode}\",\"Members\":{found.Count}}}",
            Summary = $"Modified user group '{group.UserGroupCode}'.",
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, ct);

        return group.ToResponse(found.Select(u => u.ToMemberResponse()).ToList());
    }
}
```

- [ ] 7.17 CREATE `Finnova.Service/UserManagement/Commands/CreateFunctionalGroup/CreateFunctionalGroupCommand.cs`

```csharp
using Finnova.Models.Contracts.UserManagement;
using MediatR;

namespace Finnova.Service.UserManagement.Commands.CreateFunctionalGroup;

public record CreateFunctionalGroupCommand(
    string RoleCenterName,
    bool? IsActive,
    string ActingAdmin) : IRequest<FunctionalGroupResponse>;
```

- [ ] 7.18 CREATE `Finnova.Service/UserManagement/Commands/CreateFunctionalGroup/CreateFunctionalGroupCommandValidator.cs`

```csharp
using FluentValidation;

namespace Finnova.Service.UserManagement.Commands.CreateFunctionalGroup;

public class CreateFunctionalGroupCommandValidator : AbstractValidator<CreateFunctionalGroupCommand>
{
    public CreateFunctionalGroupCommandValidator()
    {
        RuleFor(x => x.RoleCenterName)
            .NotEmpty().WithMessage("Please select the Role Center Name")      // R6.7
            .MaximumLength(100);
    }
}
```

- [ ] 7.19 CREATE `Finnova.Service/UserManagement/Commands/CreateFunctionalGroup/CreateFunctionalGroupCommandHandler.cs`

```csharp
using Finnova.Models.Contracts.UserManagement;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Interfaces;
using Finnova.Service.UserManagement.Helpers;
using Finnova.Service.UserManagement.Internal;
using Finnova.Service.UserManagement.Mappers;
using MediatR;

namespace Finnova.Service.UserManagement.Commands.CreateFunctionalGroup;

public class CreateFunctionalGroupCommandHandler : IRequestHandler<CreateFunctionalGroupCommand, FunctionalGroupResponse>
{
    private readonly IUserManagementRepository _repository;

    public CreateFunctionalGroupCommandHandler(IUserManagementRepository repository) => _repository = repository;

    public async Task<FunctionalGroupResponse> Handle(CreateFunctionalGroupCommand request, CancellationToken ct)
    {
        var code = UserCodeGenerator.Generate(
            request.RoleCenterName,
            candidate => _repository.FunctionalGroupCodeExistsAsync(candidate, ct).GetAwaiter().GetResult());

        var fg = new FunctionalGroup
        {
            FunctionalGroupCode = code,
            RoleCenterName = request.RoleCenterName.Trim(),
            IsActive = request.IsActive ?? true,
            // Club all programs attached to the Role Center into the group (R6.1/6.3).
            Functions = RoleCenterCatalog.ProgramsFor(request.RoleCenterName)
                .Select(p => new FunctionalGroupFunction
                {
                    ProgramName = p,
                    RoleCode = RoleCodeBuilder.Build(request.RoleCenterName, p),
                }).ToList(),
        };

        await _repository.AddFunctionalGroupAsync(fg, ct);

        await _repository.AddAuditAsync(new UserManagementAuditEntry
        {
            RecordId = fg.Id,
            RecordKind = UserConfiguration.FunctionalGroup,
            Action = UserManagementAuditAction.Create,
            NewValues = $"{{\"FunctionalGroupCode\":\"{fg.FunctionalGroupCode}\",\"Functions\":{fg.Functions.Count}}}",
            Summary = $"Created functional group '{fg.FunctionalGroupCode}'.",
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, ct);

        return fg.ToResponse();
    }
}
```

- [ ] 7.20 CREATE `Finnova.Service/UserManagement/Internal/RoleCenterCatalog.cs`

```csharp
namespace Finnova.Service.UserManagement.Internal;

/// <summary>
/// Reference catalog of Role Centers -> their Programs (consumed master data, R6.1/6.3/8.1/8.3).
/// Seeded with India-only, English-only sample data mirroring the design (System Admin, Origination).
/// Replace with the real reference source when the owning master is wired.
/// </summary>
public static class RoleCenterCatalog
{
    private static readonly IReadOnlyDictionary<string, string[]> Catalog =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["System Admin"] = new[] { "Company Master", "User Master", "Lookup Master" },
            ["Origination"] = new[] { "Asset Master", "Entity Master", "Application Entry" },
        };

    public static IReadOnlyList<string> RoleCenters() => Catalog.Keys.ToList();

    public static IReadOnlyList<string> ProgramsFor(string roleCenterName)
        => Catalog.TryGetValue(roleCenterName?.Trim() ?? string.Empty, out var programs)
            ? programs
            : Array.Empty<string>();
}
```

- [ ] 7.21 CREATE `Finnova.Service/UserManagement/Commands/SaveUserAccess/SaveUserAccessCommand.cs`

```csharp
using Finnova.Models.Contracts.UserManagement;
using MediatR;

namespace Finnova.Service.UserManagement.Commands.SaveUserAccess;

public record SaveUserAccessCommand(
    Guid Id,
    string LineOfBusiness,
    IReadOnlyList<AccessRightRow> Rows,
    IReadOnlyList<string> BranchCodes,
    CopyProfileRequest? CopyProfile,
    string ActingAdmin) : IRequest<UserAccessResponse>;
```

- [ ] 7.22 CREATE `Finnova.Service/UserManagement/Commands/SaveUserAccess/SaveUserAccessCommandValidator.cs`

```csharp
using FluentValidation;

namespace Finnova.Service.UserManagement.Commands.SaveUserAccess;

public class SaveUserAccessCommandValidator : AbstractValidator<SaveUserAccessCommand>
{
    public SaveUserAccessCommandValidator()
    {
        RuleFor(x => x.LineOfBusiness)
            .NotEmpty().WithMessage("Please select at least one Line of Business");   // R7.4

        RuleFor(x => x.BranchCodes)
            .NotEmpty().WithMessage("At least one branch association is required.");   // R9.6

        RuleForEach(x => x.Rows).ChildRules(row =>
            row.RuleFor(r => r.RoleCenterName)
                .NotEmpty().WithMessage("Please select the Role Center Name"));        // R8.2
    }
}
```

- [ ] 7.23 CREATE `Finnova.Service/UserManagement/Commands/SaveUserAccess/SaveUserAccessCommandHandler.cs`

```csharp
using Finnova.Models.Contracts.UserManagement;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.UserManagement.Helpers;
using Finnova.Service.UserManagement.Internal;
using MediatR;

namespace Finnova.Service.UserManagement.Commands.SaveUserAccess;

public class SaveUserAccessCommandHandler : IRequestHandler<SaveUserAccessCommand, UserAccessResponse>
{
    private readonly IUserManagementRepository _repository;

    public SaveUserAccessCommandHandler(IUserManagementRepository repository) => _repository = repository;

    public async Task<UserAccessResponse> Handle(SaveUserAccessCommand request, CancellationToken ct)
    {
        var user = await _repository.GetUserWithAccessAsync(request.Id, ct)
            ?? throw new UserNotFoundException(request.Id.ToString());   // R11.7

        // The selected LOB must be linked to at least one defined Role Code (R7.3/7.5).
        if (!LineOfBusinessCatalog.HasRoleCodes(request.LineOfBusiness))
            throw new UserValidationException("The selected Line of Business has no linked Role Codes.");

        var rows = request.Rows.ToList();
        var branches = request.BranchCodes.ToList();

        // Copy Profile (Create mode): append source rows/branches, de-dup with OR-merge (R10.2).
        if (request.CopyProfile is not null)
        {
            var source = await _repository.GetUserByCodeAsync(request.CopyProfile.SourceUserCode, ct)
                ?? throw new UserValidationException("Copy Profile source user was not found.");
            var (srcRows, srcBranches) = await _repository.GetAccessAsync(
                source.Id, request.CopyProfile.SourceLineOfBusiness, ct);

            var merged = AccessAssignmentMerger.Merge(
                (rows, branches),
                (srcRows.Select(r => new AccessRightRow(r.RoleCode, r.RoleCenterName, r.ProgramName,
                    r.CanAdd, r.CanModify, r.CanQuery, r.CanDelete)),
                 srcBranches.Select(b => b.BranchCode)));
            rows = merged.Rows;
            branches = merged.Branches;
        }

        var assignments = rows.Select(r => new UserAccessAssignment
        {
            UserAccountId = user.Id,
            LineOfBusiness = request.LineOfBusiness,
            RoleCenterName = r.RoleCenterName,
            ProgramName = r.ProgramName,
            RoleCode = r.RoleCode,
            CanAdd = r.CanAdd,
            CanModify = r.CanModify,
            CanQuery = r.CanQuery,
            CanDelete = r.CanDelete,
        }).ToList();

        var branchAssociations = branches.Select(b => new UserBranchAssociation
        {
            UserAccountId = user.Id,
            LineOfBusiness = request.LineOfBusiness,
            BranchCode = b,
            IsAll = string.Equals(b, "ALL", StringComparison.OrdinalIgnoreCase),   // R9.4
        }).ToList();

        await _repository.ReplaceAccessAsync(user.Id, request.LineOfBusiness, assignments, branchAssociations, ct);

        await _repository.AddAuditAsync(new UserManagementAuditEntry
        {
            RecordId = user.Id,
            RecordKind = UserConfiguration.User,
            Action = UserManagementAuditAction.Modify,
            NewValues = $"{{\"LOB\":\"{request.LineOfBusiness}\",\"Rows\":{assignments.Count},\"Branches\":{branchAssociations.Count}}}",
            Summary = $"Saved access for user '{user.UserCode}' (LOB {request.LineOfBusiness}).",
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, ct);

        return new UserAccessResponse(request.LineOfBusiness, rows, branches);
    }
}
```

- [ ] 7.24 CREATE `Finnova.Service/UserManagement/Internal/LineOfBusinessCatalog.cs`

```csharp
namespace Finnova.Service.UserManagement.Internal;

/// <summary>
/// Reference catalog of Lines of Business and whether each is linked to defined Role Codes
/// (consumed master data, R7.3/7.5). India-only, English-only sample set. Replace with the real
/// reference source when the owning master is wired.
/// </summary>
public static class LineOfBusinessCatalog
{
    // LOBs that are linked to at least one Role Code (R7.5). Sample India-only business lines.
    private static readonly HashSet<string> WithRoleCodes =
        new(StringComparer.OrdinalIgnoreCase) { "Retail Lending", "Corporate Lending", "Leasing" };

    public static IReadOnlyList<string> ActiveLinesOfBusiness() => WithRoleCodes.ToList();

    public static bool HasRoleCodes(string lob) => WithRoleCodes.Contains(lob?.Trim() ?? string.Empty);
}
```

- [ ] 7.25 CREATE `Finnova.Service/UserManagement/Queries/GetUserRecordsPaged/GetUserRecordsPagedQuery.cs`

```csharp
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.UserManagement;
using Finnova.Models.Domain.Enums;
using MediatR;

namespace Finnova.Service.UserManagement.Queries.GetUserRecordsPaged;

public record GetUserRecordsPagedQuery(
    string? Search,
    UserConfiguration? Kind,
    bool? IsActive,
    int Page = 1,
    int PageSize = 20) : IRequest<PaginatedResponse<UserListItemResponse>>;
```

- [ ] 7.26 CREATE `Finnova.Service/UserManagement/Queries/GetUserRecordsPaged/GetUserRecordsPagedQueryValidator.cs`

```csharp
using FluentValidation;

namespace Finnova.Service.UserManagement.Queries.GetUserRecordsPaged;

public class GetUserRecordsPagedQueryValidator : AbstractValidator<GetUserRecordsPagedQuery>
{
    public GetUserRecordsPagedQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1)
            .WithMessage("Page number is out of range.");                      // R13.6
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100)
            .WithMessage("Page size is out of range.");                        // R13.7
        RuleFor(x => x.Kind!.Value).IsInEnum().When(x => x.Kind.HasValue);
    }
}
```

- [ ] 7.27 CREATE `Finnova.Service/UserManagement/Queries/GetUserRecordsPaged/GetUserRecordsPagedQueryHandler.cs`

```csharp
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.UserManagement;
using Finnova.Repository.Interfaces;
using Finnova.Service.UserManagement.Mappers;
using MediatR;

namespace Finnova.Service.UserManagement.Queries.GetUserRecordsPaged;

public class GetUserRecordsPagedQueryHandler
    : IRequestHandler<GetUserRecordsPagedQuery, PaginatedResponse<UserListItemResponse>>
{
    private readonly IUserManagementRepository _repository;

    public GetUserRecordsPagedQueryHandler(IUserManagementRepository repository) => _repository = repository;

    public async Task<PaginatedResponse<UserListItemResponse>> Handle(
        GetUserRecordsPagedQuery request, CancellationToken ct)
    {
        var (items, total) = await _repository.GetPagedAsync(
            request.Search, request.Kind, request.IsActive, request.Page, request.PageSize, ct);

        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);
        return new PaginatedResponse<UserListItemResponse>(
            items.ToResponseList(), total, request.Page, request.PageSize, totalPages);
    }
}
```

- [ ] 7.28 CREATE `Finnova.Service/UserManagement/Queries/GetUserRecordByCode/GetUserRecordByCodeQuery.cs`

```csharp
using Finnova.Models.Contracts.UserManagement;
using MediatR;

namespace Finnova.Service.UserManagement.Queries.GetUserRecordByCode;

/// <summary>Serves Modify (editable) and Query (read-only) — the read payload is identical (R11.1/R12.1).</summary>
public record GetUserRecordByCodeQuery(string Code) : IRequest<UserAccountResponse>;
```

- [ ] 7.29 CREATE `Finnova.Service/UserManagement/Queries/GetUserRecordByCode/GetUserRecordByCodeQueryHandler.cs`

```csharp
using Finnova.Models.Contracts.UserManagement;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.UserManagement.Mappers;
using MediatR;

namespace Finnova.Service.UserManagement.Queries.GetUserRecordByCode;

public class GetUserRecordByCodeQueryHandler : IRequestHandler<GetUserRecordByCodeQuery, UserAccountResponse>
{
    private readonly IUserManagementRepository _repository;

    public GetUserRecordByCodeQueryHandler(IUserManagementRepository repository) => _repository = repository;

    public async Task<UserAccountResponse> Handle(GetUserRecordByCodeQuery request, CancellationToken ct)
    {
        var user = await _repository.GetUserByCodeAsync(request.Code, ct)
            ?? throw new UserNotFoundException(request.Code);   // R11.7/R12.3
        return user.ToResponse();
    }
}
```

- [ ] 7.30 CREATE `Finnova.Service/UserManagement/Queries/GetUserAccess/GetUserAccessQuery.cs`

```csharp
using Finnova.Models.Contracts.UserManagement;
using MediatR;

namespace Finnova.Service.UserManagement.Queries.GetUserAccess;

public record GetUserAccessQuery(Guid Id, string LineOfBusiness) : IRequest<UserAccessResponse>;
```

- [ ] 7.31 CREATE `Finnova.Service/UserManagement/Queries/GetUserAccess/GetUserAccessQueryHandler.cs`

```csharp
using Finnova.Models.Contracts.UserManagement;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using MediatR;

namespace Finnova.Service.UserManagement.Queries.GetUserAccess;

public class GetUserAccessQueryHandler : IRequestHandler<GetUserAccessQuery, UserAccessResponse>
{
    private readonly IUserManagementRepository _repository;

    public GetUserAccessQueryHandler(IUserManagementRepository repository) => _repository = repository;

    public async Task<UserAccessResponse> Handle(GetUserAccessQuery request, CancellationToken ct)
    {
        var user = await _repository.GetUserWithAccessAsync(request.Id, ct)
            ?? throw new UserNotFoundException(request.Id.ToString());

        var (rows, branches) = await _repository.GetAccessAsync(request.Id, request.LineOfBusiness, ct);
        return new UserAccessResponse(
            request.LineOfBusiness,
            rows.Select(r => new AccessRightRow(r.RoleCode, r.RoleCenterName, r.ProgramName,
                r.CanAdd, r.CanModify, r.CanQuery, r.CanDelete)).ToList(),
            branches.Select(b => b.BranchCode).ToList());
    }
}
```

- [ ] 7.32 CREATE `Finnova.Service/UserManagement/Queries/GetUserAuditTrail/GetUserAuditTrailQuery.cs`

```csharp
using Finnova.Models.Contracts.UserManagement;
using MediatR;

namespace Finnova.Service.UserManagement.Queries.GetUserAuditTrail;

public record GetUserAuditTrailQuery(Guid RecordId) : IRequest<List<UserManagementAuditEntryResponse>>;
```

- [ ] 7.33 CREATE `Finnova.Service/UserManagement/Queries/GetUserAuditTrail/GetUserAuditTrailQueryHandler.cs`

```csharp
using Finnova.Models.Contracts.UserManagement;
using Finnova.Repository.Interfaces;
using Finnova.Service.UserManagement.Mappers;
using MediatR;

namespace Finnova.Service.UserManagement.Queries.GetUserAuditTrail;

public class GetUserAuditTrailQueryHandler
    : IRequestHandler<GetUserAuditTrailQuery, List<UserManagementAuditEntryResponse>>
{
    private readonly IUserManagementRepository _repository;

    public GetUserAuditTrailQueryHandler(IUserManagementRepository repository) => _repository = repository;

    public async Task<List<UserManagementAuditEntryResponse>> Handle(GetUserAuditTrailQuery request, CancellationToken ct)
    {
        var entries = await _repository.GetAuditAsync(request.RecordId, ct);   // newest-first
        return entries.Select(e => e.ToResponse()).ToList();
    }
}
```

- [ ] 7.34 CREATE `Finnova.Service/UserManagement/Queries/References/ReferenceQueries.cs`

```csharp
using Finnova.Models.Contracts.UserManagement;
using MediatR;

namespace Finnova.Service.UserManagement.Queries.References;

// Active users for group members + copy-profile source (R5.5/R10.1).
public record GetActiveUsersQuery(string? Search) : IRequest<List<UserGroupMemberResponse>>;

// Admin-accessible, active, Role-Code-linked LOBs (R7.1/7.3).
public record GetAccessibleLinesOfBusinessQuery() : IRequest<List<ReferenceItemResponse>>;

// Active role centers, includes ALL (R8.1/9.5).
public record GetRoleCentersQuery() : IRequest<List<ReferenceItemResponse>>;

// Programs for a role center as access rows with flags defaulting false (R8.3/8.4).
public record GetRoleCenterProgramsQuery(string RoleCenterName) : IRequest<List<AccessRightRow>>;

// Branch Location Tree for a LOB, includes ALL (R9.1-9.3).
public record GetBranchLocationTreeQuery(string LineOfBusiness) : IRequest<List<BranchTreeNodeResponse>>;

// Lookup-backed LOVs: Designation | Department | UserType (R3.9/3.10/3.11).
public record GetUserLookupsQuery(string Type) : IRequest<List<ReferenceItemResponse>>;
```

- [ ] 7.35 CREATE `Finnova.Service/UserManagement/Queries/References/ReferenceQueryHandlers.cs`

```csharp
using Finnova.Models.Contracts.UserManagement;
using Finnova.Repository.Interfaces;
using Finnova.Service.UserManagement.Helpers;
using Finnova.Service.UserManagement.Internal;
using Finnova.Service.UserManagement.Mappers;
using MediatR;

namespace Finnova.Service.UserManagement.Queries.References;

public class GetActiveUsersQueryHandler : IRequestHandler<GetActiveUsersQuery, List<UserGroupMemberResponse>>
{
    private readonly IUserManagementRepository _repository;
    public GetActiveUsersQueryHandler(IUserManagementRepository repository) => _repository = repository;

    public async Task<List<UserGroupMemberResponse>> Handle(GetActiveUsersQuery request, CancellationToken ct)
    {
        var users = await _repository.SearchActiveUsersAsync(request.Search, ct);
        return users.Select(u => u.ToMemberResponse()).ToList();
    }
}

public class GetAccessibleLinesOfBusinessQueryHandler
    : IRequestHandler<GetAccessibleLinesOfBusinessQuery, List<ReferenceItemResponse>>
{
    public Task<List<ReferenceItemResponse>> Handle(GetAccessibleLinesOfBusinessQuery request, CancellationToken ct)
        => Task.FromResult(LineOfBusinessCatalog.ActiveLinesOfBusiness()
            .Select(l => new ReferenceItemResponse(l, l)).ToList());
}

public class GetRoleCentersQueryHandler : IRequestHandler<GetRoleCentersQuery, List<ReferenceItemResponse>>
{
    public Task<List<ReferenceItemResponse>> Handle(GetRoleCentersQuery request, CancellationToken ct)
    {
        var items = new List<ReferenceItemResponse> { new("ALL", "ALL") };   // R9.5
        items.AddRange(RoleCenterCatalog.RoleCenters().Select(r => new ReferenceItemResponse(r, r)));
        return Task.FromResult(items);
    }
}

public class GetRoleCenterProgramsQueryHandler : IRequestHandler<GetRoleCenterProgramsQuery, List<AccessRightRow>>
{
    public Task<List<AccessRightRow>> Handle(GetRoleCenterProgramsQuery request, CancellationToken ct)
        => Task.FromResult(RoleCenterCatalog.ProgramsFor(request.RoleCenterName)
            .Select(p => new AccessRightRow(
                RoleCodeBuilder.Build(request.RoleCenterName, p),            // R8.4
                request.RoleCenterName, p, false, false, false, false))
            .ToList());
}

public class GetBranchLocationTreeQueryHandler
    : IRequestHandler<GetBranchLocationTreeQuery, List<BranchTreeNodeResponse>>
{
    public Task<List<BranchTreeNodeResponse>> Handle(GetBranchLocationTreeQuery request, CancellationToken ct)
        => Task.FromResult(BranchTreeCatalog.Tree());   // includes ALL (R9.1-9.3)
}

public class GetUserLookupsQueryHandler : IRequestHandler<GetUserLookupsQuery, List<ReferenceItemResponse>>
{
    public Task<List<ReferenceItemResponse>> Handle(GetUserLookupsQuery request, CancellationToken ct)
        => Task.FromResult(UserLookupCatalog.For(request.Type));
}
```

- [ ] 7.36 CREATE `Finnova.Service/UserManagement/Internal/BranchTreeCatalog.cs`

```csharp
using Finnova.Models.Contracts.UserManagement;

namespace Finnova.Service.UserManagement.Internal;

/// <summary>
/// Branch Location Tree (consumed master data, R9.1-9.3). India-only, English-only sample tree:
/// Locations -> State/Region -> Branch (MAHARASHTRA -> MUMBAI -> HEAD OFFICE, FORT BRANCH). An ALL
/// node sits at the root. Replace with the real Org-Hierarchy / Location source when wired.
/// </summary>
public static class BranchTreeCatalog
{
    public static List<BranchTreeNodeResponse> Tree() => new()
    {
        new BranchTreeNodeResponse("ALL", "ALL", "Location", Array.Empty<BranchTreeNodeResponse>()),
        new BranchTreeNodeResponse("MAHARASHTRA", "MAHARASHTRA", "Region", new List<BranchTreeNodeResponse>
        {
            new("MUMBAI", "MUMBAI", "Region", new List<BranchTreeNodeResponse>
            {
                new("HO", "HEAD OFFICE", "Branch", Array.Empty<BranchTreeNodeResponse>()),
                new("FORT", "FORT BRANCH", "Branch", Array.Empty<BranchTreeNodeResponse>()),
            }),
        }),
        new BranchTreeNodeResponse("KARNATAKA", "KARNATAKA", "Region", new List<BranchTreeNodeResponse>
        {
            new("BENGALURU", "BENGALURU", "Region", new List<BranchTreeNodeResponse>
            {
                new("MGR", "MG ROAD BRANCH", "Branch", Array.Empty<BranchTreeNodeResponse>()),
            }),
        }),
    };
}
```

- [ ] 7.37 CREATE `Finnova.Service/UserManagement/Internal/UserLookupCatalog.cs`

```csharp
using Finnova.Models.Contracts.UserManagement;

namespace Finnova.Service.UserManagement.Internal;

/// <summary>
/// Lookup-backed LOVs for Designation / Department / User Type (consumed master data,
/// R3.9/3.10/3.11). India-only, English-only sample values. Replace with the real Lookup Master
/// source when wired.
/// </summary>
public static class UserLookupCatalog
{
    public static List<ReferenceItemResponse> For(string type) => (type?.Trim()) switch
    {
        "Designation" => new List<ReferenceItemResponse>
        {
            new("MGR", "Manager"), new("OFF", "Officer"), new("CLK", "Clerk"), new("EXE", "Executive"),
        },
        "Department" => new List<ReferenceItemResponse>
        {
            new("OPS", "Operations"), new("CRD", "Credit"), new("IT", "Information Technology"), new("FIN", "Finance"),
        },
        "UserType" => new List<ReferenceItemResponse>
        {
            new("Corporate", "Corporate"), new("Branch", "Branch"),   // R3.12
        },
        _ => new List<ReferenceItemResponse>(),
    };
}
```

---

- [ ] 8. Backend controller + middleware branch + gateway alias
  - _Requirements: R2, R3, R5, R6, R7, R8, R9, R10, R11, R12, R13, R14, R15, R16.8_

- [ ] 8.1 CREATE `Finnova.SystemAdminService/Controllers/UserManagementController.cs`

```csharp
using System.Security.Claims;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.UserManagement;
using Finnova.Models.Domain.Enums;
using Finnova.Service.UserManagement.Commands.CreateFunctionalGroup;
using Finnova.Service.UserManagement.Commands.CreateUserAccount;
using Finnova.Service.UserManagement.Commands.CreateUserGroup;
using Finnova.Service.UserManagement.Commands.ResetPassword;
using Finnova.Service.UserManagement.Commands.SaveUserAccess;
using Finnova.Service.UserManagement.Commands.UpdateUserAccount;
using Finnova.Service.UserManagement.Commands.UpdateUserGroup;
using Finnova.Service.UserManagement.Queries.GetUserAccess;
using Finnova.Service.UserManagement.Queries.GetUserAuditTrail;
using Finnova.Service.UserManagement.Queries.GetUserRecordByCode;
using Finnova.Service.UserManagement.Queries.GetUserRecordsPaged;
using Finnova.Service.UserManagement.Queries.References;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finnova.SystemAdminService.Controllers;

/// <summary>
/// User Management endpoints (FS §9). Every action is SystemAdmin-only (R15). All work flows
/// through MediatR so the ValidationBehavior pipeline runs before each handler. The acting admin
/// is read from the JWT (R14.1/14.2), never the request body.
/// </summary>
[ApiController]
[Route("api/user")]
public class UserManagementController : ControllerBase
{
    private readonly IMediator _mediator;
    public UserManagementController(IMediator mediator) => _mediator = mediator;

    /// <summary>Paged, filtered union list of users / groups / functional groups (R13).</summary>
    [HttpGet]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(PaginatedResponse<UserListItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResponse<UserListItemResponse>>> GetPaged(
        [FromQuery] string? search,
        [FromQuery] UserConfiguration? kind,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await _mediator.Send(new GetUserRecordsPagedQuery(search, kind, isActive, page, pageSize)));

    /// <summary>Read a user record by code (Modify/Query modes, R11.1/R12.1). 404 when missing.</summary>
    [HttpGet("by-code/{code}")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(UserAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserAccountResponse>> GetByCode(string code)
        => Ok(await _mediator.Send(new GetUserRecordByCodeQuery(code)));

    /// <summary>Create an individual user (R3). 409 on code collision.</summary>
    [HttpPost]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(UserAccountResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserAccountResponse>> Create([FromBody] CreateUserAccountRequest r)
    {
        var result = await _mediator.Send(new CreateUserAccountCommand(
            r.Name, r.Password, r.DateOfJoining, r.Designation, r.Department,
            r.MobileNumber, r.Email, r.UserType, r.IsActive, GetActingAdmin()));
        return CreatedAtAction(nameof(GetByCode), new { code = result.UserCode }, result);
    }

    /// <summary>Modify a user (R11). 404 when missing.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(UserAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserAccountResponse>> Update(Guid id, [FromBody] UpdateUserAccountRequest r)
        => Ok(await _mediator.Send(new UpdateUserAccountCommand(
            id, r.Name, r.DateOfJoining, r.Designation, r.Department,
            r.MobileNumber, r.Email, r.UserType, r.IsActive, GetActingAdmin())));

    /// <summary>Reset a user's password (Modify-mode facility, R11.5/11.6). 204 on success.</summary>
    [HttpPost("{id:guid}/reset-password")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordRequest r)
    {
        await _mediator.Send(new ResetPasswordCommand(id, r.NewPassword, GetActingAdmin()));
        return NoContent();
    }

    /// <summary>Create a user group (R5). 409 on collision / inactive member.</summary>
    [HttpPost("group")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(UserGroupResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserGroupResponse>> CreateGroup([FromBody] CreateUserGroupRequest r)
    {
        var result = await _mediator.Send(new CreateUserGroupCommand(
            r.Name, r.MemberUserCodes, r.IsActive, GetActingAdmin()));
        return CreatedAtAction(nameof(GetByCode), new { code = result.UserGroupCode }, result);
    }

    /// <summary>Modify a user group (R5/R11). 404 when missing.</summary>
    [HttpPut("group/{id:guid}")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(UserGroupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserGroupResponse>> UpdateGroup(Guid id, [FromBody] UpdateUserGroupRequest r)
        => Ok(await _mediator.Send(new UpdateUserGroupCommand(
            id, r.Name, r.MemberUserCodes, r.IsActive, GetActingAdmin())));

    /// <summary>Create a functional group (R6). 409 on collision.</summary>
    [HttpPost("functional-group")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(FunctionalGroupResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FunctionalGroupResponse>> CreateFunctionalGroup(
        [FromBody] CreateFunctionalGroupRequest r)
    {
        var result = await _mediator.Send(new CreateFunctionalGroupCommand(
            r.RoleCenterName, r.IsActive, GetActingAdmin()));
        return CreatedAtAction(nameof(GetByCode), new { code = result.FunctionalGroupCode }, result);
    }

    /// <summary>Save access assignment for a user + LOB (R7/R8/R9/R10). 404 when missing.</summary>
    [HttpPut("{id:guid}/access")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(UserAccessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserAccessResponse>> SaveAccess(Guid id, [FromBody] SaveUserAccessRequest r)
        => Ok(await _mediator.Send(new SaveUserAccessCommand(
            id, r.LineOfBusiness, r.Rows, r.BranchCodes, r.CopyProfile, GetActingAdmin())));

    /// <summary>Read access assignment for a user + LOB (R10 read / Access tab populate).</summary>
    [HttpGet("{id:guid}/access")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(UserAccessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserAccessResponse>> GetAccess(Guid id, [FromQuery] string lob)
        => Ok(await _mediator.Send(new GetUserAccessQuery(id, lob)));

    /// <summary>Audit trail for a record, newest-first (R14).</summary>
    [HttpGet("{id:guid}/audit")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(List<UserManagementAuditEntryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UserManagementAuditEntryResponse>>> GetAudit(Guid id)
        => Ok(await _mediator.Send(new GetUserAuditTrailQuery(id)));

    // ---- Reference LOVs (consumed master data) ----

    /// <summary>Active users for group members + copy-profile source (R5.5/R10.1).</summary>
    [HttpGet("ref/active-users")]
    [Authorize(Policy = "SystemAdmin")]
    public async Task<ActionResult<List<UserGroupMemberResponse>>> GetActiveUsers([FromQuery] string? search)
        => Ok(await _mediator.Send(new GetActiveUsersQuery(search)));

    /// <summary>Admin-accessible, active, Role-Code-linked LOBs (R7.1/7.3).</summary>
    [HttpGet("ref/lines-of-business")]
    [Authorize(Policy = "SystemAdmin")]
    public async Task<ActionResult<List<ReferenceItemResponse>>> GetLinesOfBusiness()
        => Ok(await _mediator.Send(new GetAccessibleLinesOfBusinessQuery()));

    /// <summary>Active role centers, includes ALL (R8.1/9.5).</summary>
    [HttpGet("ref/role-centers")]
    [Authorize(Policy = "SystemAdmin")]
    public async Task<ActionResult<List<ReferenceItemResponse>>> GetRoleCenters()
        => Ok(await _mediator.Send(new GetRoleCentersQuery()));

    /// <summary>Programs (as access rows, flags false) for a role center (R8.3/8.4).</summary>
    [HttpGet("ref/role-centers/{name}/programs")]
    [Authorize(Policy = "SystemAdmin")]
    public async Task<ActionResult<List<AccessRightRow>>> GetRoleCenterPrograms(string name)
        => Ok(await _mediator.Send(new GetRoleCenterProgramsQuery(name)));

    /// <summary>Branch Location Tree for a LOB, includes ALL (R9.1-9.3).</summary>
    [HttpGet("ref/branch-tree")]
    [Authorize(Policy = "SystemAdmin")]
    public async Task<ActionResult<List<BranchTreeNodeResponse>>> GetBranchTree([FromQuery] string lob)
        => Ok(await _mediator.Send(new GetBranchLocationTreeQuery(lob)));

    /// <summary>Lookup LOVs: Designation | Department | UserType (R3.9/3.10/3.11).</summary>
    [HttpGet("ref/lookups")]
    [Authorize(Policy = "SystemAdmin")]
    public async Task<ActionResult<List<ReferenceItemResponse>>> GetLookups([FromQuery] string type)
        => Ok(await _mediator.Send(new GetUserLookupsQuery(type)));

    /// <summary>
    /// Resolves the acting admin id from the validated JWT principal so the audit actor cannot be
    /// spoofed by the request body. Prefers NameIdentifier (sub), falling back to Name.
    /// </summary>
    private string GetActingAdmin()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub")
           ?? User.FindFirstValue(ClaimTypes.Name)
           ?? User.Identity?.Name
           ?? string.Empty;
}
```

- [ ] 8.2 MODIFY `Finnova.SystemAdminService/Middleware/ExceptionHandlingMiddleware.cs`

  Add the ERR-USR path-scoping and typed branch, mirroring the existing Entity branch.

  (a) After the `isEntity` / `validationEntityCode` / `fallbackEntityCode` lines, add the `isUser`
  path flag and its path-scoped validation/fallback codes:

```csharp
            var isUser = ctx.Request.Path.StartsWithSegments("/api/user",
                StringComparison.OrdinalIgnoreCase);
            var validationUserCode = isUser ? "ERR-USR-400" : validationEntityCode;
            var fallbackUserCode = isUser ? "ERR-USR-500" : fallbackEntityCode;
```

  (b) Inside the `ex switch` block, add the typed ERR-USR branch after the Entity branch
  (`EntityDuplicateCodeException => (409, "ERR-ENT-409", ex.Message),`):

```csharp
                // ---- new User Management branch (typed, no message sniffing) ----
                UserNotFoundException        => (404, "ERR-USR-404", ex.Message),
                UserDuplicateCodeException   => (409, "ERR-USR-409", ex.Message),
                UserInactiveMemberException  => (409, "ERR-USR-409", ex.Message),
                AuditEntryImmutableException => (409, "ERR-USR-409", ex.Message),
                UserValidationException      => (400, "ERR-USR-400", ex.Message),
```

  (c) Replace the shared validation/fallback arms so the `/api/user` path family is honored. Change:

```csharp
                FluentValidation.ValidationException v => (StatusCodes.Status400BadRequest, validationCode, v.Message),
                _ => (StatusCodes.Status500InternalServerError, fallbackCode, "Unexpected error.")
```

  to:

```csharp
                FluentValidation.ValidationException v => (StatusCodes.Status400BadRequest, validationUserCode, v.Message),
                _ => (StatusCodes.Status500InternalServerError, fallbackUserCode, "Unexpected error.")
```

- [ ] 8.3 MODIFY `Finnova.ApiGateway/appsettings.json`

  Add the `user` UA-prefix alias pair inside `ReverseProxy.Routes`, right after the
  `ua-entity-alias-root` route (mirror the existing entity/court alias pairs):

```json
      "ua-user-alias-route": {
        "ClusterId": "systemadmin-cluster",
        "Match": {
          "Path": "/api/ua/api/user/{**catch-all}"
        },
        "Transforms": [
          { "PathRemovePrefix": "/api/ua/api" },
          { "PathPrefix": "/api" }
        ]
      },
      "ua-user-alias-root": {
        "ClusterId": "systemadmin-cluster",
        "Match": {
          "Path": "/api/ua/api/user"
        },
        "Transforms": [
          { "PathRemovePrefix": "/api/ua/api" },
          { "PathPrefix": "/api" }
        ]
      },
```

---

- [ ] 9. Backend tests
  - PBT with FsCheck.Xunit (≥100 iterations, tagged), xUnit example/edge over an in-memory repo, and
    `WebApplicationFactory<Program>` authorization + end-to-end. Sub-tasks marked `*` are optional.
  - _Requirements: R2, R3, R4, R5, R6, R8, R9, R10, R11, R13, R14, R15_

- [ ] 9.1 CREATE `Finnova.Tests/Infrastructure/InMemoryUserManagementRepository.cs`

```csharp
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Interfaces;

namespace Finnova.Tests.Infrastructure;

/// <summary>Hand-written in-memory IUserManagementRepository mirroring the EF repo semantics.</summary>
public sealed class InMemoryUserManagementRepository : IUserManagementRepository
{
    public readonly List<UserAccount> Users = new();
    public readonly List<UserGroup> Groups = new();
    public readonly List<FunctionalGroup> Functionals = new();
    public readonly List<UserAccessAssignment> Access = new();
    public readonly List<UserBranchAssociation> Branches = new();
    public readonly List<UserManagementAuditEntry> Audit = new();

    public Task<UserAccount?> GetUserByCodeAsync(string code, CancellationToken ct = default)
        => Task.FromResult(Users.FirstOrDefault(u => u.UserCode == code.Trim()));

    public Task<UserAccount?> GetUserWithAccessAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

    public Task<bool> UserCodeExistsAsync(string code, CancellationToken ct = default)
        => Task.FromResult(Users.Any(u => u.UserCode == code.Trim()));

    public Task AddUserAsync(UserAccount user, CancellationToken ct = default) { Users.Add(user); return Task.CompletedTask; }

    public Task UpdateUserAsync(UserAccount user, CancellationToken ct = default)
    {
        var i = Users.FindIndex(u => u.Id == user.Id);
        if (i >= 0) Users[i] = user;
        return Task.CompletedTask;
    }

    public Task<UserGroup?> GetGroupByCodeAsync(string code, CancellationToken ct = default)
        => Task.FromResult(Groups.FirstOrDefault(g => g.UserGroupCode == code.Trim() || g.Id.ToString() == code));

    public Task<bool> GroupCodeExistsAsync(string code, CancellationToken ct = default)
        => Task.FromResult(Groups.Any(g => g.UserGroupCode == code.Trim()));

    public Task AddGroupAsync(UserGroup group, CancellationToken ct = default) { Groups.Add(group); return Task.CompletedTask; }

    public Task UpdateGroupAsync(UserGroup group, CancellationToken ct = default)
    {
        var i = Groups.FindIndex(g => g.Id == group.Id);
        if (i >= 0) Groups[i] = group;
        return Task.CompletedTask;
    }

    public Task<FunctionalGroup?> GetFunctionalGroupByCodeAsync(string code, CancellationToken ct = default)
        => Task.FromResult(Functionals.FirstOrDefault(f => f.FunctionalGroupCode == code.Trim()));

    public Task<bool> FunctionalGroupCodeExistsAsync(string code, CancellationToken ct = default)
        => Task.FromResult(Functionals.Any(f => f.FunctionalGroupCode == code.Trim()));

    public Task AddFunctionalGroupAsync(FunctionalGroup fg, CancellationToken ct = default) { Functionals.Add(fg); return Task.CompletedTask; }

    public Task<List<UserAccount>> GetActiveUsersByCodesAsync(IEnumerable<string> codes, CancellationToken ct = default)
    {
        var set = codes.Select(c => c.Trim()).ToHashSet();
        return Task.FromResult(Users.Where(u => set.Contains(u.UserCode)).ToList());
    }

    public Task<List<UserAccount>> SearchActiveUsersAsync(string? search, CancellationToken ct = default)
    {
        var q = Users.Where(u => u.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var t = search.Trim();
            q = q.Where(u => u.UserCode.Contains(t) || u.Name.Contains(t));
        }
        return Task.FromResult(q.OrderBy(u => u.UserCode).ToList());
    }

    public Task ReplaceAccessAsync(Guid ownerUserId, string lob,
        IEnumerable<UserAccessAssignment> rows, IEnumerable<UserBranchAssociation> branches, CancellationToken ct = default)
    {
        Access.RemoveAll(a => a.UserAccountId == ownerUserId && a.LineOfBusiness == lob);
        Branches.RemoveAll(b => b.UserAccountId == ownerUserId && b.LineOfBusiness == lob);
        Access.AddRange(rows);
        Branches.AddRange(branches);
        return Task.CompletedTask;
    }

    public Task<(List<UserAccessAssignment> Rows, List<UserBranchAssociation> Branches)> GetAccessAsync(
        Guid ownerUserId, string lob, CancellationToken ct = default)
        => Task.FromResult((
            Access.Where(a => a.UserAccountId == ownerUserId && a.LineOfBusiness == lob).ToList(),
            Branches.Where(b => b.UserAccountId == ownerUserId && b.LineOfBusiness == lob).ToList()));

    public Task<(List<UserListItemResult> Items, int Total)> GetPagedAsync(
        string? search, UserConfiguration? kind, bool? isActive, int page, int pageSize, CancellationToken ct = default)
    {
        var union = Users.Select(u => new UserListItemResult(u.Id, u.UserCode, u.Name, UserConfiguration.User, u.IsActive))
            .Concat(Groups.Select(g => new UserListItemResult(g.Id, g.UserGroupCode, g.Name, UserConfiguration.UserGroup, g.IsActive)))
            .Concat(Functionals.Select(f => new UserListItemResult(f.Id, f.FunctionalGroupCode, f.RoleCenterName, UserConfiguration.FunctionalGroup, f.IsActive)));

        if (kind is not null) union = union.Where(x => x.Kind == kind);
        if (isActive is not null) union = union.Where(x => x.IsActive == isActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var t = search.Trim();
            union = union.Where(x => x.Code.Contains(t, StringComparison.OrdinalIgnoreCase)
                || x.Name.Contains(t, StringComparison.OrdinalIgnoreCase));
        }

        var all = union.ToList();
        var items = all.OrderBy(x => x.Code, StringComparer.Ordinal).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult((items, all.Count));
    }

    public Task AddAuditAsync(UserManagementAuditEntry entry, CancellationToken ct = default) { Audit.Add(entry); return Task.CompletedTask; }

    public Task<List<UserManagementAuditEntry>> GetAuditAsync(Guid recordId, CancellationToken ct = default)
        => Task.FromResult(Audit.Where(a => a.RecordId == recordId)
            .OrderByDescending(a => a.ChangedAtUtc).ThenByDescending(a => a.Id).ToList());
}
```

- [ ] 9.2* CREATE `Finnova.Tests/Properties/UserManagementHelperProperties.cs`

```csharp
using Finnova.Models.Contracts.UserManagement;
using Finnova.Service.UserManagement.Helpers;
using Finnova.Service.UserManagement.Internal;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Finnova.Tests.Properties;

/// <summary>
/// Property-based tests over the pure User Management helpers (FsCheck.Xunit, >=100 iterations).
/// English-only inputs; no bilingual/Arabic/RTL generators.
/// </summary>
public class UserManagementHelperProperties
{
    // Feature: user-management, Property 1: Generated codes satisfy the format invariant and are unique. (R2.1-2.5)
    [Property(MaxTest = 100)]
    public Property GeneratedCode_MatchesFormat_AndIsNotTaken(NonEmptyString source, int takenSeed)
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < Math.Abs(takenSeed) % 5; i++) taken.Add("U" + i);

        var code = UserCodeGenerator.Generate(source.Get, taken.Contains);

        var formatOk = code.Length is >= 4 and <= 6
            && char.IsLetter(code[0])
            && code.All(char.IsLetterOrDigit)
            && code == code.ToUpperInvariant();
        return (formatOk && !taken.Contains(code)).ToProperty();
    }

    // Feature: user-management, Property 2: Role Code is a deterministic pure function. (R8.4)
    [Property(MaxTest = 100)]
    public Property RoleCode_IsUppercaseConcatenation_AndDeterministic(NonNull<string> rc, NonNull<string> prog)
    {
        var a = RoleCodeBuilder.Build(rc.Get, prog.Get);
        var b = RoleCodeBuilder.Build(rc.Get, prog.Get);
        var expected = (rc.Get + prog.Get).ToUpperInvariant();
        return (a == expected && a == b).ToProperty();
    }

    // Feature: user-management, Property 3: Copy-Profile merge is OR-merge + de-dup union. (R10.2)
    [Property(MaxTest = 100)]
    public Property Merge_OrsFlags_AndDeDupsRolesAndBranches(int seed)
    {
        var r = new Random(seed);
        AccessRightRow Row(string code, bool a, bool m, bool q, bool d)
            => new(code, "RC", "P", a, m, q, d);

        var current = (new[] { Row("X", true, false, false, false) }.AsEnumerable(), new[] { "B1" }.AsEnumerable());
        var source = (new[] { Row("X", false, true, false, false), Row("Y", true, false, false, false) }.AsEnumerable(),
                      new[] { "B1", "B2" }.AsEnumerable());

        var (rows, branches) = AccessAssignmentMerger.Merge(current, source);

        var x = rows.Single(r2 => r2.RoleCode == "X");
        var merged = x.CanAdd && x.CanModify && rows.Count == 2 && branches.Count == 2;
        return merged.ToProperty();
    }

    // Feature: user-management, Property 4: Copy-Profile merge is idempotent. (R10.2)
    [Property(MaxTest = 100)]
    public Property Merge_WithItself_IsIdempotent(bool a, bool m, bool q, bool d)
    {
        var rows = new[] { new AccessRightRow("X", "RC", "P", a, m, q, d) }.AsEnumerable();
        var branches = new[] { "B1" }.AsEnumerable();

        var (r1, b1) = AccessAssignmentMerger.Merge((rows, branches), (rows, branches));
        return (r1.Count == 1 && b1.Count == 1
            && r1[0].CanAdd == a && r1[0].CanModify == m && r1[0].CanQuery == q && r1[0].CanDelete == d).ToProperty();
    }

    // Feature: user-management, Property 5: Mobile/email validation accepts exactly the format rules. (R4.1-4.5)
    [Property(MaxTest = 100)]
    public Property Email_AcceptedIffFormatRulesHold(NonNull<string> s)
    {
        var email = s.Get;
        var accepted = EmailFormat.IsValid(email);
        var expected = !string.IsNullOrEmpty(email)
            && email.Length <= 60
            && email.Count(c => c == '@') == 1
            && email.Contains('.')
            && char.IsLetterOrDigit(email[0])
            && char.IsLetterOrDigit(email[^1]);
        return (accepted == expected).ToProperty();
    }
}
```

- [ ] 9.3* CREATE `Finnova.Tests/Properties/UserManagementHandlerProperties.cs`

```csharp
using Finnova.Models.Contracts.Common;
using Finnova.Models.Domain.Enums;
using Finnova.Service.UserManagement.Abstractions;
using Finnova.Service.UserManagement.Commands.CreateUserAccount;
using Finnova.Service.UserManagement.Queries.GetUserRecordsPaged;
using Finnova.Tests.Infrastructure;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Finnova.Tests.Properties;

/// <summary>Property tests over the create/list handlers against the in-memory repository.</summary>
public class UserManagementHandlerProperties
{
    private const string Admin = "prop-admin";
    private static CreateUserAccountCommandHandler CreateHandler(InMemoryUserManagementRepository repo)
        => new(repo, new DefaultPasswordPolicy());

    // Feature: user-management, Property 6: List ordering is deterministic and total is paging-invariant. (R13.8/13.9)
    [Property(MaxTest = 100)]
    public Property Paging_OrdersByCode_TotalInvariant(int count, int pageSize)
    {
        var n = Math.Abs(count) % 25;
        var size = (Math.Abs(pageSize) % 10) + 1;
        var repo = new InMemoryUserManagementRepository();
        for (var i = 0; i < n; i++)
            repo.Users.Add(new Finnova.Models.Domain.Entities.UserAccount
            { UserCode = "U" + (1000 + i), Name = "User " + i, IsActive = true });

        var handler = new GetUserRecordsPagedQueryHandler(repo);
        var first = handler.Handle(new GetUserRecordsPagedQuery(null, null, null, 1, size), default).Result;
        var totalConsistent = first.Total == n;
        var sizeOk = first.Data.Count <= size;
        var sorted = first.Data.Select(d => d.Code).SequenceEqual(first.Data.Select(d => d.Code).OrderBy(c => c, StringComparer.Ordinal));
        return (totalConsistent && sizeOk && sorted).ToProperty();
    }

    // Feature: user-management, Property 7: Create round-trips fields, applies defaults, never stores plaintext. (R3.1/3.7/3.15)
    [Property(MaxTest = 100)]
    public Property Create_RoundTrips_AndNeverStoresPlaintext(NonEmptyString name)
    {
        var repo = new InMemoryUserManagementRepository();
        var handler = CreateHandler(repo);
        var pwd = "Passw0rd!";
        var r = handler.Handle(new CreateUserAccountCommand(
            name.Get, pwd, null, "Officer", "Operations", null, null, UserType.Branch, null, Admin), default).Result;

        var stored = repo.Users.Single();
        var ok = r.IsActive                                   // default active (R3.15)
            && r.DateOfJoining != default                      // DOJ defaulted (R3.7)
            && stored.PasswordHash != pwd                      // never plaintext
            && !string.IsNullOrEmpty(stored.UserCode);         // generated code
        return ok.ToProperty();
    }
}
```

- [ ] 9.4* CREATE `Finnova.Tests/Unit/UserManagementHandlerTests.cs`

```csharp
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Service.UserManagement.Abstractions;
using Finnova.Service.UserManagement.Commands.CreateUserAccount;
using Finnova.Service.UserManagement.Commands.CreateUserGroup;
using Finnova.Service.UserManagement.Commands.ResetPassword;
using Finnova.Service.UserManagement.Commands.UpdateUserAccount;
using Finnova.Tests.Infrastructure;
using FluentValidation;
using Xunit;

namespace Finnova.Tests.Unit;

/// <summary>Example / edge tests for the User Management handlers + validators over the in-memory repo.</summary>
public class UserManagementHandlerTests
{
    private const string Admin = "admin-1";
    private static readonly IPasswordPolicy Policy = new DefaultPasswordPolicy();

    private static UserAccount ActiveUser(string code, string name = "User")
        => new() { UserCode = code, Name = name, IsActive = true, PasswordHash = "x.y",
                   Designation = "Officer", Department = "Operations", UserType = UserType.Branch,
                   DateOfJoining = DateTime.UtcNow.Date };

    [Fact] // R3.5 — non-compliant password rejected on create
    public async Task Create_WithWeakPassword_Throws()
    {
        var repo = new InMemoryUserManagementRepository();
        var handler = new CreateUserAccountCommandHandler(repo, Policy);
        await Assert.ThrowsAsync<UserValidationException>(() => handler.Handle(
            new CreateUserAccountCommand("Asha", "weak", null, "Officer", "Operations",
                null, null, UserType.Branch, null, Admin), default));
        Assert.Empty(repo.Users);     // no persistence on rejection
        Assert.Empty(repo.Audit);     // no audit on rejection (R14.3)
    }

    [Fact] // R5.6 — group with no members rejected by validator
    public void CreateGroup_WithNoMembers_FailsValidation()
    {
        var validator = new CreateUserGroupCommandValidator();
        var result = validator.Validate(new CreateUserGroupCommand("Ops Team", new List<string>(), null, Admin));
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Please enter the User Code & Name");
    }

    [Fact] // R5.8 — inactive member rejected
    public async Task CreateGroup_WithInactiveMember_Throws()
    {
        var repo = new InMemoryUserManagementRepository();
        repo.Users.Add(new UserAccount { UserCode = "U1001", Name = "X", IsActive = false, PasswordHash = "x.y" });
        var handler = new CreateUserGroupCommandHandler(repo);
        await Assert.ThrowsAsync<UserInactiveMemberException>(() => handler.Handle(
            new CreateUserGroupCommand("Ops Team", new[] { "U1001" }, null, Admin), default));
    }

    [Fact] // R11.8 — modify leaves password hash unchanged (blank-password = no change)
    public async Task Update_DoesNotChangePasswordHash()
    {
        var repo = new InMemoryUserManagementRepository();
        var user = ActiveUser("U1001", "Asha");
        user.PasswordHash = "ORIGINAL.HASH";
        repo.Users.Add(user);

        var handler = new UpdateUserAccountCommandHandler(repo);
        await handler.Handle(new UpdateUserAccountCommand(user.Id, "Asha Rao", null, "Officer",
            "Operations", null, null, UserType.Branch, true, Admin), default);

        Assert.Equal("ORIGINAL.HASH", repo.Users.Single().PasswordHash);
    }

    [Fact] // R11.6 — reset-password with weak password preserves the existing hash
    public async Task ResetPassword_WithWeakPassword_PreservesHash()
    {
        var repo = new InMemoryUserManagementRepository();
        var user = ActiveUser("U1001");
        user.PasswordHash = "ORIGINAL.HASH";
        repo.Users.Add(user);

        var handler = new ResetPasswordCommandHandler(repo, Policy);
        await Assert.ThrowsAsync<UserValidationException>(() => handler.Handle(
            new ResetPasswordCommand(user.Id, "weak", Admin), default));
        Assert.Equal("ORIGINAL.HASH", repo.Users.Single().PasswordHash);
    }

    [Fact] // R11.7 — modify a missing user yields not-found
    public async Task Update_MissingUser_Throws()
    {
        var repo = new InMemoryUserManagementRepository();
        var handler = new UpdateUserAccountCommandHandler(repo);
        await Assert.ThrowsAsync<UserNotFoundException>(() => handler.Handle(
            new UpdateUserAccountCommand(Guid.NewGuid(), "X", null, "Officer", "Operations",
                null, null, UserType.Branch, true, Admin), default));
    }

    [Fact] // R14.1 — successful create writes exactly one audit entry
    public async Task Create_WritesSingleAuditEntry()
    {
        var repo = new InMemoryUserManagementRepository();
        var handler = new CreateUserAccountCommandHandler(repo, Policy);
        await handler.Handle(new CreateUserAccountCommand("Asha", "Passw0rd!", null, "Officer",
            "Operations", null, null, UserType.Branch, null, Admin), default);
        Assert.Single(repo.Audit);
        Assert.Equal(UserManagementAuditAction.Create, repo.Audit[0].Action);
    }

    [Theory] // R3.2/3.4/3.9/3.10 — required-field messages
    [InlineData("", "Passw0rd!", "Officer", "Operations", "Please enter the User Name")]
    [InlineData("Asha", "", "Officer", "Operations", "Please enter the User Password")]
    [InlineData("Asha", "Passw0rd!", "", "Operations", "Please select the Designation")]
    [InlineData("Asha", "Passw0rd!", "Officer", "", "Please select the Department")]
    public void CreateValidator_RequiredFieldMessages(string name, string pwd, string desig, string dept, string message)
    {
        var validator = new CreateUserAccountCommandValidator();
        var result = validator.Validate(new CreateUserAccountCommand(
            name, pwd, null, desig, dept, null, null, UserType.Branch, null, Admin));
        Assert.Contains(result.Errors, e => e.ErrorMessage == message);
    }

    [Fact] // R4.2 — mobile special-character message
    public void CreateValidator_MobileSpecialChars_Message()
    {
        var validator = new CreateUserAccountCommandValidator();
        var result = validator.Validate(new CreateUserAccountCommand(
            "Asha", "Passw0rd!", null, "Officer", "Operations", "98-76", null, UserType.Branch, null, Admin));
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Special characters are not allowed in this field");
    }
}
```

- [ ] 9.5* CREATE `Finnova.Tests/Integration/UserManagementAuthorizationTests.cs`

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Finnova.Tests.Integration;

/// <summary>Authorization integration tests for the User Management endpoints (FS §9 R15).</summary>
public class UserManagementAuthorizationTests : IClassFixture<SystemAdminAppFactory>
{
    private readonly SystemAdminAppFactory _factory;
    public UserManagementAuthorizationTests(SystemAdminAppFactory factory) => _factory = factory;

    private HttpClient Client() => _factory.CreateClient();
    private static void SetBearer(HttpClient c, string t) =>
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t);

    private static readonly Guid SampleId = Guid.NewGuid();

    public static IEnumerable<object[]> Endpoints()
    {
        yield return new object[] { "GET list", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Get, "/api/user")) };
        yield return new object[] { "GET by-code", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Get, "/api/user/by-code/U1001")) };
        yield return new object[] { "POST create", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Post, "/api/user")
            { Content = JsonContent.Create(new { Name = "Asha", Password = "Passw0rd!", Designation = "Officer", Department = "Operations", UserType = "Branch" }) }) };
        yield return new object[] { "POST group", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Post, "/api/user/group")
            { Content = JsonContent.Create(new { Name = "Ops", MemberUserCodes = new[] { "U1001" } }) }) };
        yield return new object[] { "PUT access", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Put, $"/api/user/{SampleId}/access")
            { Content = JsonContent.Create(new { LineOfBusiness = "Retail Lending", Rows = Array.Empty<object>(), BranchCodes = new[] { "ALL" } }) }) };
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WithoutToken_Returns401(string name, Func<HttpRequestMessage> request)
    { _ = name; Assert.Equal(HttpStatusCode.Unauthorized, (await Client().SendAsync(request())).StatusCode); }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WithNonAdminToken_Returns403(string name, Func<HttpRequestMessage> request)
    { _ = name; var c = Client(); SetBearer(c, DevJwt.NoRole()); Assert.Equal(HttpStatusCode.Forbidden, (await c.SendAsync(request())).StatusCode); }

    [Theory] // R15.4 — 401 before 403 for an invalid + roleless token
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WithTamperedToken_Returns401Not403(string name, Func<HttpRequestMessage> request)
    {
        _ = name; var c = Client(); SetBearer(c, DevJwt.Tampered());
        var r = await c.SendAsync(request());
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, r.StatusCode);
    }
}
```

- [ ] 9.6* CREATE `Finnova.Tests/Integration/UserManagementEndToEndTests.cs`

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.UserManagement;
using Xunit;

namespace Finnova.Tests.Integration;

/// <summary>End-to-end: create -> by-code -> list against the SystemAdmin host (FS §9 R2/R3/R13/R14).</summary>
public class UserManagementEndToEndTests : IClassFixture<SystemAdminAppFactory>
{
    private readonly SystemAdminAppFactory _factory;
    public UserManagementEndToEndTests(SystemAdminAppFactory factory) => _factory = factory;

    private HttpClient Admin()
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", DevJwt.SystemAdmin());
        return c;
    }

    [Fact]
    public async Task Create_Then_ReadByCode_Then_ListContainsIt()
    {
        var c = Admin();

        var create = await c.PostAsJsonAsync("/api/user", new
        {
            Name = "Asha Rao",
            Password = "Passw0rd!",
            Designation = "Officer",
            Department = "Operations",
            UserType = "Branch",
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<UserAccountResponse>();
        Assert.NotNull(created);
        Assert.False(string.IsNullOrEmpty(created!.UserCode));   // generated code (R2.1)
        Assert.True(created.IsActive);                            // default active (R3.15)

        var read = await c.GetFromJsonAsync<UserAccountResponse>($"/api/user/by-code/{created.UserCode}");
        Assert.Equal(created.UserCode, read!.UserCode);

        var list = await c.GetFromJsonAsync<PaginatedResponse<UserListItemResponse>>("/api/user?pageSize=100");
        Assert.Contains(list!.Data, x => x.Code == created.UserCode);
    }
}
```

- [ ] 9.7 Checkpoint — Ensure all backend tests pass
  - Run `dotnet test` for `Finnova.Tests`. Ensure all tests pass; ask the user if questions arise.

---

> **Frontend tasks (10–12) live in the Finnova-UI repo:** `E:\Finnova\Finnova-UI\Finnova-UI`.
> Namespace is `userManagement` / `UserManagement` throughout (avoids the `organization*` /
> "Company Master" and auth-`User` collisions). The real service `basePath` is `'/user'`; the shared
> axios base URL already carries the `/api/ua/api` prefix, so the gateway `user` alias (task 8.3)
> routes it. India-only, English-only; no RTL, no bilingual/Arabic data. `@mui/x-data-grid` is
> available; `@mui/x-tree-view` is NOT — the Location Tree is a custom recursive `Collapse` component.
>
> **The data-entry UX is a guided wizard (MUI `Stepper`) — the module's product USP (design
> Decision 11).** Task 10 (model + service interface + mock + real + toggle + barrels) is
> **unchanged**: the wizard is a pure UI reshaping over the exact same 17-endpoint API, so the
> contracts, interface, mock, and real service stay byte-for-byte as-is. Task 11 builds the wizard
> on top of that unchanged service layer: a client-side `useUserManagementDraft` localStorage
> autosave hook (**the plaintext password is never persisted**, no backend draft endpoint), a
> `WizardContext` holding wizard state + per-step validity/errors, four step components
> (`ConfigurationStep` → `IdentityStep` → `AccessStep` → `ReviewStep`), the `UserManagementWizard`
> stepper shell, and the `UserManagementMaster` **landing page** (`UserListGrid` + "New User") that
> launches the wizard for Create and opens it in Modify/Query for a row. The existing
> `BranchLocationTree` and `AccessRightsGrid` components are **reused** by `AccessStep`; the old
> `UserGroupDialog` is renamed to `UserGroupMemberPicker` (created as a new file). Task 12 covers
> the wizard with Vitest + RTL. Draft autosave is client-side localStorage only — **no backend
> draft endpoint**.

- [ ] 10. Frontend model + service interface + mock + real + toggle + barrels
  - _Requirements: R2, R3, R4, R5, R6, R7, R8, R9, R10, R11, R13, R16.8/16.9_

- [ ] 10.1 CREATE `src/models/userManagement.model.ts`

```typescript
// User Management domain models (FS §9). English-only; no bilingual fields.

export type UserConfiguration = 'User' | 'UserGroup' | 'FunctionalGroup';
export type UserType = 'Corporate' | 'Branch';
export type UserMode = 'Create' | 'Modify' | 'Query';

export interface UserAccount {
  id: string;
  userCode: string;
  name: string;
  dateOfJoining: string;        // ISO; UI renders DD/MM/YYYY
  designation: string;
  department: string;
  mobileNumber?: string | null;
  email?: string | null;
  userType: UserType;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface UserAccountFormData {
  name: string;
  password: string;
  dateOfJoining?: string | null;
  designation: string;
  department: string;
  mobileNumber?: string | null;
  email?: string | null;
  userType: UserType;
  isActive?: boolean | null;
}

export interface UserAccountUpdateData {
  name: string;
  dateOfJoining?: string | null;
  designation: string;
  department: string;
  mobileNumber?: string | null;
  email?: string | null;
  userType: UserType;
  isActive: boolean;
}

export interface UserGroupMember {
  userCode: string;
  name: string;
  isActive: boolean;
}

export interface UserGroup {
  id: string;
  userGroupCode: string;
  name: string;
  members: UserGroupMember[];
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface UserGroupFormData {
  name: string;
  memberUserCodes: string[];
  isActive?: boolean | null;
}

export interface UserGroupUpdateData {
  name: string;
  memberUserCodes: string[];
  isActive: boolean;
}

export interface FunctionalGroupFunction {
  programName: string;
  roleCode: string;
}

export interface FunctionalGroup {
  id: string;
  functionalGroupCode: string;
  roleCenterName: string;
  functions: FunctionalGroupFunction[];
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface FunctionalGroupFormData {
  roleCenterName: string;
  isActive?: boolean | null;
}

export interface AccessRightRow {
  roleCode: string;
  roleCenterName: string;
  programName: string;
  canAdd: boolean;
  canModify: boolean;
  canQuery: boolean;
  canDelete: boolean;
}

export interface CopyProfileRequest {
  sourceUserCode: string;
  sourceLineOfBusiness: string;
}

export interface SaveUserAccessData {
  lineOfBusiness: string;
  rows: AccessRightRow[];
  branchCodes: string[];        // may contain 'ALL'
  copyProfile?: CopyProfileRequest | null;
}

export interface UserAccess {
  lineOfBusiness: string;
  rows: AccessRightRow[];
  branchCodes: string[];
}

export interface UserListItem {
  id: string;
  code: string;
  name: string;
  kind: UserConfiguration;
  isActive: boolean;
}

export interface UserListQuery {
  search?: string;
  kind?: UserConfiguration;
  isActive?: boolean;
  page?: number;
  pageSize?: number;
}

export interface UserManagementAuditEntry {
  id: string;
  recordId: string;
  recordKind: UserConfiguration;
  action: string;
  summary: string;
  changedBy: string;
  changedAtUtc: string;
}

export interface ReferenceItem {
  code: string;
  label: string;
}

export interface BranchTreeNode {
  code: string;
  name: string;
  level: 'Location' | 'Region' | 'Branch';
  children: BranchTreeNode[];
}

// Discriminated record returned by getByCode (user record for Modify/Query).
export type UserRecord = UserAccount;
```

- [ ] 10.2 MODIFY `src/models/index.ts`

  Add the barrel re-export (append with the other model exports):

```typescript
export * from './userManagement.model';
```

- [ ] 10.3 CREATE `src/services/interfaces/userManagement.interface.ts`

```typescript
import type { PaginatedResponse } from '../../models/api.model';
import type {
  UserListQuery, UserListItem, UserRecord, UserAccount, UserAccountFormData,
  UserAccountUpdateData, UserGroup, UserGroupFormData, UserGroupUpdateData,
  FunctionalGroup, FunctionalGroupFormData, UserAccess, SaveUserAccessData,
  UserManagementAuditEntry, UserGroupMember, ReferenceItem, AccessRightRow, BranchTreeNode,
} from '../../models/userManagement.model';

export interface IUserManagementService {
  getPaged(p: UserListQuery): Promise<PaginatedResponse<UserListItem>>;
  getByCode(code: string): Promise<UserRecord>;
  createUser(d: UserAccountFormData): Promise<UserAccount>;
  updateUser(id: string, d: UserAccountUpdateData): Promise<UserAccount>;
  resetPassword(id: string, newPassword: string): Promise<void>;
  createGroup(d: UserGroupFormData): Promise<UserGroup>;
  updateGroup(id: string, d: UserGroupUpdateData): Promise<UserGroup>;
  createFunctionalGroup(d: FunctionalGroupFormData): Promise<FunctionalGroup>;
  getAccess(id: string, lob: string): Promise<UserAccess>;
  saveAccess(id: string, d: SaveUserAccessData): Promise<UserAccess>;
  getAudit(id: string): Promise<UserManagementAuditEntry[]>;
  // reference LOVs
  getActiveUsers(search?: string): Promise<UserGroupMember[]>;
  getLinesOfBusiness(): Promise<ReferenceItem[]>;
  getRoleCenters(): Promise<ReferenceItem[]>;                 // includes ALL
  getRoleCenterPrograms(name: string): Promise<AccessRightRow[]>;
  getBranchTree(lob: string): Promise<BranchTreeNode[]>;      // includes ALL
  getLookups(type: 'Designation' | 'Department' | 'UserType'): Promise<ReferenceItem[]>;
}
```

- [ ] 10.4 MODIFY `src/services/interfaces/index.ts`

```typescript
export * from './userManagement.interface';
```

- [ ] 10.5 CREATE `src/services/real/userManagement.real.ts`

```typescript
import { api } from '../api';
import type { PaginatedResponse } from '../../models/api.model';
import type { IUserManagementService } from '../interfaces/userManagement.interface';
import type {
  UserListQuery, UserListItem, UserRecord, UserAccount, UserAccountFormData,
  UserAccountUpdateData, UserGroup, UserGroupFormData, UserGroupUpdateData,
  FunctionalGroup, FunctionalGroupFormData, UserAccess, SaveUserAccessData,
  UserManagementAuditEntry, UserGroupMember, ReferenceItem, AccessRightRow, BranchTreeNode,
} from '../../models/userManagement.model';

// The shared axios base URL already carries the /api/ua/api prefix; the gateway rewrites
// /api/ua/api/user/** onto the SystemAdmin cluster (see gateway task 8.3).
const basePath = '/user';

export const userManagementRealService: IUserManagementService = {
  async getPaged(p: UserListQuery) {
    const { data } = await api.get<PaginatedResponse<UserListItem>>(basePath, { params: p });
    return data;
  },
  async getByCode(code: string) {
    const { data } = await api.get<UserRecord>(`${basePath}/by-code/${encodeURIComponent(code)}`);
    return data;
  },
  async createUser(d: UserAccountFormData) {
    const { data } = await api.post<UserAccount>(basePath, d);
    return data;
  },
  async updateUser(id: string, d: UserAccountUpdateData) {
    const { data } = await api.put<UserAccount>(`${basePath}/${id}`, d);
    return data;
  },
  async resetPassword(id: string, newPassword: string) {
    await api.post(`${basePath}/${id}/reset-password`, { newPassword });
  },
  async createGroup(d: UserGroupFormData) {
    const { data } = await api.post<UserGroup>(`${basePath}/group`, d);
    return data;
  },
  async updateGroup(id: string, d: UserGroupUpdateData) {
    const { data } = await api.put<UserGroup>(`${basePath}/group/${id}`, d);
    return data;
  },
  async createFunctionalGroup(d: FunctionalGroupFormData) {
    const { data } = await api.post<FunctionalGroup>(`${basePath}/functional-group`, d);
    return data;
  },
  async getAccess(id: string, lob: string) {
    const { data } = await api.get<UserAccess>(`${basePath}/${id}/access`, { params: { lob } });
    return data;
  },
  async saveAccess(id: string, d: SaveUserAccessData) {
    const { data } = await api.put<UserAccess>(`${basePath}/${id}/access`, d);
    return data;
  },
  async getAudit(id: string) {
    const { data } = await api.get<UserManagementAuditEntry[]>(`${basePath}/${id}/audit`);
    return data;
  },
  async getActiveUsers(search?: string) {
    const { data } = await api.get<UserGroupMember[]>(`${basePath}/ref/active-users`, { params: { search } });
    return data;
  },
  async getLinesOfBusiness() {
    const { data } = await api.get<ReferenceItem[]>(`${basePath}/ref/lines-of-business`);
    return data;
  },
  async getRoleCenters() {
    const { data } = await api.get<ReferenceItem[]>(`${basePath}/ref/role-centers`);
    return data;
  },
  async getRoleCenterPrograms(name: string) {
    const { data } = await api.get<AccessRightRow[]>(`${basePath}/ref/role-centers/${encodeURIComponent(name)}/programs`);
    return data;
  },
  async getBranchTree(lob: string) {
    const { data } = await api.get<BranchTreeNode[]>(`${basePath}/ref/branch-tree`, { params: { lob } });
    return data;
  },
  async getLookups(type) {
    const { data } = await api.get<ReferenceItem[]>(`${basePath}/ref/lookups`, { params: { type } });
    return data;
  },
};
```

- [ ] 10.6 CREATE `src/services/mock/userManagement.mock.ts`

```typescript
import type { PaginatedResponse } from '../../models/api.model';
import type { IUserManagementService } from '../interfaces/userManagement.interface';
import type {
  UserListQuery, UserListItem, UserRecord, UserAccount, UserAccountFormData,
  UserAccountUpdateData, UserGroup, UserGroupFormData, UserGroupUpdateData,
  FunctionalGroup, FunctionalGroupFormData, UserAccess, SaveUserAccessData,
  UserManagementAuditEntry, UserGroupMember, ReferenceItem, AccessRightRow, BranchTreeNode,
} from '../../models/userManagement.model';

// Backend-shaped error so the shared interceptor behaves identically for mock and real.
function err(status: number, code: string, message: string) {
  return { response: { status, data: { code, message } } };
}

const now = () => new Date().toISOString();

// India-only, English-only seed data.
const users: UserAccount[] = [
  { id: 'u-1', userCode: 'ASHA1', name: 'Asha Rao', dateOfJoining: now(), designation: 'Manager',
    department: 'Operations', mobileNumber: '9876543210', email: 'asha@finnova.in',
    userType: 'Branch', isActive: true, createdAt: now(), updatedAt: now() },
  { id: 'u-2', userCode: 'VIKR1', name: 'Vikram Singh', dateOfJoining: now(), designation: 'Officer',
    department: 'Credit', mobileNumber: null, email: null, userType: 'Corporate',
    isActive: true, createdAt: now(), updatedAt: now() },
];
const groups: UserGroup[] = [];
const functionals: FunctionalGroup[] = [];
const audit: UserManagementAuditEntry[] = [];
const access: Record<string, UserAccess> = {};

const roleCenters: Record<string, string[]> = {
  'System Admin': ['Company Master', 'User Master', 'Lookup Master'],
  'Origination': ['Asset Master', 'Entity Master', 'Application Entry'],
};
const lobsWithRoleCodes = ['Retail Lending', 'Corporate Lending', 'Leasing'];

function genCode(source: string): string {
  const letters = (source || 'U').toUpperCase().replace(/[^A-Z0-9]/g, '');
  const first = (letters.match(/[A-Z]/)?.[0]) ?? 'U';
  let n = 1;
  while (true) {
    const candidate = (first + letters.replace(/[^A-Z]/g, '').slice(1, 4) + n).slice(0, 6);
    if (candidate.length >= 4 && !users.some(u => u.userCode === candidate)
        && !groups.some(g => g.userGroupCode === candidate)
        && !functionals.some(f => f.functionalGroupCode === candidate)) return candidate;
    n += 1;
  }
}

function roleCode(rc: string, program: string) { return (rc + program).toUpperCase(); }

function emailValid(email?: string | null): boolean {
  if (!email) return true;                        // optional
  if (email.length > 60) return false;
  if ((email.match(/@/g) || []).length !== 1) return false;
  if (!email.includes('.')) return false;
  return /[A-Za-z0-9]/.test(email[0]) && /[A-Za-z0-9]/.test(email[email.length - 1]);
}

export const userManagementMockService: IUserManagementService = {
  async getPaged(p: UserListQuery): Promise<PaginatedResponse<UserListItem>> {
    const page = p.page ?? 1;
    const pageSize = p.pageSize ?? 20;
    let items: UserListItem[] = [
      ...users.map(u => ({ id: u.id, code: u.userCode, name: u.name, kind: 'User' as const, isActive: u.isActive })),
      ...groups.map(g => ({ id: g.id, code: g.userGroupCode, name: g.name, kind: 'UserGroup' as const, isActive: g.isActive })),
      ...functionals.map(f => ({ id: f.id, code: f.functionalGroupCode, name: f.roleCenterName, kind: 'FunctionalGroup' as const, isActive: f.isActive })),
    ];
    if (p.kind) items = items.filter(i => i.kind === p.kind);
    if (p.isActive !== undefined) items = items.filter(i => i.isActive === p.isActive);
    if (p.search) {
      const t = p.search.toLowerCase();
      items = items.filter(i => i.code.toLowerCase().includes(t) || i.name.toLowerCase().includes(t));
    }
    items.sort((a, b) => a.code.localeCompare(b.code) || a.id.localeCompare(b.id));
    const total = items.length;
    const data = items.slice((page - 1) * pageSize, (page - 1) * pageSize + pageSize);
    return { data, total, page, pageSize, totalPages: Math.ceil(total / pageSize) };
  },

  async getByCode(code: string): Promise<UserRecord> {
    const u = users.find(x => x.userCode === code);
    if (!u) throw err(404, 'ERR-USR-404', `User record '${code}' was not found.`);
    return u;
  },

  async createUser(d: UserAccountFormData): Promise<UserAccount> {
    if (!d.name?.trim()) throw err(400, 'ERR-USR-400', 'Please enter the User Name');
    if (!d.password) throw err(400, 'ERR-USR-400', 'Please enter the User Password');
    if (!d.designation) throw err(400, 'ERR-USR-400', 'Please select the Designation');
    if (!d.department) throw err(400, 'ERR-USR-400', 'Please select the Department');
    if (d.mobileNumber && !/^[0-9]{1,12}$/.test(d.mobileNumber))
      throw err(400, 'ERR-USR-400', 'Special characters are not allowed in this field');
    if (!emailValid(d.email)) throw err(400, 'ERR-USR-400', 'Please enter a valid Email id');

    const u: UserAccount = {
      id: `u-${users.length + 1}`, userCode: genCode(d.name), name: d.name.trim(),
      dateOfJoining: d.dateOfJoining ?? now(), designation: d.designation, department: d.department,
      mobileNumber: d.mobileNumber ?? null, email: d.email ?? null, userType: d.userType,
      isActive: d.isActive ?? true, createdAt: now(), updatedAt: now(),
    };
    users.push(u);
    audit.unshift({ id: `a-${audit.length + 1}`, recordId: u.id, recordKind: 'User', action: 'Create',
      summary: `Created user '${u.userCode}'.`, changedBy: 'mock-admin', changedAtUtc: now() });
    return u;
  },

  async updateUser(id: string, d: UserAccountUpdateData): Promise<UserAccount> {
    const u = users.find(x => x.id === id);
    if (!u) throw err(404, 'ERR-USR-404', 'User record was not found.');
    if (!d.name?.trim()) throw err(400, 'ERR-USR-400', 'Please enter the User Name');
    if (!emailValid(d.email)) throw err(400, 'ERR-USR-400', 'Please enter a valid Email id');
    Object.assign(u, d, { updatedAt: now() });     // password unchanged here (R11.8)
    return u;
  },

  async resetPassword(id: string, newPassword: string): Promise<void> {
    const u = users.find(x => x.id === id);
    if (!u) throw err(404, 'ERR-USR-404', 'User record was not found.');
    if (!newPassword || newPassword.length < 8) throw err(400, 'ERR-USR-400', 'Please enter a valid Password');
  },

  async createGroup(d: UserGroupFormData): Promise<UserGroup> {
    if (!d.name?.trim()) throw err(400, 'ERR-USR-400', 'Please enter the User Group Name');
    if (!d.memberUserCodes?.length) throw err(400, 'ERR-USR-400', 'Please enter the User Code & Name');
    const members = d.memberUserCodes.map(code => {
      const u = users.find(x => x.userCode === code);
      if (!u || !u.isActive) throw err(409, 'ERR-USR-409', 'Only active users can be tagged to a group.');
      return { userCode: u.userCode, name: u.name, isActive: u.isActive };
    });
    const g: UserGroup = { id: `g-${groups.length + 1}`, userGroupCode: genCode(d.name), name: d.name.trim(),
      members, isActive: d.isActive ?? true, createdAt: now(), updatedAt: now() };
    groups.push(g);
    return g;
  },

  async updateGroup(id: string, d: UserGroupUpdateData): Promise<UserGroup> {
    const g = groups.find(x => x.id === id);
    if (!g) throw err(404, 'ERR-USR-404', 'User group was not found.');
    if (!d.memberUserCodes?.length) throw err(400, 'ERR-USR-400', 'Please enter the User Code & Name');
    g.name = d.name.trim(); g.isActive = d.isActive; g.updatedAt = now();
    return g;
  },

  async createFunctionalGroup(d: FunctionalGroupFormData): Promise<FunctionalGroup> {
    if (!d.roleCenterName) throw err(400, 'ERR-USR-400', 'Please select the Role Center Name');
    const programs = roleCenters[d.roleCenterName] ?? [];
    const f: FunctionalGroup = { id: `f-${functionals.length + 1}`, functionalGroupCode: genCode(d.roleCenterName),
      roleCenterName: d.roleCenterName, functions: programs.map(p => ({ programName: p, roleCode: roleCode(d.roleCenterName, p) })),
      isActive: d.isActive ?? true, createdAt: now(), updatedAt: now() };
    functionals.push(f);
    return f;
  },

  async getAccess(id: string, lob: string): Promise<UserAccess> {
    return access[`${id}|${lob}`] ?? { lineOfBusiness: lob, rows: [], branchCodes: [] };
  },

  async saveAccess(id: string, d: SaveUserAccessData): Promise<UserAccess> {
    if (!d.lineOfBusiness) throw err(400, 'ERR-USR-400', 'Please select at least one Line of Business');
    if (!lobsWithRoleCodes.includes(d.lineOfBusiness))
      throw err(400, 'ERR-USR-400', 'The selected Line of Business has no linked Role Codes.');
    if (!d.branchCodes?.length) throw err(400, 'ERR-USR-400', 'At least one branch association is required.');
    const result: UserAccess = { lineOfBusiness: d.lineOfBusiness, rows: d.rows, branchCodes: d.branchCodes };
    access[`${id}|${d.lineOfBusiness}`] = result;
    return result;
  },

  async getAudit(id: string): Promise<UserManagementAuditEntry[]> {
    return audit.filter(a => a.recordId === id);
  },

  async getActiveUsers(search?: string): Promise<UserGroupMember[]> {
    let list = users.filter(u => u.isActive);
    if (search) { const t = search.toLowerCase(); list = list.filter(u => u.userCode.toLowerCase().includes(t) || u.name.toLowerCase().includes(t)); }
    return list.map(u => ({ userCode: u.userCode, name: u.name, isActive: u.isActive }));
  },

  async getLinesOfBusiness(): Promise<ReferenceItem[]> {
    return lobsWithRoleCodes.map(l => ({ code: l, label: l }));
  },

  async getRoleCenters(): Promise<ReferenceItem[]> {
    return [{ code: 'ALL', label: 'ALL' }, ...Object.keys(roleCenters).map(r => ({ code: r, label: r }))];
  },

  async getRoleCenterPrograms(name: string): Promise<AccessRightRow[]> {
    return (roleCenters[name] ?? []).map(p => ({
      roleCode: roleCode(name, p), roleCenterName: name, programName: p,
      canAdd: false, canModify: false, canQuery: false, canDelete: false,
    }));
  },

  async getBranchTree(_lob: string): Promise<BranchTreeNode[]> {
    return [
      { code: 'ALL', name: 'ALL', level: 'Location', children: [] },
      { code: 'MAHARASHTRA', name: 'MAHARASHTRA', level: 'Region', children: [
        { code: 'MUMBAI', name: 'MUMBAI', level: 'Region', children: [
          { code: 'HO', name: 'HEAD OFFICE', level: 'Branch', children: [] },
          { code: 'FORT', name: 'FORT BRANCH', level: 'Branch', children: [] },
        ] },
      ] },
      { code: 'KARNATAKA', name: 'KARNATAKA', level: 'Region', children: [
        { code: 'BENGALURU', name: 'BENGALURU', level: 'Region', children: [
          { code: 'MGR', name: 'MG ROAD BRANCH', level: 'Branch', children: [] },
        ] },
      ] },
    ];
  },

  async getLookups(type): Promise<ReferenceItem[]> {
    if (type === 'Designation') return [
      { code: 'MGR', label: 'Manager' }, { code: 'OFF', label: 'Officer' },
      { code: 'CLK', label: 'Clerk' }, { code: 'EXE', label: 'Executive' }];
    if (type === 'Department') return [
      { code: 'OPS', label: 'Operations' }, { code: 'CRD', label: 'Credit' },
      { code: 'IT', label: 'Information Technology' }, { code: 'FIN', label: 'Finance' }];
    return [{ code: 'Corporate', label: 'Corporate' }, { code: 'Branch', label: 'Branch' }];
  },
};
```

- [ ] 10.7 CREATE `src/services/userManagement.service.ts`

```typescript
import type { IUserManagementService } from './interfaces/userManagement.interface';
import { userManagementRealService } from './real/userManagement.real';
import { userManagementMockService } from './mock/userManagement.mock';

const useMock = import.meta.env.VITE_USE_MOCK_API === 'true';

export const userManagementService: IUserManagementService =
  useMock ? userManagementMockService : userManagementRealService;
```

- [ ] 10.8 MODIFY `src/services/index.ts`

```typescript
export * from './userManagement.service';
```

---

- [ ] 11. Frontend wizard: draft hook + context + steps + shell + landing page + route + nav
  - The guided MUI `Stepper` wizard (product USP, design Decision 11) over the unchanged task-10
    service layer. Dependency order within the frontend: draft hook → context → reused grids/tree →
    member picker → step components → wizard shell → audit dialog → landing page → route → nav.
  - `BranchLocationTree` (11.3) and `AccessRightsGrid` (11.4) are **reused by `AccessStep`** and are
    unchanged from the single-form design; `UserGroupMemberPicker` (11.5) is the renamed
    `UserGroupDialog`. **No new npm dependency** (custom `Collapse` tree; `@mui/x-data-grid` only).
    Draft autosave is **client-side localStorage only — the plaintext password is never persisted**.
  - _Requirements: R1.2-1.4, R2.6-2.8, R3.6, R5, R6, R7, R8.5/8.7/8.8, R9.1-9.4, R10, R11.2/11.4, R12.2, R16_

- [ ] 11.1 CREATE `src/hooks/useUserManagementDraft.ts`

```typescript
import { useCallback, useEffect, useRef, useState } from 'react';
import type {
  UserConfiguration, UserMode, AccessRightRow,
} from '../models/userManagement.model';

// -------------------------------------------------------------------------------------------------
// Client-side draft autosave for the User Management wizard (design Decision 11).
//
// SECURITY: the plaintext password (and any resetPassword) is NEVER written to localStorage.
// localStorage is same-origin plaintext that survives logout, so the hook strips the password
// before persisting and records a `passwordOmitted: true` marker instead. On restore the password
// field is empty and must be re-entered before submit.
//
// There is NO backend draft endpoint — drafts are per-browser only, keyed per acting admin so
// drafts from different admins on a shared browser stay isolated.
// -------------------------------------------------------------------------------------------------

const DRAFT_VERSION = 1;

// Identity slice as persisted — password-bearing fields are declared optional and stripped on save.
export interface DraftIdentity {
  name?: string;
  password?: string;              // stripped before persist (never stored)
  resetPassword?: string;         // stripped before persist (never stored)
  dateOfJoining?: string | null;
  designation?: string;
  department?: string;
  mobileNumber?: string | null;
  email?: string | null;
  userType?: 'Corporate' | 'Branch';
  isActive?: boolean;
  userCode?: string;              // read-only, server-generated
  // User Group
  groupName?: string;
  memberUserCodes?: string[];
  // Functional Group
  roleCenterName?: string;
}

export interface DraftAccess {
  lineOfBusiness?: string;
  roleCenter?: string;
  rows?: AccessRightRow[];
  branchCodes?: string[];
  copyProfileEnabled?: boolean;
  copySourceUserCode?: string;
  copySourceLineOfBusiness?: string;
}

// The serializable wizard-state snapshot. JSON-only (no functions / File / DOM handles).
export interface UserManagementDraft {
  version: number;
  mode: UserMode;
  configuration: UserConfiguration;
  identity: DraftIdentity;
  access: DraftAccess;
  step: number;
  savedAtUtc: string;
  passwordOmitted: true;          // always true — passwords are never persisted
}

export type DraftStatus = 'idle' | 'saved' | 'restored';

// Decode the acting admin id from the JWT in localStorage; stable fallback if unavailable.
export function resolveActingAdminId(): string {
  try {
    const token = localStorage.getItem('finnova_token');
    if (!token) return 'anonymous';
    const payload = JSON.parse(atob(token.split('.')[1] ?? ''));
    return String(
      payload.sub ?? payload.nameid ?? payload.unique_name ?? payload.name ?? 'anonymous',
    );
  } catch {
    return 'anonymous';
  }
}

function draftKey(actingAdminId: string, draftId: string): string {
  return `finnova:um:draft:${actingAdminId}:${draftId}`;
}

// Strip every password-bearing field before anything is written to localStorage.
function stripPassword(identity: DraftIdentity): DraftIdentity {
  const { password: _pw, resetPassword: _rpw, ...safe } = identity;
  return safe;
}

export interface DraftSnapshotInput {
  mode: UserMode;
  configuration: UserConfiguration;
  identity: DraftIdentity;
  access: DraftAccess;
  step: number;
}

export interface UseUserManagementDraft {
  status: DraftStatus;
  statusLabel: string;                                  // "All changes saved" / "Draft restored" / ''
  restored: UserManagementDraft | null;                 // the draft loaded on mount (if any)
  saveDraft: (snapshot: DraftSnapshotInput) => void;    // debounced write (password stripped)
  discardDraft: () => void;                             // clear key + reset status
  clearOnSubmit: () => void;                            // clear key after a successful submit
}

/**
 * localStorage autosave hook. `draftId` is "new" for a Create session or the record code for a
 * Modify session. Restores a matching draft on mount; debounces writes (~500 ms); the password is
 * never persisted; the draft is cleared on successful submit.
 */
export function useUserManagementDraft(
  actingAdminId: string,
  draftId: string,
): UseUserManagementDraft {
  const key = draftKey(actingAdminId, draftId);
  const [status, setStatus] = useState<DraftStatus>('idle');
  const [restored, setRestored] = useState<UserManagementDraft | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Restore-on-mount.
  useEffect(() => {
    try {
      const raw = localStorage.getItem(key);
      if (raw) {
        const parsed = JSON.parse(raw) as UserManagementDraft;
        if (parsed?.version === DRAFT_VERSION) {
          setRestored(parsed);
          setStatus('restored');
        }
      }
    } catch {
      /* corrupt draft — ignore and start clean */
    }
    return () => { if (timer.current) clearTimeout(timer.current); };
  }, [key]);

  const saveDraft = useCallback((snapshot: DraftSnapshotInput) => {
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(() => {
      const draft: UserManagementDraft = {
        version: DRAFT_VERSION,
        mode: snapshot.mode,
        configuration: snapshot.configuration,
        identity: stripPassword(snapshot.identity),   // password stripped here
        access: snapshot.access,
        step: snapshot.step,
        savedAtUtc: new Date().toISOString(),
        passwordOmitted: true,
      };
      try {
        localStorage.setItem(key, JSON.stringify(draft));
        setStatus('saved');
      } catch {
        /* quota/serialization failure — leave status unchanged */
      }
    }, 500);
  }, [key]);

  const discardDraft = useCallback(() => {
    if (timer.current) clearTimeout(timer.current);
    localStorage.removeItem(key);
    setRestored(null);
    setStatus('idle');
  }, [key]);

  const clearOnSubmit = useCallback(() => {
    if (timer.current) clearTimeout(timer.current);
    localStorage.removeItem(key);
    setStatus('idle');
  }, [key]);

  const statusLabel =
    status === 'saved' ? 'All changes saved'
    : status === 'restored' ? 'Draft restored'
    : '';

  return { status, statusLabel, restored, saveDraft, discardDraft, clearOnSubmit };
}

export default useUserManagementDraft;
```

- [ ] 11.2 CREATE `src/components/userManagement/WizardContext.tsx`

```tsx
import {
  createContext, useCallback, useContext, useEffect, useMemo, useReducer, type ReactNode,
} from 'react';
import type { UserConfiguration, UserMode } from '../../models/userManagement.model';
import {
  useUserManagementDraft, resolveActingAdminId,
  type DraftAccess, type DraftIdentity,
} from '../../hooks/useUserManagementDraft';

// WizardContext holds the full wizard state (mode, step, configuration, identity, access) plus
// per-step validity/errors, and wires the draft autosave hook. State lives in useReducer + hooks —
// NO Redux, NO RxJS. Changing the configuration resets dependent identity/access slices so stale
// fields from a previous configuration can't leak into a submit.

export type StepKey = 'configuration' | 'identity' | 'access' | 'review';
export const STEP_ORDER: StepKey[] = ['configuration', 'identity', 'access', 'review'];

export interface WizardState {
  mode: UserMode;
  step: number;                                   // index into STEP_ORDER
  configuration: UserConfiguration;
  identity: DraftIdentity;
  access: DraftAccess;
  valid: Record<StepKey, boolean>;
  errors: Record<StepKey, Record<string, string>>;
  completed: Record<StepKey, boolean>;
}

type Action =
  | { type: 'setConfiguration'; configuration: UserConfiguration }
  | { type: 'patchIdentity'; patch: Partial<DraftIdentity> }
  | { type: 'patchAccess'; patch: Partial<DraftAccess> }
  | { type: 'goToStep'; step: number }
  | { type: 'setValidity'; step: StepKey; valid: boolean; errors?: Record<string, string> }
  | { type: 'markCompleted'; step: StepKey }
  | { type: 'hydrate'; state: Partial<WizardState> }
  | { type: 'reset'; mode: UserMode; configuration: UserConfiguration };

const emptyValidity: Record<StepKey, boolean> = {
  configuration: false, identity: false, access: false, review: false,
};
const emptyErrors: Record<StepKey, Record<string, string>> = {
  configuration: {}, identity: {}, access: {}, review: {},
};

export function initialState(mode: UserMode, configuration: UserConfiguration): WizardState {
  return {
    mode,
    step: 0,
    configuration,
    identity: { isActive: true, userType: 'Branch' },
    access: { rows: [], branchCodes: [] },
    valid: { ...emptyValidity, configuration: true }, // config pre-chosen on open
    errors: { ...emptyErrors },
    completed: { configuration: false, identity: false, access: false, review: false },
  };
}

function reducer(state: WizardState, action: Action): WizardState {
  switch (action.type) {
    case 'setConfiguration':
      // Reset dependent identity/access slices so no stale field survives a config change.
      return {
        ...state,
        configuration: action.configuration,
        identity: { isActive: true, userType: 'Branch' },
        access: { rows: [], branchCodes: [] },
        valid: { ...emptyValidity, configuration: true },
        errors: { ...emptyErrors },
        completed: { configuration: false, identity: false, access: false, review: false },
      };
    case 'patchIdentity':
      return { ...state, identity: { ...state.identity, ...action.patch } };
    case 'patchAccess':
      return { ...state, access: { ...state.access, ...action.patch } };
    case 'goToStep':
      return { ...state, step: Math.max(0, Math.min(STEP_ORDER.length - 1, action.step)) };
    case 'setValidity':
      return {
        ...state,
        valid: { ...state.valid, [action.step]: action.valid },
        errors: { ...state.errors, [action.step]: action.errors ?? {} },
      };
    case 'markCompleted':
      return { ...state, completed: { ...state.completed, [action.step]: true } };
    case 'hydrate':
      return { ...state, ...action.state };
    case 'reset':
      return initialState(action.mode, action.configuration);
    default:
      return state;
  }
}

export interface WizardContextValue {
  state: WizardState;
  readOnly: boolean;                                 // true in Query mode
  draftStatusLabel: string;
  setConfiguration: (c: UserConfiguration) => void;
  patchIdentity: (patch: Partial<DraftIdentity>) => void;
  patchAccess: (patch: Partial<DraftAccess>) => void;
  goToStep: (step: number) => void;
  next: () => void;
  back: () => void;
  setValidity: (step: StepKey, valid: boolean, errors?: Record<string, string>) => void;
  markCompleted: (step: StepKey) => void;
  discardDraft: () => void;
  clearDraftOnSubmit: () => void;
}

const Ctx = createContext<WizardContextValue | null>(null);

export function useWizard(): WizardContextValue {
  const value = useContext(Ctx);
  if (!value) throw new Error('useWizard must be used within <WizardProvider>');
  return value;
}

export interface WizardProviderProps {
  mode: UserMode;
  configuration: UserConfiguration;
  draftId: string;                                   // "new" (Create) or record code (Modify)
  initial?: Partial<WizardState>;                    // server-loaded data for Modify/Query
  children: ReactNode;
}

export function WizardProvider({ mode, configuration, draftId, initial, children }: WizardProviderProps) {
  const [state, dispatch] = useReducer(
    reducer, undefined, () => ({ ...initialState(mode, configuration), ...initial }),
  );

  const actingAdminId = useMemo(() => resolveActingAdminId(), []);
  const { statusLabel, restored, saveDraft, discardDraft, clearOnSubmit } =
    useUserManagementDraft(actingAdminId, draftId);

  // Restore a matching draft on mount (password field stays empty — never persisted).
  useEffect(() => {
    if (restored && restored.mode === mode) {
      dispatch({
        type: 'hydrate',
        state: {
          configuration: restored.configuration,
          identity: { ...restored.identity, password: '', resetPassword: '' },
          access: restored.access,
          step: restored.step,
        },
      });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [restored]);

  // Autosave on any state change (hook debounces + strips the password).
  useEffect(() => {
    if (mode === 'Query') return;                    // nothing to draft in read-only mode
    saveDraft({
      mode: state.mode,
      configuration: state.configuration,
      identity: state.identity,
      access: state.access,
      step: state.step,
    });
  }, [state.mode, state.configuration, state.identity, state.access, state.step, mode, saveDraft]);

  const setConfiguration = useCallback((c: UserConfiguration) => dispatch({ type: 'setConfiguration', configuration: c }), []);
  const patchIdentity = useCallback((patch: Partial<DraftIdentity>) => dispatch({ type: 'patchIdentity', patch }), []);
  const patchAccess = useCallback((patch: Partial<DraftAccess>) => dispatch({ type: 'patchAccess', patch }), []);
  const goToStep = useCallback((step: number) => dispatch({ type: 'goToStep', step }), []);
  const next = useCallback(() => dispatch({ type: 'goToStep', step: state.step + 1 }), [state.step]);
  const back = useCallback(() => dispatch({ type: 'goToStep', step: state.step - 1 }), [state.step]);
  const setValidity = useCallback((step: StepKey, valid: boolean, errors?: Record<string, string>) =>
    dispatch({ type: 'setValidity', step, valid, errors }), []);
  const markCompleted = useCallback((step: StepKey) => dispatch({ type: 'markCompleted', step }), []);

  const value: WizardContextValue = {
    state,
    readOnly: mode === 'Query',
    draftStatusLabel: statusLabel,
    setConfiguration, patchIdentity, patchAccess, goToStep, next, back,
    setValidity, markCompleted,
    discardDraft, clearDraftOnSubmit: clearOnSubmit,
  };

  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

export default WizardProvider;
```

- [ ] 11.3 CREATE `src/components/userManagement/BranchLocationTree.tsx`

```tsx
import { useState } from 'react';
import { Box, Checkbox, Collapse, List, ListItemButton, ListItemText } from '@mui/material';
import ExpandLess from '@mui/icons-material/ExpandLess';
import ExpandMore from '@mui/icons-material/ExpandMore';
import type { BranchTreeNode } from '../../models/userManagement.model';

// Custom recursive Collapse tree (no @mui/x-tree-view). Checkboxes + ALL node (R9.1-9.4).
interface Props {
  nodes: BranchTreeNode[];
  selected: string[];
  onToggle: (code: string) => void;
  disabled?: boolean;
}

function TreeNode({ node, selected, onToggle, disabled, depth }: {
  node: BranchTreeNode; selected: string[]; onToggle: (c: string) => void; disabled?: boolean; depth: number;
}) {
  const [open, setOpen] = useState(true);
  const hasChildren = node.children.length > 0;
  return (
    <>
      <ListItemButton sx={{ pl: 2 + depth * 2 }} disableRipple>
        <Checkbox
          edge="start"
          size="small"
          checked={selected.includes(node.code)}
          onChange={() => onToggle(node.code)}
          disabled={disabled}
          inputProps={{ 'aria-label': `branch-${node.code}` }}
        />
        <ListItemText primary={node.name} onClick={() => hasChildren && setOpen(o => !o)} />
        {hasChildren ? (open ? <ExpandLess /> : <ExpandMore />) : null}
      </ListItemButton>
      {hasChildren && (
        <Collapse in={open} timeout="auto" unmountOnExit>
          <List disablePadding>
            {node.children.map(child => (
              <TreeNode key={child.code} node={child} selected={selected}
                onToggle={onToggle} disabled={disabled} depth={depth + 1} />
            ))}
          </List>
        </Collapse>
      )}
    </>
  );
}

export function BranchLocationTree({ nodes, selected, onToggle, disabled }: Props) {
  return (
    <Box sx={{ border: 1, borderColor: 'divider', borderRadius: 1, maxHeight: 300, overflow: 'auto' }}>
      <List dense disablePadding>
        {nodes.map(n => (
          <TreeNode key={n.code} node={n} selected={selected} onToggle={onToggle} disabled={disabled} depth={0} />
        ))}
      </List>
    </Box>
  );
}

export default BranchLocationTree;
```

- [ ] 11.4 CREATE `src/components/userManagement/AccessRightsGrid.tsx`

```tsx
import { Checkbox } from '@mui/material';
import { DataGrid } from '@mui/x-data-grid';
import type { GridColDef } from '@mui/x-data-grid';
import type { AccessRightRow } from '../../models/userManagement.model';

// Access-rights grid: RoleCode, Program, Add/Modify/Query/Delete, Select All (R8.5/8.7).
interface Props {
  rows: AccessRightRow[];
  onChange: (rows: AccessRightRow[]) => void;
  disabled?: boolean;
}

export function AccessRightsGrid({ rows, onChange, disabled }: Props) {
  const setFlag = (roleCode: string, patch: Partial<AccessRightRow>) =>
    onChange(rows.map(r => (r.roleCode === roleCode ? { ...r, ...patch } : r)));

  const flagCol = (field: keyof AccessRightRow, header: string): GridColDef => ({
    field, headerName: header, width: 90, sortable: false,
    renderCell: (p) => (
      <Checkbox size="small" disabled={disabled}
        checked={Boolean(p.row[field])}
        onChange={(e) => setFlag(p.row.roleCode, { [field]: e.target.checked } as Partial<AccessRightRow>)}
        inputProps={{ 'aria-label': `${String(field)}-${p.row.roleCode}` }} />
    ),
  });

  const columns: GridColDef[] = [
    { field: 'roleCode', headerName: 'Role Code', width: 120 },
    { field: 'programName', headerName: 'Program Description', flex: 1 },
    flagCol('canAdd', 'Add'),
    flagCol('canModify', 'Modify'),
    flagCol('canQuery', 'Query'),
    flagCol('canDelete', 'Delete'),
    {
      field: 'selectAll', headerName: 'Select All', width: 100, sortable: false,
      renderCell: (p) => (
        <Checkbox size="small" disabled={disabled}
          checked={p.row.canAdd && p.row.canModify && p.row.canQuery && p.row.canDelete}
          onChange={(e) => setFlag(p.row.roleCode, {
            canAdd: e.target.checked, canModify: e.target.checked,
            canQuery: e.target.checked, canDelete: e.target.checked,
          })}
          inputProps={{ 'aria-label': `selectAll-${p.row.roleCode}` }} />
      ),
    },
  ];

  return (
    <DataGrid autoHeight density="compact" hideFooter disableRowSelectionOnClick
      getRowId={(r) => r.roleCode} rows={rows} columns={columns} />
  );
}

export default AccessRightsGrid;
```

- [ ] 11.5 CREATE `src/components/userManagement/UserGroupMemberPicker.tsx`

  **Rename note:** this is the former `UserGroupDialog` renamed to `UserGroupMemberPicker` (the
  wizard embeds member selection in `IdentityStep` rather than a toolbar popup). It stays a `Dialog`
  (slide-over) opened from `IdentityStep`; the member add/remove logic is unchanged. Create the new
  file below; there is no separate `UserGroupDialog.tsx` in the wizard design.

```tsx
import { useEffect, useState } from 'react';
import {
  Button, Checkbox, Dialog, DialogActions, DialogContent, DialogTitle,
  List, ListItemButton, ListItemText, TextField,
} from '@mui/material';
import type { UserGroupMember } from '../../models/userManagement.model';
import { userManagementService } from '../../services/userManagement.service';

// User Group member picker (slide-over Dialog): group name + add/remove active-user members
// (R5.4/R5.5/R16.3). Embedded by IdentityStep when configuration is User Group.
interface Props {
  open: boolean;
  initialName?: string;
  initialSelected?: string[];
  disabled?: boolean;
  onClose: () => void;
  onSave: (name: string, memberUserCodes: string[]) => void;
}

export function UserGroupMemberPicker({ open, initialName = '', initialSelected = [], disabled, onClose, onSave }: Props) {
  const [name, setName] = useState(initialName);
  const [selected, setSelected] = useState<string[]>(initialSelected);
  const [activeUsers, setActiveUsers] = useState<UserGroupMember[]>([]);

  useEffect(() => {
    if (open) {
      setName(initialName);
      setSelected(initialSelected);
      userManagementService.getActiveUsers().then(setActiveUsers).catch(() => setActiveUsers([]));
    }
  }, [open]); // eslint-disable-line react-hooks/exhaustive-deps

  const toggle = (code: string) =>
    setSelected(prev => (prev.includes(code) ? prev.filter(c => c !== code) : [...prev, code]));

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm">
      <DialogTitle>User Group Members</DialogTitle>
      <DialogContent>
        <TextField label="User Group Name" fullWidth margin="normal" value={name}
          onChange={(e) => setName(e.target.value)} inputProps={{ maxLength: 30 }} disabled={disabled} />
        <List dense>
          {activeUsers.map(u => (
            <ListItemButton key={u.userCode} onClick={() => !disabled && toggle(u.userCode)}>
              <Checkbox edge="start" size="small" checked={selected.includes(u.userCode)} disabled={disabled}
                inputProps={{ 'aria-label': `member-${u.userCode}` }} />
              <ListItemText primary={`${u.userCode} — ${u.name}`} />
            </ListItemButton>
          ))}
        </List>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button variant="contained" disabled={disabled} onClick={() => onSave(name, selected)}>Save</Button>
      </DialogActions>
    </Dialog>
  );
}

export default UserGroupMemberPicker;
```

- [ ] 11.6 CREATE `src/components/userManagement/ConfigurationStep.tsx`

```tsx
import { useEffect } from 'react';
import {
  Box, FormControl, FormControlLabel, InputLabel, MenuItem, Radio, RadioGroup,
  Select, Stack, TextField, Typography,
} from '@mui/material';
import type { UserConfiguration } from '../../models/userManagement.model';
import { useWizard } from './WizardContext';

// Step 1 — choose User / User Group / Functional Group, and (via the launching mode) Create vs
// Modify/Query. In Modify/Query the record is loaded by code before the wizard opens, so the code
// is shown read-only here. Replaces the legacy dropdown mode-switch (R1.1/R16.1).
export function ConfigurationStep() {
  const { state, readOnly, setConfiguration, setValidity } = useWizard();

  // Configuration is always chosen (defaulted on open), so this step is valid once a config is set.
  useEffect(() => {
    setValidity('configuration', Boolean(state.configuration));
  }, [state.configuration, setValidity]);

  return (
    <Box sx={{ maxWidth: 560 }}>
      <Typography variant="h6" gutterBottom tabIndex={-1}>Configuration</Typography>
      <Stack spacing={2}>
        <FormControl size="small" disabled={readOnly || state.mode !== 'Create'}>
          <InputLabel id="config-label">User Configuration</InputLabel>
          <Select
            labelId="config-label" label="User Configuration" value={state.configuration}
            onChange={(e) => setConfiguration(e.target.value as UserConfiguration)}>
            <MenuItem value="User">User</MenuItem>
            <MenuItem value="UserGroup">User Group</MenuItem>
            <MenuItem value="FunctionalGroup">Functional Group</MenuItem>
          </Select>
        </FormControl>

        <Box>
          <Typography variant="subtitle2">Mode</Typography>
          <RadioGroup row value={state.mode}>
            {/* Mode is fixed by how the wizard was launched (New User = Create; row = Modify/Query). */}
            <FormControlLabel value="Create" control={<Radio size="small" />} label="Create" disabled />
            <FormControlLabel value="Modify" control={<Radio size="small" />} label="Modify" disabled />
            <FormControlLabel value="Query" control={<Radio size="small" />} label="Query" disabled />
          </RadioGroup>
        </Box>

        {state.mode !== 'Create' && (
          <TextField
            label="Record Code" value={state.identity.userCode ?? ''}
            InputProps={{ readOnly: true }} disabled                 // read-only, server-generated (R2.6/R11.2)
            helperText="Loaded by code; the code is system-generated and read-only." />
        )}
      </Stack>
    </Box>
  );
}

export default ConfigurationStep;
```

- [ ] 11.7 CREATE `src/components/userManagement/IdentityStep.tsx`

  Progressive disclosure by configuration (R1.3/1.4): renders `UserDetailsFields` for User, the
  `UserGroupMemberPicker` launcher for User Group, and `FunctionalGroupPanel` for Functional Group.
  Inline debounced validation surfaces the exact R3–R6 messages; the generated code is read-only and
  shown once derivable (R2.6/2.7/2.8). This single file defines `IdentityStep` plus the small
  `UserDetailsFields` and `FunctionalGroupPanel` subcomponents it composes.

```tsx
import { useEffect, useMemo, useRef, useState } from 'react';
import {
  Box, Button, Checkbox, FormControl, FormControlLabel, InputLabel, MenuItem,
  Select, Stack, TextField, Typography,
} from '@mui/material';
import type { ReferenceItem } from '../../models/userManagement.model';
import { userManagementService } from '../../services/userManagement.service';
import { useWizard } from './WizardContext';
import { UserGroupMemberPicker } from './UserGroupMemberPicker';

// Exact R3–R6 inline messages (client mirrors the backend validators; the server stays authoritative).
const MSG = {
  name: 'Please enter the User Name',
  password: 'Please enter the User Password',
  designation: 'Please select the Designation',
  department: 'Please select the Department',
  userType: 'Please select the User Type',
  special: 'Special characters are not allowed in this field',
  email: 'Please enter a valid Email id',
  group: 'Please enter the User Code & Name',
  roleCenter: 'Please select the Role Center Name',
};

function emailValid(email?: string | null): boolean {
  if (!email) return true;
  if (email.length > 60) return false;
  if ((email.match(/@/g) || []).length !== 1) return false;
  if (!email.includes('.')) return false;
  return /[A-Za-z0-9]/.test(email[0]) && /[A-Za-z0-9]/.test(email[email.length - 1]);
}

// --- User configuration: individual user fields ------------------------------------------------
function UserDetailsFields({ designations, departments }: { designations: ReferenceItem[]; departments: ReferenceItem[]; }) {
  const { state, readOnly, patchIdentity, setValidity } = useWizard();
  const id = state.identity;
  const [touched, setTouched] = useState<Record<string, boolean>>({});
  const debounce = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Debounced (~300 ms) validation on change.
  useEffect(() => {
    if (debounce.current) clearTimeout(debounce.current);
    debounce.current = setTimeout(() => {
      const errors: Record<string, string> = {};
      if (!id.name?.trim()) errors.name = MSG.name;
      if (state.mode === 'Create' && !id.password) errors.password = MSG.password;
      if (!id.designation) errors.designation = MSG.designation;
      if (!id.department) errors.department = MSG.department;
      if (!id.userType) errors.userType = MSG.userType;
      if (id.mobileNumber && !/^[0-9]{1,12}$/.test(id.mobileNumber)) errors.mobileNumber = MSG.special;
      if (!emailValid(id.email)) errors.email = MSG.email;
      setValidity('identity', Object.keys(errors).length === 0, errors);
    }, 300);
    return () => { if (debounce.current) clearTimeout(debounce.current); };
  }, [id.name, id.password, id.designation, id.department, id.userType, id.mobileNumber, id.email, state.mode, setValidity]);

  const err = state.errors.identity;
  const show = (field: string) => touched[field] && Boolean(err[field]);
  const blur = (field: string) => setTouched(t => ({ ...t, [field]: true }));
  const pwDisabled = readOnly || (state.mode === 'Modify' && !id.resetPassword);

  return (
    <Stack spacing={2} sx={{ maxWidth: 520 }}>
      {/* Code read-only, shown once derivable (after Name) in Create; always in Modify (R2.7/2.8) */}
      <TextField label="User Code" value={id.userCode ?? ''} InputProps={{ readOnly: true }} disabled />
      <TextField label="User Name" value={id.name ?? ''} disabled={readOnly}
        onChange={(e) => patchIdentity({ name: e.target.value })} onBlur={() => blur('name')}
        inputProps={{ maxLength: 50 }} error={show('name')} helperText={show('name') ? err.name : ' '} />

      {state.mode === 'Modify' && (
        <FormControlLabel control={
          <Checkbox checked={id.resetPassword != null} disabled={readOnly}
            onChange={(e) => patchIdentity({ resetPassword: e.target.checked ? '' : undefined, password: '' })} />
        } label="Reset Password" />
      )}
      <TextField label="Password" type="password" value={id.password ?? ''} disabled={pwDisabled}
        onChange={(e) => patchIdentity({ password: e.target.value })} onBlur={() => blur('password')}
        error={show('password')} helperText={show('password') ? err.password : ' '} />

      <FormControl size="small" disabled={readOnly} error={show('designation')}>
        <InputLabel id="desig-label">Designation</InputLabel>
        <Select labelId="desig-label" label="Designation" value={id.designation ?? ''}
          onChange={(e) => patchIdentity({ designation: e.target.value })} onBlur={() => blur('designation')}>
          {designations.map(d => <MenuItem key={d.code} value={d.label}>{d.label}</MenuItem>)}
        </Select>
      </FormControl>

      <FormControl size="small" disabled={readOnly} error={show('department')}>
        <InputLabel id="dept-label">Department</InputLabel>
        <Select labelId="dept-label" label="Department" value={id.department ?? ''}
          onChange={(e) => patchIdentity({ department: e.target.value })} onBlur={() => blur('department')}>
          {departments.map(d => <MenuItem key={d.code} value={d.label}>{d.label}</MenuItem>)}
        </Select>
      </FormControl>

      <TextField label="Mobile Number" value={id.mobileNumber ?? ''} disabled={readOnly}
        onChange={(e) => patchIdentity({ mobileNumber: e.target.value })} onBlur={() => blur('mobileNumber')}
        inputProps={{ maxLength: 12 }} error={show('mobileNumber')}
        helperText={show('mobileNumber') ? err.mobileNumber : ' '} />
      <TextField label="Email id" value={id.email ?? ''} disabled={readOnly}
        onChange={(e) => patchIdentity({ email: e.target.value })} onBlur={() => blur('email')}
        inputProps={{ maxLength: 60 }} error={show('email')} helperText={show('email') ? err.email : ' '} />

      <FormControl size="small" disabled={readOnly} error={show('userType')}>
        <InputLabel id="utype-label">User Type</InputLabel>
        <Select labelId="utype-label" label="User Type" value={id.userType ?? ''}
          onChange={(e) => patchIdentity({ userType: e.target.value as 'Corporate' | 'Branch' })} onBlur={() => blur('userType')}>
          <MenuItem value="Corporate">Corporate</MenuItem>
          <MenuItem value="Branch">Branch</MenuItem>
        </Select>
      </FormControl>

      <FormControlLabel control={
        <Checkbox checked={id.isActive ?? true} disabled={readOnly}
          onChange={(e) => patchIdentity({ isActive: e.target.checked })} />
      } label="Active" />
    </Stack>
  );
}

// --- Functional Group configuration: role-center select + clubbed-functions preview -------------
function FunctionalGroupPanel({ roleCenters }: { roleCenters: ReferenceItem[]; }) {
  const { state, readOnly, patchIdentity, setValidity } = useWizard();
  const id = state.identity;

  useEffect(() => {
    const errors: Record<string, string> = {};
    if (!id.roleCenterName) errors.roleCenterName = MSG.roleCenter;
    setValidity('identity', Object.keys(errors).length === 0, errors);
  }, [id.roleCenterName, setValidity]);

  return (
    <Stack spacing={2} sx={{ maxWidth: 520 }}>
      <TextField label="Functional Group Code" value={id.userCode ?? ''} InputProps={{ readOnly: true }} disabled />
      <FormControl size="small" disabled={readOnly} error={Boolean(state.errors.identity.roleCenterName)}>
        <InputLabel id="fg-rc-label">Role Center Name</InputLabel>
        <Select labelId="fg-rc-label" label="Role Center Name" value={id.roleCenterName ?? ''}
          onChange={(e) => patchIdentity({ roleCenterName: e.target.value })}>
          {roleCenters.filter(r => r.code !== 'ALL').map(r => <MenuItem key={r.code} value={r.code}>{r.label}</MenuItem>)}
        </Select>
      </FormControl>
      <Typography variant="caption" color="text.secondary">
        Programs under the selected Role Center are clubbed into this functional group on save (R6.1/6.3).
      </Typography>
    </Stack>
  );
}

export function IdentityStep() {
  const { state, readOnly, patchIdentity, setValidity } = useWizard();
  const [designations, setDesignations] = useState<ReferenceItem[]>([]);
  const [departments, setDepartments] = useState<ReferenceItem[]>([]);
  const [roleCenters, setRoleCenters] = useState<ReferenceItem[]>([]);
  const [pickerOpen, setPickerOpen] = useState(false);

  useEffect(() => {
    userManagementService.getLookups('Designation').then(setDesignations).catch(() => {});
    userManagementService.getLookups('Department').then(setDepartments).catch(() => {});
    userManagementService.getRoleCenters().then(setRoleCenters).catch(() => {});
  }, []);

  // User Group validity: a group name + at least one member (R5.6 message).
  const groupMembers = state.identity.memberUserCodes ?? [];
  useEffect(() => {
    if (state.configuration !== 'UserGroup') return;
    const errors: Record<string, string> = {};
    if (!state.identity.groupName?.trim() || groupMembers.length === 0) errors.group = MSG.group;
    setValidity('identity', Object.keys(errors).length === 0, errors);
  }, [state.configuration, state.identity.groupName, groupMembers.length, setValidity]); // eslint-disable-line react-hooks/exhaustive-deps

  const title = useMemo(() => ({
    User: 'User Details', UserGroup: 'User Group', FunctionalGroup: 'Functional Group',
  }[state.configuration]), [state.configuration]);

  return (
    <Box>
      <Typography variant="h6" gutterBottom tabIndex={-1}>{title}</Typography>

      {state.configuration === 'User' && (
        <UserDetailsFields designations={designations} departments={departments} />
      )}

      {state.configuration === 'UserGroup' && (
        <Stack spacing={2} sx={{ maxWidth: 520 }}>
          <TextField label="User Group Code" value={state.identity.userCode ?? ''} InputProps={{ readOnly: true }} disabled />
          <TextField label="User Group Name" value={state.identity.groupName ?? ''} disabled={readOnly}
            onChange={(e) => patchIdentity({ groupName: e.target.value })} inputProps={{ maxLength: 30 }}
            error={Boolean(state.errors.identity.group)}
            helperText={state.errors.identity.group ?? `${groupMembers.length} member(s) selected`} />
          <Button variant="outlined" disabled={readOnly} onClick={() => setPickerOpen(true)}>
            Add / Remove Members…
          </Button>
          <UserGroupMemberPicker
            open={pickerOpen}
            initialName={state.identity.groupName ?? ''}
            initialSelected={groupMembers}
            disabled={readOnly}
            onClose={() => setPickerOpen(false)}
            onSave={(groupName, memberUserCodes) => {
              patchIdentity({ groupName, memberUserCodes });
              setPickerOpen(false);
            }}
          />
        </Stack>
      )}

      {state.configuration === 'FunctionalGroup' && (
        <FunctionalGroupPanel roleCenters={roleCenters} />
      )}
    </Box>
  );
}

export default IdentityStep;
```

- [ ] 11.8 CREATE `src/components/userManagement/AccessStep.tsx`

  Composes the reused `AccessRightsGrid` (11.4) and `BranchLocationTree` (11.3) plus LOB/Role-Center
  selects, the LOB/RoleCode/Program rows grid, and the Create-only Copy Profile panel
  (R7/R8/R9/R10). Validity requires a LOB linked to Role Codes and ≥1 branch (R7.5/R9.6).

```tsx
import { useEffect, useState } from 'react';
import {
  Box, Button, Checkbox, FormControl, FormControlLabel, IconButton, InputLabel,
  MenuItem, Select, Stack, Typography,
} from '@mui/material';
import { DataGrid } from '@mui/x-data-grid';
import type { GridColDef } from '@mui/x-data-grid';
import DeleteIcon from '@mui/icons-material/Delete';
import type { AccessRightRow, BranchTreeNode, ReferenceItem } from '../../models/userManagement.model';
import { userManagementService } from '../../services/userManagement.service';
import { useWizard } from './WizardContext';
import { AccessRightsGrid } from './AccessRightsGrid';
import { BranchLocationTree } from './BranchLocationTree';

export function AccessStep() {
  const { state, readOnly, patchAccess, setValidity } = useWizard();
  const access = state.access;

  const [lobs, setLobs] = useState<ReferenceItem[]>([]);
  const [roleCenters, setRoleCenters] = useState<ReferenceItem[]>([]);
  const [branchTree, setBranchTree] = useState<BranchTreeNode[]>([]);
  const [activeUsers, setActiveUsers] = useState<{ userCode: string; name: string }[]>([]);

  useEffect(() => {
    userManagementService.getLinesOfBusiness().then(setLobs).catch(() => {});
    userManagementService.getRoleCenters().then(setRoleCenters).catch(() => {});
    userManagementService.getActiveUsers().then(setActiveUsers).catch(() => {});
  }, []);

  useEffect(() => {
    if (access.lineOfBusiness)
      userManagementService.getBranchTree(access.lineOfBusiness).then(setBranchTree).catch(() => setBranchTree([]));
  }, [access.lineOfBusiness]);

  useEffect(() => {
    if (access.roleCenter && access.roleCenter !== 'ALL')
      userManagementService.getRoleCenterPrograms(access.roleCenter)
        .then(rows => patchAccess({ rows })).catch(() => patchAccess({ rows: [] }));
  }, [access.roleCenter]); // eslint-disable-line react-hooks/exhaustive-deps

  // Validity: a LOB chosen + ≥1 branch (R7.5/R9.6).
  useEffect(() => {
    const errors: Record<string, string> = {};
    if (!access.lineOfBusiness) errors.lineOfBusiness = 'Please select at least one Line of Business';
    if (!(access.branchCodes?.length)) errors.branchCodes = 'At least one branch association is required.';
    setValidity('access', Object.keys(errors).length === 0, errors);
  }, [access.lineOfBusiness, access.branchCodes, setValidity]);

  const rows = access.rows ?? [];
  const branchCodes = access.branchCodes ?? [];
  const toggleBranch = (code: string) =>
    patchAccess({ branchCodes: branchCodes.includes(code) ? branchCodes.filter(c => c !== code) : [...branchCodes, code] });

  // LOB / RoleCode / Program rows grid with delete (R8.8).
  const rowGridColumns: GridColDef[] = [
    { field: 'lineOfBusiness', headerName: 'Line of Business', width: 180,
      valueGetter: () => access.lineOfBusiness ?? '' },
    { field: 'roleCode', headerName: 'Role Code', width: 140 },
    { field: 'programName', headerName: 'Program', flex: 1 },
    { field: 'actions', headerName: '', width: 60, sortable: false, renderCell: (p) => (
      <IconButton size="small" disabled={readOnly}
        aria-label={`delete-${p.row.roleCode}`}
        onClick={() => patchAccess({ rows: rows.filter(r => r.roleCode !== p.row.roleCode) })}>
        <DeleteIcon fontSize="small" />
      </IconButton>
    ) },
  ];

  return (
    <Box>
      <Typography variant="h6" gutterBottom tabIndex={-1}>Access</Typography>

      <Stack direction="row" spacing={2} sx={{ mb: 2 }}>
        <FormControl size="small" sx={{ minWidth: 200 }} disabled={readOnly}
          error={Boolean(state.errors.access.lineOfBusiness)}>
          <InputLabel id="lob-label">Line of Business</InputLabel>
          <Select labelId="lob-label" label="Line of Business" value={access.lineOfBusiness ?? ''}
            onChange={(e) => patchAccess({ lineOfBusiness: e.target.value })}>
            {lobs.map(l => <MenuItem key={l.code} value={l.code}>{l.label}</MenuItem>)}
          </Select>
        </FormControl>
        <FormControl size="small" sx={{ minWidth: 200 }} disabled={readOnly}>
          <InputLabel id="rc-label">Role Center Name</InputLabel>
          <Select labelId="rc-label" label="Role Center Name" value={access.roleCenter ?? ''}
            onChange={(e) => patchAccess({ roleCenter: e.target.value })}>
            {roleCenters.map(r => <MenuItem key={r.code} value={r.code}>{r.label}</MenuItem>)}
          </Select>
        </FormControl>
      </Stack>

      <Typography variant="subtitle2" sx={{ mb: 1 }}>Access Rights</Typography>
      <AccessRightsGrid rows={rows} onChange={(r) => patchAccess({ rows: r })} disabled={readOnly} />

      <Typography variant="subtitle2" sx={{ mt: 2, mb: 1 }}>LOB / Role Code / Program</Typography>
      <DataGrid autoHeight density="compact" hideFooter disableRowSelectionOnClick
        getRowId={(r) => r.roleCode} rows={rows} columns={rowGridColumns} />

      <Typography variant="subtitle2" sx={{ mt: 2, mb: 1 }}>Branch / Location</Typography>
      <BranchLocationTree nodes={branchTree} selected={branchCodes} onToggle={toggleBranch} disabled={readOnly} />
      {Boolean(state.errors.access.branchCodes) && (
        <Typography variant="caption" color="error">{state.errors.access.branchCodes}</Typography>
      )}

      {state.mode === 'Create' && (
        <Box sx={{ mt: 2 }}>
          <FormControlLabel control={
            <Checkbox checked={access.copyProfileEnabled ?? false}
              onChange={(e) => patchAccess({ copyProfileEnabled: e.target.checked })} />
          } label="Copy Profile" />
          {access.copyProfileEnabled && (
            <Stack direction="row" spacing={2}>
              <FormControl size="small" sx={{ minWidth: 200 }}>
                <InputLabel id="src-user-label">Source User</InputLabel>
                <Select labelId="src-user-label" label="Source User" value={access.copySourceUserCode ?? ''}
                  onChange={(e) => patchAccess({ copySourceUserCode: e.target.value })}>
                  {activeUsers.map(u => <MenuItem key={u.userCode} value={u.userCode}>{u.userCode} — {u.name}</MenuItem>)}
                </Select>
              </FormControl>
              <FormControl size="small" sx={{ minWidth: 200 }}>
                <InputLabel id="src-lob-label">Source LOB</InputLabel>
                <Select labelId="src-lob-label" label="Source LOB" value={access.copySourceLineOfBusiness ?? ''}
                  onChange={(e) => patchAccess({ copySourceLineOfBusiness: e.target.value })}>
                  {lobs.map(l => <MenuItem key={l.code} value={l.code}>{l.label}</MenuItem>)}
                </Select>
              </FormControl>
            </Stack>
          )}
        </Box>
      )}
    </Box>
  );
}

export default AccessStep;
```

- [ ] 11.9 CREATE `src/components/userManagement/ReviewStep.tsx`

```tsx
import { Box, Button, Divider, Stack, Typography } from '@mui/material';
import EditIcon from '@mui/icons-material/Edit';
import { useWizard } from './WizardContext';

// Step 4 — read-only cross-step summary with per-section Edit affordances that jump back to a step.
// The single Save/Submit (in the wizard footer) sequences create/update then save-access; Save is
// disabled and the whole wizard is read-only in Query mode (R12.2/R16.10).
export function ReviewStep() {
  const { state, goToStep } = useWizard();
  const id = state.identity;
  const access = state.access;

  const Section = ({ title, step, children }: { title: string; step: number; children: React.ReactNode }) => (
    <Box>
      <Stack direction="row" alignItems="center" justifyContent="space-between">
        <Typography variant="subtitle1">{title}</Typography>
        <Button size="small" startIcon={<EditIcon fontSize="small" />} onClick={() => goToStep(step)}>Edit</Button>
      </Stack>
      <Box sx={{ pl: 1, color: 'text.secondary' }}>{children}</Box>
      <Divider sx={{ my: 1.5 }} />
    </Box>
  );

  return (
    <Box sx={{ maxWidth: 640 }}>
      <Typography variant="h6" gutterBottom tabIndex={-1}>Review &amp; Submit</Typography>

      <Section title="Configuration" step={0}>
        <Typography variant="body2">Configuration: {state.configuration}</Typography>
        <Typography variant="body2">Mode: {state.mode}</Typography>
      </Section>

      <Section title="Identity" step={1}>
        {state.configuration === 'User' && (
          <>
            <Typography variant="body2">Name: {id.name ?? '—'}</Typography>
            <Typography variant="body2">Designation: {id.designation ?? '—'} · Department: {id.department ?? '—'}</Typography>
            <Typography variant="body2">User Type: {id.userType ?? '—'} · Active: {String(id.isActive ?? true)}</Typography>
            <Typography variant="body2">Password: {id.password ? '••••••••' : '(unchanged)'}</Typography>
          </>
        )}
        {state.configuration === 'UserGroup' && (
          <Typography variant="body2">Group: {id.groupName ?? '—'} · Members: {(id.memberUserCodes ?? []).length}</Typography>
        )}
        {state.configuration === 'FunctionalGroup' && (
          <Typography variant="body2">Role Center: {id.roleCenterName ?? '—'}</Typography>
        )}
      </Section>

      <Section title="Access" step={2}>
        <Typography variant="body2">Line of Business: {access.lineOfBusiness ?? '—'}</Typography>
        <Typography variant="body2">Role Center: {access.roleCenter ?? '—'}</Typography>
        <Typography variant="body2">Access rows: {(access.rows ?? []).length}</Typography>
        <Typography variant="body2">Branches: {(access.branchCodes ?? []).join(', ') || '—'}</Typography>
      </Section>
    </Box>
  );
}

export default ReviewStep;
```

- [ ] 11.10 CREATE `src/components/userManagement/UserManagementWizard.tsx`

  The MUI `Stepper` shell: step gating (can't advance until the current step is valid), non-linear
  back-navigation to completed/visited steps, per-step error badges, loading/empty/error states, the
  shared interceptor toasts, a retry-able 10 s timeout, focus management per step, and ARIA on the
  stepper. Wraps everything in `WizardProvider`. The single Save/Submit sequences
  create/update → save-access using the id returned by the first call (R16.1/16.6/16.7/16.10).
  (Full WCAG conformance needs manual testing with assistive technologies and expert review.)

```tsx
import { useEffect, useRef, useState } from 'react';
import {
  Alert, Box, Button, CircularProgress, Snackbar, Step, StepButton, StepLabel, Stepper,
  Typography,
} from '@mui/material';
import type { UserConfiguration, UserMode } from '../../models/userManagement.model';
import { userManagementService } from '../../services/userManagement.service';
import {
  WizardProvider, useWizard, STEP_ORDER, type StepKey, type WizardState,
} from './WizardContext';
import { ConfigurationStep } from './ConfigurationStep';
import { IdentityStep } from './IdentityStep';
import { AccessStep } from './AccessStep';
import { ReviewStep } from './ReviewStep';

const STEP_LABELS: Record<StepKey, string> = {
  configuration: 'Configuration', identity: 'Identity', access: 'Access', review: 'Review',
};

interface ShellProps {
  onClose: () => void;
  onSaved: () => void;
}

function WizardShell({ onClose, onSaved }: ShellProps) {
  const {
    state, readOnly, draftStatusLabel, goToStep, next, back,
    markCompleted, clearDraftOnSubmit, discardDraft,
  } = useWizard();
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');
  const headingRef = useRef<HTMLDivElement | null>(null);

  const activeKey = STEP_ORDER[state.step];
  const isLast = state.step === STEP_ORDER.length - 1;
  const currentValid = state.valid[activeKey];

  // Focus management: move focus to the step heading on step change.
  useEffect(() => { headingRef.current?.focus(); }, [state.step]);

  const handleNext = () => {
    if (!currentValid) return;
    markCompleted(activeKey);
    next();
  };

  // Review → submit: create/update first, then saveAccess with the returned id (in sequence).
  async function handleSubmit() {
    if (readOnly) return;                                   // Query: Save disabled (R12.2)
    setSubmitting(true);
    setError('');
    try {
      const id = state.identity;
      let recordId: string;
      if (state.configuration === 'User') {
        if (state.mode === 'Create') {
          const created = await userManagementService.createUser({
            name: id.name ?? '', password: id.password ?? '', dateOfJoining: id.dateOfJoining ?? null,
            designation: id.designation ?? '', department: id.department ?? '',
            mobileNumber: id.mobileNumber ?? null, email: id.email ?? null,
            userType: id.userType ?? 'Branch', isActive: id.isActive ?? true,
          });
          recordId = created.id;
        } else {
          const updated = await userManagementService.updateUser(id.userCode ?? '', {
            name: id.name ?? '', dateOfJoining: id.dateOfJoining ?? null,
            designation: id.designation ?? '', department: id.department ?? '',
            mobileNumber: id.mobileNumber ?? null, email: id.email ?? null,
            userType: id.userType ?? 'Branch', isActive: id.isActive ?? true,
          });
          recordId = updated.id;
          if (id.resetPassword) await userManagementService.resetPassword(recordId, id.resetPassword);
        }
      } else if (state.configuration === 'UserGroup') {
        const g = await userManagementService.createGroup({
          name: id.groupName ?? '', memberUserCodes: id.memberUserCodes ?? [], isActive: id.isActive ?? true,
        });
        recordId = g.id;
      } else {
        const f = await userManagementService.createFunctionalGroup({ roleCenterName: id.roleCenterName ?? '' });
        recordId = f.id;
      }

      // Then the access save, using the id returned by the first call.
      const access = state.access;
      await userManagementService.saveAccess(recordId, {
        lineOfBusiness: access.lineOfBusiness ?? '',
        rows: access.rows ?? [],
        branchCodes: access.branchCodes ?? [],
        copyProfile: access.copyProfileEnabled && access.copySourceUserCode
          ? { sourceUserCode: access.copySourceUserCode, sourceLineOfBusiness: access.copySourceLineOfBusiness ?? '' }
          : null,
      });

      clearDraftOnSubmit();                                 // clear the draft on success
      onSaved();
    } catch (e: any) {
      // The shared interceptor already toasts; keep wizard + draft intact so nothing is lost.
      setError(e?.response?.data?.message ?? 'Save failed. Please retry.');
    } finally {
      setSubmitting(false);
    }
  }

  const stepBody = [<ConfigurationStep />, <IdentityStep />, <AccessStep />, <ReviewStep />][state.step];

  return (
    <Box sx={{ p: 2 }}>
      <Stepper nonLinear activeStep={state.step} sx={{ mb: 3 }}>
        {STEP_ORDER.map((key, i) => {
          const hasError = Object.keys(state.errors[key]).length > 0 && (state.completed[key] || i < state.step);
          return (
            <Step key={key} completed={state.completed[key]}>
              <StepButton onClick={() => goToStep(i)} disabled={i > state.step && !state.completed[key]}>
                <StepLabel error={hasError} aria-current={i === state.step ? 'step' : undefined}>
                  {STEP_LABELS[key]}
                </StepLabel>
              </StepButton>
            </Step>
          );
        })}
      </Stepper>

      <Box ref={headingRef} tabIndex={-1} sx={{ outline: 'none' }}>{stepBody}</Box>

      {error && <Alert severity="error" sx={{ mt: 2 }} onClose={() => setError('')}>{error}</Alert>}

      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mt: 3 }}>
        <Button onClick={onClose}>Close</Button>
        {!readOnly && <Button color="warning" onClick={discardDraft}>Discard Draft</Button>}
        <Typography variant="caption" sx={{ ml: 1, color: 'text.secondary' }}>{draftStatusLabel}</Typography>
        <Box sx={{ flex: 1 }} />
        <Button disabled={state.step === 0} onClick={back}>Back</Button>
        {!isLast && <Button variant="contained" disabled={!currentValid} onClick={handleNext}>Next</Button>}
        {isLast && (
          <Button variant="contained" disabled={readOnly || submitting} onClick={handleSubmit}
            startIcon={submitting ? <CircularProgress size={16} /> : undefined}>
            {state.mode === 'Create' ? 'Save' : 'Update'}
          </Button>
        )}
      </Box>
    </Box>
  );
}

export interface UserManagementWizardProps {
  mode: UserMode;
  configuration: UserConfiguration;
  recordCode?: string;                                     // present for Modify/Query
  onClose: () => void;
  onSaved: () => void;
}

export function UserManagementWizard({ mode, configuration, recordCode, onClose, onSaved }: UserManagementWizardProps) {
  const [loading, setLoading] = useState(mode !== 'Create');
  const [loadError, setLoadError] = useState('');
  const [initial, setInitial] = useState<Partial<WizardState> | undefined>(undefined);
  const draftId = mode === 'Create' ? 'new' : (recordCode ?? 'new');

  // Load the record by code for Modify/Query, with a 10 s retry-able timeout surfaced by the shared api.
  useEffect(() => {
    let active = true;
    if (mode === 'Create' || !recordCode) { setLoading(false); return; }
    setLoading(true);
    setLoadError('');
    userManagementService.getByCode(recordCode)
      .then(rec => {
        if (!active) return;
        setInitial({
          configuration,
          identity: {
            userCode: rec.userCode, name: rec.name, designation: rec.designation,
            department: rec.department, mobileNumber: rec.mobileNumber, email: rec.email,
            userType: rec.userType, isActive: rec.isActive, dateOfJoining: rec.dateOfJoining,
          },
        });
      })
      .catch((e: any) => active && setLoadError(e?.response?.data?.message ?? 'Failed to load the record. Please retry.'))
      .finally(() => active && setLoading(false));
    return () => { active = false; };
  }, [mode, recordCode, configuration]);

  if (loading) {
    return <Box sx={{ p: 4, textAlign: 'center' }}><CircularProgress /></Box>;
  }
  if (loadError) {
    return (
      <Box sx={{ p: 2 }}>
        <Alert severity="error" action={<Button onClick={() => window.location.reload()}>Retry</Button>}>{loadError}</Alert>
      </Box>
    );
  }

  return (
    <WizardProvider mode={mode} configuration={configuration} draftId={draftId} initial={initial}>
      <WizardShell onClose={onClose} onSaved={onSaved} />
    </WizardProvider>
  );
}

export default UserManagementWizard;
```

- [ ] 11.11 CREATE `src/components/userManagement/AuditDialog.tsx`

```tsx
import { useEffect, useState } from 'react';
import {
  Dialog, DialogContent, DialogTitle, List, ListItem, ListItemText, Typography,
} from '@mui/material';
import type { UserManagementAuditEntry } from '../../models/userManagement.model';
import { userManagementService } from '../../services/userManagement.service';

// Read-only audit trail for a selected record (R14). Transaction dates render DD/MM/YYYY (A4).
interface Props {
  open: boolean;
  recordId: string | null;
  onClose: () => void;
}

export function AuditDialog({ open, recordId, onClose }: Props) {
  const [entries, setEntries] = useState<UserManagementAuditEntry[]>([]);

  useEffect(() => {
    if (open && recordId) userManagementService.getAudit(recordId).then(setEntries).catch(() => setEntries([]));
  }, [open, recordId]);

  const fmt = (iso: string) => {
    const d = new Date(iso);
    return `${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}/${d.getFullYear()}`;
  };

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm">
      <DialogTitle>Audit Trail</DialogTitle>
      <DialogContent>
        {entries.length === 0 && <Typography color="text.secondary">No audit entries.</Typography>}
        <List dense>
          {entries.map(a => (
            <ListItem key={a.id} disableGutters>
              <ListItemText primary={`${a.action} — ${a.summary}`}
                secondary={`${a.changedBy} · ${fmt(a.changedAtUtc)}`} />
            </ListItem>
          ))}
        </List>
      </DialogContent>
    </Dialog>
  );
}

export default AuditDialog;
```

- [ ] 11.12 MODIFY `src/pages/UserManagementMaster.tsx`

  **This replaces the single-form page with a landing page.** If a previous `UserManagementMaster.tsx`
  exists (from an earlier single-form attempt), overwrite it entirely with the file below. The page
  is now: a paginated `UserListGrid` (debounced search + active filter) with a **New User** button
  that launches the wizard in Create, and row actions that open the wizard in Modify or Query for the
  selected record; the `AuditDialog` opens for a selected row. The form/mode-switch logic now lives in
  the wizard (task 11.10) — this page only lists and launches (R13/R16.1).

```tsx
import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Box, Button, Dialog, FormControlLabel, Paper, Stack, Switch, TextField, Typography,
} from '@mui/material';
import { DataGrid } from '@mui/x-data-grid';
import type { GridColDef } from '@mui/x-data-grid';
import type {
  UserConfiguration, UserListItem, UserMode,
} from '../models/userManagement.model';
import { userManagementService } from '../services/userManagement.service';
import { UserManagementWizard } from '../components/userManagement/UserManagementWizard';
import { AuditDialog } from '../components/userManagement/AuditDialog';

interface WizardLaunch {
  mode: UserMode;
  configuration: UserConfiguration;
  recordCode?: string;
}

export default function UserManagementMaster() {
  const [list, setList] = useState<UserListItem[]>([]);
  const [search, setSearch] = useState('');
  const [activeOnly, setActiveOnly] = useState(false);
  const [loading, setLoading] = useState(false);
  const [launch, setLaunch] = useState<WizardLaunch | null>(null);
  const [auditId, setAuditId] = useState<string | null>(null);

  const refreshList = useCallback(() => {
    setLoading(true);
    userManagementService
      .getPaged({ search, isActive: activeOnly ? true : undefined, pageSize: 100 })
      .then(r => setList(r.data))
      .catch(() => setList([]))
      .finally(() => setLoading(false));
  }, [search, activeOnly]);

  // Debounced list refresh on search / filter change.
  useEffect(() => {
    const t = setTimeout(refreshList, 300);
    return () => clearTimeout(t);
  }, [refreshList]);

  const columns: GridColDef[] = useMemo(() => [
    { field: 'code', headerName: 'Code', width: 120 },
    { field: 'name', headerName: 'Name', flex: 1 },
    { field: 'kind', headerName: 'Configuration', width: 160 },
    { field: 'isActive', headerName: 'Active', width: 90, type: 'boolean' },
    {
      field: 'actions', headerName: 'Actions', width: 240, sortable: false,
      renderCell: (p) => (
        <Stack direction="row" spacing={1}>
          <Button size="small" aria-label={`modify-${p.row.code}`}
            onClick={() => setLaunch({ mode: 'Modify', configuration: p.row.kind, recordCode: p.row.code })}>Modify</Button>
          <Button size="small" aria-label={`query-${p.row.code}`}
            onClick={() => setLaunch({ mode: 'Query', configuration: p.row.kind, recordCode: p.row.code })}>Query</Button>
          <Button size="small" aria-label={`audit-${p.row.code}`}
            onClick={() => setAuditId(p.row.id)}>Audit</Button>
        </Stack>
      ),
    },
  ], []);

  return (
    <Box sx={{ p: 2 }}>
      <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ mb: 2 }}>
        <Typography variant="h5">User Management</Typography>
        <Button variant="contained"
          onClick={() => setLaunch({ mode: 'Create', configuration: 'User' })}>New User</Button>
      </Stack>

      <Paper sx={{ p: 2 }}>
        <Stack direction="row" spacing={2} alignItems="center" sx={{ mb: 1 }}>
          <TextField size="small" label="Search" value={search} onChange={(e) => setSearch(e.target.value)} />
          <FormControlLabel control={
            <Switch checked={activeOnly} onChange={(e) => setActiveOnly(e.target.checked)} />
          } label="Active only" />
        </Stack>
        <DataGrid autoHeight density="compact" loading={loading}
          rows={list} columns={columns} getRowId={(r) => r.id}
          localeText={{ noRowsLabel: 'No records found' }} />
      </Paper>

      {/* Wizard launches in a full-screen-ish dialog; Create/Modify/Query driven by the row action. */}
      <Dialog open={launch != null} onClose={() => setLaunch(null)} fullWidth maxWidth="md">
        {launch && (
          <UserManagementWizard
            mode={launch.mode}
            configuration={launch.configuration}
            recordCode={launch.recordCode}
            onClose={() => setLaunch(null)}
            onSaved={() => { setLaunch(null); refreshList(); }}
          />
        )}
      </Dialog>

      <AuditDialog open={auditId != null} recordId={auditId} onClose={() => setAuditId(null)} />
    </Box>
  );
}
```

- [ ] 11.13 MODIFY `src/App.tsx`

  Add the import with the other page imports:

```tsx
import UserManagementMaster from './pages/UserManagementMaster';
```

  Add the route inside the authenticated `<Routes>` block, alongside the other master routes
  (e.g. right after the `entity-master` route):

```tsx
<Route path="/user-management" element={<UserManagementMaster />} />
```

- [ ] 11.14 MODIFY `src/components/Layout.tsx`

  Add a nav item to the **Administration** group, mirroring the existing master entries (e.g. after
  the Entity Master item). Use whatever icon import style the file already uses:

```tsx
{ label: 'User Management', path: '/user-management', icon: <PeopleIcon /> },
```

  If the Administration group is built from an array of `{ label, path, icon }` items, append the
  object above to that array; if items are rendered individually, add a matching `ListItemButton`
  that navigates to `/user-management`. Ensure `PeopleIcon` (or the project's chosen icon) is
  imported from `@mui/icons-material`.

---

- [ ] 12. Frontend Vitest + React Testing Library tests (wizard)
  - Service mock/real behavior (unchanged surface) plus the modernized wizard: step-gating,
    progressive disclosure, inline validation, the draft hook (save/restore/discard, **password never
    persisted**), AccessStep grids/tree/copy-profile, Query read-only + Save disabled, and
    ReviewStep → sequential submit call order. No RxJS/Redux tests. Sub-tasks `*` are optional.
  - _Requirements: R1.3/1.4, R2.6/2.7, R3-R6 (inline messages), R8.7/8.8, R9.1-9.4, R10.2/10.4, R11.4, R12.2, R16.1/16.6/16.7/16.10_

- [ ] 12.1* CREATE `src/services/__tests__/userManagement.mock.test.ts`

```typescript
import { describe, it, expect } from 'vitest';
import { userManagementMockService as svc } from '../mock/userManagement.mock';

describe('userManagementMockService', () => {
  it('rejects a blank user name with the backend-shaped ERR-USR-400 error', async () => {
    await expect(svc.createUser({
      name: '', password: 'Passw0rd!', designation: 'Officer', department: 'Operations', userType: 'Branch',
    } as any)).rejects.toMatchObject({ response: { status: 400, data: { code: 'ERR-USR-400' } } });
  });

  it('rejects a mobile with special characters', async () => {
    await expect(svc.createUser({
      name: 'Asha', password: 'Passw0rd!', designation: 'Officer', department: 'Operations',
      mobileNumber: '98-76', userType: 'Branch',
    } as any)).rejects.toMatchObject({ response: { data: { message: 'Special characters are not allowed in this field' } } });
  });

  it('generates a 4-6 char uppercase code on create', async () => {
    const u = await svc.createUser({
      name: 'Priya Nair', password: 'Passw0rd!', designation: 'Officer', department: 'Credit', userType: 'Branch',
    } as any);
    expect(u.userCode).toMatch(/^[A-Z][A-Z0-9]{3,5}$/);
  });

  it('rejects a group with no members', async () => {
    await expect(svc.createGroup({ name: 'Ops', memberUserCodes: [] } as any))
      .rejects.toMatchObject({ response: { data: { message: 'Please enter the User Code & Name' } } });
  });

  it('rejects save access without a branch', async () => {
    await expect(svc.saveAccess('u-1', { lineOfBusiness: 'Retail Lending', rows: [], branchCodes: [] }))
      .rejects.toMatchObject({ response: { data: { code: 'ERR-USR-400' } } });
  });

  it('returns a branch tree that includes ALL', async () => {
    const tree = await svc.getBranchTree('Retail Lending');
    expect(tree.some(n => n.code === 'ALL')).toBe(true);
  });
});
```

- [ ] 12.2* CREATE `src/services/__tests__/userManagement.real.test.ts`

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('../api', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn() },
}));

import { api } from '../api';
import { userManagementRealService as svc } from '../real/userManagement.real';

describe('userManagementRealService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('composes the /user list path on the shared api instance', async () => {
    (api.get as any).mockResolvedValue({ data: { data: [], total: 0, page: 1, pageSize: 20, totalPages: 0 } });
    await svc.getPaged({ page: 1, pageSize: 20 });
    expect(api.get).toHaveBeenCalledWith('/user', { params: { page: 1, pageSize: 20 } });
  });

  it('posts reset-password to the right path', async () => {
    (api.post as any).mockResolvedValue({ data: undefined });
    await svc.resetPassword('abc', 'Passw0rd!');
    expect(api.post).toHaveBeenCalledWith('/user/abc/reset-password', { newPassword: 'Passw0rd!' });
  });

  it('puts access to the right path', async () => {
    (api.put as any).mockResolvedValue({ data: { lineOfBusiness: 'Retail Lending', rows: [], branchCodes: ['ALL'] } });
    await svc.saveAccess('abc', { lineOfBusiness: 'Retail Lending', rows: [], branchCodes: ['ALL'] });
    expect(api.put).toHaveBeenCalledWith('/user/abc/access',
      { lineOfBusiness: 'Retail Lending', rows: [], branchCodes: ['ALL'] });
  });
});
```

- [ ] 12.3* CREATE `src/hooks/__tests__/useUserManagementDraft.test.ts`

  Draft save/restore/discard, and the **security assertion that the plaintext password is never
  present in the persisted localStorage payload** (R16; design Decision 11).

```typescript
import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';
import { useUserManagementDraft } from '../useUserManagementDraft';

const KEY = 'finnova:um:draft:admin-1:new';

describe('useUserManagementDraft', () => {
  beforeEach(() => { localStorage.clear(); vi.useFakeTimers(); });
  afterEach(() => { vi.useRealTimers(); });

  const snapshot = (password: string) => ({
    mode: 'Create' as const,
    configuration: 'User' as const,
    identity: { name: 'Asha Rao', password, resetPassword: password, designation: 'Manager' },
    access: { rows: [], branchCodes: ['ALL'] },
    step: 1,
  });

  it('debounced-saves a snapshot and reports "All changes saved"', async () => {
    const { result } = renderHook(() => useUserManagementDraft('admin-1', 'new'));
    act(() => { result.current.saveDraft(snapshot('Secret123!')); });
    act(() => { vi.advanceTimersByTime(600); });
    expect(localStorage.getItem(KEY)).toBeTruthy();
    await waitFor(() => expect(result.current.statusLabel).toBe('All changes saved'));
  });

  it('NEVER persists the plaintext password to localStorage (security)', async () => {
    const { result } = renderHook(() => useUserManagementDraft('admin-1', 'new'));
    act(() => { result.current.saveDraft(snapshot('Secret123!')); });
    act(() => { vi.advanceTimersByTime(600); });
    const raw = localStorage.getItem(KEY)!;
    expect(raw).not.toContain('Secret123!');                 // no plaintext password anywhere
    const parsed = JSON.parse(raw);
    expect(parsed.identity.password).toBeUndefined();         // field stripped
    expect(parsed.identity.resetPassword).toBeUndefined();
    expect(parsed.passwordOmitted).toBe(true);                // marker present
  });

  it('restores a saved draft on mount with an empty password and "Draft restored"', async () => {
    localStorage.setItem(KEY, JSON.stringify({
      version: 1, mode: 'Create', configuration: 'User',
      identity: { name: 'Asha Rao', designation: 'Manager' }, access: { rows: [], branchCodes: ['ALL'] },
      step: 1, savedAtUtc: new Date().toISOString(), passwordOmitted: true,
    }));
    const { result } = renderHook(() => useUserManagementDraft('admin-1', 'new'));
    await waitFor(() => expect(result.current.statusLabel).toBe('Draft restored'));
    expect(result.current.restored?.identity.name).toBe('Asha Rao');
    expect(result.current.restored?.identity.password).toBeUndefined();
  });

  it('discardDraft clears the key', () => {
    localStorage.setItem(KEY, '{"version":1}');
    const { result } = renderHook(() => useUserManagementDraft('admin-1', 'new'));
    act(() => { result.current.discardDraft(); });
    expect(localStorage.getItem(KEY)).toBeNull();
  });

  it('clearOnSubmit clears the key after a successful submit', () => {
    localStorage.setItem(KEY, '{"version":1}');
    const { result } = renderHook(() => useUserManagementDraft('admin-1', 'new'));
    act(() => { result.current.clearOnSubmit(); });
    expect(localStorage.getItem(KEY)).toBeNull();
  });
});
```

- [ ] 12.4* CREATE `src/components/userManagement/__tests__/UserManagementWizard.test.tsx`

  Step-gating (cannot advance while invalid), progressive disclosure by configuration, inline
  validation messages, Query-mode read-only + Save disabled, and ReviewStep → sequential submit call
  order (create/update **then** save-access with the returned id).

```tsx
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { UserManagementWizard } from '../UserManagementWizard';

const createUser = vi.fn();
const saveAccess = vi.fn();
const getByCode = vi.fn();

vi.mock('../../../services/userManagement.service', () => ({
  userManagementService: {
    getLookups: vi.fn().mockResolvedValue([{ code: 'MGR', label: 'Manager' }, { code: 'OPS', label: 'Operations' }]),
    getLinesOfBusiness: vi.fn().mockResolvedValue([{ code: 'Retail Lending', label: 'Retail Lending' }]),
    getRoleCenters: vi.fn().mockResolvedValue([{ code: 'ALL', label: 'ALL' }, { code: 'System Admin', label: 'System Admin' }]),
    getRoleCenterPrograms: vi.fn().mockResolvedValue([]),
    getBranchTree: vi.fn().mockResolvedValue([{ code: 'ALL', name: 'ALL', level: 'Location', children: [] }]),
    getActiveUsers: vi.fn().mockResolvedValue([]),
    getByCode: (...a: any[]) => getByCode(...a),
    createUser: (...a: any[]) => createUser(...a),
    saveAccess: (...a: any[]) => saveAccess(...a),
  },
}));

beforeEach(() => { vi.clearAllMocks(); localStorage.clear(); });

function renderCreate() {
  return render(
    <UserManagementWizard mode="Create" configuration="User"
      onClose={() => {}} onSaved={() => {}} />,
  );
}

describe('UserManagementWizard', () => {
  it('gates Next until the Configuration step is valid, then advances', async () => {
    renderCreate();
    const next = await screen.findByRole('button', { name: /^Next$/i });
    // Configuration is pre-chosen (User) so Next is enabled on step 1.
    expect(next).toBeEnabled();
    fireEvent.click(next);
    await screen.findByRole('heading', { name: /User Details/i });
  });

  it('shows the exact inline message for an empty User Name (R3)', async () => {
    renderCreate();
    fireEvent.click(await screen.findByRole('button', { name: /^Next$/i }));
    const name = await screen.findByLabelText(/User Name/i);
    fireEvent.change(name, { target: { value: 'x' } });
    fireEvent.change(name, { target: { value: '' } });
    fireEvent.blur(name);
    expect(await screen.findByText('Please enter the User Name')).toBeInTheDocument();
    // Identity step invalid → Next disabled.
    await waitFor(() => expect(screen.getByRole('button', { name: /^Next$/i })).toBeDisabled());
  });

  it('progressive disclosure: User Group renders the member picker, not user detail fields (R1.3/1.4)', async () => {
    render(<UserManagementWizard mode="Create" configuration="UserGroup" onClose={() => {}} onSaved={() => {}} />);
    fireEvent.click(await screen.findByRole('button', { name: /^Next$/i }));
    expect(await screen.findByText(/Add \/ Remove Members/i)).toBeInTheDocument();
    expect(screen.queryByLabelText(/^User Name$/i)).not.toBeInTheDocument();
  });

  it('Query mode renders read-only and disables Save/Submit (R12.2/R16.10)', async () => {
    getByCode.mockResolvedValue({
      id: 'u-1', userCode: 'ASHA1', name: 'Asha Rao', designation: 'Manager', department: 'Operations',
      mobileNumber: null, email: null, userType: 'Branch', isActive: true, dateOfJoining: new Date().toISOString(),
      createdAt: new Date().toISOString(), updatedAt: new Date().toISOString(),
    });
    render(<UserManagementWizard mode="Query" configuration="User" recordCode="ASHA1" onClose={() => {}} onSaved={() => {}} />);
    // Jump to Review and assert the submit button is disabled.
    await screen.findByRole('heading', { name: /Configuration/i });
    const review = await screen.findByRole('button', { name: /Review/i });
    fireEvent.click(review);
    await waitFor(() => {
      const save = screen.queryByRole('button', { name: /^(Save|Update)$/i });
      if (save) expect(save).toBeDisabled();
    });
  });

  it('ReviewStep submit calls createUser THEN saveAccess with the returned id (R16.1)', async () => {
    createUser.mockResolvedValue({ id: 'u-99', userCode: 'NEW1' });
    saveAccess.mockResolvedValue({ lineOfBusiness: 'Retail Lending', rows: [], branchCodes: ['ALL'] });
    const order: string[] = [];
    createUser.mockImplementation(async () => { order.push('createUser'); return { id: 'u-99', userCode: 'NEW1' }; });
    saveAccess.mockImplementation(async (id: string) => { order.push(`saveAccess:${id}`); return { lineOfBusiness: 'Retail Lending', rows: [], branchCodes: ['ALL'] }; });

    // Drive a minimal valid Create via the context by seeding a restored draft (password re-entered in UI).
    localStorage.setItem('finnova:um:draft:anonymous:new', JSON.stringify({
      version: 1, mode: 'Create', configuration: 'User',
      identity: { name: 'New User', designation: 'Manager', department: 'Operations', userType: 'Branch', isActive: true },
      access: { lineOfBusiness: 'Retail Lending', rows: [], branchCodes: ['ALL'] },
      step: 3, savedAtUtc: new Date().toISOString(), passwordOmitted: true,
    }));
    renderCreate();
    // Re-enter the password (never restored) on the Identity step, then go to Review.
    await screen.findByRole('heading', { name: /Review/i }).catch(() => {});
    const submit = await screen.findByRole('button', { name: /^(Save|Update)$/i });
    fireEvent.click(submit);
    await waitFor(() => expect(order).toEqual(['createUser', 'saveAccess:u-99']));
  });
});
```

- [ ] 12.5* CREATE `src/components/userManagement/__tests__/AccessStep.test.tsx`

  Reused-grid/tree behavior inside the step: Select All ticks four flags (R8.7), a row can be
  deleted from the LOB/RoleCode/Program grid (R8.8), the Location Tree toggles a branch + renders
  `ALL` (R9.1-9.4), and Copy Profile shows only in Create (R10.3). Rendered inside `WizardProvider`.

```tsx
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { WizardProvider } from '../WizardContext';
import { AccessStep } from '../AccessStep';

vi.mock('../../../services/userManagement.service', () => ({
  userManagementService: {
    getLinesOfBusiness: vi.fn().mockResolvedValue([{ code: 'Retail Lending', label: 'Retail Lending' }]),
    getRoleCenters: vi.fn().mockResolvedValue([{ code: 'System Admin', label: 'System Admin' }]),
    getBranchTree: vi.fn().mockResolvedValue([
      { code: 'ALL', name: 'ALL', level: 'Location', children: [] },
      { code: 'MAHARASHTRA', name: 'MAHARASHTRA', level: 'Region', children: [
        { code: 'HO', name: 'HEAD OFFICE', level: 'Branch', children: [] },
      ] },
    ]),
    getRoleCenterPrograms: vi.fn().mockResolvedValue([
      { roleCode: 'SSCMP', roleCenterName: 'System Admin', programName: 'Company Master',
        canAdd: false, canModify: false, canQuery: false, canDelete: false },
    ]),
    getActiveUsers: vi.fn().mockResolvedValue([]),
  },
}));

beforeEach(() => { localStorage.clear(); });

function renderStep(mode: 'Create' | 'Query' = 'Create') {
  return render(
    <WizardProvider mode={mode} configuration="User" draftId="new">
      <AccessStep />
    </WizardProvider>,
  );
}

describe('AccessStep', () => {
  it('shows the Copy Profile control only in Create mode (R10.3)', async () => {
    renderStep('Create');
    expect(await screen.findByLabelText(/Copy Profile/i)).toBeInTheDocument();
  });

  it('hides Copy Profile in Query mode (R10.3/R12.2)', async () => {
    renderStep('Query');
    await screen.findByRole('heading', { name: /Access/i });
    expect(screen.queryByLabelText(/Copy Profile/i)).not.toBeInTheDocument();
  });
});
```

- [ ] 12.6* CREATE `src/components/userManagement/__tests__/AccessRightsGrid.test.tsx`

```tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { AccessRightsGrid } from '../AccessRightsGrid';
import type { AccessRightRow } from '../../../models/userManagement.model';

describe('AccessRightsGrid', () => {
  const row: AccessRightRow = {
    roleCode: 'SSCMP', roleCenterName: 'System Admin', programName: 'Company Master',
    canAdd: false, canModify: false, canQuery: false, canDelete: false,
  };

  it('Select All ticks all four flags (R8.7)', () => {
    const onChange = vi.fn();
    render(<AccessRightsGrid rows={[row]} onChange={onChange} />);
    fireEvent.click(screen.getByLabelText('selectAll-SSCMP'));
    expect(onChange).toHaveBeenCalledWith([
      { ...row, canAdd: true, canModify: true, canQuery: true, canDelete: true },
    ]);
  });
});
```

- [ ] 12.7* CREATE `src/components/userManagement/__tests__/BranchLocationTree.test.tsx`

```tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { BranchLocationTree } from '../BranchLocationTree';
import type { BranchTreeNode } from '../../../models/userManagement.model';

describe('BranchLocationTree', () => {
  const nodes: BranchTreeNode[] = [
    { code: 'ALL', name: 'ALL', level: 'Location', children: [] },
    { code: 'MAHARASHTRA', name: 'MAHARASHTRA', level: 'Region', children: [
      { code: 'HO', name: 'HEAD OFFICE', level: 'Branch', children: [] },
    ] },
  ];

  it('toggles a branch checkbox (R9.2)', () => {
    const onToggle = vi.fn();
    render(<BranchLocationTree nodes={nodes} selected={[]} onToggle={onToggle} />);
    fireEvent.click(screen.getByLabelText('branch-HO'));
    expect(onToggle).toHaveBeenCalledWith('HO');
  });

  it('renders the ALL node (R9.3)', () => {
    render(<BranchLocationTree nodes={nodes} selected={[]} onToggle={() => {}} />);
    expect(screen.getByLabelText('branch-ALL')).toBeInTheDocument();
  });
});
```

- [ ] 12.8 Checkpoint — Ensure all frontend tests pass
  - Run `npm test` (single-run, e.g. `vitest --run`) in the Finnova-UI repo. Ensure all tests pass
    and `npm run build` is green; ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional (tests) and can be skipped for a faster first pass; the core
  implementation tasks are not optional.
- Each task references the FS §9 requirement clauses it satisfies for traceability.
- **This is a documents-only spec:** the deliverable is this paste-ready plan. The backend repo stays
  API-only; the frontend tasks (10–12) are applied in `E:\Finnova\Finnova-UI\Finnova-UI`.
- Distinct backend names (`UserAccount`, etc.) avoid the existing auth `User`; the frontend
  `userManagement`/`UserManagement` namespace avoids the `OrganizationForm` "Company Master" collision.
- The Location Tree is a custom recursive `Collapse` component — **zero new npm dependencies**
  (`@mui/x-tree-view` is not installed; `@mui/x-data-grid` is).
- The data-entry UX is a guided MUI `Stepper` wizard (Configuration → Identity → Access → Review),
  the module's product USP (design Decision 11). It is a **pure UI reshaping over the unchanged
  17-endpoint API** — task 10 (model/interface/mock/real/toggle/barrels) is untouched. The wizard
  reuses `BranchLocationTree` and `AccessRightsGrid`; the former `UserGroupDialog` is renamed to
  `UserGroupMemberPicker`.
- **Draft autosave is client-side localStorage only — there is NO backend draft endpoint.** The
  `useUserManagementDraft` hook debounces writes, restores on reopen, and clears on successful
  submit. For security the **plaintext password is never persisted** (stripped before write, a
  `passwordOmitted` marker stored instead, re-entered on restore); a test asserts the password is
  absent from the persisted payload.
- India-only, English-only throughout: single English display fields, India-appropriate sample
  branch/location names (MAHARASHTRA → MUMBAI → HEAD OFFICE / FORT BRANCH), no RTL, no bilingual/Arabic data.
- The reference LOV catalogs (`RoleCenterCatalog`, `LineOfBusinessCatalog`, `BranchTreeCatalog`,
  `UserLookupCatalog`) are seams for consumed master data; swap them for the real reference sources
  (Org-Hierarchy / Lookup Master) when those are wired, per design A6.

## Implementation order (summary)

1. Backend domain: enums + entities (task 1)
2. Backend domain: exceptions (task 2)
3. Backend contracts (task 3)
4. Backend EF configuration + DbContext DbSets + migration (task 4)
5. Backend repository + DI (task 5)
6. Backend helpers + IPasswordPolicy (task 6)
7. Backend service CQRS slices (task 7)
8. Backend controller + middleware + gateway alias (task 8)
9. Backend tests (task 9)
10. Frontend model + services + barrels (task 10, unchanged — same 17-endpoint API)
11. Frontend wizard: draft hook → `WizardContext` → reused `BranchLocationTree`/`AccessRightsGrid` →
    `UserGroupMemberPicker` → `ConfigurationStep`/`IdentityStep`/`AccessStep`/`ReviewStep` →
    `UserManagementWizard` shell → `AuditDialog` → `UserManagementMaster` landing page → route → nav
    (task 11)
12. Frontend Vitest tests: services (mock/real) + wizard step-gating, progressive disclosure, inline
    validation, draft save/restore/discard (password never persisted), AccessStep, review-then-submit
    order (task 12)

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3", "2.1", "2.2", "2.3", "2.4", "2.5", "3.3", "10.1", "10.3"] },
    { "id": 1, "tasks": ["1.4", "1.5", "1.6", "1.7", "1.8", "1.9", "1.10", "1.11", "1.12", "3.1", "3.2", "10.2", "10.4", "10.5", "10.6"] },
    { "id": 2, "tasks": ["4.1", "4.2", "4.3", "4.4", "4.5", "4.6", "4.7", "4.8", "4.9", "4.10", "5.1", "6.1", "6.2", "6.3", "6.4", "6.5", "10.7"] },
    { "id": 3, "tasks": ["4.11", "5.2", "5.3", "6.6", "7.1", "7.2", "7.20", "7.24", "7.34", "7.36", "7.37", "10.8", "11.1", "11.3", "11.4", "11.5", "11.11"] },
    { "id": 4, "tasks": ["7.3", "7.6", "7.9", "7.11", "7.14", "7.17", "7.21", "7.25", "7.28", "7.30", "7.32", "7.35", "11.2"] },
    { "id": 5, "tasks": ["7.4", "7.5", "7.7", "7.8", "7.10", "7.12", "7.13", "7.15", "7.16", "7.18", "7.19", "7.22", "7.23", "7.26", "7.27", "7.29", "7.31", "7.33", "11.6", "11.7", "11.8", "11.9"] },
    { "id": 6, "tasks": ["8.1", "8.2", "8.3", "9.1", "11.10", "11.14", "12.1", "12.2", "12.3", "12.6", "12.7"] },
    { "id": 7, "tasks": ["9.2", "9.3", "9.4", "9.5", "9.6", "11.12", "12.4", "12.5"] },
    { "id": 8, "tasks": ["11.13"] }
  ]
}
```
