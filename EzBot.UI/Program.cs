using Microsoft.FluentUI.AspNetCore.Components;
using EzBot.UI.Components;
using EzBot.Persistence;
using Microsoft.EntityFrameworkCore;
using EzBot.Services;

var builder = WebApplication.CreateBuilder(args);

// Read environment variables
string encryptionSecret = Environment.GetEnvironmentVariable("EZBOT_ENCRYPTION_SECRET") ?? string.Empty;
bool useEncryption = !string.IsNullOrEmpty(encryptionSecret);

// Validate if encryption secret is set correctly
if (useEncryption)
{
    if (encryptionSecret.Length < 32)
    {
        throw new InvalidOperationException(
            "Encryption secret must be at least 32 characters long. " +
            "Please set the ENCRYPTION_SECRET as an environment variable accordingly. " +
            "(you can use https://randomkeygen.com/ to generate a 256-bit WEP Keys for example.)");
    }
    else if (encryptionSecret.Length > 128)
    {
        throw new InvalidOperationException(
            "Encryption secret cannot exceed 128 characters. " +
            "Please set the ENCRYPTION_SECRET as an environment variable accordingly." +
            "(you can use https://randomkeygen.com/ to generate a 256-bit WEP Keys for example.)");
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
