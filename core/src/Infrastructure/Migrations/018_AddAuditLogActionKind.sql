ALTER TABLE AuditLogs
ADD COLUMN ActionKind TEXT NOT NULL DEFAULT 'activity'
    CHECK (ActionKind IN ('create', 'update', 'delete', 'activity'));

UPDATE AuditLogs
SET ActionKind = CASE
    WHEN Action IN (
        'Invited User',
        'Published Config Release',
        'Created Config Release Draft',
        'Share Link Created'
    )
        OR Action LIKE 'Created %'
        OR Action GLOB 'create_*'
        OR Action GLOB 'created_*'
        THEN 'create'
    WHEN Action IN (
        'Set Active Config Release',
        'Cleared Active Config Release',
        'Share Link Revoked'
    )
        OR Action LIKE 'Updated %'
        OR Action LIKE '% Updated %'
        OR Action LIKE 'Rolled Back %'
        OR Action GLOB 'update_*'
        OR Action GLOB 'updated_*'
        THEN 'update'
    WHEN Action LIKE 'Deleted %'
        OR Action GLOB 'delete_*'
        OR Action GLOB 'deleted_*'
        THEN 'delete'
    ELSE 'activity'
END;
