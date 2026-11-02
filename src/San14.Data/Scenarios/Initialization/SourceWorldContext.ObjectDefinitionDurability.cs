namespace San14.Data;

public sealed partial class SourceWorldContext
{
    private readonly Dictionary<int, ushort?> _objectDefinitionBaseDurability = [];

    public void SetObjectDefinitionBaseDurability(int physical, ushort? value)
    {
        Check(physical, 31, "object definition base durability");
        _objectDefinitionBaseDurability[physical] = value;
    }

    public ushort? ReadObjectDefinitionBaseDurability(ObjectDefinitionReference definition)
    {
        int physical = MapPhysical(definition.PhysicalSlot, 31, "object definition base durability");
        return _objectDefinitionBaseDurability.TryGetValue(physical, out var value) ? value : _initialObjectScalarRules?.ReadDurability(physical);
    }
}
