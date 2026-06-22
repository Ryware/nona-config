UPDATE ConfigEntries
SET ContentType = CASE
    WHEN lower(trim(ContentType)) IN ('json', 'application/json', 'text/json') THEN 'json'
    WHEN lower(trim(ContentType)) IN ('number', 'integer', 'float', 'double', 'decimal') THEN 'number'
    WHEN lower(trim(ContentType)) IN ('boolean', 'bool') THEN 'boolean'
    WHEN lower(trim(ContentType)) IN ('text', 'plain', 'text/plain', 'string') THEN
        CASE
            WHEN json_valid(Value) = 1 AND json_type(Value) IN ('true', 'false') THEN 'boolean'
            WHEN json_valid(Value) = 1 AND json_type(Value) IN ('integer', 'real') THEN 'number'
            WHEN json_valid(Value) = 1 AND json_type(Value) IN ('object', 'array', 'null') THEN 'json'
            ELSE 'text'
        END
    ELSE ContentType
END
WHERE ContentType IS NOT NULL;
