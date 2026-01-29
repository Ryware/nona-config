CREATE TABLE IF NOT EXISTS Projects (
    Name TEXT NOT NULL PRIMARY KEY COLLATE NOCASE,
    ServerApiKey TEXT,
    ClientApiKey TEXT,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_projects_serverapikey 
    ON Projects(ServerApiKey);

CREATE INDEX IF NOT EXISTS idx_projects_clientapikey 
    ON Projects(ClientApiKey);
