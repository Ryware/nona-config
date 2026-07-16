using FluentValidation;
using Mediator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nona.Application.Admin.ApiKeys.Commands;
using Nona.Application.Admin.ConfigReleases.Commands;
using Nona.Application.Admin.ConfigEntries.Commands;
using Nona.Application.Admin.Environments.Commands;
using Nona.Application.Admin.Projects.Commands;
using Nona.Application.Admin.Users.Commands;
using Nona.Application.Auth.Commands;
using Nona.Application.Auth.DTOs;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;
using NSubstitute;

namespace Nona.Application.Tests;

public class ApplicationServiceRegistrationTests
{
    [Test]
    public async Task AddApplicationServices_RegistersValidators()
    {
        var services = new ServiceCollection();
        services.AddApplicationServices(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();

        await Assert.That(provider.GetRequiredService<IValidator<CreateApiKeyRequest>>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<IValidator<PublishConfigReleaseRequest>>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<IValidator<SetActiveConfigReleaseRequest>>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<IValidator<UpsertConfigEntryRequest>>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<IValidator<CreateEnvironmentRequest>>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<IValidator<CreateProjectRequest>>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<IValidator<CreateUserRequest>>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<IValidator<UpdateUserRequest>>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<IValidator<ProjectAccessRequest>>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<IValidator<CompleteInvitationPasswordRequest>>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<IValidator<LoginRequest>>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<IValidator<RequestPasswordResetCommand>>()).IsNotNull();
    }

    [Test]
    public async Task MediatorPipeline_ValidatesRequestMessages()
    {
        var services = new ServiceCollection();
        var userRepository = Substitute.For<IUserRepository>();

        services.AddApplicationServices(new ConfigurationBuilder().Build());
        services.AddSingleton(userRepository);
        services.AddSingleton(Substitute.For<IDateTime>());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        Exception? exception = null;
        try
        {
            await mediator.Send(new RequestPasswordResetCommand("not-an-email"));
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception).IsTypeOf<ValidationException>();
        await userRepository.DidNotReceive()
            .GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
