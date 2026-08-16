IF COL_LENGTH(N'dbo.AuditLog', N'AuditEventUid') IS NULL
    ALTER TABLE dbo.AuditLog ADD AuditEventUid UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.AuditLog', N'PatientUid') IS NULL
    ALTER TABLE dbo.AuditLog ADD PatientUid UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.AuditLog', N'EventCategory') IS NULL
    ALTER TABLE dbo.AuditLog ADD EventCategory NVARCHAR(50) NULL;
IF COL_LENGTH(N'dbo.AuditLog', N'ResourceType') IS NULL
    ALTER TABLE dbo.AuditLog ADD ResourceType NVARCHAR(100) NULL;
IF COL_LENGTH(N'dbo.AuditLog', N'ResourceUid') IS NULL
    ALTER TABLE dbo.AuditLog ADD ResourceUid UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.AuditLog', N'Outcome') IS NULL
    ALTER TABLE dbo.AuditLog ADD Outcome VARCHAR(30) NULL;
IF COL_LENGTH(N'dbo.AuditLog', N'RequestCorrelationId') IS NULL
    ALTER TABLE dbo.AuditLog ADD RequestCorrelationId NVARCHAR(100) NULL;
IF COL_LENGTH(N'dbo.AuditLog', N'SourceApplication') IS NULL
    ALTER TABLE dbo.AuditLog ADD SourceApplication NVARCHAR(50) NULL;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.AuditLog')
      AND name = N'UX_AuditLog_AuditEventUid'
)
    CREATE UNIQUE INDEX UX_AuditLog_AuditEventUid
        ON dbo.AuditLog(AuditEventUid) WHERE AuditEventUid IS NOT NULL;
GO

CREATE OR ALTER PROCEDURE dbo.AuditLog_RecordPatientChartOpened
    @PatientUid UNIQUEIDENTIFIER,
    @ClinicalUserId BIGINT,
    @RequestCorrelationId NVARCHAR(100),
    @SourceApplication NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @PatientUid IS NULL OR @PatientUid = '00000000-0000-0000-0000-000000000000'
        THROW 52200, 'A patient is required.', 1;
    IF NULLIF(LTRIM(RTRIM(@RequestCorrelationId)), N'') IS NULL
        THROW 52201, 'A request correlation identifier is required.', 1;

    DECLARE @PatientId BIGINT =
    (
        SELECT PatientId FROM dbo.Patient
        WHERE PatientUid = @PatientUid AND IsDeleted = 0
    );
    IF @PatientId IS NULL THROW 52202, 'Patient not found.', 1;
    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.ApplicationUser
        WHERE UserId = @ClinicalUserId AND IsActive = 1
    )
        THROW 52203, 'Active clinical user not found.', 1;

    DECLARE @AuditEventUid UNIQUEIDENTIFIER = NEWID();

    INSERT dbo.AuditLog
    (
        AuditEventUid, UserId, PatientId, PatientUid,
        ActionName, EntityName, EntityId,
        EventCategory, ResourceType, ResourceUid,
        Outcome, RequestCorrelationId, SourceApplication, CreatedAt
    )
    VALUES
    (
        @AuditEventUid, @ClinicalUserId, @PatientId, @PatientUid,
        N'PatientChartOpened', N'PatientChart', CONVERT(NVARCHAR(100), @PatientUid),
        N'ClinicalRead', N'PatientChart', @PatientUid,
        'Succeeded', LTRIM(RTRIM(@RequestCorrelationId)),
        COALESCE(NULLIF(LTRIM(RTRIM(@SourceApplication)), N''), N'MicroEMR.Api'),
        SYSUTCDATETIME()
    );

    SELECT @AuditEventUid AS AuditEventUid;
END;
GO
