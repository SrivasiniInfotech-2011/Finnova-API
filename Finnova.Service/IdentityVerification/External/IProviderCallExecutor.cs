using Microsoft.Extensions.Options;

namespace Finnova.Service.IdentityVerification.External;

public interface IProviderCallExecutor
{
    /// <summary>
    /// Executes a provider call with retry and timeout handling.
    /// Retries only transient failures (network errors, server 5xx).
    /// </summary>
    /// <param name="execFunc">The provider call function to execute.</param>
    /// <param name="operationType">The operation type for logging and classification.</param>
    /// <param name="correlationId">The correlation identifier for diagnostics.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <typeparam name="T">The result type.</typeparam>
    /// <returns>The provider result on success.</returns>
    /// <exception cref="IdentityVerificationProviderUnavailableException">When all retries exhausted or timeout occurs.</exception>
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> execFunc,
        VerificationOperationType operationType,
        string correlationId,
        CancellationToken ct = default);
}
