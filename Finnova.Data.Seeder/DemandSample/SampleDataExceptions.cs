using System;
using System.Collections.Generic;
using System.Text;

namespace Finnova.Data.Seeder.DemandSample
{
    public sealed class SampleDataCollisionException(string entityType, string logicalKey)
    : InvalidOperationException($"Sample data ownership collision for {entityType}:{logicalKey}.");

    public sealed class SampleDataValidationException(string message) : InvalidOperationException(message);
    public sealed record SampleOwnership(string EntityType, string LogicalKey, Guid EntityId, string ContentHash);
}
