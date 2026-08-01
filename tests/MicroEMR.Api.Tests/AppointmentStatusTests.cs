using MicroEMR.Application.Scheduling;
using MicroEMR.Application.Scheduling.Services;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class AppointmentStatusTests
{
    private readonly AppointmentStatusTransitionService _service = new();

    public static TheoryData<string, AppointmentStatus> StorageValues => new()
    {
        { "Scheduled", AppointmentStatus.Scheduled },
        { "Booked", AppointmentStatus.Scheduled },
        { "Confirmed", AppointmentStatus.Confirmed },
        { "Arrived", AppointmentStatus.Arrived },
        { "CheckedIn", AppointmentStatus.CheckedIn },
        { "Roomed", AppointmentStatus.Roomed },
        { "Seen", AppointmentStatus.Seen },
        { "Completed", AppointmentStatus.Completed },
        { "Cancelled", AppointmentStatus.Cancelled },
        { "NoShow", AppointmentStatus.NoShow }
    };

    public static TheoryData<AppointmentStatus, string> CanonicalStorageValues => new()
    {
        { AppointmentStatus.Scheduled, "Scheduled" },
        { AppointmentStatus.Confirmed, "Confirmed" },
        { AppointmentStatus.Arrived, "Arrived" },
        { AppointmentStatus.CheckedIn, "CheckedIn" },
        { AppointmentStatus.Roomed, "Roomed" },
        { AppointmentStatus.Seen, "Seen" },
        { AppointmentStatus.Completed, "Completed" },
        { AppointmentStatus.Cancelled, "Cancelled" },
        { AppointmentStatus.NoShow, "NoShow" }
    };

    public static TheoryData<AppointmentStatus, AppointmentStatus> AllowedTransitions => new()
    {
        { AppointmentStatus.Scheduled, AppointmentStatus.Confirmed },
        { AppointmentStatus.Scheduled, AppointmentStatus.Arrived },
        { AppointmentStatus.Scheduled, AppointmentStatus.Cancelled },
        { AppointmentStatus.Scheduled, AppointmentStatus.NoShow },
        { AppointmentStatus.Confirmed, AppointmentStatus.Arrived },
        { AppointmentStatus.Confirmed, AppointmentStatus.CheckedIn },
        { AppointmentStatus.Confirmed, AppointmentStatus.Cancelled },
        { AppointmentStatus.Confirmed, AppointmentStatus.NoShow },
        { AppointmentStatus.Arrived, AppointmentStatus.CheckedIn },
        { AppointmentStatus.Arrived, AppointmentStatus.Roomed },
        { AppointmentStatus.Arrived, AppointmentStatus.Cancelled },
        { AppointmentStatus.CheckedIn, AppointmentStatus.Roomed },
        { AppointmentStatus.CheckedIn, AppointmentStatus.Seen },
        { AppointmentStatus.Roomed, AppointmentStatus.Seen },
        { AppointmentStatus.Seen, AppointmentStatus.Completed }
    };

    [Theory]
    [MemberData(nameof(StorageValues))]
    public void Parse_MapsSupportedStorageValue(string value, AppointmentStatus expected) =>
        Assert.Equal(expected, AppointmentStatusMapper.Parse(value));

    [Theory]
    [MemberData(nameof(CanonicalStorageValues))]
    public void ToStorageValue_PreservesCanonicalCasing(AppointmentStatus status, string expected) =>
        Assert.Equal(expected, AppointmentStatusMapper.ToStorageValue(status));

    [Fact]
    public void Parse_RejectsNullValue() =>
        Assert.Throws<ArgumentNullException>(() => AppointmentStatusMapper.Parse(null!));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_RejectsBlankValue(string value) =>
        Assert.Throws<ArgumentException>(() => AppointmentStatusMapper.Parse(value));

    [Theory]
    [InlineData("Active")]
    [InlineData("Checked In")]
    public void Parse_RejectsUnknownValue(string value) =>
        Assert.Throws<ArgumentException>(() => AppointmentStatusMapper.Parse(value));

    [Fact]
    public void Parse_AcceptsStableCaseInsensitiveInputAndCanonicalizesOutput()
    {
        var status = AppointmentStatusMapper.Parse("scheduled");

        Assert.Equal(AppointmentStatus.Scheduled, status);
        Assert.Equal("Scheduled", AppointmentStatusMapper.ToStorageValue(status));
    }

    [Fact]
    public void ToStorageValue_RejectsUndefinedStatus() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AppointmentStatusMapper.ToStorageValue((AppointmentStatus)int.MaxValue));

    [Theory]
    [MemberData(nameof(AllowedTransitions))]
    public void CanTransition_AllowsEachConfiguredTransition(
        AppointmentStatus currentStatus,
        AppointmentStatus targetStatus) =>
        Assert.True(_service.CanTransition(currentStatus, targetStatus));

    [Theory]
    [InlineData(AppointmentStatus.Arrived, AppointmentStatus.Scheduled)]
    [InlineData(AppointmentStatus.CheckedIn, AppointmentStatus.Arrived)]
    [InlineData(AppointmentStatus.Seen, AppointmentStatus.Roomed)]
    [InlineData(AppointmentStatus.Cancelled, AppointmentStatus.Scheduled)]
    [InlineData(AppointmentStatus.NoShow, AppointmentStatus.Arrived)]
    [InlineData(AppointmentStatus.Completed, AppointmentStatus.Seen)]
    public void CanTransition_RejectsBackwardAndTerminalTransitions(
        AppointmentStatus currentStatus,
        AppointmentStatus targetStatus) =>
        Assert.False(_service.CanTransition(currentStatus, targetStatus));

    [Theory]
    [InlineData(AppointmentStatus.Scheduled)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    [InlineData(AppointmentStatus.Completed)]
    public void CanTransition_RejectsSameStateTransition(AppointmentStatus status) =>
        Assert.False(_service.CanTransition(status, status));

    [Fact]
    public void EnsureCanTransition_DoesNotThrowForValidTransition() =>
        _service.EnsureCanTransition(AppointmentStatus.Scheduled, AppointmentStatus.Arrived);

    [Fact]
    public void EnsureCanTransition_ThrowsFocusedExceptionForInvalidTransition()
    {
        var exception = Assert.Throws<AppointmentStatusTransitionException>(() =>
            _service.EnsureCanTransition(AppointmentStatus.Completed, AppointmentStatus.Scheduled));

        Assert.Equal(AppointmentStatus.Completed, exception.CurrentStatus);
        Assert.Equal(AppointmentStatus.Scheduled, exception.TargetStatus);
    }
}
