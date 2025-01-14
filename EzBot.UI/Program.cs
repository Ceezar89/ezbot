using Microsoft.FluentUI.AspNetCore.Components;
using EzBot.UI.Components;
using EzBot.Persistence;
using Microsoft.EntityFrameworkCore;
using EzBot.Services;

var builder = WebApplication.CreateBuilder(args);

// Read environment variables
bool useEncryption = Environment.GetEnvironmentVariable("USE_ENCRYPTION")?.ToLower() == "true";
string encryptionSecret = Environment.GetEnvironmentVariable("ENCRYPTION_SECRET") ?? string.Empty;

// Validate secret length if encryption is enabled
if (useEncryption)
{
    if (encryptionSecret.Length < 16)
    {
        throw new InvalidOperationException(
            "Encryption secret must be at least 16 characters long. " +
            "Please set ENCRYPTION_SECRET environment variable accordingly.");
    }
    else if (encryptionSecret.Length > 128)
    {
        throw new InvalidOperationException(
            "Encryption secret cannot exceed 128 characters. " +
            "Please set ENCRYPTION_SECRET environment variable accordingly.");
    }
}

// Register Encryption service
if (useEncryption)
{
    // Register the real encryption service
    builder.Services.AddScoped<IEncryptionService>(_ =>
        new EncryptionService(encryptionSecret));
}
else
{
    // Register the no-op encryption service
    builder.Services.AddScoped<IEncryptionService, NoOpEncryptionService>();
}

// Add Razor services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddFluentUIComponents();

// Register EzBotDbContext with SQLite
builder.Services.AddDbContext<EzBotDbContext>(options =>
    options.UseSqlite("Data Source=ezbot.db"));


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
