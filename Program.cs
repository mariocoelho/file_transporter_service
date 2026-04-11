using file_transporter_service;
using Microsoft.Extensions.Logging.EventLog;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

var logPath = builder.Configuration["Serilog:WriteTo:0:Args:path"];
if (!string.IsNullOrWhiteSpace(logPath))
{
    var logDirectory = Path.GetDirectoryName(logPath);
    if (!string.IsNullOrWhiteSpace(logDirectory))
    {
        Directory.CreateDirectory(logDirectory);
    }
}

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "File Transporter Service";
});

builder.Logging.AddEventLog((EventLogSettings settings) =>
{
    settings.SourceName = "File Transporter Service";
});

builder.Services.AddSerilog((services, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services);
});

builder.Services.Configure<Settings>(builder.Configuration.GetSection("FileTransporter"));
builder.Services.AddSingleton<FileProcessor>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
await host.RunAsync();

