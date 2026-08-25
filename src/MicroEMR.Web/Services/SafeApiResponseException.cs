using System.Net;

namespace MicroEMR.Web.Services;

public sealed class SafeApiResponseException : HttpRequestException
{
    public SafeApiResponseException(HttpStatusCode statusCode, string responseBody)
        : base($"MicroEMR API request failed with status {(int)statusCode}.", null, statusCode)
    {
        ResponseBody = responseBody;
    }

    // Intended only for bounded UI validation parsing. Never write this value to operational logs.
    public string ResponseBody { get; }

    public static string ValidationBody(HttpRequestException exception) =>
        exception is SafeApiResponseException safe ? safe.ResponseBody : string.Empty;
}
