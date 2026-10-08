using San14.Data;

/// <summary>The actual D40 Model registration's held SDFD0. Source literal0
/// omits dependency4; nonzero keeps the true type20/image/attribute15 record.
/// This owner has no A8 Material/texture-group dependency.</summary>
public sealed class OriginalSdfVolumeResourceModelOwner
{
    private readonly OriginalSdfModelVolumeRegistration registration;
    private readonly IOriginalOrdinarySdfReadyVolumeRecord? volumeD0;
    private bool retained = true;
    private OriginalSdfVolumeResourceModelOwner(OriginalSdfModelVolumeRegistration registration)
    {
        this.registration = registration;
        volumeD0 = registration.VolumeD0 is { } volume ? new ReadyVolume(this,volume) : null;
    }
    public LoadedOriginalSdfModelDeclaration Source => registration.Source;
    public IOriginalOrdinarySdfReadyVolumeRecord? ActualVolumeD0 => ReadCurrentOwnedVolumeD0();
    public static OriginalSdfVolumeResourceModelOwner RegisterFreshOwnedModel(
        LoadedOriginalSdfModelDeclaration source, Func<uint, OriginalSdfVolumeResourcePayload> readActualReadyDependency4)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new(source.RegisterFreshOwnedModel(readActualReadyDependency4));
    }
    public IOriginalOrdinarySdfReadyVolumeRecord? ReadCurrentOwnedVolumeD0()
    {
        RequireRetained();
        registration.VolumeD0?.RequireCurrent();
        return volumeD0;
    }
    public void Retire() => retained = false;
    private void RequireRetained() => ObjectDisposedException.ThrowIf(!retained,this);
    private sealed class ReadyVolume(OriginalSdfVolumeResourceModelOwner owner,
        OriginalSdfVolumeResourceTexture actualTexture) : IOriginalOrdinarySdfReadyVolumeRecord
    {
        public ReadOnlyMemory<byte> ReadActualSourceHeader28()
        { owner.RequireRetained(); return actualTexture.ReadActualSourceHeader28(); }
        public object ActualVolumeResource20
        { get { owner.RequireRetained(); actualTexture.RequireCurrent(); return actualTexture.ActualImageResource20; } }
    }
}
