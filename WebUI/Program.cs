using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using WebUI.Application;
using WebUI;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<UiMessageService>();
builder.Services.AddScoped<UiAutomationService>();
builder.Services.AddScoped<AITurnService>();
builder.Services.AddScoped<GameSessionService>();
builder.Services.AddScoped<PlayerActionService>();
builder.Services.AddScoped<UiTelemetryService>();
builder.Services.AddScoped<UiTestActionService>();
builder.Services.AddScoped<TurnPlayService>();
builder.Services.AddScoped<ReplayLogParserService>();

await builder.Build().RunAsync();
