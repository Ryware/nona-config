ALTER TABLE Users ADD COLUMN PasswordResetTokenHash TEXT NULL;
ALTER TABLE Users ADD COLUMN PasswordResetTokenExpiresAt TEXT NULL;

CREATE INDEX IF NOT EXISTS idx_users_passwordresettokenhash
    ON Users(PasswordResetTokenHash);
