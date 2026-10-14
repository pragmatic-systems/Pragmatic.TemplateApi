Feature: TodoApiFeatures

Feature test for the Todo list API.

@todo-list @api
Scenario: Create and View Todo item
	Given We have user 'Dave'
	And User 'Dave' has claims 'ReadWrite'
	When We are connecting as 'Dave'
	And We create task 'New Task'
	Then The response should be 200 OK
	And The response should contain a new RecordId
	When We get our TodoList
	Then The response should be 200 OK
	And The response should contain a Todo List
	Then The result contains the created recordId

@todo-list @api
Scenario: Restricted Write Access
	Given We have user 'James'
	And User 'James' has claims 'Read'
	When We are connecting as 'James'
	And We create task 'New Task'
	Then The response should be 403 Forbidden
	When We get our TodoList
	Then The response should be 200 OK
	And The response should contain a Todo List

@todo-list @api
Scenario: Restricted Anonymous Access
	When We are connecting anonymously
	And We create task 'New Task'
	Then The response should be 401 Unauthorized
	
@todo-list @api @upload
Scenario: Upload CSV and import todo items via background job
	Given We have user 'Alice'
	And User 'Alice' has claims 'ReadWrite'
	When We are connecting as 'Alice'
	And We upload a CSV file with three todo items
	Then The response should contain a blob name
	When We wait for the background job to complete
	And We get our TodoList
	Then The response should contain a Todo List
	And The response should contain at least 3 todo items

@todo-list @api @queue
Scenario: Insert Todo item via RabbitMQ queue
	Given We have user 'Dave'
	And User 'Dave' has claims 'Read'
	When We are connecting as 'Dave'
	And We publish a todo item to the queue
	And We wait for the queue consumer to insert the item
	And We get our TodoList
	Then The response should be 200 OK
	And The response should contain a Todo List
	And The result contains the published todo item
