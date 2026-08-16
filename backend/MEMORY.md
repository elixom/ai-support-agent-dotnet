# Backend Memory

## 2026-08-16
- Introduced SQL Server EF migration baseline:
  - Added local tool manifest `backend/dotnet-tools.json` with `dotnet-ef`.
  - Generated initial migration: `backend/Migrations/20260816153331_InitialSqlServer.cs`.
- Updated runtime bootstrap in `backend/Program.cs`:
  - `Database.MigrateAsync()` replaces `EnsureCreated()`.
  - Default fallback connection points to `localhost,1433` for non-docker dev.
  - Added `UseStaticFiles()` + `MapDefaultControllerRoute()`.
- Added design-time context factory: `backend/Data/ApplicationDbContextFactory.cs`.
- Added `Microsoft.EntityFrameworkCore.Tools` package to `backend/backend.csproj`.
- Added `ConnectionStrings:DefaultConnection` + `Jwt:SecretKey` in development config.
- Added missing endpoint used by frontend: `DELETE /api/knowledge/{id}` in `KnowledgeBaseController`.
- Added toggleable InMemory provider support in `backend/Program.cs`:
  - Config keys: `Database:UseInMemory`, `Database:Provider=InMemory`, `Database:InMemoryName`.
  - Env override: `USE_INMEMORY_DB=true`.
- Updated startup DB initialization:
  - Relational providers: `Database.MigrateAsync()`.
  - InMemory/non-relational providers: `Database.EnsureCreatedAsync()`.
- Added package reference `Microsoft.EntityFrameworkCore.InMemory` to `backend/backend.csproj`.
- Added `backend/appsettings.Testing.json` with InMemory enabled by default.
- Added integration test harness in `backend.Tests`:
  - `TestWebApplicationFactory` sets environment to `Testing`, enables `Database:UseInMemory=true`, and isolates DB name per run.
  - `TestAuthHandler` provides authenticated principal for `[Authorize]` endpoints.
  - `KnowledgeBaseIntegrationTests` validates `GET /api/knowledge` succeeds and returns seeded InMemory rows.
- Added `Microsoft.AspNetCore.Mvc.Testing` package to `backend.Tests/backend.Tests.csproj`.
- Added `public partial class Program { }` at end of `backend/Program.cs` to support host bootstrapping in integration tests.
