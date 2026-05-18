using Neuracode.Crm.Api.Data;
using Neuracode.Crm.Api.Endpoints;
using Neuracode.Crm.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connStr = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=../../data/crm.db";

builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlite(connStr));

builder.Services.AddOpenApi();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IWhatsAppService, WhatsAppService>();
builder.Services.AddScoped<IAgentService, AgentService>();
builder.Services.AddScoped<IAgentMemoryRepository, AgentMemoryRepository>();
builder.Services.AddScoped<IMemoryExtractorService, MemoryExtractorService>();

var app = builder.Build();

// Startup migration: create new schema for fresh installs + additive columns for existing DBs
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    foreach (var sql in new[]
    {
        "ALTER TABLE contacts ADD COLUMN wa_id TEXT",
        "ALTER TABLE contacts ADD COLUMN opted_out INTEGER NOT NULL DEFAULT 0",
        "ALTER TABLE contacts ADD COLUMN bot_handling INTEGER NOT NULL DEFAULT 0",
        "ALTER TABLE activities ADD COLUMN wamid TEXT",
    })
    {
        try { await db.Database.ExecuteSqlRawAsync(sql); }
        catch { /* column already exists in this installation */ }
    }
    await db.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS whatsapp_messages (
            id TEXT NOT NULL PRIMARY KEY,
            wa_id TEXT NOT NULL,
            wamid TEXT NOT NULL UNIQUE,
            direction TEXT NOT NULL,
            body TEXT NOT NULL,
            status TEXT NOT NULL,
            created_at INTEGER NOT NULL
        )");
    await db.Database.ExecuteSqlRawAsync(
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_contacts_wa_id\" ON \"contacts\" (\"wa_id\") WHERE \"wa_id\" IS NOT NULL");

    await db.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS agent_memory (
            wa_id TEXT NOT NULL PRIMARY KEY,
            data TEXT NOT NULL DEFAULT '{{}}',
            updated_at INTEGER NOT NULL
        )");

    // FK indexes — absent in existing installs, critical for query performance
    foreach (var idx in new[]
    {
        "CREATE INDEX IF NOT EXISTS \"IX_activities_contact_id\" ON \"activities\" (\"contact_id\")",
        "CREATE INDEX IF NOT EXISTS \"IX_activities_deal_id\" ON \"activities\" (\"deal_id\") WHERE \"deal_id\" IS NOT NULL",
        "CREATE INDEX IF NOT EXISTS \"IX_deals_contact_id\" ON \"deals\" (\"contact_id\")",
        "CREATE INDEX IF NOT EXISTS \"IX_deals_stage_id\" ON \"deals\" (\"stage_id\")",
        "CREATE INDEX IF NOT EXISTS \"IX_whatsapp_messages_wa_id\" ON \"whatsapp_messages\" (\"wa_id\")",
    })
        await db.Database.ExecuteSqlRawAsync(idx);
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }))
   .WithTags("health");

app.MapContactEndpoints();
app.MapDealEndpoints();
app.MapActivityEndpoints();
app.MapPipelineEndpoints();
app.MapClassifyEndpoints();
app.MapFollowupEndpoints();
app.MapImportEndpoints();
app.MapExportEndpoints();
app.MapDigestEndpoints();
app.MapWebhookEndpoints();
app.MapWhatsAppEndpoints();

app.Run();

public partial class Program { }
