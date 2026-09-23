using Microsoft.EntityFrameworkCore;
using Pragmatic.CQRS;
using Pragmatic.TemplateApi.Database;
using Pragmatic.TemplateApi.Database.Model;

namespace Pragmatic.TemplateApi.Core.Handlers;

public class SelectTodoItemsHandler : IRequestHandler<SelectTodo, IEnumerable<TodoRecord>>
{
    private readonly ApplicationDbContext _dbContext;

    public SelectTodoItemsHandler(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<TodoRecord>> Handle(SelectTodo request, CancellationToken cancellationToken)
    {
        return await _dbContext
            .TodoRecords
            .Where(r => r.Open)
            .ToListAsync(cancellationToken);
    }
}

public record SelectTodo()
    : IRequest<IEnumerable<TodoRecord>>;
