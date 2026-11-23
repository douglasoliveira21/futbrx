START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261121090540_CatalogsAndSessions') THEN
    ALTER TABLE "Sessions" ADD "Created" timestamp with time zone NOT NULL DEFAULT TIMESTAMPTZ '-infinity';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261121090540_CatalogsAndSessions') THEN
    ALTER TABLE "Sessions" ADD "Device" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261121090540_CatalogsAndSessions') THEN
    UPDATE "Sessions" SET "Created" = NOW() WHERE "Created" = '-infinity'::timestamptz;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261121090540_CatalogsAndSessions') THEN
    CREATE TABLE "DataReleases" (
        "Id" uuid NOT NULL,
        "Version" text NOT NULL,
        "Season" integer NOT NULL,
        "Source" text NOT NULL,
        "License" text NOT NULL,
        "Published" boolean NOT NULL,
        "Revision" integer NOT NULL,
        "Created" timestamp with time zone NOT NULL,
        "PublishedAt" timestamp with time zone,
        CONSTRAINT "PK_DataReleases" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261121090540_CatalogsAndSessions') THEN
    CREATE TABLE "ClubData" (
        "ReleaseId" uuid NOT NULL,
        "Id" text NOT NULL,
        "Name" text NOT NULL,
        "Short" text NOT NULL,
        "Color" text NOT NULL,
        "Strength" integer NOT NULL,
        CONSTRAINT "PK_ClubData" PRIMARY KEY ("ReleaseId", "Id"),
        CONSTRAINT "FK_ClubData_DataReleases_ReleaseId" FOREIGN KEY ("ReleaseId") REFERENCES "DataReleases" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261121090540_CatalogsAndSessions') THEN
    CREATE TABLE "PlayerData" (
        "ReleaseId" uuid NOT NULL,
        "Id" integer NOT NULL,
        "ClubId" text NOT NULL,
        "Name" text NOT NULL,
        "Position" text NOT NULL,
        "Age" integer NOT NULL,
        "Rating" integer NOT NULL,
        "Value" integer NOT NULL,
        "Wage" integer NOT NULL,
        CONSTRAINT "PK_PlayerData" PRIMARY KEY ("ReleaseId", "Id"),
        CONSTRAINT "FK_PlayerData_DataReleases_ReleaseId" FOREIGN KEY ("ReleaseId") REFERENCES "DataReleases" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261121090540_CatalogsAndSessions') THEN
    CREATE UNIQUE INDEX "IX_DataReleases_Version" ON "DataReleases" ("Version");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261121090540_CatalogsAndSessions') THEN
    CREATE INDEX "IX_PlayerData_ReleaseId_ClubId" ON "PlayerData" ("ReleaseId", "ClubId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261121090540_CatalogsAndSessions') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261121090540_CatalogsAndSessions', '10.0.15');
    END IF;
END $EF$;
COMMIT;

