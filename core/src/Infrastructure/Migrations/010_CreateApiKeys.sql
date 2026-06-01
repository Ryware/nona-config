CREATE TABLE IF NOT EXISTS ApiKeys (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Key TEXT NOT NULL,
    Project TEXT NOT NULL COLLATE NOCASE,
    Environment TEXT COLLATE NOCASE,
    Scope INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_apikeys_key
    ON ApiKeys(Key);

CREATE INDEX IF NOT EXISTS idx_apikeys_project
    ON ApiKeys(Project);

CREATE INDEX IF NOT EXISTS idx_apikeys_project_environment
    ON ApiKeys(Project, Environment);
