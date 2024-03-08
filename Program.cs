using ApexCharts;
using ScannerWeb.Components;
using ScannerWeb.Interfaces;
using ScannerWeb.Mock;
using ScannerWeb.Models;
using ScannerWeb.Services;
using System.Diagnostics;
Trace.Listeners.Add(
                new TextWriterTraceListener(Console.Out)
            );

Trace.Listeners.Add(new TextWriterTraceListener(@"/home/pi/web/log.txt"));
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSingleton<IArduinoService,ArduinoMockService>();
builder.Services.AddSingleton<IPLCService,PlcMockService>();
builder.Services.AddSingleton<IMainService,MainMockService>();
builder.Services.Configure<ConfigModel>(builder.Configuration.GetSection(nameof(ConfigModel)));
builder.Services.AddHttpContextAccessor();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
