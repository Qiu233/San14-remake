using San14.Data;

/// <summary>The actual D40 Model registration's held SDFD0. Source literal0
/// omits dependency4; nonzero keeps the true type20/image/attribute15 record.
/// This owner has no A8 Material/texture-group dependency.</summary>
public sealed class SdfVolumeResourceModelOwner
{
    private readonly SdfModelVolumeRegistration registration;
    private readonly IOrdinarySdfReadyVolumeRecord? volumeD0;
    private bool retained = true;
    private SdfVolumeResourceModelOwner(SdfModelVolumeRegistration registration)
    {
        this.registration = registration;
        volumeD0 = registration.VolumeD0 is { } volume ? new ReadyVolume(this,volume) : null;
    }
    public LoadedSdfModelDeclaration Source => registration.Source;
    public IOrdinarySdfReadyVolumeRecord? ActualVolumeD0 => ReadCurrentOwnedVolumeD0();
    public static SdfVolumeResourceModelOwner RegisterFreshOwnedModel(
        LoadedSdfModelDeclaration source, Func<uint, SdfVolumeResourcePayload> readActualReadyDependency4)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new(source.RegisterFreshOwnedModel(readActualReadyDependency4));
    }
    public IOrdinarySdfReadyVolumeRecord? ReadCurrentOwnedVolumeD0()
    {
        RequireRetained();
        registration.VolumeD0?.RequireCurrent();
        return volumeD0;
    }
    public void Retire() => retained = false;
    private void RequireRetained() => ObjectDisposedException.ThrowIf(!retained,this);
    private sealed class ReadyVolume(SdfVolumeResourceModelOwner owner,
        SdfVolumeResourceTexture actualTexture) : IOrdinarySdfReadyVolumeRecord
    {
        public ReadOnlyMemory<byte> ReadActualSourceHeader28()
        { owner.RequireRetained(); return actualTexture.ReadActualSourceHeader28(); }
        public object ActualVolumeResource20
        { get { owner.RequireRetained(); actualTexture.RequireCurrent(); return actualTexture.ActualImageResource20; } }
    }
}
