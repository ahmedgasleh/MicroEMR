using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MicroEMR.Application.PlatformAdministration;

namespace MicroEMR.Api.Identity;

public sealed class AdministrationIdentityUser : IdentityUser
{
    public string? FullName { get; set; }
    public bool IsActive { get; set; } = true;
    public int? ClinicId { get; set; }
}

public sealed class AdministrationIdentityDbContext(DbContextOptions<AdministrationIdentityDbContext> options)
    : IdentityDbContext<AdministrationIdentityUser>(options);

public sealed class IdentityUserAdministration(
    UserManager<AdministrationIdentityUser> users,
    ILogger<IdentityUserAdministration> logger) : IIdentityUserAdministration
{
    public async Task<ResolveOrCreateIdentityResult> ResolveOrCreateAsync(
        ResolveOrCreateIdentityRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        var first = RequireName(request.FirstName, nameof(request.FirstName));
        var last = RequireName(request.LastName, nameof(request.LastName));
        var existing = await users.FindByEmailAsync(email);
        if (existing is not null) return new(ToProfile(existing), false);

        var user = new AdministrationIdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = false,
            FullName = $"{first} {last}",
            IsActive = true
        };
        if (string.IsNullOrWhiteSpace(request.TemporaryPassword))
            throw new ArgumentException("A temporary password is required when creating a new MicroEMR identity.");
        var result = await users.CreateAsync(user, request.TemporaryPassword);
        if (!result.Succeeded)
        {
            // A concurrent request may have won the unique-email race.
            existing = await users.FindByEmailAsync(email);
            if (existing is not null) return new(ToProfile(existing), false);
            var safeErrors = string.Join(" ", result.Errors.Select(x => x.Description));
            logger.LogWarning("Auth identity creation failed for normalized email {Email}: {Errors}", email, safeErrors);
            throw new ArgumentException($"The Auth account could not be created. {safeErrors}");
        }
        logger.LogInformation("Auth identity {UserId} created with an administrator-supplied temporary password.", user.Id);
        return new(ToProfile(user), true);
    }

    public async Task ResetPasswordAsync(string userId,string temporaryPassword,CancellationToken cancellationToken=default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(temporaryPassword);
        var user=await users.FindByIdAsync(userId)??throw new KeyNotFoundException("The Auth identity was not found.");
        var token=await users.GeneratePasswordResetTokenAsync(user);
        var result=await users.ResetPasswordAsync(user,token,temporaryPassword);
        if(!result.Succeeded)
        {
            var errors=string.Join(" ",result.Errors.Select(x=>x.Description));
            logger.LogWarning("Temporary password reset failed for Auth identity {UserId}: {Errors}",userId,errors);
            throw new ArgumentException($"The temporary password was rejected. {errors}");
        }
        await users.SetLockoutEndDateAsync(user,null);
        await users.ResetAccessFailedCountAsync(user);
        logger.LogInformation("Temporary password reset completed for Auth identity {UserId}.",userId);
    }

    private static IdentityUserProfile ToProfile(AdministrationIdentityUser user) =>
        new(user.Id, user.UserName ?? user.Email ?? string.Empty, user.FullName ?? user.UserName ?? string.Empty,
            user.Email, user.IsActive);

    private static string NormalizeEmail(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var email = value.Trim();
        if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
            throw new ArgumentException("Enter a valid email address.");
        return email;
    }

    private static string RequireName(string value, string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var result = value.Trim();
        if (result.Length > 100) throw new ArgumentOutOfRangeException(parameter, "Names must be 100 characters or fewer.");
        return result;
    }
}
