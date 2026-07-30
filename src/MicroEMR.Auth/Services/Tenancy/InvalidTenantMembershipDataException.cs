namespace MicroEMR.Auth.Services.Tenancy;

public sealed class InvalidTenantMembershipDataException : InvalidDataException
{
    public InvalidTenantMembershipDataException(string userId, string message)
        : base($"Invalid tenant membership data for user '{userId}': {message}")
    {
    }
}
