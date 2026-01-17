using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfflineProjectManager.Data;
using OfflineProjectManager.Models;
using OfflineProjectManager.Services;

namespace OfflineProjectManager.Features.Gantt
{
    public record GetGanttDataQuery() : IRequest<List<ProjectTask>>;

    public class GetGanttDataHandler(DbContextPool dbContextPool, IProjectService projectService) : IRequestHandler<GetGanttDataQuery, List<ProjectTask>>
    {
        public async Task<List<ProjectTask>> Handle(GetGanttDataQuery request, CancellationToken cancellationToken)
        {
            if (!projectService.IsProjectOpen) return [];

            using var pooledCtx = await dbContextPool.GetContextAsync(cancellationToken);
            return await pooledCtx.Context.Tasks
                .AsNoTracking()
                .Where(t => t.ProjectId == projectService.CurrentProject.Id)
                .OrderBy(t => t.StartDate ?? System.DateTime.MaxValue)
                .ToListAsync(cancellationToken);
        }
    }
}
