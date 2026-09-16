using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Api.ClinicalUsers;
using MicroEMR.Api.Controllers;
using MicroEMR.Api.Middleware;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Application.PatientResults;
using MicroEMR.Application.OperationalTelemetry;
using MicroEMR.Application.Scheduling;
using MicroEMR.Application.Scheduling.DTOs;
using MicroEMR.Application.Scheduling.Services;
using MicroEMR.Application.Scheduling.Contracts;
using MicroEMR.Application.PatientEncounters.Services;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class SafeApiExceptionResponseTests
{
    private const string Sentinel = "Patient Alice; SELECT * FROM Secret; Server=private;Password=secret; C:\\clinical\\result.sql; Bearer token-secret";

    [Fact]
    public async Task UnexpectedFailureReturnsBoundedProblemAndOneSafeCorrelatedLog()
    {
        var registrations = new ServiceCollection().AddLogging();
        registrations.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["traceId"] =
                OperationalTrace.Capture(context.HttpContext.TraceIdentifier).TraceId);
        using var services = registrations.BuildServiceProvider();
        using var activity = new Activity("safe-api-error").SetIdFormat(ActivityIdFormat.W3C).Start();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Headers.Authorization = "Bearer token-secret";
        context.Request.Body = new MemoryStream("Patient Alice"u8.ToArray());
        context.Response.Body = new MemoryStream();
        var logger = new CapturingLogger<SafeApiExceptionHandler>();
        var handler = new SafeApiExceptionHandler(
            services.GetRequiredService<Microsoft.AspNetCore.Http.IProblemDetailsService>(), logger);

        Assert.True(await handler.TryHandleAsync(context, new InvalidOperationException(Sentinel), default));

        Assert.Equal(500, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal("An unexpected error occurred.", json.RootElement.GetProperty("title").GetString());
        Assert.Equal(500, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(activity.TraceId.ToHexString(), json.RootElement.GetProperty("traceId").GetString());
        Assert.DoesNotContain("Patient Alice", body);
        Assert.DoesNotContain("SELECT *", body);
        Assert.DoesNotContain("Server=", body);
        Assert.DoesNotContain("C:\\clinical", body);
        Assert.DoesNotContain("token-secret", body);
        Assert.Single(logger.Records);
        Assert.Null(logger.Records[0].Exception);
        Assert.Contains(activity.TraceId.ToHexString(), logger.Records[0].Message);
        Assert.DoesNotContain("Patient Alice", logger.Records[0].Message);
        Assert.DoesNotContain("token-secret", logger.Records[0].Message);
        Assert.DoesNotContain("Server=", logger.Records[0].Message);
    }

    [Fact]
    public async Task SqlFailureLogsNumberWithoutSqlTextAndHandlesNonJsonAccept()
    {
        var registrations = new ServiceCollection().AddLogging();
        registrations.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["traceId"] =
                OperationalTrace.Capture(context.HttpContext.TraceIdentifier).TraceId);
        using var services = registrations.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Headers.Accept = "text/html";
        context.Response.Body = new MemoryStream();
        var logger = new CapturingLogger<SafeApiExceptionHandler>();
        var handler = new SafeApiExceptionHandler(
            services.GetRequiredService<Microsoft.AspNetCore.Http.IProblemDetailsService>(), logger);

        Assert.True(await handler.TryHandleAsync(context, SqlFailure(1205, Sentinel), default));

        Assert.Equal(500, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains(context.TraceIdentifier, body);
        Assert.DoesNotContain("Patient Alice", body);
        Assert.Single(logger.Records);
        Assert.Contains("1205", logger.Records[0].Message);
        Assert.DoesNotContain("Patient Alice", logger.Records[0].Message);
        Assert.DoesNotContain("secret_procedure", logger.Records[0].Message);
    }

    [Fact]
    public async Task SchedulingUnexpectedFailureEscapesControllerForSafeGlobalHandling()
    {
        var service = ThrowingProxy<ICalendarService>.Create(new InvalidOperationException(Sentinel));
        var controller = new CalendarController(service, NullLogger<CalendarController>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.GetMultiProviderCalendar(new MultiProviderCalendarRequest(), default));
        Assert.Equal(Sentinel, exception.Message);

        var blocks = new ResourceBlocksController(
            ThrowingProxy<IResourceBlockService>.Create(new InvalidOperationException(Sentinel)),
            NullLogger<ResourceBlocksController>.Instance) { ControllerContext = Context() };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            blocks.CreateBlock(new CreateResourceBlockRequest(), default));

        var slots = new ScheduleSlotsController(
            ThrowingProxy<IScheduleSlotService>.Create(new InvalidOperationException(Sentinel)),
            NullLogger<ScheduleSlotsController>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            slots.GenerateSlots(new GenerateScheduleSlotsRequest(), default));
    }

    [Fact]
    public async Task SchedulingKnownConflictAndValidationKeepTheirStatusesWithoutRawMessages()
    {
        var conflict = new SchedulingConflictException(Sentinel);
        var appointments = new AppointmentsController(
            ThrowingProxy<IAppointmentService>.Create(conflict), NullLogger<AppointmentsController>.Instance)
        { ControllerContext = Context() };
        var conflictResponse = await appointments.CreateAppointment(new CreateAppointmentRequest(), default);
        Assert.Equal(409, Assert.IsType<ObjectResult>(conflictResponse.Result).StatusCode);
        Assert.DoesNotContain("Patient Alice", JsonSerializer.Serialize(((ObjectResult)conflictResponse.Result!).Value));

        var calendar = new CalendarController(
            ThrowingProxy<ICalendarService>.Create(new ArgumentException(Sentinel)), NullLogger<CalendarController>.Instance);
        var validation = await calendar.GetMultiProviderCalendar(new MultiProviderCalendarRequest(), default);
        Assert.Equal(400, Assert.IsType<ObjectResult>(validation.Result).StatusCode);
        Assert.DoesNotContain("Patient Alice", JsonSerializer.Serialize(((ObjectResult)validation.Result!).Value));

        var missing = new CalendarController(
            ThrowingProxy<ICalendarService>.Create(new KeyNotFoundException(Sentinel)),
            NullLogger<CalendarController>.Instance);
        var notFound = await missing.GetMultiProviderCalendar(new MultiProviderCalendarRequest(), default);
        Assert.Equal(404, Assert.IsType<ObjectResult>(notFound.Result).StatusCode);
    }

    [Fact]
    public async Task CurrentSchedulingCreateDoesNotReflectWrappedSqlText()
    {
        var context = Context();
        ClinicalUserActorContext.Set(context.HttpContext, 1);
        var request = new CreateScheduleAppointmentRequest
        {
            PatientUid = Guid.NewGuid(),
            PrimaryResourceUid = Guid.NewGuid(),
            StartDateTimeUtc = DateTime.UtcNow,
            EndDateTimeUtc = DateTime.UtcNow.AddMinutes(30)
        };
        SchedulingController Controller(Exception failure) => new(
            ThrowingProxy<ISchedulingReadService>.Create(failure),
            ThrowingProxy<ISchedulingAppointmentService>.Create(failure),
            ThrowingProxy<IPatientEncounterService>.Create(failure)) { ControllerContext = context };

        var known = await Controller(new InvalidOperationException(Sentinel, SqlFailure(51060, Sentinel)))
            .CreateAppointment(request, default);
        Assert.Equal(400, Assert.IsType<ObjectResult>(known.Result).StatusCode);
        Assert.DoesNotContain("Patient Alice", JsonSerializer.Serialize(((ObjectResult)known.Result!).Value));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Controller(new InvalidOperationException(Sentinel)).CreateAppointment(request, default));
    }

    [Fact]
    public async Task ResultSqlValidationIsBoundedAndUnknownSqlEscapesForGlobalHandling()
    {
        var context = Context();
        ClinicalUserActorContext.Set(context.HttpContext, 1);
        var controller = new PatientResultsController(ThrowingProxy<IPatientResultRepository>.Create(
            SqlFailure(51312, Sentinel))) { ControllerContext = context };
        var response = await controller.Create(Guid.NewGuid(), new CreatePatientResultRequest(), default);
        Assert.Equal(400, Assert.IsType<BadRequestObjectResult>(response).StatusCode);
        Assert.DoesNotContain("Patient Alice", JsonSerializer.Serialize(((BadRequestObjectResult)response).Value));

        var stale = new PatientResultsController(ThrowingProxy<IPatientResultRepository>.Create(
            SqlFailure(51304, Sentinel))) { ControllerContext = context };
        var conflict = await stale.Create(Guid.NewGuid(), new CreatePatientResultRequest(), default);
        Assert.Equal(409, Assert.IsType<ConflictObjectResult>(conflict).StatusCode);

        var unexpected = new PatientResultsController(ThrowingProxy<IPatientResultRepository>.Create(
            SqlFailure(1205, Sentinel))) { ControllerContext = context };
        await Assert.ThrowsAsync<SqlException>(() =>
            unexpected.Create(Guid.NewGuid(), new CreatePatientResultRequest(), default));
    }

    [Fact]
    public async Task PrescriptionSqlDerivedConflictIsBoundedAndValidationStaysBadRequest()
    {
        var context = Context();
        ClinicalUserActorContext.Set(context.HttpContext, 1);
        var controller = new PatientPrescriptionsController(ThrowingProxy<IPatientPrescriptionService>.Create(
            new PatientPrescriptionConcurrencyException(Sentinel, SqlFailure(51504, Sentinel))))
        { ControllerContext = context };
        var response = await controller.Update(Guid.NewGuid(), Guid.NewGuid(), new PrescriptionDraftRequest(), default);
        Assert.Equal(409, Assert.IsType<ConflictObjectResult>(response.Result).StatusCode);
        Assert.DoesNotContain("Patient Alice", JsonSerializer.Serialize(((ConflictObjectResult)response.Result!).Value));

        var invalid = new PatientPrescriptionsController(ThrowingProxy<IPatientPrescriptionService>.Create(
            new ArgumentException(Sentinel))) { ControllerContext = context };
        var validation = await invalid.Create(Guid.NewGuid(), new CreatePrescriptionDraftRequest(), default);
        Assert.Equal(400, Assert.IsType<BadRequestObjectResult>(validation.Result).StatusCode);
        Assert.DoesNotContain("Patient Alice", JsonSerializer.Serialize(((BadRequestObjectResult)validation.Result!).Value));
    }

    private static ControllerContext Context() => new() { HttpContext = new DefaultHttpContext() };

    private static SqlException SqlFailure(int number, string message)
    {
        var errorConstructor = typeof(SqlError).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .OrderByDescending(x => x.GetParameters().Length).First();
        var error = errorConstructor.Invoke(errorConstructor.GetParameters().Select(parameter =>
            parameter.Name switch
            {
                "infoNumber" => (object)number,
                "errorState" => (byte)1,
                "errorClass" => (byte)16,
                "server" => "private",
                "errorMessage" => message,
                "procedure" => "secret_procedure",
                "lineNumber" => 1,
                _ => parameter.HasDefaultValue ? parameter.DefaultValue :
                    parameter.ParameterType.IsValueType ? Activator.CreateInstance(parameter.ParameterType) : null
            }).ToArray());
        var collection = Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true)!;
        typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(collection, [error]);
        var create = typeof(SqlException).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .First(x => x.Name == "CreateException" && x.GetParameters().Length == 2);
        return (SqlException)create.Invoke(null, [collection, "16.0"])!;
    }

    public class ThrowingProxy<T> : DispatchProxy where T : class
    {
        private Exception _failure = null!;
        public static T Create(Exception failure)
        {
            var proxy = DispatchProxy.Create<T, ThrowingProxy<T>>();
            ((ThrowingProxy<T>)(object)proxy)._failure = failure;
            return proxy;
        }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => throw _failure;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(string Message, Exception? Exception)> Records { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Records.Add((formatter(state, exception), exception));
    }
}
