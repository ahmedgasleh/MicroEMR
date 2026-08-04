CREATE OR ALTER PROCEDURE dbo.PatientResult_GetUnreviewedCount
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT_BIG(*) AS UnreviewedCount
    FROM dbo.PatientResult r
    INNER JOIN dbo.Patient p ON p.PatientUid = r.PatientUid
    WHERE r.ResultStatus = N'New'
      AND r.ReviewedAt IS NULL
      AND p.IsDeleted = 0;
END;
GO
