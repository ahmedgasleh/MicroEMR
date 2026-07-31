using MicroEMR.Application.Scheduling;
using MicroEMR.Application.Scheduling.Services;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class AppointmentStatusTransitionServiceTests
{
    private readonly AppointmentStatusTransitionService _service = new();

    [Theory]
    [InlineData("Scheduled", AppointmentStatus.Scheduled)]
    [InlineData("Booked", AppointmentStatus.Scheduled)]
    [InlineData("Confirmed", AppointmentStatus.Confirmed)]
    [InlineData("Arrived", AppointmentStatus.Arrived)]
    [InlineData("CheckedIn", AppointmentStatus.CheckedIn)]
    [InlineData("Roomed", AppointmentStatus.Roomed)]
    [InlineData("Seen", AppointmentStatus.Seen)]
    [InlineData("Completed", AppointmentStatus.Completed)]
    [InlineData("Cancelled", AppointmentStatus.Cancelled)]
    [InlineData("NoShow", AppointmentStatus.NoShow)]
    public void Parse_MapsStoredStatuses(string stored, AppointmentStatus expected) =>
        Assert.Equal(expected, AppointmentStatusCatalog.Parse(stored));

    [Fact]
    public void Parse_RejectsUnknownStatus() =>
        Assert.Throws<ArgumentException>(() => AppointmentStatusCatalog.Parse("Active"));

    [Theory]
    [InlineData(AppointmentStatus.Scheduled, AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.Scheduled, AppointmentStatus.Arrived)]
    [InlineData(AppointmentStatus.Confirmed, AppointmentStatus.CheckedIn)]
    [InlineData(AppointmentStatus.Arrived, AppointmentStatus.Roomed)]
    [InlineData(AppointmentStatus.CheckedIn, AppointmentStatus.Seen)]
    [InlineData(AppointmentStatus.Roomed, AppointmentStatus.Seen)]
    [InlineData(AppointmentStatus.Seen, AppointmentStatus.Completed)]
    public void CanTransition_AllowsConfiguredForwardTransitions(AppointmentStatus current, AppointmentStatus target) =>
        Assert.True(_service.CanTransition(current, target));

    [Theory]
    [InlineData(AppointmentStatus.Arrived, AppointmentStatus.Scheduled)]
    [InlineData(AppointmentStatus.Completed, AppointmentStatus.Seen)]
    [InlineData(AppointmentStatus.Cancelled, AppointmentStatus.Scheduled)]
    [InlineData(AppointmentStatus.NoShow, AppointmentStatus.Arrived)]
    public void CanTransition_RejectsBackwardAndTerminalTransitions(AppointmentStatus current, AppointmentStatus target) =>
        Assert.False(_service.CanTransition(current, target));

    [Theory]
    [InlineData(AppointmentStatus.CheckedIn, "Checked In")]
    [InlineData(AppointmentStatus.Roomed, "In Room")]
    [InlineData(AppointmentStatus.Seen, "Encounter Started")]
    [InlineData(AppointmentStatus.NoShow, "No Show")]
    public void GetLabel_ReturnsConsistentLabels(AppointmentStatus status, string label) =>
        Assert.Equal(label, AppointmentStatusCatalog.GetLabel(status));
}
