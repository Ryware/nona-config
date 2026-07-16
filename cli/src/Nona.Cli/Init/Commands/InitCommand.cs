using System.Net;
using System.Text.Json;

namespace Nona.Cli.Init.Commands;

internal sealed record SeedFlag(string Key, string Value);

internal sealed record InitCommand(
    string BaseUrl,
    string Email,
    string Password,
    string Project,
    string Environment,
    SeedFlag? SeedFlag,
    string Scope,
    string Format,
    bool PrintKey);

internal sealed class InitCommandHandler(Func<HttpClient>? httpClientFactory = null)
{
    private const string InitKeyNamePrefix = "nona init";
    private readonly CliHttpJsonClient _client = new(httpClientFactory ?? DefaultHttpClientFactory);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<int> HandleAsync(InitCommand command, CancellationToken ct)
    {
        try
        {
            return await HandleCoreAsync(command, ct);
        }
        catch (InitCannotReachException ex)
        {
            Console.Error.WriteLine($"Cannot reach {command.BaseUrl}: {ex.Message}");
            return 4;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Unexpected API response: {ex.Message}");
            return 1;
        }
    }

    private async Task<int> HandleCoreAsync(InitCommand command, CancellationToken ct)
    {
        var auth = await AuthenticateAsync(command, ct);
        if (!auth.Success)
        {
            Console.Error.WriteLine(auth.Error);
            return auth.ExitCode;
        }

        var connection = new NonaCliConnectionOptions(command.BaseUrl, auth.Token);

        if (await EnsureProjectAsync(connection, command.Project, ct) is not 0)
            return 1;

        if (await EnsureEnvironmentAsync(connection, command.Project, command.Environment, ct) is not 0)
            return 1;

        if (command.SeedFlag is not null &&
            await SeedFlagAsync(connection, command.Project, command.Environment, command.SeedFlag, command.Scope, ct) is not 0)
        {
            return 1;
        }

        var apiKey = await ResolveApiKeyAsync(connection, command.Project, command.Environment, command.Scope, ct);
        if (apiKey is null)
            return 1;

        WriteOutput(command, apiKey);
        return 0;
    }

