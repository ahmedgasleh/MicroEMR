CREATE OR ALTER PROCEDURE dbo.Appointment_ReportByStatus
    @StartDateTimeUtc DATETIME2(0),
    @EndDateTimeUtc DATETIME2(0)
AS
BEGIN
    SET NOCOUNT ON;
    IF @StartDateTimeUtc IS NULL OR @EndDateTimeUtc IS NULL OR @EndDateTimeUtc <= @StartDateTimeUtc
        THROW 51130, 'A valid appointment report date range is required.', 1;

    SELECT a.AppointmentStatus AS Status, COUNT(*) AS AppointmentCount
    FROM dbo.ScheduleAppointment a
    WHERE a.IsDeleted=0 AND a.StartDateTimeUtc>=@StartDateTimeUtc AND a.StartDateTimeUtc<@EndDateTimeUtc
    GROUP BY a.AppointmentStatus;

    SELECT a.AppointmentUid,a.StartDateTimeUtc,a.EndDateTimeUtc,a.PatientUid,
           COALESCE(NULLIF(LTRIM(RTRIM(CONCAT(p.LastName,N', ',p.FirstName))),N','),N'Unknown') AS PatientName,
           p.ChartNumber,r.DisplayName AS ProviderName,a.AppointmentStatus AS Status
    FROM dbo.ScheduleAppointment a
    JOIN dbo.Patient p ON p.PatientUid=a.PatientUid
    JOIN dbo.ScheduleResource r ON r.ResourceId=a.PrimaryResourceId
    WHERE a.IsDeleted=0 AND a.StartDateTimeUtc>=@StartDateTimeUtc AND a.StartDateTimeUtc<@EndDateTimeUtc
    ORDER BY a.StartDateTimeUtc,a.ScheduleAppointmentId;
END;
GO
