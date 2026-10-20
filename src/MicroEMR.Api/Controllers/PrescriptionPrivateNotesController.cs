using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.PatientPrescriptions;

namespace MicroEMR.Api.Controllers;

[MicroEMR.Application.SecurityAudit.SensitiveCapability(MicroEMR.Application.SecurityAudit.SecurityAuditCapabilities.PrescriptionPrivateNoteView)]
[ApiController, Authorize, RequirePermission(PermissionKeys.PatientsView), RequirePermission(PermissionKeys.PrescriptionsViewPrivateNotes)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PrescriptionPrivateNotesController(IPrescriptionPrivateNoteService notes) : ControllerBase
{
    [HttpGet("api/patients/{patientUid:guid}/prescriptions/{prescriptionUid:guid}/private-note")]
    public async Task<ActionResult<PrescriptionPrivateNoteDetail>> Get(Guid patientUid, Guid prescriptionUid, CancellationToken token)
    {
        try
        {
            var row = await notes.ReadAsync(patientUid, prescriptionUid, token);
            return row is null ? NotFound() : Ok(row);
        }
        catch (ClinicalUserResolutionException) { return Forbid(); }
        catch (PrescriptionPrivateNoteAccessException) { return Forbid(); }
    }
    [HttpGet("api/patients/{patientUid:guid}/prescriptions/{prescriptionUid:guid}/private-note/history")]
    public async Task<ActionResult<IReadOnlyList<PrescriptionPrivateNoteRevision>>> History(Guid patientUid, Guid prescriptionUid, CancellationToken token)
    {
        try
        {
            var rows = await notes.HistoryAsync(patientUid, prescriptionUid, token);
            return rows is null ? NotFound() : Ok(rows);
        }
        catch (ClinicalUserResolutionException) { return Forbid(); }
        catch (PrescriptionPrivateNoteAccessException) { return Forbid(); }
    }
}
