UPDATE Users
SET Role = 2
WHERE rowid = (SELECT MIN(rowid) FROM Users);

ALTER TABLE Users DROP COLUMN IsAdmin;
