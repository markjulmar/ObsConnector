namespace ProPresenterObsBridge.Options;

public sealed class MidiOptions
{
    public const string Section = "Midi";

    public string DeviceName { get; set; } = "ProPresenter-OBS Bridge";
    public string ProductInstanceId { get; set; } = "com.julmar.pp-obs-bridge";
}

public sealed class ObsOptions
{
    public const string Section = "Obs";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 4455;
    public string? Password { get; set; }
    public int ReconnectMaxSeconds { get; set; } = 30;
}

public sealed class BehaviorOptions
{
    public const string Section = "Behavior";

    public int ObsDisconnectedLogIntervalSeconds { get; set; } = 10;
    public bool IgnoreIfAlreadyOnScene { get; set; }
}

public sealed class MappingEntry
{
    public string? NoteType { get; set; } = "On";
    public int Channel { get; set; }
    public int Note { get; set; }
    public int Velocity { get; set; } = -1;
    public string Scene { get; set; } = string.Empty;
}
