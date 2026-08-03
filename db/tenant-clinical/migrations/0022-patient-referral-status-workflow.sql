SET XACT_ABORT ON;
GO

CREATE OR ALTER PROCEDURE dbo.PatientReferral_MarkSent
    @PatientUid UNIQUEIDENTIFIER,
    @ReferralUid UNIQUEIDENTIFIER,
    @ExpectedRowVersion BINARY(8),
    @UpdatedBy BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    DECLARE @PatientReferralId BIGINT, @Status NVARCHAR(30), @RowVersion BINARY(8),
            @ChangedAt DATETIME2(0) = SYSUTCDATETIME(), @PatientId BIGINT;
    SELECT @PatientReferralId = r.PatientReferralId, @Status = r.Status,
           @RowVersion = r.RowVersion, @PatientId = p.PatientId
    FROM dbo.PatientReferral r WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN dbo.Patient p ON p.PatientUid = r.PatientUid AND p.IsDeleted = 0
    WHERE r.PatientUid = @PatientUid AND r.ReferralUid = @ReferralUid;

    IF @PatientReferralId IS NULL BEGIN ROLLBACK; THROW 51510, 'Referral not found.', 1; END;
    IF @Status <> N'Draft' BEGIN ROLLBACK; THROW 51511, 'Invalid referral transition.', 1; END;
    IF @RowVersion <> @ExpectedRowVersion BEGIN ROLLBACK; THROW 51512, 'Referral concurrency conflict.', 1; END;
    IF NOT EXISTS (SELECT 1 FROM dbo.ApplicationUser WHERE UserId = @UpdatedBy AND IsActive = 1)
        BEGIN ROLLBACK; THROW 51513, 'Active clinical user not found.', 1; END;

    UPDATE dbo.PatientReferral
    SET Status = N'Sent', SentAt = @ChangedAt, UpdatedAt = @ChangedAt, UpdatedBy = @UpdatedBy
    WHERE PatientReferralId = @PatientReferralId;

    INSERT dbo.AuditLog (UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
    VALUES (@UpdatedBy, @PatientId, N'MarkSent', N'PatientReferral', CONVERT(NVARCHAR(100), @ReferralUid),
            N'Status=Draft', N'Status=Sent', @ChangedAt);
    COMMIT;
    EXEC dbo.PatientReferral_GetByUid @PatientUid = @PatientUid, @ReferralUid = @ReferralUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientReferral_MarkResponseReceived
    @PatientUid UNIQUEIDENTIFIER,
    @ReferralUid UNIQUEIDENTIFIER,
    @ExpectedRowVersion BINARY(8),
    @UpdatedBy BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    DECLARE @PatientReferralId BIGINT, @Status NVARCHAR(30), @RowVersion BINARY(8),
            @ChangedAt DATETIME2(0) = SYSUTCDATETIME(), @PatientId BIGINT;
    SELECT @PatientReferralId = r.PatientReferralId, @Status = r.Status,
           @RowVersion = r.RowVersion, @PatientId = p.PatientId
    FROM dbo.PatientReferral r WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN dbo.Patient p ON p.PatientUid = r.PatientUid AND p.IsDeleted = 0
    WHERE r.PatientUid = @PatientUid AND r.ReferralUid = @ReferralUid;

    IF @PatientReferralId IS NULL BEGIN ROLLBACK; THROW 51510, 'Referral not found.', 1; END;
    IF @Status <> N'Sent' BEGIN ROLLBACK; THROW 51511, 'Invalid referral transition.', 1; END;
    IF @RowVersion <> @ExpectedRowVersion BEGIN ROLLBACK; THROW 51512, 'Referral concurrency conflict.', 1; END;
    IF NOT EXISTS (SELECT 1 FROM dbo.ApplicationUser WHERE UserId = @UpdatedBy AND IsActive = 1)
        BEGIN ROLLBACK; THROW 51513, 'Active clinical user not found.', 1; END;

    UPDATE dbo.PatientReferral
    SET Status = N'ResponseReceived', ResponseReceivedAt = @ChangedAt,
        UpdatedAt = @ChangedAt, UpdatedBy = @UpdatedBy
    WHERE PatientReferralId = @PatientReferralId;

    INSERT dbo.AuditLog (UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
    VALUES (@UpdatedBy, @PatientId, N'MarkResponseReceived', N'PatientReferral', CONVERT(NVARCHAR(100), @ReferralUid),
            N'Status=Sent', N'Status=ResponseReceived', @ChangedAt);
    COMMIT;
    EXEC dbo.PatientReferral_GetByUid @PatientUid = @PatientUid, @ReferralUid = @ReferralUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientReferral_Close
    @PatientUid UNIQUEIDENTIFIER,
    @ReferralUid UNIQUEIDENTIFIER,
    @ExpectedRowVersion BINARY(8),
    @UpdatedBy BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    DECLARE @PatientReferralId BIGINT, @Status NVARCHAR(30), @RowVersion BINARY(8),
            @ChangedAt DATETIME2(0) = SYSUTCDATETIME(), @PatientId BIGINT;
    SELECT @PatientReferralId = r.PatientReferralId, @Status = r.Status,
           @RowVersion = r.RowVersion, @PatientId = p.PatientId
    FROM dbo.PatientReferral r WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN dbo.Patient p ON p.PatientUid = r.PatientUid AND p.IsDeleted = 0
    WHERE r.PatientUid = @PatientUid AND r.ReferralUid = @ReferralUid;

    IF @PatientReferralId IS NULL BEGIN ROLLBACK; THROW 51510, 'Referral not found.', 1; END;
    IF @Status <> N'ResponseReceived' BEGIN ROLLBACK; THROW 51511, 'Invalid referral transition.', 1; END;
    IF @RowVersion <> @ExpectedRowVersion BEGIN ROLLBACK; THROW 51512, 'Referral concurrency conflict.', 1; END;
    IF NOT EXISTS (SELECT 1 FROM dbo.ApplicationUser WHERE UserId = @UpdatedBy AND IsActive = 1)
        BEGIN ROLLBACK; THROW 51513, 'Active clinical user not found.', 1; END;

    UPDATE dbo.PatientReferral
    SET Status = N'Closed', ClosedAt = @ChangedAt, UpdatedAt = @ChangedAt, UpdatedBy = @UpdatedBy
    WHERE PatientReferralId = @PatientReferralId;

    INSERT dbo.AuditLog (UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
    VALUES (@UpdatedBy, @PatientId, N'Close', N'PatientReferral', CONVERT(NVARCHAR(100), @ReferralUid),
            N'Status=ResponseReceived', N'Status=Closed', @ChangedAt);
    COMMIT;
    EXEC dbo.PatientReferral_GetByUid @PatientUid = @PatientUid, @ReferralUid = @ReferralUid;
END;
GO
