using MicroEMR.Application.PlatformAdministration;
using MicroEMR.Infrastructure.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PlatformAdministrationValidationTests
{
    [Theory]
    [InlineData("Clinic-One", "clinic-one")]
    [InlineData(" clinic-2 ", "clinic-2")]
    public void TenantKeysAreNormalized(string input, string expected) =>
        Assert.Equal(expected, SqlPlatformTenantAdministrationService.NormalizeKey(input));

    [Theory]
    [InlineData("")]
    [InlineData("has spaces")]
    [InlineData("clinic_one")]
    public void InvalidTenantKeysAreRejected(string input) =>
        Assert.ThrowsAny<ArgumentException>(() => SqlPlatformTenantAdministrationService.NormalizeKey(input));

    [Theory]
    [InlineData("Server=localhost;Database=Clinical;Integrated Security=true")]
    [InlineData("Data Source=localhost")]
    [InlineData("Password=secret")]
    public void ConnectionStringsCannotBeSecretReferences(string value) =>
        Assert.Throws<ArgumentException>(() => SqlPlatformTenantAdministrationService.ValidateSecretReference(value));

    [Fact]
    public void OpaqueSecretReferenceIsAccepted() =>
        SqlPlatformTenantAdministrationService.ValidateSecretReference("development:tenant-db");

    [Theory]
    [InlineData("physician", "Physician")]
    [InlineData("ClinicAdministrator", "ClinicAdministrator")]
    public void TenantRolesUseEstablishedCasing(string input, string expected) =>
        Assert.Equal(expected, TenantRoleCatalog.Normalize(input));

    [Theory]
    [InlineData("PlatformAdministrator")]
    [InlineData("PlatformOperator")]
    [InlineData("Unknown")]
    public void PlatformOrUnknownRolesCannotBeTenantRoles(string role) =>
        Assert.Throws<ArgumentException>(() => TenantRoleCatalog.Normalize(role));
}
