namespace MicroEMR.Web.Authentication;

public sealed record RefreshedTokenSet(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt);

public sealed class TokenRefreshInvalidGrantException : Exception
{
    public TokenRefreshInvalidGrantException()
        : base("The authenticated session can no longer be renewed.")
    {
    }
}

public sealed class TokenRefreshTemporarilyUnavailableException : Exception
{
    public TokenRefreshTemporarilyUnavailableException(Exception? innerException = null)
        : base("The authentication service is temporarily unavailable.", innerException)
    {
    }
}

public sealed class WebSessionReauthenticationRequiredException : UnauthorizedAccessException
{
    public WebSessionReauthenticationRequiredException()
        : base("The authenticated Web session must be renewed interactively.")
    {
    }
}
