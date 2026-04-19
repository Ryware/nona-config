namespace Nona.Infrastructure.Authentication;

public sealed class SsoOptions
{
    public GoogleSsoOptions Google { get; set; } = new();
    public MicrosoftSsoOptions Microsoft { get; set; } = new();
}

public sealed class GoogleSsoOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string JwksUri { get; set; } = "https://www.googleapis.com/oauth2/v3/certs";
    public List<string> Issuers { get; set; } =
    [
        "https://accounts.google.com",
        "accounts.google.com"
    ];
}

public sealed class MicrosoftSsoOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string TenantId { get; set; } = "common";
    public string JwksUri { get; set; } = "https://login.microsoftonline.com/common/discovery/v2.0/keys";
    public List<string> Issuers { get; set; } = [];
}
