CREATE OR ALTER PROCEDURE dbo.ApplicationUser_Provision
    @AuthSubjectId NVARCHAR(450),
    @Username NVARCHAR(100),
    @DisplayName NVARCHAR(200),
    @Email NVARCHAR(255) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @AuthSubjectId IS NULL OR LEN(@AuthSubjectId) = 0
       OR @AuthSubjectId <> LTRIM(RTRIM(@AuthSubjectId))
        THROW 51090, 'Auth subject must be non-empty and must not contain leading or trailing whitespace.', 1;
    IF @Username IS NULL OR LEN(LTRIM(RTRIM(@Username))) = 0
        THROW 51094, 'Username is required.', 1;
    IF @DisplayName IS NULL OR LEN(LTRIM(RTRIM(@DisplayName))) = 0
        THROW 51095, 'Display name is required.', 1;

    BEGIN TRANSACTION;

    DECLARE @UserId BIGINT;

    SELECT @UserId = UserId
    FROM dbo.ApplicationUser WITH (UPDLOCK, HOLDLOCK)
    WHERE AuthSubjectId = @AuthSubjectId COLLATE Latin1_General_100_BIN2;

    IF @UserId IS NOT NULL
    BEGIN
        COMMIT TRANSACTION;
        EXEC dbo.ApplicationUser_GetByAuthSubjectId @AuthSubjectId;
        RETURN;
    END;

    IF EXISTS
    (
        SELECT 1 FROM dbo.ApplicationUser WITH (UPDLOCK, HOLDLOCK)
        WHERE AuthSubjectId IS NULL
          AND Username = @Username
    )
        THROW 51096, 'An unmapped clinical user already has the supplied username; explicit mapping is required.', 1;

    IF @Email IS NOT NULL AND EXISTS
    (
        SELECT 1 FROM dbo.ApplicationUser WITH (UPDLOCK, HOLDLOCK)
        WHERE AuthSubjectId IS NULL
          AND Email = @Email
    )
        THROW 51097, 'An unmapped clinical user already has the supplied email; explicit mapping is required.', 1;

    INSERT dbo.ApplicationUser
        (Username, DisplayName, Email, IsActive, AuthSubjectId)
    VALUES
        (@Username, @DisplayName, @Email, 1, @AuthSubjectId);

    COMMIT TRANSACTION;

    EXEC dbo.ApplicationUser_GetByAuthSubjectId @AuthSubjectId;
END;
GO
