using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using Pragmatic.CQRS;
using Pragmatic.TemplateApi.Core.Handlers;
using Pragmatic.TemplateApi.Database.Model;
using Pragmatic.TemplateApi.Instrumentation;

namespace Pragmatic.TemplateApi.Api.Mcp;

/// <summary>
/// MCP tools mirroring the Todo List REST endpoints.
/// The MCP SDK constructs this class from the request DI scope on each tool invocation;
/// <see cref="ClaimsPrincipal"/> parameters are bound by the SDK to the authenticated caller.
/// </summary>
public sealed class TodoListMcpTools
{
    private readonly IMediator _mediator;

    public TodoListMcpTools(IMediator mediator)
    {
        _mediator = mediator;
    }

    [Description("Lists all open todo items from the Todo List API.")]
    [McpServerTool(Name = "list_open_todos", ReadOnly = true, Idempotent = true)]
    public async Task<IEnumerable<TodoRecord>> ListOpenTodos(IAuthorizationService authorization, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        await AuthorizeAsync(authorization, user, Roles.TodoListRead);
        return await _mediator.Send(new SelectTodo(), cancellationToken);
    }

    [Description("Adds a new todo item with a title, an optional description and a due date (ISO-8601; stored as UTC).")]
    [McpServerTool(Name = "add_todo")]
    public async Task<TodoRecord> AddTodo(
        [Description("The todo item to add.")] InsertTodo insert,
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        await AuthorizeAsync(authorization, user, Roles.TodoListWrite);
        return await _mediator.Send(insert, cancellationToken);
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
