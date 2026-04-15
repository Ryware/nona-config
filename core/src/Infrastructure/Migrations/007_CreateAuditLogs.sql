CREATE TABLE IF NOT EXISTS AuditLogs (
    Actor TEXT NOT NULL,
    ActorIsSystem INTEGER NOT NULL DEFAULT 0,
    Action TEXT NOT NULL,
    Target TEXT NOT NULL,
    Project TEXT NULL,
    Environment TEXT NULL,
    CreatedAt TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS IX_AuditLogs_CreatedAt
    ON AuditLogs (CreatedAt DESC);

CREATE INDEX IF NOT EXISTS IX_AuditLogs_Action
    ON AuditLogs (Action);

CREATE INDEX IF NOT EXISTS IX_AuditLogs_Project
    ON AuditLogs (Project);
