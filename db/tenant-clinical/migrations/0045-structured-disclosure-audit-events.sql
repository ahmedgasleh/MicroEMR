CREATE OR ALTER PROCEDURE dbo.AuditLog_RecordStructuredRead
    @EventType NVARCHAR(100),
    @ResourceType NVARCHAR(100),
    @ResourceUid UNIQUEIDENTIFIER,
    @PatientUid UNIQUEIDENTIFIER,
    @ClinicalUserId BIGINT,
    @RequestCorrelationId NVARCHAR(100),
    @SourceApplication NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @EventType = LTRIM(RTRIM(@EventType));
    SET @ResourceType = LTRIM(RTRIM(@ResourceType));

    IF NOT
    (
        (@EventType = N'EncounterViewed' AND @ResourceType = N'Encounter')
        OR (@EventType = N'PatientDocumentViewed' AND @ResourceType = N'PatientDocument')
        OR (@EventType = N'PatientDocumentDownloaded' AND @ResourceType = N'PatientDocument')
        OR (@EventType = N'PatientFileDownloaded' AND @ResourceType = N'PatientFile')
    )
        THROW 52210, 'Unsupported structured read audit event/resource combination.', 1;

    IF @ResourceUid IS NULL OR @ResourceUid = '00000000-0000-0000-0000-000000000000'
        THROW 52211, 'A resource identifier is required.', 1;
    IF @PatientUid IS NULL OR @PatientUid = '00000000-0000-0000-0000-000000000000'
        THROW 52212, 'A patient identifier is required.', 1;
    IF NULLIF(LTRIM(RTRIM(@RequestCorrelationId)), N'') IS NULL
        THROW 52213, 'A request correlation identifier is required.', 1;

    DECLARE @PatientId BIGINT =
    (
        SELECT PatientId FROM dbo.Patient
        WHERE PatientUid = @PatientUid AND IsDeleted = 0
    );
    IF @PatientId IS NULL THROW 52214, 'Patient not found.', 1;
    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.ApplicationUser
        WHERE UserId = @ClinicalUserId AND IsActive = 1
    )
        THROW 52215, 'Active clinical user not found.', 1;

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
        @EventType, @ResourceType, CONVERT(NVARCHAR(100), @ResourceUid),
        N'ClinicalRead', @ResourceType, @ResourceUid,
        'Succeeded', LTRIM(RTRIM(@RequestCorrelationId)),
        COALESCE(NULLIF(LTRIM(RTRIM(@SourceApplication)), N''), N'MicroEMR.Api'),
        SYSUTCDATETIME()
    );

    SELECT @AuditEventUid AS AuditEventUid;
END;
GO
