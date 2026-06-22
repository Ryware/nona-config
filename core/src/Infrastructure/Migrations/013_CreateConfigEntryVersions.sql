ALTER TABLE ConfigEntries
    ADD COLUMN ActiveVersion INTEGER NOT NULL DEFAULT 1;

CREATE TABLE IF NOT EXISTS ConfigEntryVersions (
    Project TEXT NOT NULL COLLATE NOCASE,
    Environment TEXT NOT NULL COLLATE NOCASE,
    Key TEXT NOT NULL COLLATE NOCASE,
    Version INTEGER NOT NULL,
    Value TEXT NOT NULL,
    ContentType TEXT NOT NULL DEFAULT 'text',
    Scope INTEGER NOT NULL DEFAULT 3,
    CreatedAt TEXT NOT NULL,
    Actor TEXT NOT NULL DEFAULT 'System',
    PRIMARY KEY (Project, Environment, Key, Version)
);

INSERT OR IGNORE INTO ConfigEntryVersions (
    Project,
    Environment,
    Key,
    Version,
    Value,
    ContentType,
    Scope,
    CreatedAt,
    Actor
)
SELECT
    Project,
    Environment,
    Key,
    1,
    Value,
    ContentType,
    Scope,
    CreatedAt,
    'System'
FROM ConfigEntries;

CREATE INDEX IF NOT EXISTS idx_configentryversions_entry
    ON ConfigEntryVersions(Project, Environment, Key, Version DESC);
