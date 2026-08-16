# AI Support Agent - Backend Instructions

## Scope
These instructions apply to everything under `backend/`.

## Project Mode
The application is a **single .NET 10 web project** (`backend/backend.csproj`) that serves:

- Backend APIs (Web API controllers under `/api/*`)
- Frontend UI (MVC/Razor views under `Views/` + static assets in `wwwroot/`)

There is no required separate frontend process for normal development.

## Architecture

- Framework: ASP.NET Core 10 MVC + Web API
- Data access: EF Core 10 via `ApplicationDbContext`
- Database providers:
  - SQL Server (default)
  - InMemory (optional for local/test)
- Realtime: `WebSocketChatMiddleware`
- Auth: JWT Bearer

## Folder Conventions

- `Controllers/`: HTTP endpoints and MVC actions
- `Services/`: business logic (DI-injected)
- `Models/`: EF entities + DTO-like models
- `Data/`: DbContext and EF setup
- `Middleware/`: request pipeline middleware
- `Views/`: Razor UI templates
- `wwwroot/`: frontend static files

## Coding Rules

- Use async/await for all I/O (database, HTTP, file I/O).
- Keep business logic in services, not controllers.
- Return consistent API payloads: `{ success, data, error }` where applicable.
- Validate input before executing service calls.
- Use DI for external dependencies and app services.
- Keep changes focused; avoid unrelated refactors.
- Do not hardcode secrets; use config/environment variables.

## Data Rules

- Every entity should keep UTC timestamps (`CreatedAt`, `UpdatedAt`) when applicable.
- Keep team isolation intact (`TeamId` filtering on team-owned data).
- Prefer LINQ with EF Core over raw SQL.

## Runtime & Build

From repository root:

```bash
dotnet build backend/backend.csproj
dotnet run --project backend/backend.csproj
```

Optional tests:

```bash
dotnet test backend.Tests/backend.Tests.csproj
```

## Local URL Surface

- MVC home: `http://localhost:8000/`
- Dashboard UI: `http://localhost:8000/Dashboard`
- API endpoints: `http://localhost:8000/api/*`

## Memory Workflow

- At task start: read `MEMORY.md` and `backend/MEMORY.md`.
- At task end: append relevant implementation outcomes to both files.