    private async Task<AuthResult> AuthenticateAsync(InitCommand command, CancellationToken ct)
    {
        var anonymous = new NonaCliConnectionOptions(command.BaseUrl, BearerToken: null);
        var firstTime = await SendAsync<bool>(anonymous, HttpMethod.Get, "auth/first-time", body: null, ct);

        if (!firstTime.Success)
            return AuthResult.Fail(1, firstTime.Error ?? "Failed to check first-time status.");

        if (firstTime.Value)
        {
            var register = await SendAsync<LoginResponse>(
                anonymous,
                HttpMethod.Post,
                "auth/register",
                new LoginRequest(command.Email, command.Password),
                ct);

            if (register.Success)
            {
                var token = register.Value?.Token;
                if (!string.IsNullOrWhiteSpace(token))
                    return AuthResult.Ok(token);
            }

            var registerError = register.Error ?? "Registration failed.";
            if (registerError.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                return await LoginAsync(command, anonymous, ct);

            return AuthResult.Fail(1, registerError);
        }

        return await LoginAsync(command, anonymous, ct);
    }

    private async Task<AuthResult> LoginAsync(
        InitCommand command,
        NonaCliConnectionOptions anonymous,
        CancellationToken ct)
    {
        var login = await SendAsync<LoginResponse>(
            anonymous,
            HttpMethod.Post,
            "auth/login",
            new LoginRequest(command.Email, command.Password),
            ct);

        if (login.Success && !string.IsNullOrWhiteSpace(login.Value?.Token))
            return AuthResult.Ok(login.Value.Token);

        var message = login.Error ?? "Authentication failed. Check --email and --password.";
        var exitCode = login.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest ? 3 : 1;
        return AuthResult.Fail(exitCode, message);
    }

    private async Task<int> EnsureProjectAsync(
        NonaCliConnectionOptions connection,
        string project,
        CancellationToken ct)
    {
        var existing = await ListProjectsAsync(connection, ct);
        if (existing is null)
            return 1;

        if (existing.Any(p => string.Equals(p.Name, project, StringComparison.OrdinalIgnoreCase)))
            return 0;

        var create = await SendAsync<ProjectDto>(
            connection,
            HttpMethod.Post,
            "admin/projects",
            new CreateProjectRequest(project),
            ct);

        if (create.Success)
            return 0;

        if (create.StatusCode == HttpStatusCode.Conflict)
        {
            existing = await ListProjectsAsync(connection, ct);
            return existing?.Any(p => string.Equals(p.Name, project, StringComparison.OrdinalIgnoreCase)) == true ? 0 : 1;
        }

        Console.Error.WriteLine(create.Error ?? "Project could not be created.");
        return 1;
    }

    private async Task<IReadOnlyList<ProjectDto>?> ListProjectsAsync(
        NonaCliConnectionOptions connection,
        CancellationToken ct)
    {
        var result = await SendAsync<List<ProjectDto>>(
            connection,
            HttpMethod.Get,
            "admin/projects",
            body: null,
            ct);

        if (result.Success)
            return result.Value ?? [];

        Console.Error.WriteLine(result.Error ?? "Projects could not be listed.");
        return null;
    }

    private async Task<int> EnsureEnvironmentAsync(
        NonaCliConnectionOptions connection,
        string project,
        string environment,
        CancellationToken ct)
    {
        var existing = await ListEnvironmentsAsync(connection, project, ct);
        if (existing is null)
            return 1;

        if (existing.Any(e => string.Equals(e.Name, environment, StringComparison.OrdinalIgnoreCase)))
            return 0;

        var create = await SendAsync<EnvironmentDto>(
            connection,
            HttpMethod.Post,
            $"admin/projects/{Segment(project)}/environments",
            new CreateEnvironmentRequest(environment),
            ct);

        if (create.Success)
            return 0;

        if (create.StatusCode == HttpStatusCode.Conflict)
        {
            existing = await ListEnvironmentsAsync(connection, project, ct);
            return existing?.Any(e => string.Equals(e.Name, environment, StringComparison.OrdinalIgnoreCase)) == true ? 0 : 1;
        }

        Console.Error.WriteLine(create.Error ?? "Environment could not be created.");
        return 1;
    }

    private async Task<IReadOnlyList<EnvironmentDto>?> ListEnvironmentsAsync(
        NonaCliConnectionOptions connection,
        string project,
        CancellationToken ct)
    {
        var result = await SendAsync<List<EnvironmentDto>>(
            connection,
            HttpMethod.Get,
            $"admin/projects/{Segment(project)}/environments",
            body: null,
            ct);

        if (result.Success)
            return result.Value ?? [];

        Console.Error.WriteLine(result.Error ?? "Environments could not be listed.");
        return null;
    }

    private async Task<int> SeedFlagAsync(
        NonaCliConnectionOptions connection,
        string project,
        string environment,
        SeedFlag seedFlag,
        string scope,
        CancellationToken ct)
    {
        var result = await SendAsync<ConfigEntryDto>(
            connection,
            HttpMethod.Put,
            $"admin/projects/{Segment(project)}/environments/{Segment(environment)}/config-entries/{Segment(seedFlag.Key)}",
            new UpsertConfigEntryRequest(seedFlag.Value, ContentType: null, Scope: scope),
            ct);

        if (result.Success)
            return 0;

        Console.Error.WriteLine(result.Error ?? "Seed flag could not be saved.");
        return 1;
    }

    private async Task<string?> ResolveApiKeyAsync(
        NonaCliConnectionOptions connection,
        string project,
        string environment,
        string scope,
        CancellationToken ct)
    {
        var keys = await ListApiKeysAsync(connection, project, ct);
        if (keys is null)
            return null;

        var keyName = BuildKeyName(scope);
        var existing = keys
            .OrderBy(k => k.Id)
            .FirstOrDefault(k =>
                string.Equals(k.Name, keyName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(k.Environment, environment, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(k.Scope, scope, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(k.Key));

        if (existing is not null)
            return existing.Key;

        var create = await SendAsync<ApiKeyDto>(
            connection,
            HttpMethod.Post,
            $"admin/projects/{Segment(project)}/api-keys",
            new CreateApiKeyRequest(keyName, environment, scope),
            ct);

        if (create.Success && !string.IsNullOrWhiteSpace(create.Value?.Key))
            return create.Value.Key;

        Console.Error.WriteLine(create.Error ?? "API key could not be created.");
        return null;
    }

    private async Task<IReadOnlyList<ApiKeyDto>?> ListApiKeysAsync(
        NonaCliConnectionOptions connection,
        string project,
        CancellationToken ct)
    {
        var result = await SendAsync<List<ApiKeyDto>>(
            connection,
            HttpMethod.Get,
            $"admin/projects/{Segment(project)}/api-keys",
            body: null,
            ct);

        if (result.Success)
            return result.Value ?? [];

        Console.Error.WriteLine(result.Error ?? "API keys could not be listed.");
        return null;
    }

    private async Task<CliHttpJsonResult<T>> SendAsync<T>(
        NonaCliConnectionOptions connection,
        HttpMethod method,
        string path,
        object? body,
        CancellationToken ct)
    {
        try
        {
            return await _client.SendAsync<T>(connection, method, path, body, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new InitCannotReachException(ex.Message, ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new InitCannotReachException("request timed out", ex);
        }
    }

    private void WriteOutput(InitCommand command, string apiKey)
    {
        var displayKey = command.PrintKey ? apiKey : MaskApiKey(apiKey);
        var verificationUrl = BuildVerificationUrl(command.BaseUrl, command.Environment, command.SeedFlag?.Key);

        switch (command.Format)
        {
            case "json":
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    baseUrl = command.BaseUrl,
                    environmentId = command.Environment,
                    apiKey = displayKey,
                    project = command.Project,
                    seededFlag = command.SeedFlag?.Key
                }, _jsonOptions));
                break;

            case "env-export":
                Console.WriteLine($"# Nona - project \"{command.Project}\", env \"{command.Environment}\"");
                Console.WriteLine($"export VITE_NONA_BASE_URL={ShellQuote(command.BaseUrl)}");
                Console.WriteLine($"export VITE_NONA_ENV_ID={ShellQuote(command.Environment)}");
                Console.WriteLine($"export VITE_NONA_API_KEY={ShellQuote(displayKey)}");
                WriteVerificationComment(verificationUrl, command.PrintKey);
                break;

            default:
                Console.WriteLine($"# Nona - project \"{command.Project}\", env \"{command.Environment}\"");
                Console.WriteLine($"VITE_NONA_BASE_URL={command.BaseUrl}");
                Console.WriteLine($"VITE_NONA_ENV_ID={command.Environment}");
                Console.WriteLine($"VITE_NONA_API_KEY={displayKey}");
                WriteVerificationComment(verificationUrl, command.PrintKey);
                break;
        }
    }

    private static void WriteVerificationComment(string verificationUrl, bool printKey)
    {
        if (!printKey)
            Console.WriteLine("# API key masked; re-run with --print-key to emit a working value.");

        Console.WriteLine($"# Verify: curl -H \"X-Api-Key: $VITE_NONA_API_KEY\" {verificationUrl}");
    }

    private static string BuildVerificationUrl(string baseUrl, string environment, string? key)
    {
        var pathKey = string.IsNullOrWhiteSpace(key) ? "<key>" : Segment(key);
        return $"{baseUrl.TrimEnd('/')}/api/{Segment(environment)}/{pathKey}";
    }

    private static string BuildKeyName(string scope) => $"{InitKeyNamePrefix} {scope}";

    private static string MaskApiKey(string key)
    {
        if (key.Length <= 4)
            return "****";

        return $"****{key[^4..]}";
    }

    private static string ShellQuote(string value) => $"'{value.Replace("'", "'\\''", StringComparison.Ordinal)}'";

    private static string Segment(string value) => Uri.EscapeDataString(value);

    private static HttpClient DefaultHttpClientFactory() => new() { Timeout = TimeSpan.FromSeconds(10) };

    private sealed record AuthResult(bool Success, string? Token, int ExitCode, string? Error)
    {
        public static AuthResult Ok(string token) => new(true, token, 0, null);
        public static AuthResult Fail(int exitCode, string error) => new(false, null, exitCode, error);
    }

    private sealed record LoginRequest(string Email, string Password);

    private sealed record LoginResponse(string Token, string? Username, string? Role, DateTimeOffset? ExpiresAt);

    private sealed record CreateProjectRequest(string Name);

    private sealed record CreateEnvironmentRequest(string Name);

    private sealed record UpsertConfigEntryRequest(string Value, string? ContentType, string? Scope);

    private sealed record CreateApiKeyRequest(string Name, string? Environment, string? Scope);

    private sealed class ProjectDto
    {
        public string? Name { get; set; }
    }

    private sealed class EnvironmentDto
    {
        public string? Name { get; set; }
    }

    private sealed class ConfigEntryDto
    {
        public string? Key { get; set; }
    }

    private sealed class ApiKeyDto
    {
        public long Id { get; set; }
        public string? Name { get; set; }
        public string? Key { get; set; }
        public string? Environment { get; set; }
        public string? Scope { get; set; }
    }
}

internal sealed class InitCannotReachException(string message, Exception innerException)
    : Exception(message, innerException);
