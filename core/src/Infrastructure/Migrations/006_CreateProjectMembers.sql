CREATE TABLE IF NOT EXISTS ProjectMembers (
    Username TEXT NOT NULL COLLATE NOCASE,
    ProjectName TEXT NOT NULL COLLATE NOCASE,
    Role INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    PRIMARY KEY (Username, ProjectName)
);

CREATE INDEX IF NOT EXISTS idx_projectmembers_username 
    ON ProjectMembers(Username);

CREATE INDEX IF NOT EXISTS idx_projectmembers_projectname 
    ON ProjectMembers(ProjectName);
