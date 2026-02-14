#if WINDOWS
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Windows.Devices.Midi2;
using Microsoft.Windows.Devices.Midi2.Endpoints.Virtual;
using Microsoft.Windows.Devices.Midi2.Initialization;
using ProPresenterObsBridge.Options;

namespace ProPresenterObsBridge.Midi;

public sealed class WindowsMidiEndpointManager : IMidiSource
{
    private readonly ILogger<WindowsMidiEndpointManager> _logger;
    private readonly MidiOptions _options;

    private MidiDesktopAppSdkInitializer? _initializer;
    private MidiSession? _session;
    private MidiVirtualDevice? _virtualDevice;
    private MidiEndpointConnection? _connection;

    public event Action<MidiNoteEvent>? NoteReceived;

    public WindowsMidiEndpointManager(
        ILogger<WindowsMidiEndpointManager> logger,
        IOptions<MidiOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Initialize the SDK runtime
        _initializer = MidiDesktopAppSdkInitializer.Create();

        if (!_initializer.InitializeSdkRuntime())
        {
            _logger.LogCritical(EventIds.MidiRuntimeMissing,
                "Windows MIDI Services SDK runtime not found. " +
                "Install from https://microsoft.github.io/MIDI/get-latest/");
            throw new InvalidOperationException("Windows MIDI Services SDK runtime is not available.");
        }

        if (!_initializer.EnsureServiceAvailable())
        {
            _logger.LogCritical(EventIds.MidiRuntimeMissing,
                "Windows MIDI Services service is not running.");
            throw new InvalidOperationException("Windows MIDI Services service is not available.");
        }

        CreateVirtualDevice();
        return Task.CompletedTask;
    }

    private void CreateVirtualDevice()
    {
        var declaredEndpointInfo = new MidiDeclaredEndpointInfo
        {
            Name = _options.DeviceName,
            ProductInstanceId = _options.ProductInstanceId,
            SpecificationVersionMajor = 1,
            SpecificationVersionMinor = 1,
            SupportsMidi10Protocol = true,
            SupportsMidi20Protocol = true,
            SupportsReceivingJitterReductionTimestamps = false,
            SupportsSendingJitterReductionTimestamps = false,
            HasStaticFunctionBlocks = true
        };

        var declaredDeviceIdentity = new MidiDeclaredDeviceIdentity();

        var userSuppliedInfo = new MidiEndpointUserSuppliedInfo
        {
            Name = _options.DeviceName,
            Description = "ProPresenter to OBS Scene Switch Bridge"
        };

        var config = new MidiVirtualDeviceCreationConfig(
            _options.DeviceName,
            "ProPresenter to OBS virtual MIDI bridge",
            "ProPresenter-OBS Bridge",
            declaredEndpointInfo,
            declaredDeviceIdentity,
            userSuppliedInfo);

        // Single bidirectional function block for MIDI 1.0 channel voice messages
        var block = new MidiFunctionBlock
        {
            Number = 0,
            Name = "ProPresenter Input",
            IsActive = true,
            UIHint = MidiFunctionBlockUIHint.Receiver,
            FirstGroup = new MidiGroup(0),
            GroupCount = 1,
            Direction = MidiFunctionBlockDirection.Bidirectional,
            RepresentsMidi10Connection = MidiFunctionBlockRepresentsMidi10Connection.YesBanksAreNotBankChanges,
            MaxSystemExclusive8Streams = 0,
            MidiCIMessageVersionFormat = 0
        };
        config.FunctionBlocks.Add(block);

        _session = MidiSession.Create(_options.DeviceName);
        if (_session is null)
        {
            _logger.LogCritical(EventIds.MidiRuntimeMissing, "Failed to create MIDI session.");
            throw new InvalidOperationException("Failed to create MIDI session.");
        }

        _virtualDevice = MidiVirtualDeviceManager.CreateVirtualDevice(config);
        if (_virtualDevice is null)
        {
            _logger.LogCritical(EventIds.MidiDeviceCreated, "Failed to create virtual MIDI device.");
            throw new InvalidOperationException("Failed to create virtual MIDI device.");
        }

        _virtualDevice.SuppressHandledMessages = true;

        _connection = _session.CreateEndpointConnection(_virtualDevice.DeviceEndpointDeviceId);
        if (_connection is null)
        {
            _logger.LogCritical(EventIds.MidiDeviceCreated, "Failed to create MIDI endpoint connection.");
            throw new InvalidOperationException("Failed to create MIDI endpoint connection.");
        }

        _connection.AddMessageProcessingPlugin(_virtualDevice);
        _connection.MessageReceived += OnMessageReceived;

        if (!_connection.Open())
        {
            _logger.LogCritical(EventIds.MidiDeviceCreated, "Failed to open MIDI endpoint connection.");
            throw new InvalidOperationException("Failed to open MIDI endpoint connection.");
        }

        _logger.LogInformation(EventIds.MidiDeviceCreated,
            "Virtual MIDI device '{DeviceName}' created and listening.", _options.DeviceName);
    }

