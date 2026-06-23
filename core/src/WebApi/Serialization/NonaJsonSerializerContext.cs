using System.Text.Json.Serialization;
using Nona.Application.Admin.ApiKeys.Commands;
using Nona.Application.Admin.ApiKeys.DTOs;
using Nona.Application.Admin.AuditLogs.DTOs;
using Nona.Application.Admin.ConfigEntries.Commands;
using Nona.Application.Admin.ConfigEntries.DTOs;
using Nona.Application.Admin.Dashboard.DTOs;
using Nona.Application.Admin.Environments.Commands;
using Nona.Application.Admin.Environments.DTOs;
using Nona.Application.Admin.Projects.Commands;
using Nona.Application.Admin.Projects.DTOs;
using Nona.Application.Admin.Users.Commands;
using Nona.Application.Admin.Users.DTOs;
using Nona.Application.Auth.Commands;
using Nona.Application.Auth.DTOs;
using Nona.WebApi.Endpoints;

namespace Nona.WebApi.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(RegisterCommand))]
[JsonSerializable(typeof(RegisterResult))]
[JsonSerializable(typeof(RequestPasswordResetCommand))]
[JsonSerializable(typeof(SsoLoginRequest))]
[JsonSerializable(typeof(SsoPublicConfigResponse))]
[JsonSerializable(typeof(SsoProviderPublicConfig))]
[JsonSerializable(typeof(CompleteInvitationPasswordRequest))]
[JsonSerializable(typeof(InvitationDetailsResponse))]
[JsonSerializable(typeof(CreateProjectRequest))]
[JsonSerializable(typeof(ProjectDto))]
[JsonSerializable(typeof(IReadOnlyList<ProjectDto>))]
[JsonSerializable(typeof(CreateEnvironmentRequest))]
[JsonSerializable(typeof(EnvironmentDto))]
[JsonSerializable(typeof(IReadOnlyList<EnvironmentDto>))]
[JsonSerializable(typeof(CreateApiKeyRequest))]
[JsonSerializable(typeof(ApiKeyDto))]
[JsonSerializable(typeof(IReadOnlyList<ApiKeyDto>))]
[JsonSerializable(typeof(CreateUserRequest))]
[JsonSerializable(typeof(UpdateUserRequest))]
[JsonSerializable(typeof(ProjectAccessRequest))]
[JsonSerializable(typeof(CreateUserResponse))]
[JsonSerializable(typeof(ProjectAccessDto))]
[JsonSerializable(typeof(IReadOnlyList<ProjectAccessDto>))]
[JsonSerializable(typeof(UserDto))]
[JsonSerializable(typeof(IReadOnlyList<UserDto>))]
[JsonSerializable(typeof(UpsertConfigEntryRequest))]
[JsonSerializable(typeof(RollbackConfigEntryRequest))]
[JsonSerializable(typeof(ConfigEntryDto))]
[JsonSerializable(typeof(List<ConfigEntryDto>))]
[JsonSerializable(typeof(ConfigEntryVersionDto))]
[JsonSerializable(typeof(IReadOnlyList<ConfigEntryVersionDto>))]
[JsonSerializable(typeof(AuditLogDto))]
[JsonSerializable(typeof(IReadOnlyList<AuditLogDto>))]
[JsonSerializable(typeof(DashboardCountDto))]
internal sealed partial class NonaJsonSerializerContext : JsonSerializerContext;
