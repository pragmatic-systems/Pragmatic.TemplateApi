Feature: McpApiFeatures

Feature test for the Todo list MCP server endpoint.

@mcp @api
Scenario: MCP handshake and tool discovery
	Given We have user 'Dave'
	And User 'Dave' has claims 'ReadWrite'
	When We are connecting as 'Dave'
	And We send the MCP initialize request
	Then The MCP response should be an initialize result
	And The MCP server info should be 'Pragmatic.TemplateApi'
	When We send the MCP initialized notification
	Then The MCP notification should be accepted
	When We list the MCP tools
	Then The MCP tools should include 'list_open_todos'
	And The MCP tools should include 'add_todo'

@mcp @api
Scenario: Add and list todo items via the MCP tools
	Given We have user 'Dave'
	And User 'Dave' has claims 'ReadWrite'
	When We are connecting as 'Dave'
	And We send the MCP initialize request
	And We send the MCP initialized notification
	When We call the MCP tool 'add_todo' with title 'MCP Created Task' and due date '2026-03-01'
	Then The MCP result should contain a todo item titled 'MCP Created Task'
	When We call the MCP tool 'list_open_todos' without arguments
	Then The MCP result should be a list containing a todo item titled 'MCP Created Task'

@mcp @api
Scenario: MCP add_todo is rejected for a read-only user
	Given We have user 'James'
	And User 'James' has claims 'Read'
	When We are connecting as 'James'
	And We send the MCP initialize request
	And We send the MCP initialized notification
	When We call the MCP tool 'add_todo' with title 'Should Not Be Created' and due date '2026-03-01'
	Then The MCP call should be rejected
	When We call the MCP tool 'list_open_todos' without arguments
	Then The MCP result should not contain a todo item titled 'Should Not Be Created'

@mcp @api
Scenario: MCP is rejected for anonymous callers
	When We are connecting anonymously
	And We send the MCP initialize request
	Then The MCP response should be 401 Unauthorized