    private void OnMessageReceived(IMidiMessageReceivedEventSource sender, MidiMessageReceivedEventArgs args)
    {
        // UMP Message Type 2 = MIDI 1.0 Channel Voice (32-bit)
        // Word layout: [tttt gggg] [ssss cccc] [nnnnnnnn] [vvvvvvvv]
        //   t = message type (4 bits), g = group (4 bits)
        //   s = status (4 bits), c = channel (4 bits)
        //   n = note number (8 bits), v = velocity (8 bits)

        var word = args.PeekFirstWord();
        var messageType = (word >> 28) & 0x0F;

        if (messageType != 0x02) // Not a MIDI 1.0 channel voice message
        {
            _logger.LogDebug("Ignoring non-MIDI1.0-CV message (type=0x{Type:X})", messageType);
            return;
        }

        var status = (word >> 20) & 0x0F;
        var channel = ((word >> 16) & 0x0F) + 1; // MIDI channels are 1-based in our model
        var note = (int)((word >> 8) & 0x7F);
        var velocity = (int)(word & 0x7F);

        // 0x9 = Note On, 0x8 = Note Off
        bool isNoteOn;
        if (status == 0x09)
        {
            // Note On with velocity 0 is conventionally treated as Note Off
            isNoteOn = velocity > 0;
        }
        else if (status == 0x08)
        {
            isNoteOn = false;
        }
        else
        {
            _logger.LogDebug("Ignoring MIDI1.0 status 0x{Status:X} (Ch={Channel})", status, channel);
            return;
        }

        var evt = new MidiNoteEvent
        {
            IsNoteOn = isNoteOn,
            Channel = (int)channel,
            Note = note,
            Velocity = velocity,
            Timestamp = DateTimeOffset.UtcNow
        };

        _logger.LogDebug("MIDI {NoteType} Ch={Channel} Note={Note} Vel={Velocity}",
            isNoteOn ? "NoteOn" : "NoteOff", evt.Channel, evt.Note, evt.Velocity);

        NoteReceived?.Invoke(evt);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Cleanup();
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Cleanup();
        return ValueTask.CompletedTask;
    }

    private void Cleanup()
    {
        if (_connection is not null)
        {
            _connection.MessageReceived -= OnMessageReceived;
            if (_session is not null)
                _session.DisconnectEndpointConnection(_connection.ConnectionId);
        }

        _session?.Dispose();
        _session = null;

        _initializer?.Dispose();
        _initializer = null;

        _virtualDevice = null;
        _connection = null;
    }

    private static class EventIds
    {
        public static readonly EventId MidiRuntimeMissing = new(1001, "MidiRuntimeMissing");
        public static readonly EventId MidiDeviceCreated = new(1002, "MidiDeviceCreated");
    }
}
#endif
