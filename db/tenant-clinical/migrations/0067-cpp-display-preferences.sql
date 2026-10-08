SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.CppDisplayPreference', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CppDisplayPreference
    (
        UserId BIGINT NOT NULL CONSTRAINT PK_CppDisplayPreference PRIMARY KEY,
        SettingsJson NVARCHAR(MAX) NOT NULL,
        UpdatedAtUtc DATETIME2(7) NOT NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_CppDisplayPreference_User FOREIGN KEY(UserId) REFERENCES dbo.ApplicationUser(UserId),
        CONSTRAINT CK_CppDisplayPreference_Json CHECK(ISJSON(SettingsJson) = 1)
    );
END;
GO

CREATE OR ALTER PROCEDURE dbo.CppDisplayPreference_Get
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT SettingsJson, RowVersion FROM dbo.CppDisplayPreference WHERE UserId = @UserId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.CppDisplayPreference_Save
    @UserId BIGINT,
    @SettingsJson NVARCHAR(MAX),
    @ExpectedRowVersion BINARY(8) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @UserId IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId = @UserId AND IsActive = 1)
        THROW 52602, 'An active clinical user is required.', 1;
    IF @SettingsJson IS NULL OR ISJSON(@SettingsJson) <> 1
        THROW 52603, 'CPP display preferences must be valid JSON.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @OldValue NVARCHAR(MAX), @CurrentVersion BINARY(8);
        SELECT @OldValue = SettingsJson, @CurrentVersion = RowVersion
        FROM dbo.CppDisplayPreference WITH(UPDLOCK, HOLDLOCK) WHERE UserId = @UserId;

        IF @CurrentVersion IS NOT NULL
        BEGIN
            IF @ExpectedRowVersion IS NULL OR @CurrentVersion <> @ExpectedRowVersion
                THROW 52601, 'CPP display preferences changed. Reload before saving.', 1;
            UPDATE dbo.CppDisplayPreference
            SET SettingsJson = @SettingsJson, UpdatedAtUtc = SYSUTCDATETIME()
            WHERE UserId = @UserId AND RowVersion = @ExpectedRowVersion;
        END
        ELSE
        BEGIN
            IF @ExpectedRowVersion IS NOT NULL
                THROW 52601, 'CPP display preferences changed. Reload before saving.', 1;
            INSERT dbo.CppDisplayPreference(UserId, SettingsJson, UpdatedAtUtc)
            VALUES(@UserId, @SettingsJson, SYSUTCDATETIME());
        END;

        -- Configuration audit, with no patient ID and no clinical mutation event.
        INSERT dbo.AuditLog(UserId, ActionName, EntityName, EntityId, OldValue, NewValue)
        VALUES(@UserId, CASE WHEN @OldValue IS NULL THEN N'Create' ELSE N'Update' END,
            N'CppDisplayPreference', CONVERT(NVARCHAR(100), @UserId), @OldValue, @SettingsJson);

        -- Return the token for this save while the row lock is still held.
        EXEC dbo.CppDisplayPreference_Get @UserId = @UserId;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
