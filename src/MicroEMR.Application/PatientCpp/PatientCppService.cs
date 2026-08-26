using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientAllergies.Services;
using MicroEMR.Application.PatientDocuments.Repositories;
using MicroEMR.Application.PatientEncounters.Repositories;
using MicroEMR.Application.PatientImmunizations;
using MicroEMR.Application.PatientMedications.Services;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Application.PatientProblems.Services;
using MicroEMR.Application.PatientReferrals;
using MicroEMR.Application.PatientResults;
using MicroEMR.Application.PatientVitals.Services;
using MicroEMR.Application.Patients.Repositories;
using MicroEMR.Application.ReadAudit;

namespace MicroEMR.Application.PatientCpp;

public interface IPatientCppService
{
    Task<PatientCppSummaryResponse?> GetAsync(
        Guid patientUid,
        string requestCorrelationId,
        CancellationToken cancellationToken = default);
}

public sealed class PatientCppService(
    IPatientRepository patients,
    IPatientProblemService problems,
    IPatientAllergyService allergies,
    IPatientMedicationService medications,
    IPatientPrescriptionService prescriptions,
    IPatientImmunizationService immunizations,
    IPatientResultRepository results,
    IPatientVitalService vitals,
    IPatientEncounterRepository encounters,
    IPatientReferralService referrals,
    IPatientDocumentRepository documents,
    ICurrentUserPermissionService permissions,
    IPatientChartReadAuditService readAudit,
    ILogger<PatientCppService> logger) : IPatientCppService
{
    private const int ClinicalLimit = 5;
    private const int ContextLimit = 1;

    public async Task<PatientCppSummaryResponse?> GetAsync(
        Guid patientUid,
        string requestCorrelationId,
        CancellationToken cancellationToken = default)
    {
        if (patientUid == Guid.Empty) throw new ArgumentException("A patient is required.", nameof(patientUid));

        var patient = await patients.GetByUidAsync(patientUid, cancellationToken);
        if (patient is null) return null;

        // This is the single fail-closed read-audit boundary for a normal chart + CPP load.
        await readAudit.RecordOpenedAsync(patientUid, requestCorrelationId, cancellationToken);

        var effective = await permissions.GetEffectivePermissionsAsync(cancellationToken);
        var problemTask = LoadProblems(patientUid, requestCorrelationId, cancellationToken);
        var allergyTask = LoadAllergies(patientUid, requestCorrelationId, cancellationToken);
        var medicationTask = LoadMedications(patientUid, requestCorrelationId, cancellationToken);
        var prescriptionTask = LoadPrescriptions(patientUid, requestCorrelationId, cancellationToken);
        var immunizationTask = LoadImmunizations(patientUid, requestCorrelationId, cancellationToken);
        var vitalTask = LoadVitals(patientUid, requestCorrelationId, cancellationToken);
        var resultTask = Authorized(effective, PermissionKeys.ResultsView)
            ? LoadResults(patientUid, requestCorrelationId, cancellationToken)
            : Task.FromResult(PatientCppSection<PatientCppResult>.NotAuthorized());
        var encounterTask = Authorized(effective, PermissionKeys.EncountersView)
            ? LoadEncounters(patientUid, requestCorrelationId, cancellationToken)
            : Task.FromResult(PatientCppSection<PatientCppEncounter>.NotAuthorized());
        var referralTask = Authorized(effective, PermissionKeys.ReferralsView)
            ? LoadReferrals(patientUid, requestCorrelationId, cancellationToken)
            : Task.FromResult(PatientCppSection<PatientCppReferral>.NotAuthorized());
        var documentTask = Authorized(effective, PermissionKeys.DocumentsView)
            ? LoadDocuments(patientUid, requestCorrelationId, cancellationToken)
            : Task.FromResult(PatientCppSection<PatientCppDocument>.NotAuthorized());

        await Task.WhenAll(problemTask, allergyTask, medicationTask, prescriptionTask, immunizationTask,
            vitalTask, resultTask, encounterTask, referralTask, documentTask);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - patient.DateOfBirth.Year;
        if (patient.DateOfBirth > today.AddYears(-age)) age--;
        var contact = !string.IsNullOrWhiteSpace(patient.PhoneNumber) ? patient.PhoneNumber : patient.Email;

        return new PatientCppSummaryResponse(
            patientUid,
            new PatientCppDemographics(patient.FullName, patient.PreferredName, patient.DateOfBirth, age,
                patient.SexAtBirth, patient.GenderIdentity, contact),
            await problemTask, await allergyTask, await medicationTask, await prescriptionTask,
            await immunizationTask, await resultTask, await vitalTask, await encounterTask,
            await referralTask, await documentTask);
    }

    private static bool Authorized(IReadOnlySet<string> effective, string permission) => effective.Contains(permission);

    private async Task<PatientCppSection<PatientCppProblem>> LoadProblems(Guid patientUid, string trace, CancellationToken token) =>
        await Load("Problems", trace, async () =>
        {
            var rows = (await problems.GetByPatientUidAsync(patientUid, "Active", token))
                .Where(x => string.Equals(x.ProblemStatus, "Active", StringComparison.OrdinalIgnoreCase)).ToArray();
            return PatientCppSection<PatientCppProblem>.From(rows.OrderByDescending(x => x.OnsetDate ?? x.CreatedAt)
                .Take(ClinicalLimit).Select(x => new PatientCppProblem(x.PatientProblemUid, x.ProblemName, x.ProblemStatus, x.OnsetDate)).ToArray(), rows.Length);
        });

    private async Task<PatientCppSection<PatientCppAllergy>> LoadAllergies(Guid patientUid, string trace, CancellationToken token) =>
        await Load("Allergies", trace, async () =>
        {
            var rows = (await allergies.GetByPatientUidAsync(patientUid, token))
                .Where(x => string.Equals(x.Status, "Active", StringComparison.OrdinalIgnoreCase)).ToArray();
            return PatientCppSection<PatientCppAllergy>.From(rows.OrderByDescending(x => x.OnsetDate ?? x.CreatedAt)
                .Take(ClinicalLimit).Select(x => new PatientCppAllergy(x.AllergyUid, x.AllergenName, x.Status, x.Reaction, x.Severity)).ToArray(), rows.Length);
        });

    private async Task<PatientCppSection<PatientCppMedication>> LoadMedications(Guid patientUid, string trace, CancellationToken token) =>
        await Load("Medications", trace, async () =>
        {
            var rows = (await medications.GetByPatientUidAsync(patientUid, token))
                .Where(x => string.Equals(x.Status, "Active", StringComparison.OrdinalIgnoreCase)).ToArray();
            return PatientCppSection<PatientCppMedication>.From(rows.OrderByDescending(x => x.StartDate ?? x.CreatedAt)
                .Take(ClinicalLimit).Select(x => new PatientCppMedication(x.MedicationUid, x.MedicationName, x.Strength, x.Route, x.Frequency, x.StartDate)).ToArray(), rows.Length);
        });

    private async Task<PatientCppSection<PatientCppPrescription>> LoadPrescriptions(Guid patientUid, string trace, CancellationToken token) =>
        await Load("Prescriptions", trace, async () =>
        {
            var rows = (await prescriptions.ListAsync(patientUid, token))
                .Where(x => string.Equals(x.Status, PrescriptionStatuses.Finalized, StringComparison.Ordinal)).ToArray();
            return PatientCppSection<PatientCppPrescription>.From(rows.OrderByDescending(x => x.PrescribedDate)
                .Take(ClinicalLimit).Select(x => new PatientCppPrescription(x.PrescriptionUid, x.ProductDisplayText, x.PrescribedDate, x.Directions)).ToArray(), rows.Length);
        });

    private async Task<PatientCppSection<PatientCppImmunization>> LoadImmunizations(Guid patientUid, string trace, CancellationToken token) =>
        await Load("Immunizations", trace, async () =>
        {
            var rows = (await immunizations.ListAsync(patientUid, "Completed", token))
                .Where(x => string.Equals(x.Status, "Completed", StringComparison.OrdinalIgnoreCase)).ToArray();
            return PatientCppSection<PatientCppImmunization>.From(rows.OrderByDescending(x => x.AdministrationDate)
                .Take(ClinicalLimit).Select(x => new PatientCppImmunization(x.ImmunizationUid, x.VaccineName, x.AdministrationDate, x.SourceType)).ToArray(), rows.Length);
        });

    private async Task<PatientCppSection<PatientCppResult>> LoadResults(Guid patientUid, string trace, CancellationToken token) =>
        await Load("Results", trace, async () =>
        {
            var rows = (await results.List(patientUid, "All", token))
                .Where(x => string.Equals(x.LifecycleStatus, "Current", StringComparison.OrdinalIgnoreCase)).ToArray();
            return PatientCppSection<PatientCppResult>.From(rows.OrderByDescending(x => x.ResultDate)
                .Take(ClinicalLimit).Select(x => new PatientCppResult(x.PatientResultUid, x.ResultName, x.ResultType,
                    x.ResultDate, x.ResultValue, x.ResultUnit, x.Abnormality, x.ResultStatus, Provenance(x))).ToArray(), rows.Length);
        });

    private async Task<PatientCppSection<PatientCppVitals>> LoadVitals(Guid patientUid, string trace, CancellationToken token) =>
        await Load("Vitals", trace, async () =>
        {
            var row = (await vitals.GetByPatientUidAsync(patientUid, token)).OrderByDescending(x => x.RecordedAt).FirstOrDefault();
            var items = row is null ? [] : new[] { new PatientCppVitals(row.PatientVitalUid, row.RecordedAt,
                row.BloodPressureSystolic, row.BloodPressureDiastolic, row.HeartRate, row.WeightKg, row.Bmi, row.OxygenSaturation) };
            return PatientCppSection<PatientCppVitals>.From(items, items.Length);
        });

    private async Task<PatientCppSection<PatientCppEncounter>> LoadEncounters(Guid patientUid, string trace, CancellationToken token) =>
        await Load("Encounters", trace, async () =>
        {
            var rows = (await encounters.GetByPatientUidAsync(patientUid, token))
                .Where(x => string.Equals(x.Status, "Signed", StringComparison.OrdinalIgnoreCase)).ToArray();
            return PatientCppSection<PatientCppEncounter>.From(rows.OrderByDescending(x => x.EncounterDateUtc).Take(ContextLimit)
                .Select(x => new PatientCppEncounter(x.EncounterUid, x.EncounterDateUtc, x.EncounterType, x.ProviderName, x.ReasonForVisit)).ToArray(), rows.Length);
        });

    private async Task<PatientCppSection<PatientCppReferral>> LoadReferrals(Guid patientUid, string trace, CancellationToken token) =>
        await Load("Referrals", trace, async () =>
        {
            var rows = (await referrals.GetByPatientUidAsync(patientUid, token))
                .Where(x => x.Status is "Draft" or "Sent" or "ResponseReceived").ToArray();
            return PatientCppSection<PatientCppReferral>.From(rows.OrderByDescending(x => x.CreatedAtUtc).Take(ContextLimit)
                .Select(x => new PatientCppReferral(x.ReferralUid, x.RecipientName, x.RecipientOrganization, x.Status, x.CreatedAtUtc)).ToArray(), rows.Length);
        });

    private async Task<PatientCppSection<PatientCppDocument>> LoadDocuments(Guid patientUid, string trace, CancellationToken token) =>
        await Load("Documents", trace, async () =>
        {
            var rows = await documents.GetByPatientUidAsync(patientUid, token);
            return PatientCppSection<PatientCppDocument>.From(rows.OrderByDescending(x => x.CreatedAt).Take(ContextLimit)
                .Select(x => new PatientCppDocument(x.DocumentUid, x.Title, x.DocumentType, x.Status, x.CreatedAt)).ToArray(), rows.Count);
        });

    private async Task<PatientCppSection<T>> Load<T>(string section, string trace, Func<Task<PatientCppSection<T>>> action)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            return await action();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "CPP section {CppSection} is unavailable. DurationMs: {DurationMs}; TraceIdentifier: {TraceIdentifier}.",
                section, Stopwatch.GetElapsedTime(started).TotalMilliseconds, trace);
            return PatientCppSection<T>.Unavailable();
        }
    }

    private static string Provenance(PatientResultResponse result) => result.SourceType switch
    {
        "External" when !string.IsNullOrWhiteSpace(result.SourceOrganization) => $"External · {result.SourceOrganization}",
        "External" when !string.IsNullOrWhiteSpace(result.SourceSystem) => $"External · {result.SourceSystem}",
        "External" => "External",
        _ => "Manual"
    };
}
