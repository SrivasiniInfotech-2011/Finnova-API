using System;
using System.Collections.Generic;
using System.Text;

namespace Finnova.Data.Seeder.DemandSample;

/// <summary>
/// Fixed fixture settings. The seeder rejects any value other than the defaults;
/// the type exists so tests can prove that rejection.
/// </summary>
public sealed class SampleDataOptions
{
    public string SampleDataKey { get; init; } = SampleDataCatalog.DatasetKey;
    public DateOnly SampleAsOfDate { get; init; } = SampleDataCatalog.AsOfDate;
    public string BusinessTimezone { get; init; } = "Asia/Kolkata";
}
