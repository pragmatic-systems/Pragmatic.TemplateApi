# Pragmatic .NET API Template Header
This project is a template published by https://github.com/pragmatic-systems

To Install: 
* Clone Repo to local folder, eg `c:\git\Pragmatic.TemplateApi` (Windows) or `~/repos/Pragmatic.TemplateApi` (Linux/macOS)
* Run `dotnet new install <path-to-cloned-repo>`

To Create a project: 
* Navigate to your new empty project directory.
* Run `dotnet new Pragmatic.TemplateApi --ProjectName:MyAppName`

## Status

[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=pragmatic-systems_Pragsys.Template.Api&metric=alert_status&token=6047a2bbb68224d6ef02044a0b82b9aae2f68067)](https://sonarcloud.io/summary/new_code?id=pragmatic-systems_Pragsys.Template.Api)
[![Bugs](https://sonarcloud.io/api/project_badges/measure?project=pragmatic-systems_Pragsys.Template.Api&metric=bugs)](https://sonarcloud.io/summary/new_code?id=pragmatic-systems_Pragsys.Template.Api)
[![Code Smells](https://sonarcloud.io/api/project_badges/measure?project=pragmatic-systems_Pragsys.Template.Api&metric=code_smells)](https://sonarcloud.io/summary/new_code?id=pragmatic-systems_Pragsys.Template.Api)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=pragmatic-systems_Pragsys.Template.Api&metric=coverage&token=6047a2bbb68224d6ef02044a0b82b9aae2f68067)](https://sonarcloud.io/summary/new_code?id=pragmatic-systems_Pragsys.Template.Api)

[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=pragmatic-systems_Pragsys.Template.Api&metric=security_rating&token=6047a2bbb68224d6ef02044a0b82b9aae2f68067)](https://sonarcloud.io/summary/new_code?id=pragmatic-systems_Pragsys.Template.Api)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=pragmatic-systems_Pragsys.Template.Api&metric=vulnerabilities&token=6047a2bbb68224d6ef02044a0b82b9aae2f68067)](https://sonarcloud.io/summary/new_code?id=pragmatic-systems_Pragsys.Template.Api)
[![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=pragmatic-systems_Pragsys.Template.Api&metric=reliability_rating&token=6047a2bbb68224d6ef02044a0b82b9aae2f68067)](https://sonarcloud.io/summary/new_code?id=pragmatic-systems_Pragsys.Template.Api)
[![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=pragmatic-systems_Pragsys.Template.Api&metric=sqale_rating&token=6047a2bbb68224d6ef02044a0b82b9aae2f68067)](https://sonarcloud.io/summary/new_code?id=pragmatic-systems_Pragsys.Template.Api)

## End Of Template Project Header

## Table of Contents

- [Overview](#overview)
  - [Features](#features)
  - [Prerequisites](#prerequisites)
  - [To Run](#to-run)
- [Formatting](#formatting)
- [Local Services](#local-services)
  - [Seq](#seq)
  - [Postgres (+ PgAdmin)](#postgres--pgadmin)
  - [Keycloak](#keycloak)
  - [Database Migrations](#database-migrations)
  - [Authentication](#authentication)
- [Local Keycloak Hosting](#local-keycloak-hosting)
  - [Keycloak SSL Setup](#keycloak-ssl-setup)
  - [Keycloak (pre-configured)](#keycloak-pre-configured)
  - [Keycloak – Generate JWT](#keycloak--generate-jwt)
  - [Keycloak – Validate JWT](#keycloak--validate-jwt)
- [Azure Entra ID Config](#azure-entra-id-config)
  - [Entra ID – Generate JWT](#entra-id--generate-jwt)
  - [Entra ID – Validate JWT](#entra-id--validate-jwt)
- [AWS Cognito Config](#aws-cognito-config)
  - [Cognito – Generate JWT](#cognito--generate-jwt)
  - [Cognito – Validate JWT](#cognito--validate-jwt)
- [Docker Pack and Push](#docker-pack-and-push)

## Overview

This solution contains a simple Todo List API with background Workerhost, a postgres DB, and a DB up project to handle DB migrations.

### Features
* OpenIdConnect support for JWT based auth and RBAC.
* `/_system/Ping`, `/_system/Health`, and `/_system/Metrics` endpoints for service observability.
* Database migration project to managed postgres migrations.
* Unit Tests, Integration Tests and Benchmarks with packaged reports.
* Test Server / Test Container based test suite with Specflow.

### Prerequisites

| Requirement | Version | Notes |
|-------------|---------|-------|
| **.NET SDK** | 8.0.401+ (rollForward: latestMinor) | [Download](https://dotnet.microsoft.com/download) |
| **Docker Desktop** | Latest stable | Required for `docker compose` and all local services |
| **Git** | Latest | Required for cloning and versioning |
| **Cake CLI** | 4.x | `dotnet tool install --global Cake.Tool` — used for the build pipeline |

### To Run

Run `docker compose -f docker-compose.infra.yml up` 
Launch apps:
* Pragmatic.TemplateApi.API

## Formatting
The project needs to be correctly formatted to pass the build. To format the project using the dotnet format tool - 

Run `dotnet format`

Note that not all errors are auto resolvable and some require manual fixing.

## Local Services

### Seq
Url: https://localhost:5030

### Postgres (+ PgAdmin)
Adminer: https://localhost:5434
Username: user@local.com
Password: password

Postgres: https://localhost:5432
Username: pgadmin
Password: password

### Keycloak
Url: https://localhost:8443
Username: admin
Password: password

Realm `todolist-realm` is pre-configured on startup (see [Keycloak (pre-configured)](#keycloak-pre-configured)).

### Database Migrations
Currently we run migrations on app-start, this simplifies startup and development, but for more mature projects we can separate the launch application and run this prior to deploying a cluster.

### Authentication
If you have an existing AWS Cognito or Azure Entra setup, you can skip local keycloak setup and register the application with them.

## Local Keycloak Hosting

The local Keycloak container ships with a pre-built realm, so no manual Keycloak configuration is required for the default local development flow.

### Keycloak SSL Setup

For local keycloak to work properly as an OIDC server with another application, it needs to be running HTTPS with a valid, trusted certificate, otherwise you will receive an SSL error at runtime. To support this, you will need to generate a certificate for localhost and add it to trusted root, and load this certificate into Keycloak.

See the SSL Setup guide for local Keycloak in this repo: https://github.com/pragmatic-systems/Pragmatic.DockerTools

### Keycloak Configuration

The `local-keycloak` container starts with a pre-built realm.
The realm definition lives in [`keycloak/todolist-realm.json`](keycloak/todolist-realm.json) and is imported on startup (`start-dev --import-realm`):

| What | Value |
|------|-------|
| Realm | `todolist-realm` |
| Client | `todolist-client` (confidential, `Direct access grants`, client authorization) |
| Client secret | `todolist-client-secret` |
| User | `todolist-user` |
| User password | `password` |
| User roles | `TodoList:Read`, `TodoList:Write`, `Hangfire:Dashboard` (via the user `roles` attribute + `roles-mapper` on `todolist-client-dedicated`) |


### Keycloak – Generate JWT

Post: https://localhost:8443/realms/todolist-realm/protocol/openid-connect/token

With URL form:
grant_type: password
client_id: todolist-client
client_secret: todolist-client-secret
username: todolist-user
password: password

### Keycloak – Validate JWT

For the configuration in your dotnet application, you will need:

Issuer: `https://localhost:8443/realms/todolist-realm`
Audience: `todolist-client`

## Azure Entra ID Config

* Go to **[Entra ID](https://entra.microsoft.com/)** → **App Registrations** → Register your app. Note the **`ApplicationId`** / **`ClientId`**.
* Go to **Authentication** → Enable **`Access tokens`** and **`ID tokens`**.
* Go to **Certificates and Secrets** → Create a **client secret** and record the value.
* Go to **Token Configuration** → Add optional claims for **`Access Token`** → select `acct`, `acrs`, `aud`, `email`, `family_name`, `given_name`.
* Go to **Expose an API** → Set your **`Application ID URI`** (e.g. `api://todolist`).
* **Add a scope** to create the permission scopes.
* **Add a Client Application** → This is the app consuming your API. For the demo, use the AppId you just created.
* Go to **App roles** → Create roles for your application: `TodoList:Read`, `TodoList:Write`, and `Hangfire:Dashboard`.
* Go to **Owners** → Add your root account and any relevant management accounts.
* Go to **API Permissions** → **Add a permission** → **My APIs** → Choose your API → **Application Permissions**.
* Select **`TodoList:Read`**, **`TodoList:Write`**, and **`Hangfire:Dashboard`**.
* Click **Grant Admin Consent** → This ensures the roles appear in the JWT.

### Entra ID – Generate JWT

Post: https://login.microsoftonline.com/{tenant-id}/oauth2/v2.0/token

With URL form:
grant_type: client_credentials
client_id: {your-application-client-id}
client_secret: {your-client-secret}
scope: api://{your-application-client-id}/.default

### Entra ID – Validate JWT

For the configuration in your dotnet application, you will need:

Issuer: `https://login.microsoftonline.com/{tenant-id}/v2.0`
Audience: `api://todolist`

## AWS Cognito Config

> ⚠️ **Disclaimer:** Not production-ready. This configuration bypasses standard security practices for demonstration purposes only.

* Go to **[AWS Cognito](https://aws.amazon.com/cognito/)** → **Manage User Pools** → Select your pool.
* Go to **App Integration** → **App clients** → Select your client (or create one).
* Enable **"Generate client secret"**.
* Go to **Authorization flows** → Check **"Access token – client credentials"**.
* Go to **Token settings** → Ensure **Access Token** is enabled.
* **Demo Hack (Lambda):** Create a **Pre Token Generation** Lambda trigger. Configure it to always inject `{"TodoList:Read": ["true"], "TodoList:Write": ["true"], "Hangfire:Dashboard": ["true"]}` into `claimsToAddOrOverride`, ignoring all user/group context.
* Attach the Lambda to your User Pool under **Triggers** → **Pre Token Generation**.
* Test by requesting a token via **Client Credentials** flow and decoding the JWT to verify the custom claims are present.

### Cognito – Generate JWT

Post: https://{your-region}.auth.cognito-idp.amazonaws.com/{user-pool-id}/oauth2/token

With URL form:
grant_type: client_credentials
client_id: {your-cognito-client-id}
client_secret: {your-cognito-client-secret}

### Cognito – Validate JWT

For the configuration in your dotnet application, you will need:

Issuer: `https://cognito-idp.{your-region}.amazonaws.com/{user-pool-id}`
Audience: `{your-cognito-client-id}`

## Docker Pack and Push

`dotnet cake --Target=DockerPackAndPush --ContainerRegistry={container-registry} --ContainerRegistryToken={token} --ContainerRegistryUserName={user}`