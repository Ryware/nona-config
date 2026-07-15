namespace Nona.Cli.Auth.Commands;

internal sealed record RegisterFirstAdminCommand(
    string BaseUrl,
    string Email,
    string Password,
    bool SaveSession);

internal sealed class RegisterFirstAdminCommandHandler(
    CliSessionStore sessionStore,
    Func<HttpClient>? httpClientFactory = null)
{
    private readonly CliHttpJsonClient _client = new(httpClientFactory);

    public async Task<int> HandleAsync(RegisterFirstAdminCommand command, CancellationToken ct)
    {
        var result = await _client.SendAsync<RegisterFirstAdminResponse>(
            new NonaCliConnectionOptions(command.BaseUrl, BearerToken: null),
            HttpMethod.Post,
            "auth/register",
            new RegisterFirstAdminRequest(command.Email, command.Password),
            ct);

        if (!result.Success)
        {
            Console.Error.WriteLine(result.Error ?? "Registration failed.");
            return 1;
        }

        if (result.Value is null)
        {
            Console.Error.WriteLine("Registration failed.");
            return 1;
        }

        if (!result.Value.Success)
        {
            Console.Error.WriteLine(result.Value.Error ?? "Registration failed.");
            return 1;
        }

        var response = result.Value.Response;
        if (string.IsNullOrWhiteSpace(response?.Token))
        {
            Console.Error.WriteLine("Registration succeeded but did not return a session token.");
            return 1;
        }

        if (command.SaveSession)
        {
            sessionStore.Save(new CliAuthSession
            {
                BaseUrl = command.BaseUrl,
                Token = response.Token,
                Username = string.IsNullOrWhiteSpace(response.Username) ? command.Email : response.Username,
                Role = response.Role ?? string.Empty,
                ExpiresAt = response.ExpiresAt?.UtcDateTime ?? DateTime.UtcNow.AddHours(24),
                SavedAtUtc = DateTime.UtcNow
            });
        }

        Console.WriteLine($"Created first admin: {command.Email}");
        if (command.SaveSession)
            Console.WriteLine("Saved CLI session.");

        return 0;
    }

    private sealed record RegisterFirstAdminRequest(string Email, string Password);

    private sealed record RegisterFirstAdminResponse(bool Success, RegisterFirstAdminLoginResponse? Response, string? Error);

    private sealed record RegisterFirstAdminLoginResponse(
        string Token,
        string? Username,
        string? Role,
        DateTimeOffset? ExpiresAt);
}
