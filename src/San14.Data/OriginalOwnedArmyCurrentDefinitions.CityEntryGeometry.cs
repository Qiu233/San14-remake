namespace San14.Data;

public sealed partial class OriginalOwnedArmyCurrentDefinitions
{
    private Dictionary<int, (bool? Valid, ushort? Location, ushort? Goal)>? _cityEntryGeometry;

    //Fixed source registration/link/Object geometry. Current owners and Army
    //fields are supplied separately; movement does not rebuild this map prefix.
    internal (bool? Valid, ushort? Location, ushort? Goal) ReadCityEntryGeometry(int physical)
    {
        if (_cityEntryGeometry is null)
        {
            var source = CaptureSourcePrefix();
            var geometry = new Dictionary<int, (bool? Valid, ushort? Location, ushort? Goal)>();
            for (int city = 0; city <= 51; city++)
            {
                var target = new OriginalBaseReference(OriginalBaseKind.City, city);
                ushort? link = source.ReadBaseObjectLink(target);
                var obj = link is ushort identity ? source.ReadObjectTableReference(identity <= 3000 ? identity : 0) : null;
                geometry[city] = (source.ReadBaseNativeValidity(target), link,
                    obj is null ? null : source.ReadObjectHexIndex(obj));
            }
            _cityEntryGeometry = geometry;
        }
        return _cityEntryGeometry.GetValueOrDefault(physical);
    }
}
