using MicroEMR.Application.Providers;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ProviderContactDetailsTests
{
    [Fact]
    public async Task CreateTrimsOptionalContactDetails()
    {
        var repository = new RecordingRepository();
        var request = Request();
        request.OrganizationName = "  ABC Family Health  ";
        request.Phone = "  (416) 555-1234  ";
        request.Fax = "  416-555-5678  ";

        await new ProviderAdministrationService(repository).CreateAsync(request, 42);

        Assert.Equal("ABC Family Health", repository.Saved!.OrganizationName);
        Assert.Equal("(416) 555-1234", repository.Saved.Phone);
        Assert.Equal("416-555-5678", repository.Saved.Fax);
    }

    [Fact]
    public async Task CreateAllowsMissingContactDetails()
    {
        var repository = new RecordingRepository();
        await new ProviderAdministrationService(repository).CreateAsync(Request(), 42);
        Assert.Null(repository.Saved!.OrganizationName);
        Assert.Null(repository.Saved.Phone);
        Assert.Null(repository.Saved.Fax);
    }

    [Fact]
    public async Task UpdateRejectsContactDetailsLongerThanDatabaseColumns()
    {
        var repository = new RecordingRepository();
        var request = Request();
        request.RowVersion = Convert.ToBase64String(new byte[8]);
        request.Fax = new string('1', 31);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new ProviderAdministrationService(repository).UpdateAsync(Guid.NewGuid(), request, 42));
        Assert.Null(repository.Saved);
    }

    private static SaveProviderRequest Request() => new()
    {
        FirstName = "Michael", LastName = "Smith", DisplayName = "Dr. Michael Smith",
        ProviderType = "Physician"
    };

    private sealed class RecordingRepository : IProviderAdministrationRepository
    {
        public SaveProviderRequest? Saved { get; private set; }
        public Task<ProviderAdministrationItem> CreateAsync(SaveProviderRequest request, long actor,
            CancellationToken token = default)
        {
            Saved = request;
            return Task.FromResult<ProviderAdministrationItem>(null!);
        }
        public Task<ProviderAdministrationItem?> UpdateAsync(Guid uid, SaveProviderRequest request, long actor,
            CancellationToken token = default)
        {
            Saved = request;
            return Task.FromResult<ProviderAdministrationItem?>(null);
        }
        public Task<IReadOnlyList<ProviderAdministrationItem>> ListAsync(string status, CancellationToken token = default) => throw new NotSupportedException();
        public Task<ProviderAdministrationItem?> GetAsync(Guid uid, CancellationToken token = default) => throw new NotSupportedException();
        public Task<ProviderAdministrationItem?> SetActiveAsync(Guid uid, bool active, string version, long actor, CancellationToken token = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<EligibleApplicationUser>> EligibleUsersAsync(Guid? uid, CancellationToken token = default) => throw new NotSupportedException();
        public Task<ProviderAdministrationItem?> LinkAsync(Guid uid, ProviderLinkRequest request, long actor, CancellationToken token = default) => throw new NotSupportedException();
        public Task<ProviderAdministrationItem?> UnlinkAsync(Guid uid, ProviderLinkRequest request, long actor, CancellationToken token = default) => throw new NotSupportedException();
    }
}
