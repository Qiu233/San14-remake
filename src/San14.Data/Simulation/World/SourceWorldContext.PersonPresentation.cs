namespace San14.Data;

public sealed partial class SourceWorldContext
{
    private readonly Dictionary<int, ushort?> _personPortraitAppearance = [];
    public void SetPersonPortraitAppearance(int physical, ushort? appearance)
    {
        Check(physical, 1851, "person physical slot");
        _personPortraitAppearance[physical] = appearance;
    }
    public ushort? ReadPersonPortraitAppearance(int physical)
    {
        Check(physical, 1851, "person physical slot");
        return _personPortraitAppearance.GetValueOrDefault(physical);
    }
}
