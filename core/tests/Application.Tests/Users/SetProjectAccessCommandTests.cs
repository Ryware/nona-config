using Nona.Application.Admin.Users.Commands;
using Nona.Application.Admin.Users.Validators;
using Nona.Application.Tests.Common;
using Nona.Domain.Entities;
using NSubstitute;

namespace Nona.Application.Tests.Users;

public class SetProjectAccessCommandTests
{
    private const long UserId = 42;
    private const string Email = "user@example.com";
    private const string ProjectName = "alpha";

    [Test]
    public async Task SetProjectAccess_AllowsEditorRole()
    {
        var fixture = new TestFixture();
        fixture.UserRepository.GetByIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new User
            {
                Id = UserId,
                Email = Email,
                Name = "User"
            });
        fixture.ProjectRepository.ExistsAsync(ProjectName, Arg.Any<CancellationToken>()).Returns(true);

        var handler = new SetProjectAccessCommandHandler(
            fixture.UserRepository,
            fixture.ProjectRepository,
            fixture.ProjectMemberRepository);

        var result = await handler.Handle(
            new SetProjectAccessCommand(UserId, ProjectName, "editor"),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ProjectAccess).IsNotNull();
        await Assert.That(result.ProjectAccess!.ProjectName).IsEqualTo(ProjectName);
        await Assert.That(result.ProjectAccess.Role).IsEqualTo("editor");
        await fixture.ProjectMemberRepository.Received(1).AddAsync(
            Arg.Is<ProjectMember>(member =>
                member.Username == Email &&
                member.ProjectId == ProjectName &&
                member.Role == ProjectRole.Editor),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Validator_AcceptsViewerAndEditorRoles()
    {
        var validator = new ProjectAccessRequestValidator();

        var viewer = await validator.ValidateAsync(new ProjectAccessRequest("viewer"));
        var editor = await validator.ValidateAsync(new ProjectAccessRequest("editor"));

        await Assert.That(viewer.IsValid).IsTrue();
        await Assert.That(editor.IsValid).IsTrue();
    }

    [Test]
    public async Task Validator_RejectsAdminRole()
    {
        var validator = new ProjectAccessRequestValidator();

        var result = await validator.ValidateAsync(new ProjectAccessRequest("admin"));

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Select(error => error.ErrorMessage)).Contains("Role must be 'viewer' or 'editor'");
    }
}
