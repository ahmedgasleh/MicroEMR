SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.SchedulingResourcePreference', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchedulingResourcePreference
    (
        UserId BIGINT NOT NULL CONSTRAINT PK_SchedulingResourcePreference PRIMARY KEY,
        ResourceUidsJson NVARCHAR(MAX) NOT NULL,
        UpdatedAtUtc DATETIME2(7) NOT NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_SchedulingResourcePreference_User FOREIGN KEY(UserId) REFERENCES dbo.ApplicationUser(UserId),
        CONSTRAINT CK_SchedulingResourcePreference_Json CHECK(ISJSON(ResourceUidsJson) = 1)
    );
END;
GO

CREATE OR ALTER PROCEDURE dbo.SchedulingResourcePreference_Get
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ResourceUidsJson, RowVersion FROM dbo.SchedulingResourcePreference WHERE UserId = @UserId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.SchedulingResourcePreference_Save
    @UserId BIGINT,
    @ResourceUidsJson NVARCHAR(MAX),
    @ExpectedRowVersion BINARY(8) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @ResourceUidsJson IS NULL OR ISJSON(@ResourceUidsJson) <> 1 OR LEFT(LTRIM(@ResourceUidsJson), 1) <> N'['
        THROW 53903, 'Select supported scheduling resources.', 1;
    IF (SELECT COUNT(*) FROM OPENJSON(@ResourceUidsJson)) NOT BETWEEN 1 AND 200
        OR EXISTS (SELECT 1 FROM OPENJSON(@ResourceUidsJson)
            WHERE [type] <> 1 OR LEN([value]) <> 36 OR TRY_CONVERT(UNIQUEIDENTIFIER, [value]) IS NULL
                OR TRY_CONVERT(UNIQUEIDENTIFIER, [value]) = '00000000-0000-0000-0000-000000000000')
        THROW 53903, 'Select supported scheduling resources.', 1;
    IF (SELECT COUNT(*) FROM OPENJSON(@ResourceUidsJson)) <>
        (SELECT COUNT(DISTINCT TRY_CONVERT(UNIQUEIDENTIFIER, [value])) FROM OPENJSON(@ResourceUidsJson))
        THROW 53903, 'Resource defaults must not contain duplicates.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;
        IF @UserId IS NULL OR NOT EXISTS
            (SELECT 1 FROM dbo.ApplicationUser WITH(HOLDLOCK) WHERE UserId = @UserId AND IsActive = 1)
            THROW 53902, 'An active clinical user is required.', 1;

        DECLARE @OldValue NVARCHAR(MAX), @CurrentVersion BINARY(8);
        SELECT @OldValue = ResourceUidsJson, @CurrentVersion = RowVersion
        FROM dbo.SchedulingResourcePreference WITH(UPDLOCK, HOLDLOCK) WHERE UserId = @UserId;

        -- Validate again inside the save transaction: a stale availability read cannot save invalid resources.
        IF EXISTS (SELECT 1 FROM OPENJSON(@ResourceUidsJson) chosen
            LEFT JOIN dbo.ScheduleResource resource WITH(HOLDLOCK)
                ON resource.ResourceUid = TRY_CONVERT(UNIQUEIDENTIFIER, chosen.[value])
                AND resource.IsActive = 1 AND resource.ResourceType IN (N'Provider', N'Room', N'Equipment')
            WHERE resource.ResourceId IS NULL)
            THROW 53903, 'Select only available scheduling resources in the current tenant.', 1;

        IF @CurrentVersion IS NOT NULL
        BEGIN
            IF @ExpectedRowVersion IS NULL OR @CurrentVersion <> @ExpectedRowVersion
                THROW 53901, 'Resource defaults changed. Reload before saving.', 1;
            UPDATE dbo.SchedulingResourcePreference
            SET ResourceUidsJson = @ResourceUidsJson, UpdatedAtUtc = SYSUTCDATETIME()
            WHERE UserId = @UserId AND RowVersion = @ExpectedRowVersion;
        END
        ELSE
        BEGIN
            IF @ExpectedRowVersion IS NOT NULL
                THROW 53901, 'Resource defaults changed. Reload before saving.', 1;
            INSERT dbo.SchedulingResourcePreference(UserId, ResourceUidsJson, UpdatedAtUtc)
            VALUES(@UserId, @ResourceUidsJson, SYSUTCDATETIME());
        END;

        INSERT dbo.AuditLog(UserId, ActionName, EntityName, EntityId, OldValue, NewValue)
        VALUES(@UserId, CASE WHEN @OldValue IS NULL THEN N'Create' ELSE N'Update' END,
            N'SchedulingResourcePreference', CONVERT(NVARCHAR(100), @UserId), @OldValue, @ResourceUidsJson);
        EXEC dbo.SchedulingResourcePreference_Get @UserId = @UserId;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
