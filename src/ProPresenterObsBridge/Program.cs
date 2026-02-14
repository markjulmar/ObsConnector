using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProPresenterObsBridge.Mapping;
using ProPresenterObsBridge.Midi;
using ProPresenterObsBridge.Obs;
using ProPresenterObsBridge.Options;
using ProPresenterObsBridge.Util;
using ProPresenterObsBridge.Worker;

namespace ProPresenterObsBridge;

public static class Program
{
    private const string MutexName = "Global\\ProPresenterObsBridge_SingleInstance";

    public static int Main(string[] args)
    {
        using var mutex = new Mutex(false, MutexName, out bool createdNew);
        if (!createdNew)
        {
            Console.Error.WriteLine("Another instance of ProPresenterObsBridge is already running.");
            return 1;
        }

        var builder = Host.CreateApplicationBuilder(args);

        builder.Configuration
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

        // Bind config sections
        var midiOptions = new MidiOptions();
        builder.Configuration.GetSection(MidiOptions.Section).Bind(midiOptions);

        var obsOptions = new ObsOptions();
        builder.Configuration.GetSection(ObsOptions.Section).Bind(obsOptions);

        var behaviorOptions = new BehaviorOptions();
        builder.Configuration.GetSection(BehaviorOptions.Section).Bind(behaviorOptions);

        var mappings = new List<MappingEntry>();
        builder.Configuration.GetSection("Mappings").Bind(mappings);

        // Normalize NoteType defaults
        foreach (var m in mappings)
        {
            if (string.IsNullOrWhiteSpace(m.NoteType))
                m.NoteType = "On";
        }

        // Validate
        var errors = ConfigValidator.Validate(obsOptions, mappings);
        if (errors.Count > 0)
        {
            Console.Error.WriteLine("Configuration errors:");
            foreach (var err in errors)
                Console.Error.WriteLine($"  - {err}");
            return 1;
        }

        // Register options (reload when appsettings.json changes)
        builder.Services.Configure<MidiOptions>(builder.Configuration.GetSection(MidiOptions.Section));
        builder.Services.Configure<ObsOptions>(builder.Configuration.GetSection(ObsOptions.Section));
        builder.Services.Configure<BehaviorOptions>(builder.Configuration.GetSection(BehaviorOptions.Section));
        builder.Services.Configure<List<MappingEntry>>(builder.Configuration.GetSection("Mappings"));
        builder.Services.PostConfigure<List<MappingEntry>>(list =>
        {
            foreach (var m in list)
                if (string.IsNullOrWhiteSpace(m.NoteType))
                    m.NoteType = "On";
        });
        builder.Services.AddSingleton<IMappingEngine, MappingEngine>();
        builder.Services.AddSingleton<HostResolver>();
        builder.Services.AddSingleton<ObsClient>();
        builder.Services.AddSingleton<IObsClient>(sp => sp.GetRequiredService<ObsClient>());

        // Platform-specific services
        if (OperatingSystem.IsWindows())
        {
            ConfigureWindows(builder);
        }
        else
        {
            builder.Services.AddSingleton<IMidiSource, DryWetMidiSource>();
        }

        // Start/stop the MIDI source with the host lifetime
        builder.Services.AddHostedService<MidiHostedService>();
        builder.Services.AddHostedService<BridgeWorker>();

        var host = builder.Build();

        var logger = host.Services.GetRequiredService<ILogger<IHost>>();
        LogStartupConfig(logger, midiOptions, obsOptions, behaviorOptions, mappings);

        host.Run();
        return 0;
    }

    private static void ConfigureWindows(HostApplicationBuilder builder)
    {
        // These calls require the Windows-only packages which are conditionally referenced.
        // Using reflection-style invocation isn't needed — the conditional ItemGroup in csproj
        // ensures the packages are only restored on Windows, and this method is only called
        // on Windows at runtime.
#if WINDOWS
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "ProPresenterObsBridge";
        });
        builder.Logging.AddEventLog(settings =>
        {
            settings.SourceName = "ProPresenterObsBridge";
        });
        builder.Services.AddSingleton<IMidiSource, WindowsMidiEndpointManager>();
#endif
    }

    private static void LogStartupConfig(
        ILogger logger,
        MidiOptions midi,
        ObsOptions obs,
        BehaviorOptions behavior,
        List<MappingEntry> mappings)
    {
        var nl = Environment.NewLine;
        var lines = new List<string>
        {
            "=== ProPresenterObsBridge starting ===",
            $"Platform: {(OperatingSystem.IsWindows() ? "Windows" : "macOS/Other")}",
            $"MIDI Device: {midi.DeviceName} (Instance: {midi.ProductInstanceId})",
            $"OBS Host: {obs.Host}:{obs.Port}",
            $"OBS Password: {(string.IsNullOrEmpty(obs.Password) ? "(not set)" : "********")}",
            $"Reconnect max: {obs.ReconnectMaxSeconds}s",
            $"IgnoreIfAlreadyOnScene: {behavior.IgnoreIfAlreadyOnScene}",
            $"OBS disconnected log interval: {behavior.ObsDisconnectedLogIntervalSeconds}s",
            $"Mappings ({mappings.Count}):"
        };
        foreach (var m in mappings)
        {
            var vel = m.Velocity == -1 ? "any" : m.Velocity.ToString();
            lines.Add($"  Note {m.NoteType} Ch={m.Channel} Note={m.Note} Vel={vel} -> Scene \"{m.Scene}\"");
        }
        logger.LogInformation("{Config}", string.Join(nl, lines));
    }
}
