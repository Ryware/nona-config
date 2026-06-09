DROP INDEX IF EXISTS idx_projects_serverapikey;
DROP INDEX IF EXISTS idx_projects_clientapikey;

INSERT INTO ApiKeys (Name, Key, Project, Environment, Scope, CreatedAt, UpdatedAt)
SELECT 'Legacy server key', ServerApiKey, Name, NULL, 1, CreatedAt, UpdatedAt
FROM Projects
WHERE ServerApiKey IS NOT NULL
  AND ServerApiKey <> ''
  AND NOT EXISTS (
      SELECT 1
      FROM ApiKeys
      WHERE ApiKeys.Key = Projects.ServerApiKey
  );

INSERT INTO ApiKeys (Name, Key, Project, Environment, Scope, CreatedAt, UpdatedAt)
SELECT 'Legacy client key', ClientApiKey, Name, NULL, 2, CreatedAt, UpdatedAt
FROM Projects
WHERE ClientApiKey IS NOT NULL
  AND ClientApiKey <> ''
  AND NOT EXISTS (
      SELECT 1
      FROM ApiKeys
      WHERE ApiKeys.Key = Projects.ClientApiKey
  );

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
