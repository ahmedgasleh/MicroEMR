using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Web.Models.Scheduling;
using MicroEMR.Web.Services.Scheduling;
using WebSchedulingController = MicroEMR.Web.Controllers.Scheduling.SchedulingController;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class SchedulingWebArrivedControllerTests
{
    [Fact]
    public async Task MarkAppointmentArrived_ForwardsAppointmentUidAndReturnsSuccess()
    {
        var appointmentUid = Guid.NewGuid();
        var client = new StubSchedulingApiClient
        {
            ArrivedResult = new UpdateAppointmentStatusResponse
            {
                AppointmentUid = appointmentUid,
                Status = "Arrived"
            }
        };
        var controller = CreateController(client);

        var result = await controller.MarkAppointmentArrived(appointmentUid, default);

        Assert.IsType<JsonResult>(result);
        Assert.Equal(appointmentUid, client.ReceivedAppointmentUid);
    }

    [Fact]
    public async Task MarkAppointmentArrived_MissingAppointmentReturnsNotFound()
    {
        var controller = CreateController(new StubSchedulingApiClient());

        var result = await controller.MarkAppointmentArrived(Guid.NewGuid(), default);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Theory]
    [InlineData(false, "This appointment can no longer be marked Arrived.")]
    [InlineData(true, "This appointment was updated by another user. Refresh and try again.")]
    public async Task MarkAppointmentArrived_TranslatesConflictSafely(
        bool concurrencyConflict,
        string expectedMessage)
    {
        var controller = CreateController(new StubSchedulingApiClient
        {
            ArrivedException = new AppointmentArrivedConflictException(concurrencyConflict)
        });

        var result = await controller.MarkAppointmentArrived(Guid.NewGuid(), default);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(conflict.Value);
        Assert.Contains(expectedMessage, json, StringComparison.Ordinal);
        Assert.DoesNotContain("database", json, StringComparison.OrdinalIgnoreCase);
    }

    private static WebSchedulingController CreateController(ISchedulingApiClient client) =>
        new(client, null!, NullLogger<WebSchedulingController>.Instance);

    private sealed class StubSchedulingApiClient : ISchedulingApiClient
    {
        public Guid? ReceivedAppointmentUid { get; private set; }
        public UpdateAppointmentStatusResponse? ArrivedResult { get; init; }
        public Exception? ArrivedException { get; init; }

        public Task<UpdateAppointmentStatusResponse?> MarkAppointmentArrivedAsync(
            Guid appointmentUid,
            CancellationToken cancellationToken = default)
        {
            ReceivedAppointmentUid = appointmentUid;
            return ArrivedException is null
                ? Task.FromResult(ArrivedResult)
                : Task.FromException<UpdateAppointmentStatusResponse?>(ArrivedException);
        }

        public Task<IReadOnlyList<ScheduleResourceResponse>> GetActiveResourcesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ScheduleAppointmentListItemResponse>> GetAppointmentsAsync(DateTime startUtc, DateTime endUtc, Guid? resourceUid, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ScheduleMonthSummaryItemResponse>> GetMonthSummaryAsync(DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ScheduleAppointmentListItemResponse> CreateAppointmentAsync(CreateScheduleAppointmentRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ScheduleAppointmentDetailsResponse?> GetAppointmentByUidAsync(Guid appointmentUid, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AppointmentHistoryResponse>> GetAppointmentHistoryAsync(Guid appointmentUid, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CancelScheduleAppointmentResponse?> CancelAppointmentAsync(Guid appointmentUid, CancelScheduleAppointmentRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ScheduleAppointmentDetailsResponse?> UpdateAppointmentAsync(Guid appointmentUid, UpdateScheduleAppointmentRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ScheduleAppointmentDetailsResponse?> RescheduleAppointmentAsync(Guid appointmentUid, RescheduleAppointmentRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UpdateAppointmentStatusResponse?> UpdateAppointmentStatusAsync(Guid appointmentUid, UpdateAppointmentStatusRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<StartEncounterFromAppointmentResponse?> StartEncounterFromAppointmentAsync(Guid appointmentUid, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SchedulingBlockedTimeResponse>> GetBlockedTimesAsync(DateTime startDateTimeUtc, DateTime endDateTimeUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SchedulingBlockedTimeResponse?> CreateBlockedTimeAsync(CreateSchedulingBlockedTimeRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SchedulingBlockedTimeResponse?> CancelBlockedTimeAsync(Guid blockedTimeUid, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
