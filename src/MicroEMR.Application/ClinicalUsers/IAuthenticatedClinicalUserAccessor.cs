namespace MicroEMR.Application.ClinicalUsers;

public interface IAuthenticatedClinicalUserAccessor
{
    Task<long> GetRequiredUserIdAsync(
        CancellationToken cancellationToken = default);
}

public sealed class ClinicalUserResolutionException(string message) : Exception(message);
