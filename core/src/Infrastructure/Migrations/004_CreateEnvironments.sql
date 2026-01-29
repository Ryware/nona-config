CREATE TABLE IF NOT EXISTS Environments (
    Name TEXT NOT NULL COLLATE NOCASE,
    Project TEXT NOT NULL COLLATE NOCASE,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    PRIMARY KEY (Project, Name)
);

CREATE INDEX IF NOT EXISTS idx_environments_project 
    ON Environments(Project);
