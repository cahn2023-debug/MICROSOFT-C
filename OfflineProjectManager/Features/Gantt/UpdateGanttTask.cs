using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OfflineProjectManager.Data;
using OfflineProjectManager.Services;

namespace OfflineProjectManager.Features.Gantt
{
    public record UpdateGanttTaskDatesCommand(int TaskId, DateTime StartDate, DateTime EndDate) : IRequest<bool>;

    public class UpdateGanttTaskDatesHandler : IRequestHandler<UpdateGanttTaskDatesCommand, bool>
    {
        private readonly DbContextPool _dbContextPool;
        private readonly ITaskService _taskService;

        public UpdateGanttTaskDatesHandler(DbContextPool dbContextPool, ITaskService taskService)
        {
            _dbContextPool = dbContextPool;
            _taskService = taskService;
        }

        public async Task<bool> Handle(UpdateGanttTaskDatesCommand request, CancellationToken cancellationToken)
        {
            using (var pooledCtx = await _dbContextPool.GetContextAsync())
            {
                var task = await pooledCtx.Context.Tasks.FindAsync(request.TaskId);
                if (task == null) return false;

                task.StartDate = request.StartDate;
                task.EndDate = request.EndDate;
                task.UpdatedAt = DateTime.UtcNow;

                await pooledCtx.Context.SaveChangesAsync(cancellationToken);
                _taskService.NotifyTasksChanged();
                return true;
            }
        }
    }
}
