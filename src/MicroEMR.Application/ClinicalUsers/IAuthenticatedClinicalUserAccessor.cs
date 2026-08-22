namespace MicroEMR.Application.ClinicalUsers;

public interface IAuthenticatedClinicalUserAccessor
{
    Task<long> GetRequiredUserIdAsync(
        CancellationToken cancellationToken = default);
}

public sealed class ClinicalUserResolutionException : Exception
{
    public ClinicalUserResolutionException(string message) : base(message)
    {
    }

    public ClinicalUserResolutionException(string message, bool isCompletedUnresolved)
        : base(message)
    {
        IsCompletedUnresolved = isCompletedUnresolved;
    }

    public bool IsCompletedUnresolved { get; }
}
