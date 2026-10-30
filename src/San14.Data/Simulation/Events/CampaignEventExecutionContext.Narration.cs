namespace San14.Data;

/// <summary>The actual global player's qualified geographic experience bonus.
/// Present is independent of Percentage: a qualified zero still changes the line.</summary>
public readonly record struct CampaignNarrationExperienceBonus(bool Present, int Percentage, int Multiplier);

internal sealed partial class CampaignEventExecutionContext
{
    //3B2690 changes only this special narration. Its Number1 store survives
    //into subsequent commands; it never replaces Number0 or dialogue Force.
    internal int PrepareNarration(int messageId, ref int?[]? numbers)
    {
        if (messageId != 0x87F3) return messageId;
        var bonus = inputs.ReadNarrationExperienceBonus?.Invoke()
            ?? throw Missing("narration_global_player_experience_bonus");
        if (!bonus.Present) return messageId;
        if (numbers is not { Length: 5 } || numbers[0] is not int basis)
            throw Missing("narration_experience_Number0");
        //The source performs both signed32 products before truncating /100.
        //Overflow remains the original integer arithmetic, not a float quote.
        numbers[1] = unchecked(unchecked(basis * bonus.Multiplier) * bonus.Percentage) / 100;
        return 0x87F4;
    }
}
