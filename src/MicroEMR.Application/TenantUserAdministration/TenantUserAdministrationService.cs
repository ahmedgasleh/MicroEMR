using Microsoft.Extensions.Logging;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.PlatformAdministration;
using MicroEMR.Application.Tenancy;
using MicroEMR.Application.AccessProfiles;

namespace MicroEMR.Application.TenantUserAdministration;

public sealed record TenantUserAdministrationItem(
    string AuthUserId,
    string UserName,
    string DisplayName,
    string? Email,
    bool AuthUserActive,
    string MembershipStatus,
    IReadOnlyCollection<string> TenantRoles,
    bool ClinicalUserProvisioned,
    long? ClinicalUserId,
    bool? ClinicalUserActive,
    DateTimeOffset? MembershipUpdatedAt,
    string RowVersion,
    bool IsCurrentUser,
    Guid? AccessProfileUid = null,
    string? AccessProfileName = null);

public interface ITenantUserAdministrationService
{
    Task<AddTenantUserResult> AddTenantUserAsync(AddTenantUserRequest request,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantUserAdministrationItem>> GetTenantUsersAsync(
        CancellationToken cancellationToken = default);
    async Task<TenantUserAdministrationItem?> GetTenantUserAsync(string authUserId,
        CancellationToken cancellationToken = default) =>
        (await GetTenantUsersAsync(cancellationToken)).SingleOrDefault(x =>
            string.Equals(x.AuthUserId, authUserId, StringComparison.Ordinal));
    Task<TenantUserAdministrationItem> DeactivateMembershipAsync(string authUserId, string rowVersion,
        CancellationToken cancellationToken = default);
    Task<TenantUserAdministrationItem> ActivateMembershipAsync(string authUserId, string rowVersion,
        CancellationToken cancellationToken = default);
    Task<TenantUserAdministrationItem> UpdateTenantRolesAsync(string authUserId,
        IReadOnlyCollection<string> selectedRoles, string rowVersion, CancellationToken cancellationToken = default);
    Task<TenantUserAdministrationItem> ProvisionClinicalUserAsync(string authUserId,
        CancellationToken cancellationToken = default);
    Task ResetPasswordAsync(string authUserId,string temporaryPassword,CancellationToken cancellationToken=default);
}

public sealed record AddTenantUserRequest(string FirstName, string LastName, string Email,
    string InitialRole, bool ProvisionClinicalUser, string? TemporaryPassword = null);
public sealed record AddTenantUserResult(TenantUserAdministrationItem User, bool AuthIdentityCreated,
    bool ClinicalProvisioningFailed, string Message);

public sealed class TenantUserAdministrationService(
    ITenantContext tenantContext,
    IPlatformMembershipAdministrationService memberships,
    IIdentityUserProfileLookup identityUsers,
    IClinicalUserRepository clinicalUsers,
    ITenantMembershipLifecycleRepository lifecycle,
    ITenantRoleManagementRepository roleManagement,
    IAuthenticatedSubjectAccessor subjectAccessor,
    IIdentityUserAdministration identityAdministration,
    ITenantUserCreationRepository userCreation,
    IAccessProfileRepository accessProfiles,
    ILogger<TenantUserAdministrationService> logger) : ITenantUserAdministrationService
{
    public TenantUserAdministrationService(ITenantContext tenantContext,
        IPlatformMembershipAdministrationService memberships, IIdentityUserProfileLookup identityUsers,
        IClinicalUserRepository clinicalUsers, ITenantMembershipLifecycleRepository lifecycle,
        ITenantRoleManagementRepository roleManagement, IAuthenticatedSubjectAccessor subjectAccessor,
        IIdentityUserAdministration identityAdministration,ITenantUserCreationRepository userCreation,
        ILogger<TenantUserAdministrationService> logger)
        : this(tenantContext,memberships,identityUsers,clinicalUsers,lifecycle,roleManagement,subjectAccessor,
            identityAdministration,userCreation,new UnsupportedAccessProfiles(),logger) { }

    public TenantUserAdministrationService(ITenantContext tenantContext,
        IPlatformMembershipAdministrationService memberships, IIdentityUserProfileLookup identityUsers,
        IClinicalUserRepository clinicalUsers, ITenantMembershipLifecycleRepository lifecycle,
        ITenantRoleManagementRepository roleManagement, IAuthenticatedSubjectAccessor subjectAccessor,
        ILogger<TenantUserAdministrationService> logger)
        : this(tenantContext, memberships, identityUsers, clinicalUsers, lifecycle, roleManagement, subjectAccessor,
            new UnsupportedIdentityAdministration(), new UnsupportedUserCreationRepository(), new UnsupportedAccessProfiles(), logger) { }

    public async Task<AddTenantUserResult> AddTenantUserAsync(AddTenantUserRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var role = NormalizeRole(request.InitialRole);
        var identity = await identityAdministration.ResolveOrCreateAsync(
            new(request.FirstName, request.LastName, request.Email, request.TemporaryPassword), cancellationToken);
        var existing = await memberships.GetTenantMembershipsAsync(tenantContext.TenantUid, cancellationToken);
        if (existing.Any(x => string.Equals(x.UserId, identity.Profile.UserId, StringComparison.Ordinal)))
            throw new TenantMembershipAlreadyExistsException("This user already belongs to this clinic.");

        var actor = subjectAccessor.GetRequiredSubject();
        await userCreation.CreateAsync(identity.Profile.UserId, tenantContext.TenantUid, role, actor, cancellationToken);
        logger.LogInformation("Tenant membership and initial role {Role} created for {TargetUserId} in {TenantUid} by {ActorUserId}.",
            role, identity.Profile.UserId, tenantContext.TenantUid, actor);

        var provisioningFailed = false;
        if (request.ProvisionClinicalUser)
        {
            try
            {
                await clinicalUsers.ProvisionAsync(identity.Profile.UserId, identity.Profile.Username,
                    identity.Profile.DisplayName, identity.Profile.Email, cancellationToken);
                logger.LogInformation("Clinical user provisioned for {TargetUserId} in {TenantUid} by {ActorUserId}.",
                    identity.Profile.UserId, tenantContext.TenantUid, actor);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                provisioningFailed = true;
                logger.LogError(ex, "Clinical provisioning failed after tenant access was created for {TargetUserId} in {TenantUid}.",
                    identity.Profile.UserId, tenantContext.TenantUid);
            }
        }

        var user = (await GetTenantUsersAsync(cancellationToken)).Single(x =>
            string.Equals(x.AuthUserId, identity.Profile.UserId, StringComparison.Ordinal));
        var message = provisioningFailed
            ? "User added to clinic, but clinical provisioning failed. Retry from User Details."
            : identity.Created
                ? "User account created with the temporary password and added to the clinic. Ask the user to sign in and change it immediately."
                : "User added to clinic.";
        return new(user, identity.Created, provisioningFailed, message);
    }

    private static string NormalizeRole(string role)
    {
        try { return TenantRoleCatalog.Normalize(role); }
        catch (ArgumentException ex) { throw new TenantRoleValidationException("The initial tenant role is invalid.", ex); }
    }

    private sealed class UnsupportedIdentityAdministration : IIdentityUserAdministration
    {
        public Task<ResolveOrCreateIdentityResult> ResolveOrCreateAsync(ResolveOrCreateIdentityRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class UnsupportedUserCreationRepository : ITenantUserCreationRepository
    {
        public Task CreateAsync(string authUserId, Guid tenantUid, string initialRole, string actorAuthUserId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class UnsupportedAccessProfiles : IAccessProfileRepository
    {
        public Task<IReadOnlyList<AccessProfileSummary>> ListAsync(Guid t,CancellationToken c=default)=>Task.FromResult<IReadOnlyList<AccessProfileSummary>>([]);
        public Task<AccessProfileDetails?> GetAsync(Guid t,Guid p,CancellationToken c=default)=>Task.FromResult<AccessProfileDetails?>(null);
        public Task<IReadOnlyDictionary<string,UserAccessProfile>> GetAssignmentsAsync(Guid t,IReadOnlyCollection<string> u,CancellationToken c=default)=>Task.FromResult<IReadOnlyDictionary<string,UserAccessProfile>>(new Dictionary<string,UserAccessProfile>());
        public Task UpdatePermissionsAsync(Guid t,Guid p,IReadOnlyCollection<string> k,string v,string a,CancellationToken c=default)=>throw new NotSupportedException();
        public Task AssignAsync(Guid t,string u,Guid p,string v,string a,CancellationToken c=default)=>throw new NotSupportedException();
        public Task<(string MembershipStatus,IReadOnlyCollection<string> PermissionKeys)> GetEffectiveAsync(Guid t,string u,CancellationToken c=default)=>Task.FromResult<(string,IReadOnlyCollection<string>)>(("Missing",[]));
    }
    public async Task<IReadOnlyList<TenantUserAdministrationItem>> GetTenantUsersAsync(
        CancellationToken cancellationToken = default)
    {
        var actorSubject = subjectAccessor.GetRequiredSubject();
        var tenantMemberships = await memberships.GetTenantMembershipsAsync(
            tenantContext.TenantUid, cancellationToken);
        var userIds = tenantMemberships.Select(x => x.UserId).Distinct(StringComparer.Ordinal).ToArray();
        var identities = await identityUsers.GetByIdsAsync(userIds, cancellationToken);
        var clinicalUsersBySubject = await clinicalUsers.GetByAuthSubjectIdsAsync(userIds, cancellationToken);
        var profileAssignments = await accessProfiles.GetAssignmentsAsync(tenantContext.TenantUid,userIds,cancellationToken);
        var results = new List<TenantUserAdministrationItem>(tenantMemberships.Count);

        foreach (var membership in tenantMemberships)
        {
            var identity = identities.GetValueOrDefault(membership.UserId)
                ?? throw new InvalidDataException("A tenant membership references an unavailable Auth identity.");
            var clinical = clinicalUsersBySubject.GetValueOrDefault(identity.UserId);

            results.Add(new TenantUserAdministrationItem(
                identity.UserId,
                identity.Username,
                identity.DisplayName,
                identity.Email,
                identity.IsActive,
                membership.MembershipStatus,
                membership.Roles.ToArray(),
                clinical is not null,
                clinical?.UserId,
                clinical?.IsActive,
                membership.UpdatedAt,
                membership.RowVersion ?? throw new InvalidDataException("Membership concurrency metadata is unavailable."),
                string.Equals(identity.UserId, actorSubject, StringComparison.Ordinal),
                profileAssignments.GetValueOrDefault(identity.UserId)?.AccessProfileUid,
                profileAssignments.GetValueOrDefault(identity.UserId)?.AccessProfileName));
        }

        logger.LogInformation("Tenant user administration list loaded for tenant {TenantUid}; count {UserCount}.",
            tenantContext.TenantUid, results.Count);
        return results.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.UserName, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<TenantUserAdministrationItem?> GetTenantUserAsync(string authUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authUserId);
        return (await GetTenantUsersAsync(cancellationToken)).SingleOrDefault(x =>
            string.Equals(x.AuthUserId, authUserId, StringComparison.Ordinal));
    }

    public Task<TenantUserAdministrationItem> DeactivateMembershipAsync(string authUserId, string rowVersion,
        CancellationToken cancellationToken = default) =>
        ChangeMembershipAsync(authUserId, rowVersion, activate: false, cancellationToken);

    public Task<TenantUserAdministrationItem> ActivateMembershipAsync(string authUserId, string rowVersion,
        CancellationToken cancellationToken = default) =>
        ChangeMembershipAsync(authUserId, rowVersion, activate: true, cancellationToken);

    public async Task<TenantUserAdministrationItem> UpdateTenantRolesAsync(string authUserId,
        IReadOnlyCollection<string> selectedRoles, string rowVersion, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rowVersion);
        ArgumentNullException.ThrowIfNull(selectedRoles);
        string[] roles;
        try
        {
            roles = selectedRoles.Select(TenantRoleCatalog.Normalize).Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        }
        catch (ArgumentException ex) { throw new TenantRoleValidationException("One or more tenant roles are not recognized.", ex); }
        if (roles.Length == 0) throw new TenantRoleValidationException("An active membership must have at least one tenant role.");
        var actor = subjectAccessor.GetRequiredSubject();
        await roleManagement.ReplaceRolesAsync(authUserId, tenantContext.TenantUid, roles, rowVersion, actor, cancellationToken);
        logger.LogInformation("Tenant roles updated for user {TargetUserId} in tenant {TenantUid} by {ActorUserId}.",
            authUserId, tenantContext.TenantUid, actor);
        return (await GetTenantUsersAsync(cancellationToken)).Single(x =>
            string.Equals(x.AuthUserId, authUserId, StringComparison.Ordinal));
    }

    public async Task<TenantUserAdministrationItem> ProvisionClinicalUserAsync(string authUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authUserId);
        var membership = (await memberships.GetTenantMembershipsAsync(tenantContext.TenantUid, cancellationToken))
            .SingleOrDefault(x => string.Equals(x.UserId, authUserId, StringComparison.Ordinal));
        if (membership is null)
            throw new TenantMembershipNotFoundException("The membership was not found in the active tenant.");
        if (!string.Equals(membership.MembershipStatus, "Active", StringComparison.Ordinal))
            throw new TenantClinicalProvisioningNotEligibleException(
                "Activate the membership before provisioning a clinical user.");

        var identity = await identityUsers.GetByIdAsync(membership.UserId, cancellationToken)
            ?? throw new TenantClinicalProvisioningIdentityNotFoundException(
                "The Auth identity for this tenant member is unavailable.");
        if (!identity.IsActive || !string.Equals(identity.UserId, membership.UserId, StringComparison.Ordinal))
            throw new TenantClinicalProvisioningNotEligibleException(
                "The Auth account must be active before provisioning a clinical user.");

        var clinical = await clinicalUsers.ProvisionAsync(identity.UserId, identity.Username,
            identity.DisplayName, identity.Email, cancellationToken);
        var actor = subjectAccessor.GetRequiredSubject();
        logger.LogInformation(
            "Clinical user {ClinicalUserId} provisioned or resolved for Auth user {TargetUserId} in tenant {TenantUid} by administrator {ActorUserId}.",
            clinical.UserId, identity.UserId, tenantContext.TenantUid, actor);
        return (await GetTenantUsersAsync(cancellationToken)).Single(x =>
            string.Equals(x.AuthUserId, identity.UserId, StringComparison.Ordinal));
    }

    public async Task ResetPasswordAsync(string authUserId,string temporaryPassword,CancellationToken cancellationToken=default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authUserId);ArgumentException.ThrowIfNullOrWhiteSpace(temporaryPassword);
        var membership=(await memberships.GetTenantMembershipsAsync(tenantContext.TenantUid,cancellationToken)).SingleOrDefault(x=>string.Equals(x.UserId,authUserId,StringComparison.Ordinal));
        if(membership is null)throw new TenantMembershipNotFoundException("The membership was not found in the active tenant.");
        await identityAdministration.ResetPasswordAsync(authUserId,temporaryPassword,cancellationToken);
        logger.LogWarning("Temporary password reset for tenant user {TargetUserId} in {TenantUid} by {ActorUserId}.",authUserId,tenantContext.TenantUid,subjectAccessor.GetRequiredSubject());
    }

    private async Task<TenantUserAdministrationItem> ChangeMembershipAsync(string authUserId, string rowVersion,
        bool activate, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rowVersion);
        var actor = subjectAccessor.GetRequiredSubject();
        if (activate)
            await lifecycle.ActivateAsync(authUserId, tenantContext.TenantUid, rowVersion, actor, cancellationToken);
        else
            await lifecycle.DeactivateAsync(authUserId, tenantContext.TenantUid, rowVersion, actor, cancellationToken);
        return (await GetTenantUsersAsync(cancellationToken)).Single(x =>
            string.Equals(x.AuthUserId, authUserId, StringComparison.Ordinal));
    }
}
