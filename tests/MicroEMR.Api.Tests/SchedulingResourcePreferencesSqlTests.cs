using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.Scheduling.Preferences;
using MicroEMR.Infrastructure.Scheduling;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class SchedulingResourcePreferencesSqlTests(NextAvailableSqlDatabase db) : IClassFixture<NextAvailableSqlDatabase>
{
    [SchedulingSearchSqlFact]
    public async Task MigrationPersistsIsolatedAuditedDefaultsWithAtomicConcurrencyAndNoClinicalChanges()
    {
        foreach(var target in new[]{db.Connection,db.OtherConnection})
        {
            // Extend only this existing disposable fixture's minimal user schema.
            await Execute(target,"ALTER TABLE dbo.ApplicationUser ADD IsActive bit NOT NULL DEFAULT 1;");
            await Execute(target,"INSERT dbo.ApplicationUser(UserId,DisplayName,IsActive) VALUES(8,N'Other test user',1);");
            var root=new DirectoryInfo(AppContext.BaseDirectory);
            while(root is not null && !File.Exists(Path.Combine(root.FullName,"AGENTS.md")))root=root.Parent;
            var script=File.ReadAllText(Path.Combine(root!.FullName,"db/tenant-clinical/migrations/0103-scheduling-user-resource-defaults.sql"));
            foreach(var batch in Regex.Split(script,@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase).Where(x=>!string.IsNullOrWhiteSpace(x)))
                await Execute(target,batch);
        }
        var (patient,provider,room)=await db.Seed();
        var (_,otherProvider,otherRoom)=await SeedOtherTenant();
        var inactive=Guid.NewGuid();
        await Execute(db.Connection,$"INSERT dbo.ScheduleResource(ResourceUid,ResourceType,DisplayName,IsActive) VALUES('{inactive}',N'Equipment',N'Inactive test equipment',0);");
        await db.Appointments.CreateAsync(new(){PatientUid=patient,PrimaryResourceUid=provider,RoomResourceUid=room,
            StartDateTimeUtc=new(2030,1,4,9,0,0,DateTimeKind.Utc),EndDateTimeUtc=new(2030,1,4,9,15,0,DateTimeKind.Utc)},7);
        var before=await ClinicalSnapshot();
        var repo=new SchedulingResourcePreferencesRepository(new NextAvailableSqlDatabase.Factory(db.Connection));
        var other=new SchedulingResourcePreferencesRepository(new NextAvailableSqlDatabase.Factory(db.OtherConnection));
        Assert.Null(await repo.GetAsync(7));Assert.Null(await repo.GetAsync(8));Assert.Null(await other.GetAsync(7));
        var saved=await repo.SaveAsync(7,JsonSerializer.Serialize(new[]{room,provider}),null);
        Assert.Equal(saved,await repo.GetAsync(7));Assert.Null(await repo.GetAsync(8));Assert.Null(await other.GetAsync(7));
        await repo.SaveAsync(8,JsonSerializer.Serialize(new[]{provider}),null);
        await other.SaveAsync(7,JsonSerializer.Serialize(new[]{otherRoom,otherProvider}),null);
        Assert.Equal(saved,await repo.GetAsync(7));
        Assert.NotEqual(saved.ResourceUidsJson,(await other.GetAsync(7))!.ResourceUidsJson);
        foreach(var invalid in new[]{"[]","not-json",JsonSerializer.Serialize(new[]{otherRoom}),JsonSerializer.Serialize(new[]{inactive}),JsonSerializer.Serialize(new[]{provider,provider}),JsonSerializer.Serialize(new[]{Guid.NewGuid()})})
            await Assert.ThrowsAsync<ArgumentException>(()=>repo.SaveAsync(7,invalid,saved.RowVersion));
        Assert.Equal(saved,await repo.GetAsync(7));

        var outcomes=await Task.WhenAll(Attempt(provider),Attempt(room));
        Assert.Equal(1,outcomes.Count(x=>x));Assert.Equal(1,outcomes.Count(x=>!x));
        var winner=await repo.GetAsync(7);
        await Assert.ThrowsAsync<SchedulingResourcePreferencesConcurrencyException>(()=>repo.SaveAsync(7,JsonSerializer.Serialize(new[]{provider}),saved.RowVersion));
        Assert.Equal(winner,await repo.GetAsync(7));
        Assert.Equal(before,await ClinicalSnapshot());
        await using(var connection=new SqlConnection(db.Connection))
        {
            await connection.OpenAsync();
            await using var query=new SqlCommand("SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityName='SchedulingResourcePreference' AND PatientId IS NULL;",connection);
            Assert.Equal(3,(int)(await query.ExecuteScalarAsync())!); // Two creates and exactly one winning update.
        }

        // A failed configuration audit must roll back the preference and its row version.
        await Execute(db.Connection,"CREATE TRIGGER dbo.PreferenceAuditFailure ON dbo.AuditLog AFTER INSERT AS IF EXISTS(SELECT 1 FROM inserted WHERE EntityName='SchedulingResourcePreference') THROW 51999,'Test audit failure',1;");
        try
        {
            await Assert.ThrowsAsync<SqlException>(()=>repo.SaveAsync(7,JsonSerializer.Serialize(new[]{provider}),winner!.RowVersion));
            Assert.Equal(winner,await repo.GetAsync(7));
        }
        finally {await Execute(db.Connection,"DROP TRIGGER dbo.PreferenceAuditFailure;");}

        async Task<bool> Attempt(Guid uid)
        {
            try {await repo.SaveAsync(7,JsonSerializer.Serialize(new[]{uid}),saved.RowVersion);return true;}
            catch(SchedulingResourcePreferencesConcurrencyException){return false;}
        }
        async Task<string> ClinicalSnapshot()
        {
            await using var connection=new SqlConnection(db.Connection);await connection.OpenAsync();
            await using var query=new SqlCommand("""
                SELECT (SELECT * FROM dbo.ScheduleAppointment ORDER BY ScheduleAppointmentId FOR JSON PATH) Appointments,
                    (SELECT * FROM dbo.AppointmentHistory ORDER BY AppointmentHistoryId FOR JSON PATH) History,
                    (SELECT * FROM dbo.AuditLog WHERE EntityName='ScheduleAppointment' ORDER BY AuditLogId FOR JSON PATH) ClinicalAudit,
                    (SELECT * FROM dbo.ScheduleResource ORDER BY ResourceId FOR JSON PATH) Resources
                FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;
                """,connection);
            return (string)(await query.ExecuteScalarAsync())!;
        }
        async Task<(Guid,Guid,Guid)> SeedOtherTenant()
        {
            var otherPatient=Guid.NewGuid();var p=Guid.NewGuid();var r=Guid.NewGuid();
            await Execute(db.OtherConnection,$"INSERT dbo.Patient(PatientUid,IsDeleted,FirstName,LastName) VALUES('{otherPatient}',0,N'Test',N'Other'); INSERT dbo.ScheduleResource(ResourceUid,ResourceType,DisplayName,IsActive) VALUES('{p}',N'Provider',N'Other provider',1),('{r}',N'Room',N'Other room',1);");
            return(otherPatient,p,r);
        }
    }

    private static async Task Execute(string target,string sql)
    {
        await using var connection=new SqlConnection(target);await connection.OpenAsync();
        await using var command=new SqlCommand(sql,connection);await command.ExecuteNonQueryAsync();
    }
}
