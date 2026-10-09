-- Removes every object created by orgchart-schema.sql (the "org" schema) from the CURRENT database.
-- Nothing outside the org schema is touched. Safe to run more than once and after a partial install.
-- Run it while connected to the database that should be cleaned (e.g. master).
SET XACT_ABORT ON;
SET NOCOUNT ON;

PRINT N'Cleaning org schema in database: ' + DB_NAME();

BEGIN TRANSACTION;

-- OrgUnits and Positions reference each other; break that cycle first.
IF OBJECT_ID(N'[org].[FK_OrgUnits_Positions_ManagerPositionId]', N'F') IS NOT NULL
    ALTER TABLE [org].[OrgUnits] DROP CONSTRAINT [FK_OrgUnits_Positions_ManagerPositionId];

-- Children first so foreign keys never block a drop.
DROP TABLE IF EXISTS [org].[DelegationScopes];
DROP TABLE IF EXISTS [org].[Delegations];
DROP TABLE IF EXISTS [org].[Assignments];
DROP TABLE IF EXISTS [org].[Positions];
DROP TABLE IF EXISTS [org].[OrgUnits];
DROP TABLE IF EXISTS [org].[PositionTypes];
DROP TABLE IF EXISTS [org].[OrgUnitTypes];
DROP TABLE IF EXISTS [org].[AuditLogs];
DROP TABLE IF EXISTS [org].[ChartStamps];
DROP TABLE IF EXISTS [org].[__EFMigrationsHistory];

IF SCHEMA_ID(N'org') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.objects WHERE schema_id = SCHEMA_ID(N'org'))
        PRINT N'Schema org still contains other objects; it was NOT dropped. Review them manually.';
    ELSE
        EXEC(N'DROP SCHEMA [org];');
END;

COMMIT TRANSACTION;

SELECT
    DB_NAME() AS [Database],
    CASE WHEN SCHEMA_ID(N'org') IS NULL THEN N'org schema removed' ELSE N'org schema still exists' END AS [Result];
