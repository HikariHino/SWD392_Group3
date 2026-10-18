/*
   Align the existing Azure SQL question bank with the current .NET model.
   This preserves the legacy primary key names and all existing rows.
   The script is safe to rerun.
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH('dbo.Courses', 'CreatedBy') IS NULL
    ALTER TABLE dbo.Courses ADD CreatedBy nvarchar(max) NULL;
IF COL_LENGTH('dbo.Courses', 'UpdatedAt') IS NULL
    ALTER TABLE dbo.Courses ADD UpdatedAt datetime2 NULL;
IF COL_LENGTH('dbo.Courses', 'UpdatedBy') IS NULL
    ALTER TABLE dbo.Courses ADD UpdatedBy nvarchar(max) NULL;

IF COL_LENGTH('dbo.Questions', 'CreatedBy') IS NULL
    ALTER TABLE dbo.Questions ADD CreatedBy nvarchar(max) NULL;
IF COL_LENGTH('dbo.Questions', 'UpdatedAt') IS NULL
    ALTER TABLE dbo.Questions ADD UpdatedAt datetime2 NULL;
IF COL_LENGTH('dbo.Questions', 'UpdatedBy') IS NULL
    ALTER TABLE dbo.Questions ADD UpdatedBy nvarchar(max) NULL;

IF COL_LENGTH('dbo.Rubrics', 'CreatedAt') IS NULL
    ALTER TABLE dbo.Rubrics ADD CreatedAt datetime2 NOT NULL
        CONSTRAINT DF_Rubrics_CreatedAt DEFAULT SYSUTCDATETIME() WITH VALUES;
IF COL_LENGTH('dbo.Rubrics', 'CreatedBy') IS NULL
    ALTER TABLE dbo.Rubrics ADD CreatedBy nvarchar(max) NULL;
IF COL_LENGTH('dbo.Rubrics', 'UpdatedAt') IS NULL
    ALTER TABLE dbo.Rubrics ADD UpdatedAt datetime2 NULL;
IF COL_LENGTH('dbo.Rubrics', 'UpdatedBy') IS NULL
    ALTER TABLE dbo.Rubrics ADD UpdatedBy nvarchar(max) NULL;

UPDATE dbo.Courses SET CreatedAt = SYSUTCDATETIME() WHERE CreatedAt IS NULL;
UPDATE dbo.Questions SET CreatedAt = SYSUTCDATETIME() WHERE CreatedAt IS NULL;
ALTER TABLE dbo.Courses ALTER COLUMN CreatedAt datetime2 NOT NULL;
ALTER TABLE dbo.Questions ALTER COLUMN CreatedAt datetime2 NOT NULL;

COMMIT TRANSACTION;
