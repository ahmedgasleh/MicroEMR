-- PC08.02: separately authored blocks; no legacy content backfill or replacement.
SET XACT_ABORT ON;
GO
IF OBJECT_ID(N'dbo.PatientEncounterContribution', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PatientEncounterContribution
    (
        ContributionId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ContributionUid UNIQUEIDENTIFIER NOT NULL CONSTRAINT UQ_EncounterContribution_Uid UNIQUE,
        EncounterUid UNIQUEIDENTIFIER NOT NULL,
        PatientUid UNIQUEIDENTIFIER NOT NULL,
        SectionKey NVARCHAR(100) NOT NULL,
        SectionTitle NVARCHAR(200) NOT NULL,
        OriginalContributorId BIGINT NOT NULL,
        OriginalContributorDisplayName NVARCHAR(200) NOT NULL,
        CreatedAtUtc DATETIME2(7) NOT NULL
    );
    CREATE INDEX IX_EncounterContribution_Note ON dbo.PatientEncounterContribution(EncounterUid, PatientUid, ContributionId);
END;
GO
IF OBJECT_ID(N'dbo.PatientEncounterContributionRevision', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PatientEncounterContributionRevision
    (
        ContributionUid UNIQUEIDENTIFIER NOT NULL,
        RevisionNumber INT NOT NULL CONSTRAINT CK_EncounterContribution_Revision CHECK (RevisionNumber > 0),
        Content NVARCHAR(MAX) NOT NULL,
        EditorId BIGINT NOT NULL,
        EditorDisplayName NVARCHAR(200) NOT NULL,
        RevisedAtUtc DATETIME2(7) NOT NULL,
        CONSTRAINT PK_EncounterContributionRevision PRIMARY KEY(ContributionUid, RevisionNumber),
        CONSTRAINT FK_EncounterContributionRevision_Block FOREIGN KEY(ContributionUid)
            REFERENCES dbo.PatientEncounterContribution(ContributionUid)
    );
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientEncounterContribution_Get
    @PatientUid UNIQUEIDENTIFIER, @EncounterUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.ContributionUid, c.SectionKey, c.SectionTitle, c.OriginalContributorId,
        c.OriginalContributorDisplayName, c.CreatedAtUtc,
        r.RevisionNumber, r.Content, r.EditorId, r.EditorDisplayName, r.RevisedAtUtc
    FROM dbo.PatientEncounterContribution c
    INNER JOIN dbo.PatientEncounter e ON e.EncounterUid=c.EncounterUid AND e.PatientUid=c.PatientUid
    INNER JOIN dbo.Patient p ON p.PatientUid=e.PatientUid AND p.IsDeleted=0
    INNER JOIN dbo.PatientEncounterContributionRevision r ON r.ContributionUid=c.ContributionUid
    WHERE c.PatientUid=@PatientUid AND c.EncounterUid=@EncounterUid
    ORDER BY c.ContributionId, r.RevisionNumber;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientEncounterContribution_Save
    @PatientUid UNIQUEIDENTIFIER, @EncounterUid UNIQUEIDENTIFIER,
    @SectionKey NVARCHAR(100), @Content NVARCHAR(MAX), @ExpectedRowVersion BINARY(8),
    @ActorId BIGINT, @ContributionUid UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NULL THROW 51171, 'Clinical audit storage is required.', 1;
    IF NULLIF(LTRIM(RTRIM(@Content)),N'') IS NULL OR DATALENGTH(@Content)>64000
        THROW 51171, 'Contribution content is required and must not exceed 32000 characters.', 1;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @Status NVARCHAR(30), @Version BINARY(8), @TemplateVersionUid UNIQUEIDENTIFIER,
            @PatientId BIGINT, @SectionTitle NVARCHAR(200), @ActorName NVARCHAR(200),
            @Revision INT=1, @Now DATETIME2(7)=SYSUTCDATETIME(), @OldContent NVARCHAR(MAX),
            @Action NVARCHAR(30)=N'ContributionCreated';
        -- Same parent lock as note saves/signing; every block write advances the parent RowVersion.
        SELECT @Status=e.EncounterStatus, @Version=e.RowVersion, @TemplateVersionUid=e.TemplateVersionUid,
            @PatientId=p.PatientId
        FROM dbo.PatientEncounter e WITH (UPDLOCK,HOLDLOCK)
        INNER JOIN dbo.Patient p ON p.PatientUid=e.PatientUid AND p.IsDeleted=0
        WHERE e.PatientUid=@PatientUid AND e.EncounterUid=@EncounterUid;
        IF @PatientId IS NULL BEGIN ROLLBACK; RETURN; END;
        IF @Status<>N'Open' OR @Status IS NULL THROW 51071, 'Only open encounters can be edited.', 1;
        IF @ExpectedRowVersion IS NULL OR @Version<>@ExpectedRowVersion
            THROW 51073, 'The encounter was changed by another user.', 1;
        SELECT @ActorName=COALESCE(NULLIF(DisplayName,N''),CONCAT(N'Clinical user ',UserId))
            FROM dbo.ApplicationUser WHERE UserId=@ActorId;
        IF @ActorName IS NULL THROW 51171, 'A tenant clinical actor is required.', 1;
        IF @TemplateVersionUid IS NULL
            SET @SectionTitle=CASE @SectionKey COLLATE Latin1_General_100_BIN2
                WHEN N'subjective' THEN N'Subjective' WHEN N'objective' THEN N'Objective'
                WHEN N'assessment' THEN N'Assessment' WHEN N'plan' THEN N'Plan' WHEN N'notes' THEN N'Note' END;
        ELSE
            SELECT @SectionTitle=s.Title FROM dbo.DocumentTemplateVersion v
            CROSS APPLY OPENJSON(v.DefinitionJson,'$.sections') WITH ([Key] NVARCHAR(100) '$.key', Title NVARCHAR(200) '$.title') s
            WHERE v.TemplateVersionUid=@TemplateVersionUid AND s.[Key] COLLATE Latin1_General_100_BIN2=@SectionKey COLLATE Latin1_General_100_BIN2;
        IF @SectionTitle IS NULL THROW 51171, 'The section does not belong to the encounter.', 1;
        IF @ContributionUid IS NULL
        BEGIN
            SET @ContributionUid=NEWID();
            INSERT dbo.PatientEncounterContribution(ContributionUid,EncounterUid,PatientUid,SectionKey,SectionTitle,
                OriginalContributorId,OriginalContributorDisplayName,CreatedAtUtc)
            VALUES(@ContributionUid,@EncounterUid,@PatientUid,@SectionKey,@SectionTitle,@ActorId,@ActorName,@Now);
        END
        ELSE
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM dbo.PatientEncounterContribution
                WHERE ContributionUid=@ContributionUid AND EncounterUid=@EncounterUid AND PatientUid=@PatientUid
                    AND SectionKey COLLATE Latin1_General_100_BIN2=@SectionKey COLLATE Latin1_General_100_BIN2)
                BEGIN ROLLBACK; RETURN; END;
            SELECT TOP(1) @Revision=RevisionNumber+1, @OldContent=Content
                FROM dbo.PatientEncounterContributionRevision WHERE ContributionUid=@ContributionUid ORDER BY RevisionNumber DESC;
            SET @Action=N'ContributionRevised';
        END;
        -- Revisions and original identity are immutable: no update/delete path.
        INSERT dbo.PatientEncounterContributionRevision(ContributionUid,RevisionNumber,Content,EditorId,EditorDisplayName,RevisedAtUtc)
            VALUES(@ContributionUid,@Revision,@Content,@ActorId,@ActorName,@Now);
        UPDATE dbo.PatientEncounter SET UpdatedBy=@ActorId, UpdatedAt=@Now
            WHERE EncounterUid=@EncounterUid AND PatientUid=@PatientUid;
        INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
            VALUES(@ActorId,@PatientId,@Action,N'PatientEncounterContribution',CONVERT(NVARCHAR(100),@ContributionUid),@OldContent,@Content,@Now);
        EXEC dbo.PatientEncounterHistory_Create @EncounterUid=@EncounterUid,@PatientUid=@PatientUid,
            @ActionType=@Action,@ActionDescription=N'Encounter contribution saved; original attribution preserved.',
            @CreatedBy=@ActorId,@ReturnResult=0;
        COMMIT;
        SELECT @ContributionUid;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT>0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO
