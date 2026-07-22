ALTER TABLE ConfigReleaseEntries
    ADD COLUMN NormalizedKey TEXT NULL;

UPDATE ConfigReleaseEntries
SET NormalizedKey = UPPER(Key)
WHERE Key NOT GLOB '*[^ -~]*';

CREATE INDEX IF NOT EXISTS idx_configreleaseentries_normalized_key
    ON ConfigReleaseEntries(Project, Environment, ReleaseVersion, NormalizedKey, Key);

CREATE INDEX IF NOT EXISTS idx_configreleaseentries_pending_normalized_key
    ON ConfigReleaseEntries(Project, Environment, ReleaseVersion)
    WHERE NormalizedKey IS NULL;
