-- Use the server-generated upload identity for metadata and audit. Existing rows/files are unchanged.
CREATE OR ALTER PROCEDURE dbo.PatientFile_Create
    @PatientUid UNIQUEIDENTIFIER,
    @OriginalFileName NVARCHAR(255),
    @StorageKey NVARCHAR(500),
    @ContentType NVARCHAR(200),
    @FileSizeBytes BIGINT,
    @FileExtension NVARCHAR(20) = NULL,
    @Sha256Hash CHAR(64) = NULL,
    @Description NVARCHAR(1000) = NULL,
    @Category NVARCHAR(100),
    @Title NVARCHAR(200),
    @SourceOrganization NVARCHAR(200) = NULL,
    @AuthorName NVARCHAR(200) = NULL,
    @DocumentDate DATE = NULL,
    @ReceivedDate DATE = NULL,
    @UploadedBy BIGINT,
    @FileUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS
        (SELECT 1 FROM dbo.Patient WHERE PatientUid = @PatientUid AND IsDeleted = 0)
        THROW 51700, 'Patient not found.', 1;
    IF NOT EXISTS
        (SELECT 1 FROM dbo.ApplicationUser WHERE UserId = @UploadedBy AND IsActive = 1)
        THROW 51701, 'Active clinical user not found.', 1;
    IF NULLIF(LTRIM(RTRIM(@Title)), N'') IS NULL
        THROW 51702, 'Document title is required.', 1;
    IF NULLIF(LTRIM(RTRIM(@Category)), N'') IS NULL
        THROW 51703, 'Document type or category is required.', 1;
    IF @DocumentDate > CONVERT(DATE, SYSUTCDATETIME())
       OR @ReceivedDate > CONVERT(DATE, SYSUTCDATETIME())
        THROW 51704, 'Document and received dates cannot be in the future.', 1;

    IF @FileUid IS NULL OR @FileUid = '00000000-0000-0000-0000-000000000000'
        THROW 51705, 'A nonempty file identifier is required.', 1;
    DECLARE @PatientId BIGINT =
        (SELECT PatientId FROM dbo.Patient WHERE PatientUid = @PatientUid AND IsDeleted = 0);

    BEGIN TRANSACTION;

    INSERT dbo.PatientFile
    (
        FileUid, PatientUid, OriginalFileName, StorageKey, ContentType,
        FileSizeBytes, FileExtension, Sha256Hash, Description, Category,
        Title, SourceOrganization, AuthorName, DocumentDate, ReceivedDate,
        UploadedBy
    )
    VALUES
    (
        @FileUid, @PatientUid, @OriginalFileName, @StorageKey, @ContentType,
        @FileSizeBytes, @FileExtension, @Sha256Hash,
        NULLIF(LTRIM(RTRIM(@Description)), N''), LTRIM(RTRIM(@Category)),
        LTRIM(RTRIM(@Title)), NULLIF(LTRIM(RTRIM(@SourceOrganization)), N''),
        NULLIF(LTRIM(RTRIM(@AuthorName)), N''), @DocumentDate, @ReceivedDate,
        @UploadedBy
    );

    DECLARE @AuditValue NVARCHAR(MAX) =
    (
        SELECT
            N'Active' AS [Status], LTRIM(RTRIM(@Title)) AS [Title],
            LTRIM(RTRIM(@Category)) AS [Category],
            NULLIF(LTRIM(RTRIM(@SourceOrganization)), N'') AS [SourceOrganization],
            NULLIF(LTRIM(RTRIM(@AuthorName)), N'') AS [AuthorName],
            @DocumentDate AS [DocumentDate], @ReceivedDate AS [ReceivedDate],
            @OriginalFileName AS [OriginalFileName], @Sha256Hash AS [Sha256Hash]
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    );

    INSERT dbo.AuditLog
        (UserId, PatientId, ActionName, EntityName, EntityId, NewValue, CreatedAt)
    VALUES
        (@UploadedBy, @PatientId, N'Create', N'PatientFile',
         CONVERT(NVARCHAR(100), @FileUid), @AuditValue, SYSUTCDATETIME());

    COMMIT TRANSACTION;

    EXEC dbo.PatientFile_GetByUid @PatientUid, @FileUid;
END;
GO
