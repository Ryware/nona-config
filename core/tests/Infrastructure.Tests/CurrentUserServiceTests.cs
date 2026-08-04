using Microsoft.AspNetCore.Http;
using Nona.Domain.Entities;
using Nona.WebApi.Services;
using System.Security.Claims;

namespace Nona.Infrastructure.Tests;

public class CurrentUserServiceTests
{
    [Test]
    public async Task Role_ReadsExplicitAdminClaim()
    {
        var service = CreateService(new Claim(ClaimTypes.Role, "Admin"));

        await Assert.That(service.Role).IsEqualTo(UserRole.Admin);
    }

    [Test]
    public async Task Role_PromotesLegacyViewerAdminToken()
    {
        var service = CreateService(
            new Claim(ClaimTypes.Role, "Viewer"),
            new Claim("isAdmin", "true"));

        await Assert.That(service.Role).IsEqualTo(UserRole.Admin);
    }

    private static CurrentUserService CreateService(params Claim[] claims)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
        };

        return new CurrentUserService(new HttpContextAccessor { HttpContext = context });
    }
}
