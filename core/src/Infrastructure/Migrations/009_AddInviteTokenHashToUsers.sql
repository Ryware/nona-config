ALTER TABLE Users ADD COLUMN InviteTokenHash TEXT NULL;

CREATE INDEX IF NOT EXISTS idx_users_invitetokenhash
    ON Users(InviteTokenHash);
