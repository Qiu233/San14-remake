using System.Buffers.Binary;

/// <summary>Source FPS mode and fixed animation step values for the playable
/// Map. The application owns physical frame admission; elapsed time never
/// manufactures additional source callbacks here.</summary>
public sealed class D3D11AnimationPacer
{
    public const uint SixtyFramesPerSecondMode = 1;
    private readonly uint[] _fps;
    private ulong? _lastProcessFrame;
    public uint FpsMode {get;private set;}
    public int FramesPerSecond(uint mode) => mode < _fps.Length ? checked((int)_fps[mode])
        : throw new ArgumentOutOfRangeException(nameof(mode));

    public D3D11AnimationPacer(string fpsSource)
    {
        var bytes=File.ReadAllBytes(fpsSource);
        if(bytes.Length!=8)throw new InvalidDataException("Actual source FPS table extent.");
        _fps=[BinaryPrimitives.ReadUInt32LittleEndian(bytes),BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4))];
        if(_fps.Any(value=>value==0))throw new InvalidDataException("Actual source FPS table contains zero.");
    }
    // Camera and dialog indicators share this real process-frame identity.
    // Query/hover callbacks cannot manufacture another update in the same frame.
    internal bool TryAcceptProcessFrame(ulong processFrame)
    {
        if (_lastProcessFrame == processFrame) return false;
        _lastProcessFrame = processFrame;
        return true;
    }
    public void ApplyLoadedOption31(uint selectedSettings84)
    {
        if(selectedSettings84<2)FpsMode=selectedSettings84;
    }
}
