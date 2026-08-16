# .NET 10 Single-Project Setup Guide

This repository now uses a **single ASP.NET Core 10 project** in `backend/` for both:

- MVC UI pages (frontend)
- Web API endpoints (backend)

There is no required separate frontend runtime for local development.

## Architecture Summary

| Area | Implementation |
|---|---|
| Runtime | ASP.NET Core 10 (`Microsoft.NET.Sdk.Web`) |
| UI | Razor Views + static assets (`Views/`, `wwwroot/`) |
| API | Controller-based Web API under `/api/*` |
| Database | SQL Server (default) or EF Core InMemory (optional) |
| ORM | Entity Framework Core 10 |
| Realtime | Native ASP.NET Core WebSockets middleware |
| Auth | JWT Bearer |

## Prerequisites

- .NET 10 SDK
- Optional: SQL Server 2022 / Azure SQL

## Environment Configuration

Set values in `backend/appsettings.Development.json` or environment variables.

### Required (SQL Server mode)

```bash
export ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=support_agent;User Id=sa;Password=Your_Secure_Password123!;Encrypt=False;TrustServerCertificate=True;"
export JWT_SECRET="replace-with-a-long-random-secret"
```

### Optional (InMemory mode)

```bash
export USE_INMEMORY_DB=true
export Database__InMemoryName=support_agent_dev
```

When InMemory mode is enabled, SQL Server is not required.

## Run Locally

```bash
cd backend
dotnet tool restore
dotnet ef database update
dotnet run
```

Default development URL is `http://localhost:8000`.

## Verify App Surface

- MVC UI: `http://localhost:8000/`
- Dashboard pages: `http://localhost:8000/Dashboard`
- API base: `http://localhost:8000/api/*`

## Build and Test

```bash
dotnet build backend/backend.csproj
dotnet test backend.Tests/backend.Tests.csproj
```

> `backend.Tests` is optional test coverage. The runtime app itself is the single project in `backend/`.

## Deploy

Publish only the backend web project:

```bash
dotnet publish backend/backend.csproj -c Release
```

Configure environment variables (`ConnectionStrings__DefaultConnection`, `JWT_SECRET`, AI provider keys) in your host.

## Current Project Layout

```text
ai-support-agent-dotnet/
├── backend/                  # Single ASP.NET Core MVC + Web API app
│   ├── Controllers/
│   ├── Data/
│   ├── Middleware/
│   ├── Models/
│   ├── Services/
│   ├── Views/
│   ├── wwwroot/
│   ├── Program.cs
│   └── backend.csproj
├── backend.Tests/            # Optional integration/unit tests
└── SETUP_DOTNET.md
```
