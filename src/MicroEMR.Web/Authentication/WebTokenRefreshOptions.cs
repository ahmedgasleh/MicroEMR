namespace MicroEMR.Web.Authentication;

public sealed class WebTokenRefreshOptions
{
    public const string SectionName = "Authentication:TokenRefresh";

    public TimeSpan RefreshThreshold { get; set; } = TimeSpan.FromMinutes(1);
}
