CREATE OR ALTER PROCEDURE dbo.PatientTask_GetOverdueCount
    @AssignedTo BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT_BIG(*)
    FROM dbo.PatientTask t
    INNER JOIN dbo.Patient p ON p.PatientUid = t.PatientUid AND p.IsDeleted = 0
    WHERE t.TaskStatus = N'Open'
      AND t.DueAt IS NOT NULL
      AND t.DueAt < SYSUTCDATETIME()
      AND (t.AssignedTo = @AssignedTo OR t.AssignedTo IS NULL);
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientTask_GetOverdue
    @AssignedTo BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT t.PatientTaskUid, t.PatientUid,
        LTRIM(RTRIM(CONCAT(p.FirstName, N' ', p.LastName))) PatientDisplayName,
        p.ChartNumber, t.TaskTitle, t.DueAt, t.TaskStatus, t.TaskPriority,
        au.DisplayName AssignedToDisplayName
    FROM dbo.PatientTask t
    INNER JOIN dbo.Patient p ON p.PatientUid = t.PatientUid AND p.IsDeleted = 0
    LEFT JOIN dbo.ApplicationUser au ON au.UserId = t.AssignedTo
    WHERE t.TaskStatus = N'Open'
      AND t.DueAt IS NOT NULL
      AND t.DueAt < SYSUTCDATETIME()
      AND (t.AssignedTo = @AssignedTo OR t.AssignedTo IS NULL)
    ORDER BY t.DueAt, t.PatientTaskId;
END;
GO
