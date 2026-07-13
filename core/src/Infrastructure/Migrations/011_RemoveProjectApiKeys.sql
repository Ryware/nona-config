DROP INDEX IF EXISTS idx_projects_serverapikey;
DROP INDEX IF EXISTS idx_projects_clientapikey;

CREATE TABLE IF NOT EXISTS Projects_Rebuilt (
    Name TEXT NOT NULL COLLATE NOCASE,
    UrlSlug TEXT,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);

INSERT INTO Projects_Rebuilt (rowid, Name, UrlSlug, CreatedAt, UpdatedAt)
SELECT rowid, Name, UrlSlug, CreatedAt, UpdatedAt
FROM Projects;

DROP TABLE Projects;

ALTER TABLE Projects_Rebuilt RENAME TO Projects;
