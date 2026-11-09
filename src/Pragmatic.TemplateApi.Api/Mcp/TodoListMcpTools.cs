using System;
using System.ComponentModel;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using Pragmatic.CQRS;
using Pragmatic.TemplateApi.Core.Handlers;
using Pragmatic.TemplateApi.Instrumentation;

namespace Pragmatic.TemplateApi.Api.Mcp;

/// <summary>
/// MCP tools mirroring the Todo List REST endpoints.
/// The MCP SDK constructs this class from the request DI scope on each tool invocation;
/// <see cref="ClaimsPrincipal"/> parameters are bound by the SDK to the authenticated caller.
/// </summary>
public sealed class TodoListMcpTools
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IMediator _mediator;

    public TodoListMcpTools(IMediator mediator)
    {
        _mediator = mediator;
    }

    [Description("Lists all open todo items from the Todo List API.")]
    [McpServerTool(Name = "list_open_todos", ReadOnly = true, Idempotent = true)]
    public async Task<string> ListOpenTodos(IAuthorizationService authorization, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        await AuthorizeAsync(authorization, user, Roles.TodoListRead);

        var items = await _mediator.Send(new SelectTodo(), cancellationToken);
        return JsonSerializer.Serialize(items, JsonOptions);
    }

    [Description("Adds a new todo item with a title, an optional description and a due date (ISO-8601; stored as UTC).")]
    [McpServerTool(Name = "add_todo")]
    public async Task<string> AddTodo(
        string title,
        string? description,
        DateTime dueDate,
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        await AuthorizeAsync(authorization, user, Roles.TodoListWrite);

        var record = await _mediator.Send(new InsertTodo(title, description, dueDate), cancellationToken);
        return JsonSerializer.Serialize(record, JsonOptions);
    }

    // Tools invoke the MediatR pipeline directly, bypassing the controller-level [Authorize] policies,
    // so the same role policy must be enforced at the MCP boundary.
    private static async Task AuthorizeAsync(IAuthorizationService authorization, ClaimsPrincipal? user, string policy)
    {
        if (user is null || user.Identity?.IsAuthenticated != true)
            throw new UnauthorizedAccessException($"Caller is not authorized for the '{policy}' policy.");

        var result = await authorization.AuthorizeAsync(user, policy);

        if (!result.Succeeded)
            throw new UnauthorizedAccessException($"Caller is not authorized for the '{policy}' policy.");
    }
}
