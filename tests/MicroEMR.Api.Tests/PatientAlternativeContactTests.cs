using System.Reflection;
using System.Text.Json;
using MicroEMR.Application.Patients.Contracts;
using MicroEMR.Application.Patients.Repositories;
using MicroEMR.Application.Patients.Services;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PatientAlternativeContactTests
{
    [Fact]
    public async Task CreateReadAndUpdateCarryContactsActorAndConcurrencyToken()
    {
        PatientDetailsResponse? stored = null;
        var uid = Guid.NewGuid();
        var repository = Stub((method, args) =>
        {
            switch (method)
            {
                case "CreateAsync":
                    Assert.Equal(17L, args[1]);
                    var create = (CreatePatientRequest)args[0]!;
                    stored = new() { PatientUid = uid, AlternativeContacts = Copy(create.AlternativeContacts!), RowVersion = "AQIDBAUGBwg=" };
                    return Task.FromResult(stored);
                case "GetByUidAsync":
                    Assert.Equal(uid, args[0]);
                    return Task.FromResult(stored);
                case "UpdateDemographicsAsync":
                    Assert.Equal(uid, args[0]); Assert.Equal(18L, args[2]);
                    var update = (UpdatePatientDemographicsRequest)args[1]!;
                    Assert.Equal(stored!.RowVersion, update.RowVersion);
                    if (update.AlternativeContacts is not null) stored.AlternativeContacts = Copy(update.AlternativeContacts);
                    return Task.FromResult(stored);
                default: throw new InvalidOperationException(method);
            }
        });
        var service = new PatientService(repository);
        await service.CreateAsync(new() { AlternativeContacts = [Contact()] }, 17);
        Assert.Equal("42", (await service.GetByUidAsync(uid))!.AlternativeContacts[0].WorkPhoneExtension);
        await service.UpdateDemographicsAsync(uid, new() { RowVersion = stored!.RowVersion }, 18);
        Assert.Single(stored.AlternativeContacts); // Older clients omit contacts.
        await service.UpdateDemographicsAsync(uid, new() { RowVersion = stored.RowVersion, AlternativeContacts = [] }, 18);
        Assert.Empty((await service.GetByUidAsync(uid))!.AlternativeContacts);
    }

    [Fact]
    public async Task NoContactIsOptionalAndMalformedContactNeverReachesMutation()
    {
        var calls = 0;
        var service = new PatientService(Stub((method, args) =>
        {
            calls++;
            return Task.FromResult(new PatientDetailsResponse());
        }));
        await service.CreateAsync(new(), 1);
        await service.UpdateDemographicsAsync(Guid.NewGuid(), new() { AlternativeContacts = [] }, 1);
        Assert.Equal(2, calls);
        foreach (var invalid in new[] {
            new PatientAlternativeContact { FirstName = " ", LastName = "Lee", Purposes = ["Emergency Contact"] },
            new PatientAlternativeContact { FirstName = "Alex", LastName = "Lee", Purposes = ["Unknown"] },
            new PatientAlternativeContact { FirstName = "Alex", LastName = "Lee", Purposes = [] },
            new PatientAlternativeContact { FirstName = "Alex", LastName = "Lee", Purposes = ["Emergency Contact"], Email = "invalid" } })
            await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(new() { AlternativeContacts = [invalid] }, 1));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ContactEditPropagatesExistingConcurrencyConflictWithActorAndToken()
    {
        var uid = Guid.NewGuid();
        var service = new PatientService(Stub((method, args) =>
        {
            Assert.Equal("UpdateDemographicsAsync", method);
            Assert.Equal(uid, args[0]); Assert.Equal(19L, args[2]);
            Assert.Equal("stale", ((UpdatePatientDemographicsRequest)args[1]!).RowVersion);
            throw new MicroEMR.Application.Patients.Exceptions.PatientDemographicsConcurrencyException();
        }));
        await Assert.ThrowsAsync<MicroEMR.Application.Patients.Exceptions.PatientDemographicsConcurrencyException>(() =>
            service.UpdateDemographicsAsync(uid, new() { AlternativeContacts = [Contact()], RowVersion = "stale" }, 19));
    }

    [Fact]
    public void WebEditMappingAndJsonTransportKeepAllContactFieldsAndExplicitRemoval()
    {
        var json = JsonSerializer.Serialize(Contact());
        var webContact = JsonSerializer.Deserialize<MicroEMR.Web.Models.Patients.PatientAlternativeContact>(json)!;
        var patient = new MicroEMR.Web.Models.Patients.PatientDetailsResponse
            { AlternativeContacts = [webContact], RowVersion = "AQIDBAUGBwg=" };
        var controller = typeof(MicroEMR.Web.Controllers.PatientsController);
        var edit = (MicroEMR.Web.Models.Patients.EditPatientDemographicsViewModel)controller
            .GetMethod("MapEditViewModel", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [patient])!;
        var request = (MicroEMR.Web.Models.Patients.UpdatePatientDemographicsRequest)controller
            .GetMethod("MapUpdateRequest", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [edit])!;
        var application = JsonSerializer.Deserialize<UpdatePatientDemographicsRequest>(JsonSerializer.Serialize(request))!;
        Assert.Equal(json, JsonSerializer.Serialize(application.AlternativeContacts![0]));
        Assert.Equal(patient.RowVersion, application.RowVersion);
        edit.AlternativeContacts.Clear();
        request = (MicroEMR.Web.Models.Patients.UpdatePatientDemographicsRequest)controller
            .GetMethod("MapUpdateRequest", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [edit])!;
        Assert.NotNull(request.AlternativeContacts);
        Assert.Empty(request.AlternativeContacts);
    }

    [Fact]
    public void AdditiveMigrationUsesPatientProceduresAuditAndConcurrencyWithoutRemovingHistory()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "database", "tenant-clinical");
        var sql = File.ReadAllText(Path.Combine(folder, "migrations", "0066-patient-alternative-contacts.sql"));
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "manifest.json")));
        Assert.Equal("0066-patient-alternative-contacts", manifest.RootElement.EnumerateArray().Last().GetProperty("migrationId").GetString());
        foreach (var expected in new[] { "Patient ADD AlternativeContactsJson NVARCHAR(MAX) NULL", "ISJSON(@AlternativeContactsJson)",
            "PROCEDURE dbo.Patient_Create", "PROCEDURE dbo.Patient_GetByUid", "PROCEDURE dbo.Patient_UpdateDemographics",
            "AlternativeContactsJson = COALESCE(@AlternativeContactsJson, AlternativeContactsJson)",
            "AND RowVersion = @RowVersion", "AND IsDeleted = CONVERT(BIT, 0)", "@CreatedBy, @PatientId", "@UpdatedBy, @PatientId" })
            Assert.Contains(expected, sql);
        Assert.Equal(3, sql.Split("AS AlternativeContacts").Length - 1); // Create new, update old and new.
        Assert.Equal(2, sql.Split("INSERT dbo.AuditLog").Length - 1);
        Assert.DoesNotContain("DELETE FROM", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static PatientAlternativeContact Contact() => new()
    {
        FirstName = "Alex", LastName = "Lee", Purposes = ["Emergency Contact", "Substitute Decision Maker"],
        ResidencePhone = "416-555-0101", CellPhone = "416-555-0102", WorkPhone = "416-555-0103",
        WorkPhoneExtension = "42", Email = "alex@example.test", Note = "Call first"
    };
    private static List<PatientAlternativeContact> Copy(List<PatientAlternativeContact> contacts) =>
        JsonSerializer.Deserialize<List<PatientAlternativeContact>>(JsonSerializer.Serialize(contacts))!;
    private static IPatientRepository Stub(Func<string, object?[], object> call)
    {
        var proxy = DispatchProxy.Create<IPatientRepository, Proxy>();
        ((Proxy)(object)proxy).Call = call;
        return proxy;
    }
    public class Proxy : DispatchProxy
    {
        public Func<string, object?[], object> Call { get; set; } = null!;
        protected override object Invoke(MethodInfo? method, object?[]? args) => Call(method!.Name, args!);
    }
}
