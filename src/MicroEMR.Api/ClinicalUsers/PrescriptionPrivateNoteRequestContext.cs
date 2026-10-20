using MicroEMR.Application.PatientPrescriptions;

namespace MicroEMR.Api.ClinicalUsers;

public sealed class PrescriptionPrivateNoteRequestContext(IHttpContextAccessor http) : IPrescriptionPrivateNoteRequestContext
{
    public string CorrelationId => http.HttpContext?.TraceIdentifier
        ?? throw new InvalidOperationException("A clinical request correlation is required.");
}
