using ApexCharts;
using ScannerWeb.Components;
using ScannerWeb.Interfaces;
using ScannerWeb.Mock;
using ScannerWeb.Models;
using ScannerWeb.Observer;
using ScannerWeb.Observer.MainProcessOberserver;
using ScannerWeb.Services;
using Serilog;
using Serilog.Filters;
using System.Diagnostics;
Trace.Listeners.Add(
                new TextWriterTraceListener(Console.Out)
            );

Trace.Listeners.Add(new TextWriterTraceListener(@"/home/pi/web/log.txt"));
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
string basepath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log");
if (!Directory.Exists(basepath))
    Directory.CreateDirectory(basepath);
LoggerConfiguration _log = new LoggerConfiguration()
        //   .WriteTo.Logger(l => l.Filter.ByIncludingOnly(x => x.Level == Serilog.Events.LogEventLevel.Error && Matching.FromSource(nameof(APIServerObserver))(x))).WriteTo.File(Path.Combine(basepath, "api-observer-error.txt"))
        // .WriteTo.Logger(l => l.Filter.ByIncludingOnly(x => x.Level == Serilog.Events.LogEventLevel.Error && Matching.FromSource(nameof(TopProcessObserver))(x))).WriteTo.File(Path.Combine(basepath, "top-process-observer-error.txt"))
        //.WriteTo.Logger(l => l.Filter.ByIncludingOnly(x => x.Level == Serilog.Events.LogEventLevel.Error && Matching.FromSource(nameof(BottomProcessObserver))(x))).WriteTo.File(Path.Combine(basepath, "bottom-process-observer-error.txt"))

        ;
if (builder.Environment.IsDevelopment())
{
    _log = _log.Enrich.FromLogContext().MinimumLevel.Debug()
            .WriteTo.Logger(l => l.Filter.ByIncludingOnly(x => x.Level == Serilog.Events.LogEventLevel.Information && Matching.FromSource<MainService>()(x)).WriteTo.File(Path.Combine(basepath, "main-info.txt")))
        .WriteTo.Logger(l => l.Enrich.FromLogContext().Filter.ByIncludingOnly(x => x.Level == Serilog.Events.LogEventLevel.Error && Matching.FromSource<MainService>()(x)).WriteTo.File(Path.Combine(basepath, "main-error.txt")))
        .WriteTo.Logger(l => l.Enrich.FromLogContext().Filter.ByIncludingOnly(x => x.Level == Serilog.Events.LogEventLevel.Information && Matching.FromSource<ArduinoService>()(x)).WriteTo.File(Path.Combine(basepath, "arduino-info.txt")))
        .WriteTo.Logger(l => l.Enrich.FromLogContext().Filter.ByIncludingOnly(x => x.Level == Serilog.Events.LogEventLevel.Error && Matching.FromSource<ArduinoService>()(x)).WriteTo.File(Path.Combine(basepath, "arduino-error.txt")))
        .WriteTo.Logger(l => l.Enrich.FromLogContext().Filter.ByIncludingOnly(x => x.Level == Serilog.Events.LogEventLevel.Information && Matching.FromSource<PLCService>()(x)).WriteTo.File(Path.Combine(basepath, "plc-info.txt")))
        .WriteTo.Logger(l => l.Enrich.FromLogContext().Filter.ByIncludingOnly(x => x.Level == Serilog.Events.LogEventLevel.Information && Matching.FromSource<ArduinoMockService>()(x)).WriteTo.File(Path.Combine(basepath, "arduino-mock-info.txt")))
        .WriteTo.Logger(l => l.Enrich.FromLogContext().Filter.ByIncludingOnly(x => x.Level == Serilog.Events.LogEventLevel.Error && Matching.FromSource<ArduinoMockService>()(x)).WriteTo.File(Path.Combine(basepath, "arduino-mock-error.txt")))
        .WriteTo.Logger(l => l.Enrich.FromLogContext().Filter.ByIncludingOnly(x => x.Level == Serilog.Events.LogEventLevel.Error && Matching.FromSource<PLCService>()(x)).WriteTo.File(Path.Combine(basepath, "plc-error.txt")))
        .WriteTo.Logger(l => l.Enrich.FromLogContext().Filter.ByIncludingOnly(x => x.Level == Serilog.Events.LogEventLevel.Information && Matching.FromSource<PlcMockService>()(x)).WriteTo.File(Path.Combine(basepath, "plc-mock-info.txt")))
        .WriteTo.Logger(l => l.Enrich.FromLogContext().Filter.ByIncludingOnly(x => x.Level == Serilog.Events.LogEventLevel.Error && Matching.FromSource<PlcMockService>()(x)).WriteTo.File(Path.Combine(basepath, "plc-mock-error.txt")))
        .WriteTo.Logger(l => l.Enrich.FromLogContext().Filter.ByIncludingOnly(x => x.Level == Serilog.Events.LogEventLevel.Information && Matching.FromSource<MainMockService>()(x)).WriteTo.File(Path.Combine(basepath, "main-mock-info.txt")))
        .WriteTo.Logger(l => l.Enrich.FromLogContext().Filter.ByIncludingOnly(x => x.Level == Serilog.Events.LogEventLevel.Error && Matching.FromSource<MainMockService>()(x)).WriteTo.File(Path.Combine(basepath, "main-mock-error.txt")))
        .WriteTo.Logger(l => l.Enrich.FromLogContext().Filter.ByIncludingOnly(x => x.Level == Serilog.Events.LogEventLevel.Error).WriteTo.File(Path.Combine(basepath, "Error.txt")))
        .WriteTo.Logger(l => l.Enrich.FromLogContext().Filter.ByIncludingOnly(x => x.Level == Serilog.Events.LogEventLevel.Debug).WriteTo.File(Path.Combine(basepath, "debug.txt")))
            .WriteTo.Logger(l => l.Filter.ByIncludingOnly(x => x.Level == Serilog.Events.LogEventLevel.Debug && Matching.FromSource<MainService>()(x)).WriteTo.File(Path.Combine(basepath, "main-debug.txt")));
}
Log.Logger = _log.CreateLogger();
builder.Logging.AddSerilog();
builder.Services.AddSingleton<IArduinoService,ArduinoService>();
builder.Services.AddSingleton<IPLCService,PLCService>();
builder.Services.AddSingleton<IMainService,MainService>();
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
