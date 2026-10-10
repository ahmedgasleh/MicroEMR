using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.Scheduling;
using MicroEMR.Application.Scheduling.Contracts;
using MicroEMR.Application.Scheduling.Services;
using MicroEMR.Infrastructure.Scheduling;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class AdHocAppointmentSqlTests(NextAvailableSqlDatabase db) : IClassFixture<NextAvailableSqlDatabase>
{
    private static DateTime At(int hour) => new(2030, 1, 4, hour, 0, 0, DateTimeKind.Utc);
    private static CreateScheduleAppointmentRequest Create(Guid patient, Guid provider, int hour, bool adHoc = false, Guid? room = null) => new()
    { PatientUid=patient, PrimaryResourceUid=provider, RoomResourceUid=room, StartDateTimeUtc=At(hour), EndDateTimeUtc=At(hour).AddMinutes(30), IsAdHoc=adHoc };
    private static UpdateScheduleAppointmentRequest Edit(Guid provider, int hour, bool adHoc) => new()
    { PrimaryResourceUid=provider, StartDateTimeUtc=At(hour), EndDateTimeUtc=At(hour).AddMinutes(30), IsAdHoc=adHoc, IsCritical=true };
    private static RescheduleAppointmentRequest Move(Guid provider, int hour) => new()
    { PrimaryResourceUid=provider, StartDateTimeUtc=At(hour), EndDateTimeUtc=At(hour).AddMinutes(30) };

    [SchedulingSearchSqlFact]
    public async Task ExplicitOverlapPersistsAndBothDaySheetOrdersRetainEveryPatient()
    {
        var (patient, provider, _) = await db.Seed(); var (secondPatient, _, _) = await db.Seed();
        await Sql("UPDATE dbo.Patient SET LastName=N'Zulu' WHERE PatientUid=@uid",patient);
        await Sql("UPDATE dbo.Patient SET LastName=N'Alpha' WHERE PatientUid=@uid",secondPatient);
        var ordinary=await db.Appointments.CreateAsync(Create(patient, provider, 8),7);
        await Assert.ThrowsAsync<SchedulingConflictException>(()=>db.Appointments.CreateAsync(Create(secondPatient,provider,8),7));
        var overlap=Create(secondPatient,provider,8,true);overlap.StartDateTimeUtc=At(8).AddMinutes(15);overlap.EndDateTimeUtc=At(8).AddMinutes(45);
        var adHoc=await db.Appointments.CreateAsync(overlap,7);
        Assert.False(ordinary.IsAdHoc); Assert.True(adHoc.IsAdHoc);
        Assert.True((await db.Reads.GetAppointmentByUidAsync(adHoc.AppointmentUid))!.IsAdHoc);
        var rows=await db.Reads.GetAppointmentsAsync(At(0),At(23),provider);
        Assert.Equal(2,rows.Count); Assert.Contains(rows,r=>r.PatientUid==patient&&!r.IsAdHoc);
        Assert.Contains(rows,r=>r.PatientUid==secondPatient&&r.IsAdHoc);
        var service=new SchedulingReadService(db.Reads);
        foreach(var order in new[]{"Alphabetic","Chronological"})
        {
            var sheet=await service.GetDaySheetAsync(new(){Date=new(2030,1,4),Start=new(At(0)),End=new(At(0).AddDays(1)),ClinicianUids=[provider],Order=order});
            Assert.Equal(2,sheet.Appointments.Count); Assert.Single(sheet.Appointments,r=>r.IsAdHoc);
            var expected=order=="Alphabetic"?new[]{"Alpha, Test","Zulu, Test"}:new[]{"Zulu, Test","Alpha, Test"};
            Assert.Equal(expected,sheet.Appointments.Select(r=>r.PatientName));
        }
        Assert.Equal(2,(await db.Reads.GetAvailabilityBusyPeriodsAsync(provider,null,At(8),At(9))).Count);
    }

    [SchedulingSearchSqlFact]
    public async Task RoomBlocksResourcesActorAndTimeStillRejectWithoutPartialWrites()
    {
        var (patient,provider,room)=await db.Seed(); var (_,otherProvider,_)=await db.Seed();
        await db.Appointments.CreateAsync(Create(patient,otherProvider,9,false,room),7);
        await Assert.ThrowsAsync<SchedulingConflictException>(()=>db.Appointments.CreateAsync(Create(patient,provider,9,true,room),7));
        await db.Appointments.CreateBlockedTimeAsync(new(){ResourceUid=provider,StartDateTimeUtc=At(10),EndDateTimeUtc=At(11),Reason="Test"},7);
        await Assert.ThrowsAsync<SchedulingBlockedTimeConflictException>(()=>db.Appointments.CreateAsync(Create(patient,provider,10,true),7));
        await db.Appointments.CreateBlockedTimeAsync(new(){ResourceUid=room,StartDateTimeUtc=At(11),EndDateTimeUtc=At(12),Reason="Test"},7);
        await Assert.ThrowsAsync<SchedulingBlockedTimeConflictException>(()=>db.Appointments.CreateAsync(Create(patient,provider,11,true,room),7));
        foreach(var invalid in new[]{Create(patient,room,12,true),Create(Guid.NewGuid(),provider,12,true),Create(patient,Guid.NewGuid(),12,true),Create(patient,provider,12,true,provider)})
            await Assert.ThrowsAsync<InvalidOperationException>(()=>db.Appointments.CreateAsync(invalid,7));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.Appointments.CreateAsync(Create(patient,provider,12,true),null));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.Appointments.CreateAsync(Create(patient,provider,12,true),999));
        var invalidTime=Create(patient,provider,12,true); invalidTime.EndDateTimeUtc=invalidTime.StartDateTimeUtc;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.Appointments.CreateAsync(invalidTime,7));
        await Sql("UPDATE dbo.ScheduleResource SET IsActive=0 WHERE ResourceUid=@uid",provider);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.Appointments.CreateAsync(Create(patient,provider,12,true),7));
        Assert.Empty(await db.Reads.GetAppointmentsAsync(At(0),At(23),provider));
    }

    [SchedulingSearchSqlFact]
    public async Task EditConversionAndDragDropRescheduleRevalidateAndPreserveModePatientAndCritical()
    {
        var(patient,provider,room)=await db.Seed();
        var ordinary=await db.Appointments.CreateAsync(Create(patient,provider,13),7);
        var adHoc=await db.Appointments.CreateAsync(Create(patient,provider,13,true,room),7);
        await Assert.ThrowsAsync<SchedulingConflictException>(()=>db.Appointments.UpdateAsync(adHoc.AppointmentUid,Edit(provider,13,false),7));
        Assert.True((await db.Reads.GetAppointmentByUidAsync(adHoc.AppointmentUid))!.IsAdHoc);
        var edited=await db.Appointments.UpdateAsync(adHoc.AppointmentUid,Edit(provider,13,true),7);
        Assert.True(edited!.IsCritical); Assert.Equal(patient,edited.PatientUid);
        var move=await db.Appointments.RescheduleAsync(adHoc.AppointmentUid,Move(provider,13),7);
        Assert.True(move!.IsAdHoc); Assert.True(move.IsCritical);
        await Assert.ThrowsAsync<SchedulingConflictException>(()=>db.Appointments.RescheduleAsync(ordinary.AppointmentUid,Move(provider,13),7));
        await db.Appointments.CreateBlockedTimeAsync(new(){ResourceUid=provider,StartDateTimeUtc=At(14),EndDateTimeUtc=At(15),Reason="Test"},7);
        await Assert.ThrowsAsync<SchedulingBlockedTimeConflictException>(()=>db.Appointments.RescheduleAsync(adHoc.AppointmentUid,Move(provider,14),7));
        Assert.False((await db.Appointments.UpdateAsync(adHoc.AppointmentUid,Edit(provider,15,false),7))!.IsAdHoc);
        Assert.True((await db.Appointments.UpdateAsync(adHoc.AppointmentUid,Edit(provider,15,true),7))!.IsAdHoc);
        var history=await db.Reads.GetHistoryAsync(adHoc.AppointmentUid);
        Assert.Contains(history,h=>h.ActionDescription!.Contains("mode changed"));
        Assert.Contains(history,h=>h.ActionType=="Rescheduled");
        Assert.All(history,h=>{Assert.Equal(7,h.CreatedBy);Assert.NotEqual(default,h.CreatedAt);});
        await using var sql=new SqlConnection(db.Connection);await sql.OpenAsync();
        using var audit=new SqlCommand("SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityId=@uid AND JSON_VALUE(OldValue,'$.IsAdHoc')='true' AND JSON_VALUE(NewValue,'$.IsAdHoc')='false' AND UserId=7 AND PatientId=(SELECT PatientId FROM dbo.Patient WHERE PatientUid=@patient)",sql);
        audit.Parameters.AddWithValue("@uid",adHoc.AppointmentUid.ToString());audit.Parameters.AddWithValue("@patient",patient);
        Assert.Equal(1,(int)(await audit.ExecuteScalarAsync())!);
    }

    [SchedulingSearchSqlFact]
    public async Task RescheduleRetainedRoomStillEnforcesBlocksAndActiveRoom()
    {
        var(patient,provider,room)=await db.Seed();
        var appointment=await db.Appointments.CreateAsync(Create(patient,provider,16,true,room),7);
        await db.Appointments.CreateBlockedTimeAsync(new(){ResourceUid=room,StartDateTimeUtc=At(17),EndDateTimeUtc=At(18),Reason="Test"},7);
        await Assert.ThrowsAsync<SchedulingBlockedTimeConflictException>(()=>db.Appointments.RescheduleAsync(appointment.AppointmentUid,Move(provider,17),7));
        await Sql("UPDATE dbo.ScheduleResource SET IsActive=0 WHERE ResourceUid=@uid",room);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.Appointments.RescheduleAsync(appointment.AppointmentUid,Move(provider,18),7));
        Assert.Equal(At(16),(await db.Reads.GetAppointmentByUidAsync(appointment.AppointmentUid))!.StartDateTimeUtc);
    }

    [SchedulingSearchSqlFact]
    public async Task CancellationStatusAuditAndSeparateTenantDatabaseKeepIdentityAndHistory()
    {
        var(patient,provider,_)=await db.Seed();
        var appointment=await db.Appointments.CreateAsync(Create(patient,provider,19,true),7);
        await db.Appointments.UpdateStatusAsync(appointment.AppointmentUid,new(){Status="Arrived"},7);
        Assert.True((await db.Reads.GetAppointmentByUidAsync(appointment.AppointmentUid))!.IsAdHoc);
        var other=new SchedulingAppointmentRepository(new NextAvailableSqlDatabase.Factory(db.OtherConnection));
        var reads=new SchedulingReadRepository(new NextAvailableSqlDatabase.Factory(db.OtherConnection),NullLogger<SchedulingReadRepository>.Instance);
        Assert.Null(await reads.GetAppointmentByUidAsync(appointment.AppointmentUid));
        Assert.Empty(await reads.GetHistoryAsync(appointment.AppointmentUid));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>other.UpdateAsync(appointment.AppointmentUid,Edit(provider,19,true),7));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>other.RescheduleAsync(appointment.AppointmentUid,Move(provider,19),7));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>other.CreateAsync(Create(patient,provider,19,true),7));
        var ownProvider=Guid.NewGuid();
        await using(var otherSql=new SqlConnection(db.OtherConnection))
        {await otherSql.OpenAsync();using var seed=new SqlCommand("INSERT dbo.ScheduleResource(ResourceUid,ResourceType,DisplayName,IsActive) VALUES(@uid,N'Provider',N'Other tenant provider',1)",otherSql);seed.Parameters.AddWithValue("@uid",ownProvider);await seed.ExecuteNonQueryAsync();}
        Assert.Null(await other.UpdateAsync(appointment.AppointmentUid,Edit(ownProvider,19,true),7));
        Assert.Null(await other.RescheduleAsync(appointment.AppointmentUid,Move(ownProvider,19),7));
        Assert.Null(await other.CancelAsync(appointment.AppointmentUid,new(){CancelReason="Foreign"},7));
        await db.Appointments.CancelAsync(appointment.AppointmentUid,new(){CancelReason="Test"},7);
        var saved=await db.Reads.GetAppointmentByUidAsync(appointment.AppointmentUid);
        Assert.True(saved!.IsAdHoc); Assert.Equal("Cancelled",saved.Status); Assert.Equal(patient,saved.PatientUid);
        Assert.Contains(await db.Reads.GetHistoryAsync(appointment.AppointmentUid),h=>h.ActionType=="Cancelled"&&h.CreatedBy==7);
        await using var sql=new SqlConnection(db.Connection);await sql.OpenAsync();
        using var command=new SqlCommand("SELECT ActionName,UserId,PatientId,NewValue,CreatedAt FROM dbo.AuditLog WHERE EntityId=@uid ORDER BY AuditLogId",sql);
        command.Parameters.AddWithValue("@uid",appointment.AppointmentUid.ToString());
        using var reader=await command.ExecuteReaderAsync();var count=0;
        while(await reader.ReadAsync()) {Assert.Equal(7,reader.GetInt64(1));Assert.False(reader.IsDBNull(2));Assert.False(reader.IsDBNull(4));if(count==0)Assert.Contains("\"IsAdHoc\":true",reader.GetString(3));count++;}
        Assert.True(count>=3);
    }

    [SchedulingSearchSqlFact]
    public async Task ConcurrentOrdinaryBookingsHaveExactlyOneWinnerAndAdHocRemainsBusy()
    {
        var(patient,provider,_)=await db.Seed();
        async Task<bool> Attempt(){try{await db.Appointments.CreateAsync(Create(patient,provider,20),7);return true;}catch(SchedulingConflictException){return false;}}
        var outcomes=await Task.WhenAll(Attempt(),Attempt());Assert.Single(outcomes,x=>x);
        await db.Appointments.CreateAsync(Create(patient,provider,20,true),7);
        Assert.Equal(2,(await db.Reads.GetAvailabilityBusyPeriodsAsync(provider,null,At(20),At(21))).Count);
        Assert.Equal(2,(await db.Reads.GetAppointmentsAsync(At(20),At(21),provider)).Count);
        var month=await db.Reads.GetMonthSummaryAsync(At(0),At(0).AddDays(1));Assert.Contains(month,x=>x.AdHocCount>=1);
    }

    private async Task Sql(string statement,Guid uid)
    {await using var sql=new SqlConnection(db.Connection);await sql.OpenAsync();using var command=new SqlCommand(statement,sql);command.Parameters.AddWithValue("@uid",uid);await command.ExecuteNonQueryAsync();}
}
