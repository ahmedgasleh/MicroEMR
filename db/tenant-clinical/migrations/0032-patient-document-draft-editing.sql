SET XACT_ABORT ON;
GO

CREATE OR ALTER PROCEDURE dbo.PatientDocument_GetByUid
    @DocumentUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT pd.PatientDocumentUid AS DocumentUid, pd.PatientUid,
        pd.TemplateUid, pd.TemplateVersionUid, pd.DocumentType,
        pd.DocumentTitle AS Title, pd.DocumentStatus,
        content.DocumentContent, pd.CreatedBy,
        applicationUser.DisplayName AS CreatedByDisplayName,
        pd.CreatedAt, pd.UpdatedAt, pd.RowVersion,
        content.RowVersion AS ContentRowVersion
    FROM dbo.PatientDocument AS pd
    LEFT JOIN dbo.PatientDocumentContent AS content
        ON content.PatientDocumentUid = pd.PatientDocumentUid
    LEFT JOIN dbo.ApplicationUser AS applicationUser ON applicationUser.UserId = pd.CreatedBy
    WHERE pd.PatientDocumentUid = @DocumentUid AND pd.IsDeleted = 0;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientDocument_UpdateDraft
    @DocumentUid UNIQUEIDENTIFIER,
    @DocumentTitle NVARCHAR(250),
    @DocumentType NVARCHAR(100),
    @DocumentContent NVARCHAR(MAX),
    @ExpectedDocumentRowVersion BINARY(8),
    @ExpectedContentRowVersion BINARY(8),
    @UpdatedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @PatientId BIGINT;
    DECLARE @DocumentStatus NVARCHAR(50);
    DECLARE @DocumentRowVersion BINARY(8);
    DECLARE @ContentRowVersion BINARY(8);

    BEGIN TRANSACTION;

    SELECT @PatientId = document.PatientId,
           @DocumentStatus = document.DocumentStatus,
           @DocumentRowVersion = document.RowVersion,
           @ContentRowVersion = content.RowVersion
    FROM dbo.PatientDocument AS document WITH (UPDLOCK, HOLDLOCK)
    LEFT JOIN dbo.PatientDocumentContent AS content WITH (UPDLOCK, HOLDLOCK)
        ON content.PatientDocumentUid = document.PatientDocumentUid
    WHERE document.PatientDocumentUid = @DocumentUid
      AND document.IsDeleted = 0;

    IF @PatientId IS NULL
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51080, 'The requested patient document was not found.', 1;
    END;

    IF @DocumentStatus <> N'Draft'
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51081, 'Only draft patient documents can be edited.', 1;
    END;

    IF @ContentRowVersion IS NULL
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51083, 'The patient document content was not found.', 1;
    END;

    IF @DocumentRowVersion <> @ExpectedDocumentRowVersion
       OR @ContentRowVersion <> @ExpectedContentRowVersion
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51082, 'The patient document was changed by another user.', 1;
    END;

    UPDATE dbo.PatientDocument
    SET DocumentTitle = LTRIM(RTRIM(@DocumentTitle)),
        DocumentType = LTRIM(RTRIM(@DocumentType)),
        UpdatedAt = SYSUTCDATETIME()
    WHERE PatientDocumentUid = @DocumentUid
      AND IsDeleted = 0
      AND DocumentStatus = N'Draft'
      AND RowVersion = @ExpectedDocumentRowVersion;

    IF @@ROWCOUNT <> 1
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51082, 'The patient document was changed by another user.', 1;
    END;

    UPDATE dbo.PatientDocumentContent
    SET DocumentContent = @DocumentContent,
        UpdatedAt = SYSUTCDATETIME(),
        UpdatedBy = @UpdatedBy
    WHERE PatientDocumentUid = @DocumentUid
      AND RowVersion = @ExpectedContentRowVersion;

    IF @@ROWCOUNT <> 1
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51082, 'The patient document was changed by another user.', 1;
    END;

    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
        INSERT dbo.AuditLog
            (UserId, PatientId, ActionName, EntityName, EntityId, NewValue, CreatedAt)
        VALUES
            (@UpdatedBy, @PatientId, N'UpdateDraft', N'PatientDocument',
             CONVERT(NVARCHAR(100), @DocumentUid), N'Draft document updated', SYSUTCDATETIME());

    COMMIT TRANSACTION;
    EXEC dbo.PatientDocument_GetByUid @DocumentUid;
END;
GO
