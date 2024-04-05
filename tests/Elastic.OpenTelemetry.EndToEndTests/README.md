# E2E Tests: Elastic's .NET OpenTelemetry Distribution 


## Target Environment

Requires an already running serverless observability project on cloud. 

The configuration can be provided either as asp.net secrets or environment variables.

```bash
dotnet user-secrets set "E2E:Endpoint" "<url>" --project tests/Elastic.OpenTelemetry.EndToEndTests
dotnet user-secrets set "E2E:Authorization" "<header>" --project tests/Elastic.OpenTelemetry.EndToEndTests
```

The equivalent environment variables are `E2E__ENDPOINT` and `E2E__AUTHORIZATION`. For local development setting 
secrets is preferred.

This ensures the instrumented applications will send OTLP data.

## Browser authentication

The tests require a headless browser to login. This requires a non OAuth login to be setup on your serverless 
observability project.

To do this is to invite an email address you own to your organization:

https://cloud.elastic.co/account/members

This user only needs instance access to the `Target Environment`. 

**NOTE:** since you can only be part of a single organization on cloud be sure that the organization you are part of is 
not used for any production usecases and you have clearance from the organization owner. 

By default accounts on cloud are part of their own personal organization.

Once invited and accepted the invited email can be used to login during the automated tests.

These can be provided again as user secrets:

```bash
dotnet user-secrets set "E2E:BrowserEmail" "<email>" --project tests/Elastic.OpenTelemetry.EndToEndTests
dotnet user-secrets set "E2E:BrowserPassword" "<password>" --project tests/Elastic.OpenTelemetry.EndToEndTests
```

or environment variables (`E2E__BROWSEREMAIL` and `E2E__BROWSERPASSWORD`).
