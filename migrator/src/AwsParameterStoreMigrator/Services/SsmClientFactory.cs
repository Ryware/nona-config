using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.SimpleSystemsManagement;

namespace Nona.Migrator.AwsParameterStore.Services;

internal interface ISsmClientFactory
{
    string? ResolveDefaultRegion();
    IAmazonSimpleSystemsManagement Create(string region);
}

internal sealed class SsmClientFactory : ISsmClientFactory
{
    private readonly string? _profileName;
    private readonly AWSCredentials? _profileCredentials;
    private readonly string? _profileRegion;

    public SsmClientFactory(string? profileName)
    {
        _profileName = string.IsNullOrWhiteSpace(profileName) ? null : profileName;
        if (_profileName is null)
            return;

        var profileStore = new CredentialProfileStoreChain();
        if (!profileStore.TryGetAWSCredentials(_profileName, out _profileCredentials))
            throw new InvalidOperationException($"AWS profile '{_profileName}' was not found or could not provide credentials.");

        if (profileStore.TryGetProfile(_profileName, out var profile))
            _profileRegion = profile.Region?.SystemName;
    }

    public string? ResolveDefaultRegion()
        => _profileRegion
            ?? GetEnvironmentRegion()
            ?? FallbackRegionFactory.GetRegionEndpoint()?.SystemName;

    public IAmazonSimpleSystemsManagement Create(string region)
    {
        var endpoint = RegionEndpoint.GetBySystemName(region);
        return _profileCredentials is null
            ? new AmazonSimpleSystemsManagementClient(endpoint)
            : new AmazonSimpleSystemsManagementClient(_profileCredentials, endpoint);
    }

    private static string? GetEnvironmentRegion()
        => GetNonEmptyEnvironmentVariable("AWS_REGION")
            ?? GetNonEmptyEnvironmentVariable("AWS_DEFAULT_REGION");

    private static string? GetNonEmptyEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
