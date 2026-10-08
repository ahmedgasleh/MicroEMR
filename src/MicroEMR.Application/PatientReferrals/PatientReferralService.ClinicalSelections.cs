using MicroEMR.Application.AccessProfiles;

namespace MicroEMR.Application.PatientReferrals;

public sealed partial class PatientReferralService
{
    public async Task<PatientReferralClinicalSelectionsResponse?> GetClinicalSelectionsAsync(
        Guid patientUid, Guid referralUid, CancellationToken cancellationToken = default)
    {
        var effective = await RequireSelectionAccessAsync(PermissionKeys.ReferralsView, cancellationToken);
        await EnsurePatientExistsAsync(patientUid, cancellationToken);
        var response = await referrals.GetClinicalSelectionsAsync(patientUid, referralUid, cancellationToken);
        if (response is not null)
            EnsureSourcePermissions(response.Selections.Select(x => x.SelectionKind), effective);
        return response;
    }

    public async Task<PatientReferralClinicalSelectionsResponse?> ReplaceDraftClinicalSelectionsAsync(
        Guid patientUid, Guid referralUid, ReplacePatientReferralClinicalSelectionsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateClinicalSelections(request);
        var effective = await RequireSelectionAccessAsync(PermissionKeys.ReferralsManage, cancellationToken);
        EnsureSourcePermissions(request.Selections.Select(x => x.SelectionKind), effective);
        await EnsurePatientExistsAsync(patientUid, cancellationToken);
        var current = await referrals.GetByUidAsync(patientUid, referralUid, cancellationToken);
        if (current is null) return null;
        if (current.Status != ReferralStatus.Draft)
            throw new PatientReferralTransitionException("Clinical selections can only change while the referral is Draft.");
        if (!string.Equals(current.RowVersion, request.RowVersion, StringComparison.Ordinal))
            throw new PatientReferralConcurrencyException();
        // Replacement must not silently remove restricted existing choices the caller cannot read.
        var selections = await referrals.GetClinicalSelectionsAsync(patientUid, referralUid, cancellationToken);
        if (selections is null) return null;
        EnsureSourcePermissions(selections.Selections.Select(x => x.SelectionKind), effective);
        var actor = await clinicalUserAccessor.GetRequiredUserIdAsync(cancellationToken);
        // Stored procedure rechecks version/status and validates each source's same-patient ownership atomically.
        return await referrals.ReplaceDraftClinicalSelectionsAsync(patientUid, referralUid, request, actor, cancellationToken);
    }

    private async Task<IReadOnlySet<string>> RequireSelectionAccessAsync(string referralPermission, CancellationToken token)
    {
        var effective = await (permissions ?? throw new UnauthorizedAccessException("Clinical selection permissions are unavailable."))
            .GetEffectivePermissionsAsync(token);
        if (!effective.Contains(PermissionKeys.PatientsView) || !effective.Contains(PermissionKeys.ReferralsView)
            || !effective.Contains(referralPermission))
            throw new UnauthorizedAccessException("Referral clinical-selection access is not permitted.");
        return effective;
    }

    private static void EnsureSourcePermissions(IEnumerable<string> kinds, IReadOnlySet<string> effective)
    {
        foreach (var kind in kinds.Distinct(StringComparer.Ordinal))
        {
            var permission = kind switch
            {
                ReferralClinicalSelectionKinds.Cpp => PermissionKeys.PatientsView,
                ReferralClinicalSelectionKinds.Encounter => PermissionKeys.EncountersView,
                ReferralClinicalSelectionKinds.Result => PermissionKeys.ResultsView,
                ReferralClinicalSelectionKinds.File => PermissionKeys.DocumentsView,
                _ => throw new ReferralClinicalSelectionRuleException("Unsupported clinical selection kind.")
            };
            if (!effective.Contains(permission))
                throw new UnauthorizedAccessException("Selected clinical-source access is not permitted.");
        }
    }

    private static void ValidateClinicalSelections(ReplacePatientReferralClinicalSelectionsRequest request)
    {
        byte[] version;
        try { version = Convert.FromBase64String(request.RowVersion); }
        catch (Exception e) when (e is ArgumentNullException or FormatException)
        { throw new ArgumentException("RowVersion is invalid.", nameof(request), e); }
        if (version.Length != 8) throw new ArgumentException("RowVersion is invalid.", nameof(request));
        if (request.Selections is null || request.Selections.Count > 500)
            throw new ArgumentException("Supply at most 500 clinical selections.", nameof(request));
        var unique = new HashSet<ReferralClinicalSelectionInput>();
        foreach (var selection in request.Selections)
        {
            if (selection is null) throw new ArgumentException("A clinical selection is required.", nameof(request));
            var valid = selection.SelectionKind switch
            {
                ReferralClinicalSelectionKinds.Cpp => selection.CppCategoryCode is ReferralCppCategoryCodes.Problems
                    or ReferralCppCategoryCodes.Allergies or ReferralCppCategoryCodes.Medications
                    && selection.EncounterUid is null && selection.ResultUid is null,
                ReferralClinicalSelectionKinds.Encounter => selection.CppCategoryCode is null && selection.ResultUid is null
                    && selection.EncounterUid.HasValue && selection.EncounterUid != Guid.Empty,
                ReferralClinicalSelectionKinds.Result => selection.CppCategoryCode is null && selection.EncounterUid is null
                    && selection.ResultUid.HasValue && selection.ResultUid != Guid.Empty,
                _ => false
            };
            if (selection.SelectionKind == ReferralClinicalSelectionKinds.File)
                valid = selection.FileUid.HasValue && selection.FileUid != Guid.Empty && selection.CppCategoryCode is null
                    && selection.EncounterUid is null && selection.ResultUid is null;
            else valid = valid && selection.FileUid is null;
            if (!valid) throw new ArgumentException("Invalid clinical selection kind or reference.", nameof(request));
            if (!unique.Add(selection)) throw new ArgumentException("Duplicate clinical selections are not allowed.", nameof(request));
        }
    }
}
