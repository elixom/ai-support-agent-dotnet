# AI Support Agent — .NET C# Backend Instructions

## Project Overview
This is the **backend** for a production-ready AI customer support agent handling **WhatsApp, Email (Gmail), and Web Chat**. It's the .NET/C# port of the original Python/Django system, designed for CodeWithMuh YouTube tutorial series on replacing expensive SaaS tools ($990-$1500/mo) with a self-hosted solution (~$85/mo).

**Core Principle:** 80/20 hybrid approach — AI handles 80% of routine tickets, humans handle the complex 20%. This is NOT a full replacement; it's a **force multiplier**.

---

## Tech Stack (C# / .NET 10 Version)
- **Framework:** ASP.NET Core 10.0 MVC / Web API
- **Database:** SQL Server 2022 / Azure SQL (vector embeddings as JSON in nvarchar(max))
- **AI API:** 
  - **Primary:** Azure AI Foundry Chat Completions (Claude Haiku & Sonnet)
  - **Fallback:** Direct Anthropic API
- **Embeddings:** OpenAI `text-embedding-3-small` (1536-dim, with SHA-512 pseudo-embedding fallback)
- **Vector Search:** In-memory Cosine Similarity (C# calculation, cross-database compatible)
- **Multi-Channel Integration:**
  - WhatsApp Business Cloud API (Meta)
  - Gmail API (Google Cloud, service account)
  - Native WebSocket support (ASP.NET Core middleware)
- **Frontend:** Next.js 15 (separate dashboard repo)
- **Infrastructure:** Docker Compose (SQL Server 2022, Redis, .NET 10 backend, Next.js frontend)

---

## Project Structure

### Controllers/
**API endpoints & HTTP handling** (similar to Django views.py + serializers)
- `AuthApiController.cs` — JWT/API key authentication for team endpoints
- `AuthController.cs` — User login/logout/session management
- `ConversationController.cs` — Conversation CRUD & retrieval (GET /api/conversations)
- `DashboardController.cs` — Dashboard analytics & ticket queue data
- `EscalationController.cs` — Escalation event tracking & human handoff
- `KnowledgeBaseController.cs` — FAQ/docs management & search
- `TeamController.cs` — Multi-tenancy: team config, members, settings
- `WebhookController.cs` — Incoming webhooks (WhatsApp, Gmail, web chat)
- `ViewController.cs` — MVC views for dashboard UI

### Services/
**Business logic & core AI functionality** (equivalent to Django core/, channels_app/, escalation/)

#### Core AI Services:
- `TicketClassifierService.cs` — **Haiku-based ticket routing**
  - Classifies messages into: `billing`, `technical`, `account`, `complaint`, `escalate`
  - Returns confidence score (0.0-1.0)
  - Auto-escalates if confidence < 0.7
  
- `ResponseGeneratorService.cs` — **Sonnet-based response generation**
  - RAG-based: answers only from knowledge base context
  - System prompt enforces KB-only constraint
  - Returns graceful decline if no relevant KB chunks found

- `GuardrailService.cs` — **Anti-hallucination checks** (3 layers)
  - Layer 1: System prompt forbids answering without KB context
  - Layer 2: Empty RAG results → don't attempt response
  - Layer 3: Post-generation scan for policies/prices/guarantees not in KB

#### Knowledge Base & Embeddings:
- `KnowledgeBaseService.cs` — pgvector RAG retrieval
  - Semantic search via embeddings
  - Chunk-based FAQ/docs retrieval
  
- `EmbeddingService.cs` — Text chunking & vector generation
  - Breaks FAQs/docs into searchable chunks
  - Calls Claude API or third-party embeddings API

#### Multi-Channel Services:
- `WhatsAppService.cs` — WhatsApp Business Cloud API integration
  - Send/receive WhatsApp messages
  - Webhook handler for incoming messages
  - 24-hour free messaging window logic

- `GmailService.cs` — Gmail API integration
  - Poll Gmail inbox for new tickets
  - Send reply emails
  - Watch API for push notifications (optional)

- `MessengerService.cs` — Facebook Messenger integration

- `TelegramService.cs` — Telegram Bot integration

- `WebChatService.cs` — WebSocket handler for embedded web chat

#### Channel Normalization:
- `UnifiedMessageService.cs` — Normalize all channels to common format
  - Extracts: `sender_id`, `content`, `channel_type`, `metadata`
  - Creates `Message` entity in DB

#### Escalation & Detection:
- `EscalationDetectorService.cs` — Triggers escalation (3 conditions)
  - Condition 1: Classifier confidence < 0.7
  - Condition 2: Negative sentiment detected
  - Condition 3: Explicit human request keywords

- `SentimentAnalysisService.cs` — Detect frustrated/angry customers
  - Can use Claude or standalone model
  - Returns sentiment score + confidence

- `HandoffService.cs` — Package context for human agents
  - Summarize conversation
  - Include original customer message
  - Suggest AI-generated response
  - Flag reason for escalation

### Models/
**Database entities** (EF Core with SQL Server 2022 / Azure SQL)
- `Conversation.cs` — Conversation thread
  - `Id`, `CustomerId`, `ChannelType`, `Status`, `AssignedTeamMemberId`, `CreatedAt`
  - Relationship to `Message`, `Escalation`, `InternalNote`

- `Message.cs` — Individual message
  - `Id`, `ConversationId`, `SenderType` (customer/ai/human), `Content`, `ChannelMetadata`, `CreatedAt`

- `KnowledgeBase.cs` — FAQ/doc chunks with vector embeddings
  - `Id`, `Title`, `Content`, `EmbeddingJson` (nvarchar(max) JSON array of 1536 floats), `TeamId`, `CreatedAt`
  - **Vector Storage:** Embeddings serialized as JSON string (e.g., `[0.123, -0.456, 0.789, ...]`)
  - **No external extension needed** — SQL Server natively supports JSON columns

- `Escalation.cs` — Escalation event
  - `Id`, `ConversationId`, `Reason`, `ContextSnapshot`, `SuggestedResponse`, `CreatedAt`

- `InternalNote.cs` — Human agent notes
  - `Id`, `ConversationId`, `AuthorId`, `Content`, `CreatedAt`

- `Team.cs` — Multi-tenancy: organization/workspace
  - `Id`, `Name`, `ApiKey`, `Members` (relationship)

- `TeamMembership.cs` — User-to-Team mapping
  - `UserId`, `TeamId`, `Role` (admin/agent/viewer)

- `TeamWhatsAppConfig.cs` — WhatsApp credentials per team
- `TeamTelegramConfig.cs` — Telegram Bot Token per team
- `TeamMessengerConfig.cs` — Messenger Page Token per team
- `TeamGmailConfig.cs` — Gmail service account per team

- `Tag.cs` & `ConversationTag.cs` — Tagging system for conversations
  - Labels: `urgent`, `billing`, `blocked`, etc.

- `CannedResponse.cs` — Pre-written response templates
  - Team-specific templates for common scenarios

- `User.cs` — Human agents & admins
  - `Id`, `Email`, `PasswordHash`, `TeamIds`

### Data/
**Database context & configuration**
- `ApplicationDbContext.cs` — EF Core DbContext
  - All model definitions via DbSet<T>
  - pgvector extension configuration
  - Migration management

### Middleware/
**Request/response processing**
- `WebSocketChatMiddleware.cs` — WebSocket handler for embedded web chat
  - Upgrade HTTP to WebSocket
  - Route chat messages to `WebChatService`

---

## Architecture Flow (C# Version)

```
Customer Message (WhatsApp / Email / Web Chat)
    ↓
WebhookController → Normalize to UnifiedMessage (UnifiedMessageService)
    ↓
TicketClassifierService (Haiku) → Classify & confidence score
    ↓                                   ↓ (confidence < 0.7 OR angry OR "speak to human")
    ↓                                   → EscalationDetectorService → Dashboard Queue
    ↓
KnowledgeBaseService (pgvector RAG) → Retrieve relevant chunks
    ↓
ResponseGeneratorService (Sonnet) → Generate response (constrained to KB)
    ↓
GuardrailService → Verify no hallucinated policies/prices/guarantees
    ↓
Send Response via original channel (WhatsAppService / GmailService / WebChatService)
    ↓
Store in PostgreSQL (Conversation + Message entities)
```

---

## Coding Conventions for C#/.NET

### Project Structure
- **One feature = one controller** (ConversationController, KnowledgeBaseController, etc.)
- **Services folder** for business logic (DI-injected into controllers)
- **Models folder** for EF Core entities (always with `Id`, `CreatedAt`, `UpdatedAt`)
- **Data folder** for database context & migrations

### Code Style
- **Type safety first** — always use explicit types, never `var` for unclear types
- **Async all the way** — `async/await` for I/O (database, API calls)
- **Dependency Injection** — all external services via constructor injection
- **Entity Framework Core** — use LINQ queries (no raw SQL except pgvector)
- **Environment variables** — read via `IConfiguration` (never hardcode secrets)
- **Logging** — use `ILogger<T>` with conversation IDs for traceability
- **Error handling** — return `BadRequest()`/`NotFound()`/`Conflict()`, no exceptions to client
- **API responses** — consistent JSON structure with `{ success, data, error }`

### Naming Conventions
- **Classes** — PascalCase: `TicketClassifierService`, `WhatsAppService`
- **Methods** — PascalCase: `ClassifyTicketAsync()`, `SendWhatsAppMessageAsync()`
- **Fields/Properties** — PascalCase public, `_camelCase` private
- **Constants** — UPPER_SNAKE_CASE: `MAX_RETRY_ATTEMPTS = 3`
- **Enums** — PascalCase values: `TicketCategory.Billing`, `ChannelType.WhatsApp`

### Example Service Pattern
```csharp
public interface ITicketClassifierService
{
    Task<ClassificationResult> ClassifyAsync(string messageContent);
}

public class TicketClassifierService : ITicketClassifierService
{
    private readonly IAnthropicClient _anthropic;
    private readonly ILogger<TicketClassifierService> _logger;

    public TicketClassifierService(
        IAnthropicClient anthropic,
        ILogger<TicketClassifierService> logger)
    {
        _anthropic = anthropic;
        _logger = logger;
    }

    public async Task<ClassificationResult> ClassifyAsync(string messageContent)
    {
        // Use Claude Haiku for classification
        // Return structured result with category + confidence
        // Log with conversation ID for traceability
    }
}
```

### Database Conventions
- **Entity Timestamps** — every entity has `CreatedAt` (auto-set) and `UpdatedAt` (auto-update)
- **Soft Deletes** — optional `DeletedAt` field instead of hard delete
- **Foreign Keys** — explicit `[ForeignKey]` attribute on navigation properties
- **Indexing** — add `[Index]` for frequently queried fields (`TeamId`, `CustomerId`)
- **Migrations** — run `dotnet ef migrations add [MigrationName]` before any schema changes

### API Response Format (Consistent)
```json
{
  "success": true,
  "data": { ... },
  "error": null
}
```

or on error:
```json
{
  "success": false,
  "data": null,
  "error": "Human-readable error message"
}
```

---

## Key Features by Component

### Multi-Tenancy (Team-Based)
- Every entity (except User) belongs to a `Team`
- API authentication: Bearer token (JWT) or `X-API-Key` header
- Middleware validates `TeamId` from token & enforces data isolation
- Config per channel stored in `TeamWhatsAppConfig`, `TeamGmailConfig`, etc.

### Ticket Classification Flow
1. **Input:** Raw customer message (text)
2. **Claude Haiku prompt:** Structured XML output with category + confidence
3. **Categories:** `billing`, `technical`, `account`, `complaint`, `escalate`
4. **Output:** Confidence score (0.0-1.0) — auto-escalate if < 0.7
5. **Database:** Store result in `Conversation` (Category, ConfidenceScore)

### Response Generation Flow
1. **Prerequisite:** Ticket NOT escalated (confidence >= 0.7)
2. **Retrieve KB:** `KnowledgeBaseService.SearchAsync()` via pgvector
3. **Claude Sonnet prompt:** "Answer ONLY using this context: [KB chunks]"
4. **If no KB match:** Return predefined "I'll connect you with a human agent"
5. **Guardrail check:** Scan response for hallucinated policies/prices
6. **Send response** via original channel + store in DB

### Escalation Triggers
- Classifier confidence < 0.7
- Sentiment analysis detects anger/frustration
- Customer keywords: "speak to human", "manager", "supervisor"
- Manual escalation by human agent via dashboard

### Database Queries (LINQ Examples)
```csharp
// Get all conversations for a team, not yet closed
var openTickets = await _context.Conversations
    .Where(c => c.TeamId == teamId && c.Status != "closed")
    .Include(c => c.Messages)
    .OrderByDescending(c => c.CreatedAt)
    .ToListAsync();

// Search knowledge base via pgvector (EF Core extension)
var results = await _context.KnowledgeBases
    .FromSqlInterpolated($@"
        SELECT * FROM KnowledgeBase 
        WHERE TeamId = {teamId}
        ORDER BY Embedding <=> {queryEmbedding}
        LIMIT 5")
    .ToListAsync();
```

---

## Environment Variables (appsettings.Development.json)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=support_agent;User=postgres;Password=your_password"
  },
  "Anthropic": {
    "ApiKey": "your_anthropic_api_key",
    "HaikuModel": "claude-3-5-haiku-20241022",
    "SonnetModel": "claude-3-5-sonnet-20241022"
  },
  "WhatsApp": {
    "AccessToken": "your_whatsapp_access_token",
    "PhoneNumberId": "your_phone_number_id",
    "VerifyToken": "your_verify_token"
  },
  "Gmail": {
    "CredentialsPath": "/path/to/service-account-key.json",
    "WatchEmail": "support@yourdomain.com"
  },
  "Jwt": {
    "SecretKey": "your_jwt_secret_key_at_least_32_chars",
    "Issuer": "ai-support-agent",
    "Audience": "api"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

---

## Important Design Notes

### For YouTube Tutorial Viewers
- **Readability first** — code should be self-explanatory
- **Comments on complex logic** — especially Claude prompts and pgvector queries
- **Avoid over-engineering** — simple patterns, standard .NET conventions
- **One feature per branch** — easy for viewers to follow progress

### Cost Optimization
- **Haiku for classification** (~$1/M tokens) — fast, cheap
- **Sonnet for responses** (~$3/M tokens) — smarter context understanding
- **Batch processing** — queue emails/chat to classify in bulk at lower cost
- **Cache embeddings** — store KB embeddings in pgvector, don't regenerate

### Anti-Hallucination Strategy
1. System prompt: "Only answer based on provided knowledge base context"
2. If KB retrieval returns 0 results → don't call Sonnet
3. Post-generation guardrail: scan for pricing/policy/guarantee claims not in KB

### Scalability Considerations
- **Database indexes** — TeamId, ChannelType, Status, CreatedAt
- **Connection pooling** — EF Core default is sufficient for small teams
- **pgvector performance** — chunk knowledge base smartly (300-500 tokens per chunk)
- **API rate limiting** — implement per team (e.g., 100 req/min)

### Security & Multi-Tenancy
- **JWT or API Key** — enforced on all endpoints
- **Data isolation** — every query includes `.Where(x => x.TeamId == teamId)`
- **No cross-team data leaks** — middleware validates token → extract TeamId
- **Secrets rotation** — WhatsApp tokens, Gmail credentials, API keys in secure vault

---

## Build & Run

```bash
# Build backend
dotnet build backend/backend.csproj

# Run migrations
dotnet ef database update

# Run dev server (hot reload)
dotnet watch run

# Run tests
dotnet test backend.Tests/backend.Tests.csproj

# Docker: everything runs via docker-compose up
# (PostgreSQL + backend + frontend all configured)
```

---

## When Working on This Project

✅ **DO:**
- Use `async/await` for all I/O (database, API calls)
- Inject dependencies via constructor
- Return appropriate HTTP status codes (200, 400, 404, 409, 500)
- Log with conversation/team context for debugging
- Validate input in controllers before passing to services
- Use EF Core LINQ queries (not raw SQL except pgvector)
- Keep services focused on one responsibility
- Write unit tests for business logic (classifier, guardrails, escalation)

❌ **DON'T:**
- Mix data access with business logic (use services layer)
- Hardcode configuration values (use appsettings.json + IConfiguration)
- Make synchronous HTTP/database calls
- Catch all exceptions silently (log and return appropriate error)
- Bypass authentication/authorization checks
- Store credentials in code or logs
- Over-complicate RAG/embedding logic (simple semantic search is fine)
- Assume monolithic deployment (design for multi-tenancy from day one)

---

## Memory System Guidelines
Rules for Memory File Usage:
Read on Start: At the beginning of any development task, the developer or AI assistant must read the root MEMORY.md and folder-specific memory files (php-src/MEMORY.md, dotnet-src/MEMORY.md) to establish full historical context, architectural patterns, and known issues.
Update on End: At the end of every development task, the developer or AI assistant must update the root MEMORY.md and folder-specific memory files to record new learnings, resolved bugs, modified database schema mappings, implementation strategies, or pending structural refactorings.


---

## Questions? Refer to:
- **Django Original:** See `/CLAUDE.md` at project root
- **Architecture:** See `architecture.mermaid`
- **.NET Best Practices:** Microsoft Docs on ASP.NET Core, EF Core
- **Anthropic Claude:** https://docs.anthropic.com
- **pgvector:** https://github.com/pgvector/pgvector
