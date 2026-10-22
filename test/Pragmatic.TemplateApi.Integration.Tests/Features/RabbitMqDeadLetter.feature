Feature: RabbitMqDeadLetter

Dead letter and re-drive behaviour for the RabbitMQ todo message consumer.

@todo-list @queue @dead-letter
Scenario: Failed queue message is dead lettered
	Given We have user 'Dave'
	And User 'Dave' has claims 'Read'
	When We are connecting as 'Dave'
	And We publish a poison todo item to the queue
	Then The dead letter queue should contain the poison message
	When We get our TodoList
	Then The response should be 200 OK
	And The response should contain a Todo List
	And The result does not contain the poison todo item