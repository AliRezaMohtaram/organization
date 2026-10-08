-- OrgChart module schema for SQL Server. Generated from EF Core migrations (OrgChart.EFCore); do not edit by hand.
-- Regenerate: dotnet ef migrations script -p src/OrgChart.EFCore -s src/OrgChart.EFCore --idempotent
-- Idempotent: safe to run more than once; only migrations not yet applied are executed.
-- Filtered indexes require these SET options (sqlcmd defaults QUOTED_IDENTIFIER to OFF).
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO

IF OBJECT_ID(N'[org].[__EFMigrationsHistory]') IS NULL
BEGIN
    IF SCHEMA_ID(N'org') IS NULL EXEC(N'CREATE SCHEMA [org];');
    CREATE TABLE [org].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008100654_InitialCreate'
)
BEGIN
    IF SCHEMA_ID(N'org') IS NULL EXEC(N'CREATE SCHEMA [org];');
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008100654_InitialCreate'
)
BEGIN
    CREATE TABLE [org].[AuditLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [At] datetime2 NOT NULL,
        [ActorUserId] nvarchar(450) NULL,
        [Operation] nvarchar(64) NOT NULL,
        [EntityType] nvarchar(128) NOT NULL,
        [EntityId] nvarchar(128) NULL,
        [ChangeJson] nvarchar(max) NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008100654_InitialCreate'
)
BEGIN
    CREATE TABLE [org].[OrgUnitTypes] (
        [Id] int NOT NULL IDENTITY,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(450) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(450) NULL,
        [Key] nvarchar(256) NOT NULL,
        [NormalizedKey] nvarchar(256) NOT NULL,
        [Title] nvarchar(256) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_OrgUnitTypes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008100654_InitialCreate'
)
BEGIN
    CREATE TABLE [org].[PositionTypes] (
        [Id] int NOT NULL IDENTITY,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(450) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(450) NULL,
        [Key] nvarchar(256) NOT NULL,
        [NormalizedKey] nvarchar(256) NOT NULL,
        [Title] nvarchar(256) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_PositionTypes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008100654_InitialCreate'
)
BEGIN
    CREATE TABLE [org].[Assignments] (
        [Id] int NOT NULL IDENTITY,
        [PositionId] int NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [Kind] int NOT NULL,
        [ValidFrom] datetime2 NULL,
        [ValidTo] datetime2 NULL,
        [Note] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(450) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(450) NULL,
        CONSTRAINT [PK_Assignments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Assignments_ValidRange] CHECK ([ValidFrom] IS NULL OR [ValidTo] IS NULL OR [ValidFrom] <= [ValidTo])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008100654_InitialCreate'
)
BEGIN
    CREATE TABLE [org].[OrgUnits] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(64) NULL,
        [TypeId] int NOT NULL,
        [ParentId] int NULL,
        [ManagerPositionId] int NULL,
        [ValidFrom] datetime2 NULL,
        [ValidTo] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(450) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(450) NULL,
        [Key] nvarchar(256) NOT NULL,
        [NormalizedKey] nvarchar(256) NOT NULL,
        [Title] nvarchar(256) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_OrgUnits] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_OrgUnits_ValidRange] CHECK ([ValidFrom] IS NULL OR [ValidTo] IS NULL OR [ValidFrom] <= [ValidTo]),
        CONSTRAINT [FK_OrgUnits_OrgUnitTypes_TypeId] FOREIGN KEY ([TypeId]) REFERENCES [org].[OrgUnitTypes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OrgUnits_OrgUnits_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [org].[OrgUnits] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008100654_InitialCreate'
)
BEGIN
    CREATE TABLE [org].[Positions] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(64) NULL,
        [OrgUnitId] int NOT NULL,
        [TypeId] int NULL,
        [IsManagerial] bit NOT NULL,
        [ValidFrom] datetime2 NULL,
        [ValidTo] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(450) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(450) NULL,
        [Key] nvarchar(256) NOT NULL,
        [NormalizedKey] nvarchar(256) NOT NULL,
        [Title] nvarchar(256) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Positions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Positions_ValidRange] CHECK ([ValidFrom] IS NULL OR [ValidTo] IS NULL OR [ValidFrom] <= [ValidTo]),
        CONSTRAINT [FK_Positions_OrgUnits_OrgUnitId] FOREIGN KEY ([OrgUnitId]) REFERENCES [org].[OrgUnits] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Positions_PositionTypes_TypeId] FOREIGN KEY ([TypeId]) REFERENCES [org].[PositionTypes] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008100654_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Assignments_PositionId] ON [org].[Assignments] ([PositionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008100654_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Assignments_UserId] ON [org].[Assignments] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008100654_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_At] ON [org].[AuditLogs] ([At]);
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008100654_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_EntityType_EntityId] ON [org].[AuditLogs] ([EntityType], [EntityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008100654_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_OrgUnits_Code] ON [org].[OrgUnits] ([Code]) WHERE [Code] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008100654_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_OrgUnits_ManagerPositionId] ON [org].[OrgUnits] ([ManagerPositionId]) WHERE [ManagerPositionId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008100654_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OrgUnits_NormalizedKey] ON [org].[OrgUnits] ([NormalizedKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008100654_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrgUnits_ParentId] ON [org].[OrgUnits] ([ParentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008100654_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrgUnits_TypeId] ON [org].[OrgUnits] ([TypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008100654_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OrgUnitTypes_NormalizedKey] ON [org].[OrgUnitTypes] ([NormalizedKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008100654_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Positions_Code] ON [org].[Positions] ([Code]) WHERE [Code] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008100654_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Positions_NormalizedKey] ON [org].[Positions] ([NormalizedKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008100654_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Positions_OrgUnitId] ON [org].[Positions] ([OrgUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008100654_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Positions_TypeId] ON [org].[Positions] ([TypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008100654_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PositionTypes_NormalizedKey] ON [org].[PositionTypes] ([NormalizedKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008100654_InitialCreate'
)
BEGIN
    ALTER TABLE [org].[Assignments] ADD CONSTRAINT [FK_Assignments_Positions_PositionId] FOREIGN KEY ([PositionId]) REFERENCES [org].[Positions] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008100654_InitialCreate'
)
BEGIN
    ALTER TABLE [org].[OrgUnits] ADD CONSTRAINT [FK_OrgUnits_Positions_ManagerPositionId] FOREIGN KEY ([ManagerPositionId]) REFERENCES [org].[Positions] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008100654_InitialCreate'
)
BEGIN
    INSERT INTO [org].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261008100654_InitialCreate', N'9.0.20');
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008103136_ChartStamps'
)
BEGIN
    CREATE TABLE [org].[ChartStamps] (
        [Id] int NOT NULL,
        [Stamp] uniqueidentifier NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ChartStamps] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008103136_ChartStamps'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Stamp', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[org].[ChartStamps]'))
        SET IDENTITY_INSERT [org].[ChartStamps] ON;
    EXEC(N'INSERT INTO [org].[ChartStamps] ([Id], [Stamp], [UpdatedAt])
    VALUES (1, ''5d0f3c9e-2f6b-4b8e-9a51-0c7f6e1d2a10'', ''2026-01-01T00:00:00.0000000Z'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Stamp', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[org].[ChartStamps]'))
        SET IDENTITY_INSERT [org].[ChartStamps] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008103136_ChartStamps'
)
BEGIN
    INSERT INTO [org].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261008103136_ChartStamps', N'9.0.20');
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008120251_PositionHierarchyAndTypeLevels'
)
BEGIN
    ALTER TABLE [org].[Positions] ADD [ParentPositionId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008120251_PositionHierarchyAndTypeLevels'
)
BEGIN
    ALTER TABLE [org].[OrgUnitTypes] ADD [CanBeRoot] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008120251_PositionHierarchyAndTypeLevels'
)
BEGIN
    ALTER TABLE [org].[OrgUnitTypes] ADD [Level] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008120251_PositionHierarchyAndTypeLevels'
)
BEGIN
    CREATE INDEX [IX_Positions_ParentPositionId] ON [org].[Positions] ([ParentPositionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008120251_PositionHierarchyAndTypeLevels'
)
BEGIN
    EXEC(N'ALTER TABLE [org].[OrgUnitTypes] ADD CONSTRAINT [CK_OrgUnitTypes_Level] CHECK ([Level] IS NULL OR [Level] >= 1)');
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008120251_PositionHierarchyAndTypeLevels'
)
BEGIN
    ALTER TABLE [org].[Positions] ADD CONSTRAINT [FK_Positions_Positions_ParentPositionId] FOREIGN KEY ([ParentPositionId]) REFERENCES [org].[Positions] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [org].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008120251_PositionHierarchyAndTypeLevels'
)
BEGIN
    INSERT INTO [org].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261008120251_PositionHierarchyAndTypeLevels', N'9.0.20');
END;

COMMIT;
GO

