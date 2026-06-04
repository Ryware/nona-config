using Google.Apis.Auth.OAuth2;
using Google.Apis.FirebaseRemoteConfig.v1;
using Google.Apis.Services;
using Nona.FirebaseRemoteConfigMigrator;
using Nona.FirebaseRemoteConfigMigrator.Models;
using Nona.FirebaseRemoteConfigMigrator.Options;
using System.Text.Json;

namespace Nona.Migrator.FirebaseRemoteConfig.Service;

internal sealed class FirebaseRemoteConfigClient(FirebaseOptions options)
{
    private const string FirebaseRemoteConfigScope = "https://www.googleapis.com/auth/firebase.remoteconfig";

    public async Task<FirebaseRemoteConfigTemplate> GetTemplateAsync(FirebaseImportSource source, CancellationToken cancellationToken)
    {
        var credential = await CreateCredentialAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(source.Namespace)
            ? await GetDefaultTemplateAsync(credential, cancellationToken)
            : await GetNamespacedTemplateAsync(credential, source.Namespace, cancellationToken);
    }

    private async Task<FirebaseRemoteConfigTemplate> GetDefaultTemplateAsync(GoogleCredential credential, CancellationToken cancellationToken)
    {
        using var service = new FirebaseRemoteConfigService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Nona.FirebaseRemoteConfigMigrator"
        });

        var request = service.Projects.GetRemoteConfig(BuildProjectResourceName());
        await using var responseStream = await request.ExecuteAsStreamAsync(cancellationToken);
        // Keep parsing into local model so planner still sees parameter groups and current Firebase fields.
        return await DeserializeTemplateAsync(responseStream, cancellationToken);
    }

    private async Task<FirebaseRemoteConfigTemplate> GetNamespacedTemplateAsync(GoogleCredential credential, string namespaceName, CancellationToken cancellationToken)
    {
        var tokenAccess = credential.UnderlyingCredential as ITokenAccess
            ?? throw new InvalidOperationException("Google credential does not support token access.");

        var accessToken = await tokenAccess.GetAccessTokenForRequestAsync(null, cancellationToken);
        using var httpClient = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildNamespacedRemoteConfigUri(namespaceName));
        request.Headers.Authorization = new("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Firebase namespaced Remote Config fetch failed for namespace '{namespaceName}' ({(int)response.StatusCode}): {content}");
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await DeserializeTemplateAsync(responseStream, cancellationToken);
    }

    private async Task<GoogleCredential> CreateCredentialAsync(CancellationToken cancellationToken)
    {
        ServiceAccountCredential serviceAccountCredential;
        if (!string.IsNullOrWhiteSpace(options.ServiceAccountJson))
        {
            serviceAccountCredential = CredentialFactory.FromJson<ServiceAccountCredential>(options.ServiceAccountJson);
        }
        else
        {
            var path = options.ServiceAccountJsonPath
                ?? throw new InvalidOperationException("Firebase service account path missing.");

            serviceAccountCredential = await CredentialFactory.FromFileAsync<ServiceAccountCredential>(path, cancellationToken);
        }

        var credential = serviceAccountCredential.ToGoogleCredential();
        if (credential.IsCreateScopedRequired)
            credential = credential.CreateScoped(FirebaseRemoteConfigScope);

        return credential;
    }

    private string BuildProjectResourceName()
    {
        return $"projects/{options.ProjectId}";
    }

    private string BuildNamespacedRemoteConfigUri(string namespaceName)
    {
        return $"https://firebaseremoteconfig.googleapis.com/v1/projects/{Uri.EscapeDataString(options.ProjectId)}/namespaces/{Uri.EscapeDataString(namespaceName)}/remoteConfig";
    }

    private static async Task<FirebaseRemoteConfigTemplate> DeserializeTemplateAsync(Stream responseStream, CancellationToken cancellationToken)
    {
        var template = await JsonSerializer.DeserializeAsync(
            responseStream,
            FirebaseSerializerContext.Default.FirebaseRemoteConfigTemplate,
            cancellationToken);

        return template ?? throw new InvalidOperationException("Firebase Remote Config response empty.");
    }
}
