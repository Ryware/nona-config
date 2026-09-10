namespace Nona.Migrator.AwsParameterStore.Tests;

public sealed class AwsCredentialRuntimeDependencyTests
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

    [Test]
    public async Task SigninCredentialProviderAssembly_IsAvailableAtRuntime()
    {
        var signinClientType = Type.GetType(
            "Amazon.Signin.AmazonSigninClient, AWSSDK.Signin",
            throwOnError: false);

        await Assert.That(signinClientType).IsNotNull();
    }
}
