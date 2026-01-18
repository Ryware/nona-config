CREATE INDEX IF NOT EXISTS idx_configentries_project 
    ON ConfigEntries(Project);

CREATE INDEX IF NOT EXISTS idx_configentries_project_environment 
    ON ConfigEntries(Project, Environment);
