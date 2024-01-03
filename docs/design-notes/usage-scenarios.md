# Usage Scenarios

This document lists usage scenarios that consumers may have. Initially these will be manually verified, but 
we should ensure that we introduce unit/integration tests before GA.

## Scenario 1

A consumer wants to collect trace, metrics and/or logs and export them to Elastic APM with limited effort. They have 
no specific requirements besides adding a custom `ActivitySource` name for their application.

