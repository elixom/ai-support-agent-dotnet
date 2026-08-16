# Project Memory

## 2026-08-16
- Reviewed `.NET backend` + `Next.js dashboard` integration paths.
- Added EF Core migration tooling and initial SQL Server migration under `backend/Migrations`.
- Switched backend startup from `EnsureCreated` to `Database.MigrateAsync()` and aligned local dev defaults to `localhost:1433`.
- Set backend dev launch URL to `http://localhost:8000` for dashboard compatibility.
- Enabled static file hosting in ASP.NET pipeline and default MVC route mapping.
- Added missing KB delete API (`DELETE /api/knowledge/{id}`) and `source_file` projection for dashboard compatibility.
- Validated backend build/tests and dashboard production build successfully.
- Only interested in .NET project in the `backend/` folder

- Added optional EF Core InMemory database mode for backend runtime (`Database:UseInMemory` / `USE_INMEMORY_DB`).
- Added `appsettings.Testing.json` defaults for isolated in-memory test runs.
- Updated backend initialization flow to use `EnsureCreatedAsync()` for non-relational providers and `MigrateAsync()` for relational providers.
- Added backend integration testing infrastructure using `WebApplicationFactory<Program>` with forced InMemory DB and test auth scheme.
- Added `KnowledgeBaseIntegrationTests` to verify authenticated `/api/knowledge` works against seeded InMemory data.
- Added integration test coverage for `DELETE /api/knowledge/{id}` including second-delete `404` behavior.

- Consolidated the .NET app direction around a single runtime project in `backend/` (MVC + Web API in one host).
- Updated root solution definition (`AISupportAgent.slnx`) to include only `backend/backend.csproj`.
- Updated backend OAuth callback redirect to use same-host MVC dashboard route (`/Dashboard/Settings`) instead of external frontend URL.
- Refreshed setup/instruction docs (`SETUP_DOTNET.md`, `backend/AGENTS.md`) to match single-project full-stack architecture.
