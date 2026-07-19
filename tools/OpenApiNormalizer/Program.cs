using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: OpenApiNormalizer <spec> [<spec> ...]");
    return 1;
}

foreach (var specPath in args)
{
    var root = JsonNode.Parse(await File.ReadAllTextAsync(specPath)) as JsonObject
        ?? throw new InvalidDataException($"OpenAPI document is not a JSON object: {specPath}");

    Normalize(root);

    var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(specPath, json + "\n", new UTF8Encoding(false));
}

return 0;

static void Normalize(JsonObject spec)
{
    var components = GetOrAddObject(spec, "components");
    var schemas = GetOrAddObject(components, "schemas");

    AddIfMissing(schemas, "ProblemDetails", new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["type"] = NullableStringSchema(),
            ["title"] = NullableStringSchema(),
            ["status"] = new JsonObject
            {
                ["type"] = "integer",
                ["format"] = "int32",
                ["nullable"] = true
            },
            ["detail"] = NullableStringSchema(),
            ["instance"] = NullableStringSchema()
        },
        ["additionalProperties"] = new JsonObject()
    });

    AddIfMissing(schemas, "ErrorResponse", new JsonObject
    {
        ["required"] = StringArray("error"),
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["error"] = StringSchema(),
            ["errorCode"] = NullableStringSchema()
        }
    });

    if (schemas["ConfigEntryDto"] is JsonObject configEntry)
    {
        GetOrAddObject(configEntry, "properties")["activeVersion"] = IntegerSchema();
        var required = GetOrAddArray(configEntry, "required");
        InsertRequiredAfter(required, "activeVersion", "scope");
    }

    AddIfMissing(schemas, "ConfigEntryVersionDto", new JsonObject
    {
        ["required"] = StringArray(
            "project",
            "environment",
            "key",
            "version",
            "value",
            "contentType",
            "scope",
            "createdAt",
            "actor"),
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["project"] = StringSchema(),
            ["environment"] = StringSchema(),
            ["key"] = StringSchema(),
            ["version"] = IntegerSchema(),
            ["value"] = StringSchema(),
            ["contentType"] = StringSchema(),
            ["scope"] = StringSchema(),
            ["createdAt"] = new JsonObject { ["type"] = "string", ["format"] = "date-time" },
            ["actor"] = NullableStringSchema()
        }
    });
    GetOrAddObject(GetOrAddObject(schemas, "ConfigEntryVersionDto"), "properties")["version"] = IntegerSchema();

    AddIfMissing(schemas, "RollbackConfigEntryRequest", new JsonObject
    {
        ["required"] = StringArray("version"),
        ["type"] = "object",
        ["properties"] = new JsonObject { ["version"] = IntegerSchema() }
    });
    GetOrAddObject(GetOrAddObject(schemas, "RollbackConfigEntryRequest"), "properties")["version"] = IntegerSchema();

    var paths = GetOrAddObject(spec, "paths");
    const string configEntryPath = "/admin/projects/{projectId}/environments/{environmentName}/config-entries/{key}";
    var baseParameters = new JsonArray(
        PathParameter("projectId"),
        PathParameter("environmentName"),
        PathParameter("key"));
    var errorResponse = Reference("#/components/schemas/ErrorResponse");

    GetOrAddObject(paths, $"{configEntryPath}/history")["get"] = new JsonObject
    {
        ["tags"] = StringArray("AdminConfigEntries"),
        ["parameters"] = baseParameters.DeepClone(),
        ["responses"] = new JsonObject
        {
            ["200"] = Response(
                "OK",
                new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = Reference("#/components/schemas/ConfigEntryVersionDto")
                }),
            ["404"] = Response("Not Found", errorResponse)
        },
        ["security"] = BearerSecurity()
    };

    GetOrAddObject(paths, $"{configEntryPath}/rollback")["post"] = new JsonObject
    {
        ["tags"] = StringArray("AdminConfigEntries"),
        ["parameters"] = baseParameters.DeepClone(),
        ["requestBody"] = RequestBody(Reference("#/components/schemas/RollbackConfigEntryRequest")),
        ["responses"] = new JsonObject
        {
            ["200"] = Response("OK", Reference("#/components/schemas/ConfigEntryDto")),
            ["400"] = Response("Bad Request", errorResponse),
            ["404"] = Response("Not Found", errorResponse)
        },
        ["security"] = BearerSecurity()
    };

    var methods = new HashSet<string>(
        ["get", "put", "post", "delete", "options", "head", "patch", "trace"],
        StringComparer.OrdinalIgnoreCase);

    foreach (var pathItem in paths.Select(item => item.Value).OfType<JsonObject>())
    {
        foreach (var (method, operationNode) in pathItem.ToArray())
        {
            if (!methods.Contains(method) || operationNode is not JsonObject operation)
            {
                continue;
            }

            var responses = GetOrAddObject(operation, "responses");
            AddIfMissing(responses, "4XX", Response("Client Error", errorResponse));
            AddIfMissing(
                responses,
                "5XX",
                Response("Server Error", Reference("#/components/schemas/ProblemDetails")));
        }
    }
}

static JsonObject GetOrAddObject(JsonObject parent, string name)
{
    if (parent[name] is JsonObject value)
    {
        return value;
    }

    value = new JsonObject();
    parent[name] = value;
    return value;
}

static JsonArray GetOrAddArray(JsonObject parent, string name)
{
    if (parent[name] is JsonArray value)
    {
        return value;
    }

    value = new JsonArray();
    parent[name] = value;
    return value;
}

static void AddIfMissing(JsonObject parent, string name, JsonNode value)
{
    if (!parent.ContainsKey(name))
    {
        parent[name] = value.DeepClone();
    }
}

static void InsertRequiredAfter(JsonArray required, string value, string after)
{
    if (required.Any(item => item?.GetValue<string>() == value))
    {
        return;
    }

    var afterIndex = -1;
    for (var index = 0; index < required.Count; index++)
    {
        if (required[index]?.GetValue<string>() == after)
        {
            afterIndex = index;
            break;
        }
    }

    required.Insert(afterIndex + 1, value);
}

static JsonObject StringSchema() => new() { ["type"] = "string" };

static JsonObject NullableStringSchema() => new() { ["type"] = "string", ["nullable"] = true };

static JsonObject IntegerSchema() => new() { ["type"] = "integer", ["format"] = "int32" };

static JsonObject Reference(string value) => new() { ["$ref"] = value };

static JsonObject PathParameter(string name) => new()
{
    ["name"] = name,
    ["in"] = "path",
    ["required"] = true,
    ["schema"] = StringSchema()
};

static JsonArray StringArray(params string[] values)
    => new(values.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());

static JsonArray BearerSecurity()
    => new(new JsonObject { ["Bearer"] = new JsonArray() });

static JsonObject Response(string description, JsonNode schema) => new()
{
    ["description"] = description,
    ["content"] = JsonContent(schema)
};

static JsonObject JsonContent(JsonNode schema) => new()
{
    ["application/json"] = new JsonObject { ["schema"] = schema.DeepClone() },
    ["text/json"] = new JsonObject { ["schema"] = schema.DeepClone() }
};

static JsonObject RequestBody(JsonNode schema) => new()
{
    ["content"] = new JsonObject
    {
        ["application/json"] = new JsonObject { ["schema"] = schema.DeepClone() },
        ["text/json"] = new JsonObject { ["schema"] = schema.DeepClone() },
        ["application/*+json"] = new JsonObject { ["schema"] = schema.DeepClone() }
    },
    ["required"] = true
};
