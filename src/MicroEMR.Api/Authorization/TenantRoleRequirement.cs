using Microsoft.AspNetCore.Authorization;

namespace MicroEMR.Api.Authorization;

public sealed record TenantRoleRequirement(string Role) : IAuthorizationRequirement;
