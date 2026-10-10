using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Web.Models.Scheduling;
using MicroEMR.Web.Services.Scheduling;
using Xunit;
using WebController=MicroEMR.Web.Controllers.Scheduling.SchedulingController;
using ApiController=MicroEMR.Api.Controllers.SchedulingController;

namespace MicroEMR.Api.Tests;

public sealed class AdHocAppointmentTests
{
    [Theory]
    [InlineData("CreateAppointment")][InlineData("UpdateAppointment")][InlineData("RescheduleAppointment")]
    public async Task AdHocWriteRoutesRequireManageAndRejectDeniedUser(string action)
    {
        foreach(var type in new[]{typeof(ApiController),typeof(WebController)})
        {
            Assert.Contains(type.GetCustomAttributes<AuthorizeAttribute>(),a=>a.Policy=="Permission:"+PermissionKeys.SchedulingView);
            Assert.Contains(type.GetMethod(action)!.GetCustomAttributes<AuthorizeAttribute>(),a=>a.Policy=="Permission:"+PermissionKeys.SchedulingManage);
        }
        var denied=Proxy<ICurrentUserPermissionService>((_,_)=>Task.FromResult(false));
        var requirement=new MicroEMR.Api.Authorization.PermissionRequirement(PermissionKeys.SchedulingManage);
        var context=new AuthorizationHandlerContext([requirement],new ClaimsPrincipal(new ClaimsIdentity([],"test")),null);
        await new MicroEMR.Api.Authorization.PermissionAuthorizationHandler(denied,NullLogger<MicroEMR.Api.Authorization.PermissionAuthorizationHandler>.Instance).HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task ScheduleProjectionRetainsBothOverlappingAppointmentsAndExplicitModes()
    {
        var start=DateTime.UtcNow;var provider=Guid.NewGuid();var first=Guid.NewGuid();var second=Guid.NewGuid();
        IReadOnlyList<ScheduleAppointmentListItemResponse> appointments=[
            new(){AppointmentUid=first,PrimaryResourceUid=provider,PatientDisplayName="First",StartDateTimeUtc=start,EndDateTimeUtc=start.AddMinutes(30)},
            new(){AppointmentUid=second,PrimaryResourceUid=provider,PatientDisplayName="Second",StartDateTimeUtc=start,EndDateTimeUtc=start.AddMinutes(30),IsAdHoc=true}];
        var client=Proxy<ISchedulingApiClient>((name,_)=>{Assert.Equal("GetAppointmentsAsync",name);return Task.FromResult(appointments);});
        var controller=new WebController(client,null!,NullLogger<WebController>.Instance){ControllerContext=new(){HttpContext=new DefaultHttpContext()}};
        var result=Assert.IsType<JsonResult>(await controller.Events(start,start.AddDays(1),provider,default));
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(result.Value));var rows=json.RootElement.EnumerateArray().ToArray();
        Assert.Equal(2,rows.Length);Assert.Equal(first,rows[0].GetProperty("id").GetGuid());Assert.False(rows[0].GetProperty("isAdHoc").GetBoolean());
        Assert.Equal(second,rows[1].GetProperty("id").GetGuid());Assert.True(rows[1].GetProperty("isAdHoc").GetBoolean());
        Assert.Equal(rows[0].GetProperty("start").GetString(),rows[1].GetProperty("start").GetString());
    }
    [Fact]
    public void OrdinaryDefaultAndRescheduleCannotSupplyAnOverlapOverride()
    {
        Assert.False(new CreateScheduleAppointmentRequest().IsAdHoc);
        Assert.False(new UpdateScheduleAppointmentRequest().IsAdHoc);
        Assert.Null(typeof(RescheduleAppointmentRequest).GetProperty("IsAdHoc"));
    }
    public class Stub:DispatchProxy{public Func<string,object?[],object?> Handler=null!;protected override object? Invoke(MethodInfo? method,object?[]? args)=>Handler(method!.Name,args??[]);}
    private static T Proxy<T>(Func<string,object?[],object?> handler)where T:class{var value=DispatchProxy.Create<T,Stub>();((Stub)(object)value).Handler=handler;return value;}
}
