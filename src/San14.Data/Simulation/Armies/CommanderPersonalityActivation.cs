using San14.Core;

namespace San14.Data;

/// <summary>Stages one commander's reaction without publishing a partial nine-trait
/// scan. The input owns every unchanged officer; this operation owns the
/// activation replacement it writes and publishes one replacement only after all reads resolve.</summary>
internal sealed class CommanderPersonalityActivation(GameState input, int commanderId)
{
    public const int TraitSlotCount = OfficerTraitActivationSourceAdapter.TraitSlotCount;
    private const int MaximumActivationDuration = 720;

    private ushort? _mask;
    private OfficerTraitActivation? _candidate;

    public ushort? Mask => _mask;
    public void ReadMask(ushort? current) => _mask ??= current;
    public ushort? Timer(int index, ushort? current) => _candidate is null ? current :
        _candidate.RemainingDuration(index) is int duration ? checked((ushort)duration) : null;
    public bool HasChanges => _candidate is not null;

    public bool TryActivate(int index, ushort activeMask, ushort duration,
        out ushort maskAfter, out ushort timerAfter)
    {
        maskAfter = activeMask;
        timerAfter = 0;
        OfficerState commander = input.Officers[commanderId];
        if (commander.TraitActivation is not { DurationSlotCount: TraitSlotCount } owner)
            return false;
        maskAfter = checked((ushort)(activeMask | (1 << index)));
        timerAfter = (ushort)Math.Min(MaximumActivationDuration, (int)duration);
        _candidate = new OfficerTraitActivation(OfficerTraitActivationSourceAdapter.ReadSlots(activeMask),
            (_candidate ?? owner).RemainingDurations).Activate(index, timerAfter);
        _mask = maskAfter;
        return true;
    }

    public GameState Publish()
    {
        if (_candidate is null) return input;
        OfficerState commander = input.Officers[commanderId];
        return input with { Officers = new(input.Officers) { [commander.Id] = commander with
        {
            TraitActivation = _candidate
        } } };
    }
}
