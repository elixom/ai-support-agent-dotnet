using System;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using backend.Data;
using backend.Middleware;
using backend.Models;
using backend.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowNextJs", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://127.0.0.1:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 2. HTTP Client registration
builder.Services.AddHttpClient();

// 3. Database context with SQL Server
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? "Server=localhost,1433;Database=support_agent;User Id=sa;Password=Your_Secure_Password123!;Encrypt=False;TrustServerCertificate=True;";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// 4. Dependency Injection for AI, Conversations and Token Services
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IClassifierService, ClassifierService>();
builder.Services.AddScoped<IResponderService, ResponderService>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<IGuardrailsService, GuardrailsService>();
builder.Services.AddScoped<IConversationService, ConversationService>();

// 5. Authentication Configuration (JWT Bearer)
var jwtSecret = builder.Configuration["JWT_SECRET"]
    ?? builder.Configuration["Jwt:SecretKey"]
    ?? "super-secret-temporary-key-that-must-be-long-enough-for-hs256";
var key = Encoding.UTF8.GetBytes(jwtSecret);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

var app = builder.Build();

// 6. Database migrations + seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        await db.Database.MigrateAsync();
        await SeedDataAsync(db, scope.ServiceProvider.GetRequiredService<IEmbeddingService>());
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database migration/seeding failed: {ex.Message}");
        throw;
    }
}

// 7. Configure HTTP pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseCors("AllowNextJs");
app.UseStaticFiles();

// 8. WebSocket Middleware setup
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2)
});

app.UseMiddleware<WebSocketChatMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultControllerRoute();
app.MapControllers();

app.Run();

// ---------------------------------------------------------------------------
// Seeding Sample Data
// ---------------------------------------------------------------------------

async Task SeedDataAsync(ApplicationDbContext db, IEmbeddingService embeddingService)
{
    if (await db.KnowledgeBases.AnyAsync()) return;

    var sampleFaqs = new[]
    {
        new { Category = "billing", Content = "Our pricing plans are: Starter ($9/month) for up to 100 conversations, Professional ($29/month) for up to 1,000 conversations, and Enterprise ($99/month) for unlimited conversations. All plans include WhatsApp, Email, and Web Chat support. Annual billing saves 20%." },
        new { Category = "billing", Content = "To cancel your subscription, go to Settings > Billing > Cancel Plan. Your account will remain active until the end of your current billing period. We do not offer partial refunds for unused time. If you were charged in error, contact our billing team and we will review your case within 48 hours." },
        new { Category = "billing", Content = "We accept Visa, Mastercard, American Express, and PayPal. Invoices are generated on the 1st of each month and sent to your registered email. You can download past invoices from Settings > Billing > Invoice History." },
        new { Category = "technical", Content = "To reset your password: Click 'Forgot Password' on the login page, enter your registered email, and check your inbox for a reset link. The link expires in 30 minutes. If you don't receive it, check your spam folder or contact support." },
        new { Category = "technical", Content = "To integrate our API, generate an API key from Settings > API Keys. Use the key in the Authorization header as 'Bearer <your-key>'. Rate limits: 100 requests/minute for Starter, 500 for Professional, unlimited for Enterprise. Full API documentation is available at docs.example.com." },
        new { Category = "technical", Content = "If you're experiencing slow performance, try: 1) Clear your browser cache, 2) Disable browser extensions, 3) Try a different browser (we recommend Chrome or Firefox), 4) Check your internet connection. If the issue persists, contact technical support with your browser version and a screenshot of the issue." },
        new { Category = "account", Content = "To update your profile information, go to Settings > Profile. You can change your name, email, phone number, and company details. Email changes require verification via a confirmation link sent to both old and new emails. Username changes are limited to once every 30 days." },
        new { Category = "account", Content = "To add team members, go to Settings > Team > Invite Member. Enter their email and select a role: Admin (full access), Agent (can handle tickets), or Viewer (read-only). Each role has different permissions. Team member limits depend on your plan: Starter (2), Professional (10), Enterprise (unlimited)." },
        new { Category = "general", Content = "Our support hours are Monday to Friday, 9 AM to 6 PM EST. AI support is available 24/7 for common questions. For urgent issues outside business hours, email urgent@example.com and our on-call team will respond within 1 hour. Average response time during business hours is under 15 minutes." },
        new { Category = "general", Content = "We take data security seriously. All data is encrypted at rest (AES-256) and in transit (TLS 1.3). We are SOC 2 Type II certified and GDPR compliant. You can request a data export or deletion at any time from Settings > Privacy. For our full privacy policy, visit privacy.example.com." }
    };

    foreach (var faq in sampleFaqs)
    {
        var embedding = await embeddingService.GenerateEmbeddingAsync(faq.Content);
        var kb = new KnowledgeBase
        {
            Category = faq.Category,
            Content = faq.Content,
            Embedding = embedding,
            MetadataJson = "{\"source\": \"seed_script\", \"type\": \"faq\"}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.KnowledgeBases.Add(kb);
    }

    await db.SaveChangesAsync();
    Console.WriteLine("Seeded 10 knowledge base entries successfully.");
}
