using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.ClinicalUsers;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.Patients.Contracts;
using MicroEMR.Application.Patients.Services;

namespace MicroEMR.Api.Controllers;

[ApiController, Authorize, Route("api/patients/registration")]
[RequirePermission(PermissionKeys.PatientsEdit)]
public sealed class PatientRegistrationController(
    IPatientRegistrationService registration,
    IAuthorizationService authorization) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<RegisterPatientResult>> Register(
        RegisterPatientRequest request, CancellationToken cancellationToken)
    {
        if (request.ReferringProviderUid.HasValue || request.AttendingProviderUid.HasValue ||
            request.PrimaryCareProviderUid.HasValue)
        {
            var patientAccess = await authorization.AuthorizeAsync(
                User, PermissionPolicyProvider.Prefix + PermissionKeys.PatientsView);
            var providerAccess = await authorization.AuthorizeAsync(
                User, PermissionPolicyProvider.Prefix + PermissionKeys.ProvidersView);
            if (!patientAccess.Succeeded || !providerAccess.Succeeded) return Forbid();
        }

        var result = await registration.RegisterAsync(
            request, ClinicalUserActorContext.GetRequired(HttpContext), cancellationToken);
        return CreatedAtAction("GetByUid", "Patients", new { patientUid = result.Patient.PatientUid }, result);
    }
}
