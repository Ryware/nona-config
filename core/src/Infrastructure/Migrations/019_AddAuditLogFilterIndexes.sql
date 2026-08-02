CREATE INDEX IF NOT EXISTS IX_AuditLogs_Action_NoCase
    ON AuditLogs (Action COLLATE NOCASE);

CREATE INDEX IF NOT EXISTS IX_AuditLogs_Environment_NoCase
    ON AuditLogs (Environment COLLATE NOCASE);
