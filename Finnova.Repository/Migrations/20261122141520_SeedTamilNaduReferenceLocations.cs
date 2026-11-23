using System;
using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finnova.Repository.Migrations
{
    /// <inheritdoc />
    public partial class SeedTamilNaduReferenceLocations : Migration
    {
        // Parents appear before children so Down can delete in reverse order.
        // Values are compile-time constants; nothing user-supplied reaches the SQL.
        private static readonly (string Id, string Code, string Name, int Level, string ParentCode, string Description)[] Rows =
        [
            ("7a1e0000-0000-4000-8000-000000000001", "TN",  "Tamil Nadu",      2, "IND", "State of Tamil Nadu"),
            ("7a1e0000-0000-4000-8000-000000000002", "CHN", "Chennai",         3, "TN",  "Capital of Tamil Nadu"),
            ("7a1e0000-0000-4000-8000-000000000003", "CBE", "Coimbatore",      3, "TN",  "City in Tamil Nadu"),
            ("7a1e0000-0000-4000-8000-000000000004", "MDU", "Madurai",         3, "TN",  "City in Tamil Nadu"),
            ("7a1e0000-0000-4000-8000-000000000005", "SLM", "Salem",           3, "TN",  "City in Tamil Nadu"),
            ("7a1e0000-0000-4000-8000-000000000006", "TRI", "Tiruchirappalli", 3, "TN",  "City in Tamil Nadu"),
            ("7a1e0000-0000-4000-8000-000000000007", "TNJ", "Thanjavur",       3, "TN",  "City in Tamil Nadu")
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: insert only when the parent exists and has no child with this code
            // (matches the unique index on ParentId + Code). The parent id is read from the
            // database, so a parent created earlier with a different id is still used.
            foreach (var row in Rows)
            {
                migrationBuilder.Sql($@"
INSERT INTO [locations] ([Id], [Code], [Name], [Level], [ParentId], [Description], [IsActive], [CreatedAt], [UpdatedAt])
SELECT '{row.Id}', N'{row.Code}', N'{row.Name}', {row.Level}, p.[Id], N'{row.Description}', 1, '2024-01-01T00:00:00', '2024-01-01T00:00:00'
FROM [locations] p
WHERE p.[Code] = N'{row.ParentCode}' AND p.[Level] = {row.Level - 1}
  AND NOT EXISTS (SELECT 1 FROM [locations] c WHERE c.[ParentId] = p.[Id] AND c.[Code] = N'{row.Code}');");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove only the rows this migration owns, children first, and only when nothing
            // (for example sample zones) still hangs under them.
            foreach (var row in Rows.Reverse())
            {
                migrationBuilder.Sql($@"
DELETE FROM [locations]
WHERE [Id] = '{row.Id}'
  AND NOT EXISTS (SELECT 1 FROM [locations] c WHERE c.[ParentId] = '{row.Id}');");
            }
        }
    }
}