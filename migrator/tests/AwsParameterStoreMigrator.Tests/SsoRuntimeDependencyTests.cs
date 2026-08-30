namespace Nona.Migrator.AwsParameterStore.Tests;

public sealed class SsoRuntimeDependencyTests
{
    [Test]
    public async Task SsoCredentialProviderAssemblies_AreAvailableAtRuntime()
    {
        var ssoClientType = Type.GetType(
            "Amazon.SSO.AmazonSSOClient, AWSSDK.SSO",
            throwOnError: false);
        var ssoOidcClientType = Type.GetType(
            "Amazon.SSOOIDC.AmazonSSOOIDCClient, AWSSDK.SSOOIDC",
            throwOnError: false);

        await Assert.That(ssoClientType).IsNotNull();
        await Assert.That(ssoOidcClientType).IsNotNull();
    }
}
