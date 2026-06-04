using Nona.Application.Auth.DTOs;

namespace Nona.Application.Common.Interfaces;

public interface ISsoPublicConfigurationProvider
{
    SsoPublicConfigResponse GetConfiguration();
}
