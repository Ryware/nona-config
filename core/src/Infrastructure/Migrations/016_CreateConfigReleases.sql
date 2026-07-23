ALTER TABLE Environments
    ADD COLUMN ActiveReleaseVersion TEXT NULL;

CREATE TABLE IF NOT EXISTS ConfigReleases (
    Project TEXT NOT NULL COLLATE NOCASE,
    Environment TEXT NOT NULL COLLATE NOCASE,
    Version TEXT NOT NULL COLLATE NOCASE,
    Major INTEGER NOT NULL,
    Minor INTEGER NOT NULL,
    Patch INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    Actor TEXT NOT NULL DEFAULT 'System',
    PRIMARY KEY (Project, Environment, Version)
);

CREATE TABLE IF NOT EXISTS ConfigReleaseEntries (
    Project TEXT NOT NULL COLLATE NOCASE,
    Environment TEXT NOT NULL COLLATE NOCASE,
    ReleaseVersion TEXT NOT NULL COLLATE NOCASE,
    Key TEXT NOT NULL COLLATE NOCASE,
    Value TEXT NOT NULL,
    ContentType TEXT NOT NULL DEFAULT 'text',
    Scope INTEGER NOT NULL DEFAULT 3,
    PRIMARY KEY (Project, Environment, ReleaseVersion, Key)
);

CREATE INDEX IF NOT EXISTS idx_configreleases_line
    ON ConfigReleases(Project, Environment, Major, Minor, Patch DESC);

CREATE INDEX IF NOT EXISTS idx_configreleaseentries_release
    ON ConfigReleaseEntries(Project, Environment, ReleaseVersion, Key);

INSERT OR IGNORE INTO ConfigReleases (
    Project,
    Environment,
    Version,
    Major,
    Minor,
    Patch,
    CreatedAt,
    Actor
)
SELECT
    Project,
    Name,
    '0.0.0',
    0,
    0,
    0,
    UpdatedAt,
    'Migration'
FROM Environments;

INSERT OR IGNORE INTO ConfigReleaseEntries (
    Project,
    Environment,
    ReleaseVersion,
    Key,
    Value,
    ContentType,
    Scope
)
SELECT
    Project,
    Environment,
    '0.0.0',
    Key,
    Value,
    ContentType,
    Scope
FROM ConfigEntries;

UPDATE Environments
SET ActiveReleaseVersion = '0.0.0'
WHERE ActiveReleaseVersion IS NULL;
