SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF OBJECT_ID(N'dbo.UserTenantAccessProfile', N'U') IS NULL
    THROW 51410, 'UserTenantAccessProfile must exist before applying migration 011.', 1;
GO

IF EXISTS
(
    SELECT 1
    FROM sys.key_constraints AS key_constraint
    INNER JOIN sys.indexes AS index_definition
        ON index_definition.object_id = key_constraint.parent_object_id
        AND index_definition.index_id = key_constraint.unique_index_id
    WHERE key_constraint.parent_object_id = OBJECT_ID(N'dbo.UserTenantAccessProfile')
        AND key_constraint.name = N'PK_UserTenantAccessProfile'
        AND index_definition.type = 1
)
BEGIN
    ALTER TABLE dbo.UserTenantAccessProfile
        DROP CONSTRAINT PK_UserTenantAccessProfile;

    ALTER TABLE dbo.UserTenantAccessProfile
        ADD CONSTRAINT PK_UserTenantAccessProfile
        PRIMARY KEY NONCLUSTERED (UserId, TenantUid);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.key_constraints AS key_constraint
    INNER JOIN sys.indexes AS index_definition
        ON index_definition.object_id = key_constraint.parent_object_id
        AND index_definition.index_id = key_constraint.unique_index_id
    WHERE key_constraint.parent_object_id = OBJECT_ID(N'dbo.UserTenantAccessProfile')
        AND key_constraint.name = N'PK_UserTenantAccessProfile'
        AND index_definition.type = 2
)
    THROW 51411, 'UserTenantAccessProfile primary key was not converted to nonclustered.', 1;
GO
