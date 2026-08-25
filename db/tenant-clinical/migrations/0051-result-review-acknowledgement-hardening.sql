CREATE OR ALTER PROCEDURE dbo.PatientResult_GetUnreviewed
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        r.PatientResultUid,
        r.PatientUid,
        CONCAT(p.FirstName, N' ', p.LastName) AS PatientDisplayName,
        p.ChartNumber,
        r.ResultType,
        r.ResultName,
        r.ResultDate,
        r.ResultSummary,
        r.ResultValue,
        r.ResultUnit,
        r.ReferenceRange,
        r.ResultStatus,
        r.CreatedAt,
        r.RowVersion
    FROM dbo.PatientResult r
    INNER JOIN dbo.Patient p ON p.PatientUid = r.PatientUid
    WHERE r.ResultStatus = N'New'
      AND r.ReviewedAt IS NULL
      AND p.IsDeleted = 0
    ORDER BY r.ResultDate ASC, r.CreatedAt ASC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientResult_MarkReviewed
    @PatientUid UNIQUEIDENTIFIER,
    @PatientResultUid UNIQUEIDENTIFIER,
    @ReviewNote NVARCHAR(1000) = NULL,
    @ReviewedBy BIGINT,
    @ExpectedRowVersion BINARY(8)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.ApplicationUser
        WHERE UserId = @ReviewedBy
          AND IsActive = 1
    )
        THROW 51303, 'Active clinical reviewer not found.', 1;

    BEGIN TRANSACTION;

    DECLARE @CurrentStatus NVARCHAR(50);
    DECLARE @CurrentReviewedAt DATETIME2(0);
    DECLARE @CurrentRowVersion BINARY(8);
    DECLARE @PatientId BIGINT;

    SELECT
        @CurrentStatus = r.ResultStatus,
        @CurrentReviewedAt = r.ReviewedAt,
        @CurrentRowVersion = r.RowVersion,
        @PatientId = p.PatientId
    FROM dbo.PatientResult r WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN dbo.Patient p ON p.PatientUid = r.PatientUid
    WHERE r.PatientUid = @PatientUid
      AND r.PatientResultUid = @PatientResultUid;

    IF @PatientId IS NULL
    BEGIN
        ROLLBACK;
        RETURN;
    END;

    DECLARE @ReviewWasApplied BIT = 0;

    IF @CurrentStatus = N'New' AND @CurrentReviewedAt IS NULL
    BEGIN
        IF @ExpectedRowVersion IS NULL OR @ExpectedRowVersion <> @CurrentRowVersion
            THROW 51304, 'Result changed before it could be reviewed.', 1;

        DECLARE @ReviewedAt DATETIME2(0) = SYSUTCDATETIME();

        UPDATE dbo.PatientResult
        SET ResultStatus = N'Reviewed',
            ReviewedAt = @ReviewedAt,
            ReviewedBy = @ReviewedBy,
            ReviewNote = NULLIF(LTRIM(RTRIM(@ReviewNote)), N''),
            UpdatedAt = @ReviewedAt,
            UpdatedBy = @ReviewedBy
        WHERE PatientUid = @PatientUid
          AND PatientResultUid = @PatientResultUid
          AND ResultStatus = N'New'
          AND ReviewedAt IS NULL
          AND RowVersion = @ExpectedRowVersion;

        IF @@ROWCOUNT = 1
        BEGIN
            INSERT dbo.AuditLog
                (UserId, PatientId, ActionName, EntityName, EntityId, NewValue, CreatedAt)
            VALUES
                (@ReviewedBy, @PatientId, N'ResultReviewed', N'PatientResult',
                 CONVERT(NVARCHAR(100), @PatientResultUid), N'Status=Reviewed', @ReviewedAt);

            SET @ReviewWasApplied = 1;
        END;
    END;

    COMMIT;

    SELECT
        r.*,
        cu.DisplayName AS CreatedByDisplayName,
        uu.DisplayName AS UpdatedByDisplayName,
        ru.DisplayName AS ReviewedByDisplayName,
        @ReviewWasApplied AS ReviewWasApplied
    FROM dbo.PatientResult r
    LEFT JOIN dbo.ApplicationUser cu ON cu.UserId = r.CreatedBy
    LEFT JOIN dbo.ApplicationUser uu ON uu.UserId = r.UpdatedBy
    LEFT JOIN dbo.ApplicationUser ru ON ru.UserId = r.ReviewedBy
    WHERE r.PatientUid = @PatientUid
      AND r.PatientResultUid = @PatientResultUid;
END;
GO
