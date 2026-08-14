# .NET 10.0 MVC AI Support Agent Setup and Migration Guide

This document describes the complete architecture rewrite of the AI Support Agent backend from **Python Django** to **ASP.NET Core 10.0 MVC (C#)**, utilizing **SQL Server** for persistence and vector searches, and supporting **Azure AI Foundry** for hosting Claude (Haiku & Sonnet) models.

---

## 🏗️ Architecture Migration Overview

### 1. Technology Shift

| Component | Original Tech Stack | New C# Tech Stack |
|-----------|----------------------|-------------------|
| **Backend Framework** | Django + DRF + Django Channels | **ASP.NET Core 10.0 MVC / Web API** |
| **Database** | PostgreSQL + pgvector | **SQL Server 2022 / Azure SQL** |
| **AI Integration** | Anthropic Python SDK | **Azure AI Foundry Chat Completions / REST SDK** |
| **Embeddings** | OpenAI text-embedding-3-small | **OpenAI text-embedding-3-small (with pseudo fallback)** |
| **WebSockets** | Daphne + Redis Channel Layer | **Native ASP.NET Core WebSockets Middleware** |
| **Docker Base** | `python:3.12-slim` | `mcr.microsoft.com/dotnet/aspnet:10.0` |

### 2. Database & Vector Searches

In the Python Django stack, semantic RAG search was performed inside PostgreSQL using the `pgvector` extension.

In our **ASP.NET Core + SQL Server** stack:
- Embeddings are 1536-dimensional vectors of floats returned by OpenAI's `text-embedding-3-small`.
- These are stored inside SQL Server in a dedicated `nvarchar(max)` column (`embedding_json`) as a serialized JSON float array.
- For maximum cross-database compatibility (supporting Azure SQL, SQL Server 2022, localdb, SQL Express, and containerized versions), the vector similarity search is performed using an extremely fast **in-memory Cosine Similarity** calculation in C#:
  - When a search is triggered, relevant knowledge base candidates are retrieved and filtered by category.
  - C# computes the **Dot Product** (which is identical to Cosine Similarity since OpenAI's embeddings are pre-normalized to unit length).
  - This is extremely high performance (running cosine similarity calculations across thousands of candidates takes less than 1ms in modern C#).

---

## 🛠️ Setup & Installation

### 1. Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- [Docker & Docker Compose](https://www.docker.com/products/docker-desktop/)
- [Node.js 18+](https://nodejs.org/) (for running the dashboard frontend)

### 2. Environment Variables (`.env`)

Configure your `.env` file in the project root with the following keys. Both direct Anthropic keys and Azure AI Foundry endpoints are supported:

```env
# Database Connection
# If running locally without docker:
DATABASE_URL=Server=localhost,1433;Database=support_agent;User Id=sa;Password=Your_Secure_Password123!;Encrypt=False;TrustServerCertificate=True;

# Azure AI Foundry Claude Configuration (Recommended)
AZURE_AI_FOUNDRY_HAIKU_URL=https://<your-haiku-deployment>.<region>.models.ai.azure.com/v1/chat/completions
AZURE_AI_FOUNDRY_HAIKU_KEY=your-azure-haiku-deployment-key
AZURE_AI_FOUNDRY_SONNET_URL=https://<your-sonnet-deployment>.<region>.models.ai.azure.com/v1/chat/completions
AZURE_AI_FOUNDRY_SONNET_KEY=your-azure-sonnet-deployment-key

# Direct Anthropic / Claude Fallback (Optional — will be used if Azure config is empty)
ANTHROPIC_API_KEY=sk-ant-your-direct-key-here

# Embeddings API Key (Falls back to deterministic SHA-512 pseudo-embeddings if blank)
OPENAI_API_KEY=sk-your-openai-key-here

# JWT Configuration
JWT_SECRET=super-secret-temporary-key-that-must-be-long-enough-for-hs256
```

---

## 🚀 Running the App

### Option A: Running with Docker Compose (Recommended)

One single command spins up SQL Server 2022, Redis, the compiled .NET 10 MVC backend, and the Next.js frontend:

```bash
docker compose up -d --build
```

#### Services started:
- **db**: SQL Server on `localhost:1433`
- **redis**: Redis cache on `localhost:6379`
- **backend**: .NET 10 App Service on `http://localhost:8000`
- **frontend**: Next.js 15 Agent Dashboard on `http://localhost:3000`

---

### Option B: Running Locally (For Development)

#### 1. Spin up SQL Server & Redis in Docker
```bash
docker compose up db redis -d
```

#### 2. Start the .NET 10 Backend
```bash
cd backend
dotnet run
```
The backend automatically creates the SQL Server database schema on startup and seeds **10 standard sample FAQ Knowledge Base entries** (no manual migration scripts are required!). It will listen on `http://localhost:8000`.

#### 3. Start the Next.js Dashboard
```bash
cd dashboard
npm install
npm run dev
```
Open [http://localhost:3000](http://localhost:3000) to view the Landing Page and Login!

---

## 🌐 Deploying to Azure App Service

Since the backend is written using standard ASP.NET Core, it runs natively on **Azure App Service**:

1. Create a Linux or Windows App Service in the Azure Portal selecting **.NET 10 (LTS)** as the runtime stack.
2. In **Settings > Configuration**, add your environment variables (`AZURE_AI_FOUNDRY_HAIKU_URL`, `AZURE_AI_FOUNDRY_SONNET_URL`, `DATABASE_URL`, `JWT_SECRET`, etc.) as Application Settings.
3. Publish using Visual Studio, Azure CLI, or GitHub Actions:
   ```bash
   dotnet publish backend/backend.csproj -c Release
   ```
4. Set up an **Azure SQL Database** and paste its connection string into `DATABASE_URL` or `ConnectionStrings__DefaultConnection` (Entity Framework will automatically deploy the schema on startup!).

---

## 📁 Project Structure

```
ai-support-agent/
├── AISupportAgent.sln       # Root .NET solution file
├── backend/                 # .NET 10 MVC Backend
│   ├── Controllers/         # Auth, Team, Conversation, KB, Webhook, and View controllers
│   ├── Data/                # EF Core ApplicationDbContext
│   ├── Middleware/          # Raw WebSocket Chat Middleware
│   ├── Models/              # C# EF Domain Entities
│   ├── Services/            # AI services (Classifier, Responder, Embedding, Guardrails, Conversation)
│   ├── Program.cs           # DI containers and middleware registration
│   └── backend.csproj       # Project dependencies
├── backend.Tests/           # xUnit Unit Test Suite
├── dashboard/               # Next.js 15 frontend agent dashboard
├── templates/               # Meta verification static HTML files (privacy/terms)
└── docker-compose.yml       # SQL Server, Redis, Backend & Frontend orchestrator
```
