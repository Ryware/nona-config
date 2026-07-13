CREATE TABLE IF NOT EXISTS ParameterShareLinks (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    TokenHash TEXT NOT NULL,
    Project TEXT NOT NULL COLLATE NOCASE,
    Environment TEXT NOT NULL COLLATE NOCASE,
    Key TEXT NOT NULL COLLATE NOCASE,
    CanEdit INTEGER NOT NULL DEFAULT 1,
    CreatedBy TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    ExpiresAt TEXT NOT NULL,
    RevokedAt TEXT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_parametersharelinks_tokenhash
    ON ParameterShareLinks(TokenHash);

CREATE INDEX IF NOT EXISTS idx_parametersharelinks_scope
    ON ParameterShareLinks(Project, Environment, Key);

CREATE INDEX IF NOT EXISTS idx_parametersharelinks_expiresat
    ON ParameterShareLinks(ExpiresAt);
