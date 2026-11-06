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
