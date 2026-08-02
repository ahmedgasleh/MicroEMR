using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using MicroEMR.Api.Controllers;
using MicroEMR.Application.Scheduling;
using MicroEMR.Application.Scheduling.Contracts;
using MicroEMR.Application.Scheduling.Repositories;
using MicroEMR.Application.Scheduling.Services;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class AppointmentArrivedTransitionTests
{
    [Fact]
    public async Task MarkArrived_ScheduledAppointmentPersistsArrivedStatus()
    {
        var appointmentUid = Guid.NewGuid();
        var repository = new StubSchedulingAppointmentRepository
        {
            CurrentStatus = AppointmentStatus.Scheduled,
            ArrivedResult = new UpdateAppointmentStatusResponse
            {
                AppointmentUid = appointmentUid,
                Status = "Arrived"
            }
        };
        var service = CreateService(repository);

        var result = await service.MarkArrivedAsync(appointmentUid, 42);

        Assert.NotNull(result);
        Assert.Equal("Arrived", result.Status);
        Assert.Equal(AppointmentStatus.Scheduled, repository.PersistedExpectedStatus);
        Assert.Equal(42, repository.PersistedBy);
        Assert.Equal(1, repository.MarkArrivedCallCount);
    }

    [Theory]
    [InlineData(AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.Arrived)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    [InlineData(AppointmentStatus.Completed)]
    public async Task MarkArrived_InvalidSourceRejectsWithoutPersistence(AppointmentStatus currentStatus)
    {
        var repository = new StubSchedulingAppointmentRepository { CurrentStatus = currentStatus };
        var service = CreateService(repository);

        await Assert.ThrowsAsync<AppointmentStatusTransitionException>(() =>
            service.MarkArrivedAsync(Guid.NewGuid(), 42));

        Assert.Equal(0, repository.MarkArrivedCallCount);
    }

    [Fact]
    public async Task MarkArrived_MissingAppointmentReturnsNullWithoutPersistence()
    {
        var repository = new StubSchedulingAppointmentRepository { CurrentStatus = null };
        var service = CreateService(repository);

        var result = await service.MarkArrivedAsync(Guid.NewGuid(), 42);

        Assert.Null(result);
        Assert.Equal(0, repository.MarkArrivedCallCount);
    }

    [Fact]
    public async Task ArriveEndpoint_ValidAppointmentReturnsFocusedSuccess()
    {
        var appointmentUid = Guid.NewGuid();
        var repository = new StubSchedulingAppointmentRepository
        {
            CurrentStatus = AppointmentStatus.Scheduled,
            ArrivedResult = new UpdateAppointmentStatusResponse
            {
                AppointmentUid = appointmentUid,
                Status = "Arrived"
            }
        };
        var controller = CreateController(repository);

        var action = await controller.MarkAppointmentArrived(appointmentUid);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<UpdateAppointmentStatusResponse>(ok.Value);
        Assert.Equal("Arrived", response.Status);
    }

    [Fact]
    public async Task ArriveEndpoint_MissingAppointmentReturnsNotFound()
    {
        var controller = CreateController(new StubSchedulingAppointmentRepository());

        var action = await controller.MarkAppointmentArrived(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(action.Result);
    }

    [Fact]
    public async Task ArriveEndpoint_InvalidTransitionReturnsSafeConflict()
    {
        var controller = CreateController(new StubSchedulingAppointmentRepository
        {
            CurrentStatus = AppointmentStatus.Cancelled
        });

        var action = await controller.MarkAppointmentArrived(Guid.NewGuid());

        var conflict = Assert.IsType<ConflictObjectResult>(action.Result);
        Assert.DoesNotContain("SQL", conflict.Value!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ArriveEndpoint_ConcurrencyFailureReturnsSafeConflict()
    {
        var controller = CreateController(new StubSchedulingAppointmentRepository
        {
            CurrentStatus = AppointmentStatus.Scheduled,
            PersistException = new AppointmentStatusConcurrencyException("database detail")
        });

        var action = await controller.MarkAppointmentArrived(Guid.NewGuid());

        var conflict = Assert.IsType<ConflictObjectResult>(action.Result);
        Assert.DoesNotContain("database detail", conflict.Value!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ArriveMigration_IsAtomicAndExpectedStatusProtected()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "database",
            "tenant-clinical",
            "migrations",
            "0014-scheduling-mark-arrived.sql");
        var sql = File.ReadAllText(path);

        Assert.Contains("UPDLOCK, HOLDLOCK", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@ExpectedStatus", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AppointmentStatus = N'Arrived'", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AppointmentHistory_Create", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BEGIN TRANSACTION", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COMMIT TRANSACTION", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static SchedulingAppointmentService CreateService(
        ISchedulingAppointmentRepository repository) =>
        new(repository, new AppointmentStatusTransitionService());

    private static SchedulingController CreateController(
        ISchedulingAppointmentRepository repository)
    {
        var controller = new SchedulingController(null!, CreateService(repository), null!);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "42")],
                    "Test"))
            }
        };
        MicroEMR.Api.ClinicalUsers.ClinicalUserActorContext.Set(
            controller.HttpContext,
            42);
        return controller;
    }

    private sealed class StubSchedulingAppointmentRepository : ISchedulingAppointmentRepository
    {
        public AppointmentStatus? CurrentStatus { get; init; }
        public UpdateAppointmentStatusResponse? ArrivedResult { get; init; }
        public Exception? PersistException { get; init; }
        public int MarkArrivedCallCount { get; private set; }
        public AppointmentStatus? PersistedExpectedStatus { get; private set; }
        public long? PersistedBy { get; private set; }

        public Task<AppointmentStatus?> GetStatusAsync(
            Guid appointmentUid,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CurrentStatus);

        public Task<UpdateAppointmentStatusResponse?> MarkArrivedAsync(
            Guid appointmentUid,
            AppointmentStatus expectedStatus,
            long? updatedBy,
            CancellationToken cancellationToken = default)
        {
            MarkArrivedCallCount++;
            PersistedExpectedStatus = expectedStatus;
            PersistedBy = updatedBy;
            return PersistException is null
                ? Task.FromResult(ArrivedResult)
                : Task.FromException<UpdateAppointmentStatusResponse?>(PersistException);
        }

        public Task<ScheduleAppointmentListItemResponse> CreateAsync(CreateScheduleAppointmentRequest request, long? createdBy, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CancelScheduleAppointmentResponse?> CancelAsync(Guid appointmentUid, CancelScheduleAppointmentRequest request, long? cancelledBy, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ScheduleAppointmentDetailsResponse?> UpdateAsync(Guid appointmentUid, UpdateScheduleAppointmentRequest request, long? modifiedBy, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ScheduleAppointmentDetailsResponse?> RescheduleAsync(Guid appointmentUid, RescheduleAppointmentRequest request, long? modifiedBy, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UpdateAppointmentStatusResponse?> UpdateStatusAsync(Guid appointmentUid, UpdateAppointmentStatusRequest request, long? updatedBy, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SchedulingBlockedTimeResponse?> CreateBlockedTimeAsync(CreateSchedulingBlockedTimeRequest request, long? createdBy, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SchedulingBlockedTimeResponse?> CancelBlockedTimeAsync(Guid blockedTimeUid, long? cancelledBy, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
