using Microsoft.FluentUI.AspNetCore.Components;
using EzBot.UI.Components;
using EzBot.Persistence;
using Microsoft.EntityFrameworkCore;
using EzBot.Persistence.Repositories;
using EzBot.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddFluentUIComponents();

// Register EzBotDbContext with SQLite
builder.Services.AddDbContext<EzBotDbContext>(options =>
    options.UseSqlite("Data Source=ezbot.db"));

// Register repositories
builder.Services.AddScoped<IExchangeApiKeyRepository, ExchangeApiKeyRepository>();

// Register services
builder.Services.AddScoped<DbService>();

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
