DELETE FROM ProjectMembers
WHERE Username IN (
    SELECT Email
    FROM Users
    WHERE Role = 1
);

UPDATE Users
SET Role = 0
WHERE Role <> 2;

CREATE TRIGGER ProtectLastAdminUpdate
BEFORE UPDATE OF Role ON Users
WHEN OLD.Role = 2
 AND NEW.Role <> 2
 AND (SELECT COUNT(1) FROM Users WHERE Role = 2) = 1
BEGIN
    SELECT RAISE(ABORT, 'At least one admin is required');
END;

CREATE TRIGGER ProtectLastAdminDelete
BEFORE DELETE ON Users
WHEN OLD.Role = 2
 AND (SELECT COUNT(1) FROM Users WHERE Role = 2) = 1
BEGIN
    SELECT RAISE(ABORT, 'At least one admin is required');
END;
