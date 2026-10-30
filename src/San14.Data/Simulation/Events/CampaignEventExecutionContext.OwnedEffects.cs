using San14.Core;

namespace San14.Data;

internal sealed partial class CampaignEventExecutionContext
{
    /// <summary>Ordinary Story opcode109 describes an invocation-owned effect
    /// operation. The live host owns creation/admission and the first free one
    /// of three slots; preparing a command never allocates or reserves a slot.
    /// The source pose is identity at the origin with unit scale and this
    /// invocation as parent. SelectorMode chooses its actual camera owner.</summary>
    public StoryOwnedEffect? PrepareOwnedEffect(int selector)
    {
        if (selector <= 0) return new(StoryOwnedEffectOperation.StopAll, 0, 0);
        if (selector > 12) return null; //The original instruction is a no-op.
        int mode = selector is >= 8 and <= 11 ? 1 : 2;
        return new(StoryOwnedEffectOperation.Create, 117 + selector, mode);
    }
}
