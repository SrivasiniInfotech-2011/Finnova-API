using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finnova.Repository.Migrations
{
    /// <summary>
    /// Repair migration. The migration chain in this project was squashed and never carried the
    /// creation of the base "organizations" and "users" tables (they were assumed to pre-exist
    /// from an earlier, pre-history schema). On a fresh database those tables are missing, so the
    /// later "AddUserPasswordAndSeedAdmin" migration — which ALTERs and seeds "users" — fails.
    ///
    /// This migration creates both tables so the chain applies cleanly on a fresh database. It is
    /// ordered (by its id timestamp) immediately after AddLookupValues and before
    /// AddUserPasswordAndSeedAdmin. The "users" table is created WITHOUT PasswordHash on purpose:
    /// AddUserPasswordAndSeedAdmin adds that column next, exactly as the original chain intended.
    ///
    /// Table creation is guarded with IF NOT EXISTS so a legacy database that already carries the
    /// pre-history tables is left untouched (the migration becomes a no-op there).
    /// </summary>
    public partial class CreateOrganizationsAndUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[organizations]', N'U') IS NULL
BEGIN
    CREATE TABLE [organizations] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(10) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [ConstitutionType] nvarchar(50) NOT NULL,
        [Description] nvarchar(500) NULL,
        [CeoName] nvarchar(200) NULL,
        [RegistrationNumber] nvarchar(100) NULL,
        [RegistrationDate] datetime2 NULL,
        [PanNumber] nvarchar(20) NULL,
        [GstNumber] nvarchar(30) NULL,
        [CorporateAddress] nvarchar(500) NULL,
        [CorporateCity] nvarchar(100) NULL,
        [CorporateState] nvarchar(100) NULL,
        [CorporateCountry] nvarchar(100) NULL,
        [CorporatePincode] nvarchar(10) NULL,
        [CommunicationAddress] nvarchar(500) NULL,
        [CommunicationCity] nvarchar(100) NULL,
        [CommunicationState] nvarchar(100) NULL,
        [CommunicationCountry] nvarchar(100) NULL,
        [CommunicationPincode] nvarchar(10) NULL,
        [Telephone] nvarchar(20) NULL,
        [Mobile] nvarchar(20) NULL,
        [Email] nvarchar(255) NULL,
        [Website] nvarchar(500) NULL,
        [AccountingCurrency] nvarchar(10) NOT NULL DEFAULT N'INR',
        [Status] nvarchar(50) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_organizations] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_organizations_Code] ON [organizations] ([Code]);
END;
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[users]', N'U') IS NULL
BEGIN
    CREATE TABLE [users] (
        [Id] uniqueidentifier NOT NULL,
        [FirstName] nvarchar(100) NOT NULL,
        [MiddleName] nvarchar(100) NULL,
        [LastName] nvarchar(100) NOT NULL,
        [Email] nvarchar(255) NOT NULL,
        [Phone] nvarchar(20) NULL,
        [Role] nvarchar(50) NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [OrganizationId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_users] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_users_organizations_OrganizationId] FOREIGN KEY ([OrganizationId])
            REFERENCES [organizations] ([Id]) ON DELETE SET NULL
    );
    CREATE UNIQUE INDEX [IX_users_Email] ON [users] ([Email]);
    CREATE INDEX [IX_users_OrganizationId] ON [users] ([OrganizationId]);
END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS [users];");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [organizations];");
        }
    }
}
