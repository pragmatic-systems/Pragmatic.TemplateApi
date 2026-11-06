Feature: HangfireDashboard

End-to-end test for Hangfire dashboard cookie-based authentication flow.

@hangfire @api
Scenario: Login with bearer token, access dashboard with cookie, then logout
	Given We have user 'HangfireAdmin' with claims 'HangfireDashboard'
	When We are connecting as 'HangfireAdmin'
	And We call hangfire login with bearer token
	Then The response should redirect to hangfire dashboard
	And The hangfire cookie should be present
	When We call hangfire dashboard with cookie
	Then The response should be 200 OK
	When We call hangfire logout
	Then The response should be 200 OK
	And The hangfire cookie should be removed
