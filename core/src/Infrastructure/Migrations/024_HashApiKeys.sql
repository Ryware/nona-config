DROP INDEX IF EXISTS idx_apikeys_key;
DROP INDEX IF EXISTS idx_apikeys_project;
DROP INDEX IF EXISTS idx_apikeys_project_environment;

CREATE TABLE ApiKeys_Hashed (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    KeyHash TEXT NOT NULL,
    Fingerprint TEXT NOT NULL,
    HashVersion INTEGER NOT NULL DEFAULT 1,
    Project TEXT NOT NULL COLLATE NOCASE,
    Environment TEXT COLLATE NOCASE,
    Scope INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);

INSERT INTO ApiKeys_Hashed (
    Id,
    Name,
    KeyHash,
    Fingerprint,
    HashVersion,
    Project,
    Environment,
    Scope,
    CreatedAt,
    UpdatedAt
)
SELECT
    Id,
    Name,
    Key,
    SUBSTR(Key, -8),
    0,
    Project,
    Environment,
    Scope,
    CreatedAt,
    UpdatedAt
FROM ApiKeys;

DROP TABLE ApiKeys;
ALTER TABLE ApiKeys_Hashed RENAME TO ApiKeys;

CREATE UNIQUE INDEX idx_apikeys_keyhash
    ON ApiKeys(KeyHash);

CREATE INDEX idx_apikeys_project
    ON ApiKeys(Project);

CREATE INDEX idx_apikeys_project_environment
    ON ApiKeys(Project, Environment);
