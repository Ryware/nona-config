CREATE TABLE IF NOT EXISTS ExternalIdentities (
    Provider TEXT NOT NULL,
    Issuer TEXT NOT NULL,
    Subject TEXT NOT NULL,
    UserEmail TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    LastLoginAt TEXT NULL,
    UNIQUE (Provider, Issuer, Subject)
);

CREATE INDEX IF NOT EXISTS IX_ExternalIdentities_UserEmail
    ON ExternalIdentities (UserEmail);
