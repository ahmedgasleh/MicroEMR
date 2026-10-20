using System.Security.Claims;
using MicroEMR.Api.ClinicalUsers;
using MicroEMR.Application.PatientPrescriptions;

namespace MicroEMR.Api.Middleware;

// Runs before authorization/actor resolution so their short-circuit denials are covered.
public sealed class PrescriptionPrivateNoteDeniedAuditMiddleware(RequestDelegate next,
    ILogger<PrescriptionPrivateNoteDeniedAuditMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        if (context.User.Identity?.IsAuthenticated == true &&
            path.StartsWith("/api/patients/", StringComparison.OrdinalIgnoreCase) &&
            path.Contains("/prescriptions", StringComparison.OrdinalIgnoreCase) &&
            (path.Contains("/private-note", StringComparison.OrdinalIgnoreCase) ||
             !HttpMethods.IsGet(context.Request.Method)))
        {
            context.Response.OnStarting(async () =>
            {
                var status = context.Response.StatusCode;
                if (status is not (403 or 404 or 409)) return;
                var subject = context.User.FindFirstValue("sub") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrWhiteSpace(subject)) return;
                Guid? Scope(string name) => Guid.TryParse(context.Request.RouteValues[name]?.ToString(), out var uid) ? uid : null;
                try
                {
                    var repository = context.RequestServices.GetRequiredService<IPrescriptionPrivateNoteRepository>();
                    await repository.RecordDeniedAsync(subject,
                        ClinicalUserActorContext.TryGet(context, out var actor) ? actor : null,
                        Scope("patientUid"), Scope("prescriptionUid"), context.TraceIdentifier, status, context.RequestAborted);
                }
                catch (Exception)
                {
                    // A rejected operation stays rejected if audit storage is unavailable.
                    // No exception/request payload can enter operational logging here.
                    logger.LogError("Private prescription denial audit unavailable. Status: {Status}; CorrelationId: {CorrelationId}", status, context.TraceIdentifier);
                }
            });
        }
        await next(context);
    }
}
