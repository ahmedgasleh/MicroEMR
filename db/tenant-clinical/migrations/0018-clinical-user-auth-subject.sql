IF COL_LENGTH(N'dbo.ApplicationUser', N'AuthSubjectId') IS NULL
BEGIN
    ALTER TABLE dbo.ApplicationUser
        ADD AuthSubjectId NVARCHAR(450) COLLATE Latin1_General_100_BIN2 NULL;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.ApplicationUser')
      AND name = N'UX_ApplicationUser_AuthSubjectId'
)
BEGIN
    CREATE UNIQUE INDEX UX_ApplicationUser_AuthSubjectId
        ON dbo.ApplicationUser(AuthSubjectId)
        WHERE AuthSubjectId IS NOT NULL;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ApplicationUser_GetByAuthSubjectId
    @AuthSubjectId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT UserId, UserUid, Username, DisplayName, IsActive, AuthSubjectId
    FROM dbo.ApplicationUser
    WHERE AuthSubjectId = @AuthSubjectId COLLATE Latin1_General_100_BIN2
      AND IsActive = 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ApplicationUser_SetAuthSubjectId
    @UserId BIGINT,
    @AuthSubjectId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @AuthSubjectId IS NULL
       OR LEN(@AuthSubjectId) = 0
       OR @AuthSubjectId <> LTRIM(RTRIM(@AuthSubjectId))
        THROW 51090, 'Auth subject must be non-empty and must not contain leading or trailing whitespace.', 1;

    BEGIN TRANSACTION;

    DECLARE @ExistingSubject NVARCHAR(450);

    SELECT @ExistingSubject = AuthSubjectId
    FROM dbo.ApplicationUser WITH (UPDLOCK, HOLDLOCK)
    WHERE UserId = @UserId
      AND IsActive = 1;

    IF @@ROWCOUNT = 0
        THROW 51091, 'The active clinical user was not found.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.ApplicationUser WITH (UPDLOCK, HOLDLOCK)
        WHERE AuthSubjectId = @AuthSubjectId COLLATE Latin1_General_100_BIN2
          AND UserId <> @UserId
    )
        THROW 51092, 'The Auth subject is already mapped to another clinical user.', 1;

    IF @ExistingSubject IS NOT NULL
       AND @ExistingSubject <> @AuthSubjectId COLLATE Latin1_General_100_BIN2
        THROW 51093, 'The clinical user is already mapped to another Auth subject.', 1;

    UPDATE dbo.ApplicationUser
    SET AuthSubjectId = @AuthSubjectId
    WHERE UserId = @UserId
      AND AuthSubjectId IS NULL;

    COMMIT TRANSACTION;

    EXEC dbo.ApplicationUser_GetByAuthSubjectId @AuthSubjectId;
END;
GO
