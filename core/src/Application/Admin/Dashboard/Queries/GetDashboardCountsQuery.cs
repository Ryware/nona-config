using Mediator;
using Nona.Application.Admin.Dashboard.DTOs;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Dashboard.Queries;

public record GetDashboardCountsQuery : IRequest<DashboardCountDto>;

internal class GetDashboardCountQueryHandler(IUserRepository userRepository, IProjectRepository projectRepository, IConfigEntryRepository configEntryRepository) : IRequestHandler<GetDashboardCountsQuery, DashboardCountDto>
{
    public async ValueTask<DashboardCountDto> Handle(GetDashboardCountsQuery request, CancellationToken cancellationToken)
    {
        var (userCount, projectCount, configEntryCount) = await GetCountsAsync(cancellationToken);

        return new DashboardCountDto
        {
            Users = userCount,
            Projects = projectCount,
            ConfigEntries = configEntryCount
        };
    }

    private async Task<(int userCount, int projectCount, int configEntryCount)> GetCountsAsync(CancellationToken cancellationToken)
    {
        var counts = await Task.WhenAll(
            userRepository.CountAsync(cancellationToken),
            projectRepository.CountAsync(cancellationToken),
            configEntryRepository.CountAsync(cancellationToken)
        );
        return (counts[0], counts[1], counts[2]);
    }
}
