using San14.Core;
using San14.Data;

namespace San14.Client.Gameplay;

/// <summary>A small, immutable projection for one accumulated header transition.
/// It reads the saved thresholds once; rendering does not query the world.</summary>
internal sealed record ForceExperienceHeaderChange(int ForceId, int CurrentExperience, int Amount,
    int? SourcePreviousLevel, int[] Thresholds)
{
    internal static bool IsGainSource(CampaignEffectSource source) => source is
        CampaignEffectSource.TalentCommands or CampaignEffectSource.PersonnelAdministrationCommands or
        CampaignEffectSource.MonthlyMaintenance;

    internal int LevelAt(int total)
    {
        int level = 0;
        for (int i = 1; i < Thresholds.Length; i++)
        { if (total < Thresholds[i]) break; level = i; }
        return level;
    }

    internal static ForceExperienceHeaderChange? Create(GameState world, int forceId)
    {
        if (OwnedForce164Inputs.ReadCurrent(world, forceId) is not int current ||
            world.NativeArmySimulation?.Parameters.Int32Values is not { } values) return null;
        var gains = (world.CampaignEffectRequests?.PendingBatches ?? [])
            .Where(batch => IsGainSource(batch.Source))
            .SelectMany(batch => batch.Requests)
            .Select(request => request.TalentForceExperiencePresentation)
            .Where(request => request is { Operation: TalentForceExperiencePresentationOperation.ExperienceGain, Amount: > 0 } &&
                request.ForceId == forceId).ToArray();
        if (gains.Length == 0) return null;
        long amount = gains.Sum(request => (long)request!.Amount);
        if (amount > int.MaxValue) return null;
        var thresholds = new int[11];
        for (int i = 0; i < thresholds.Length; i++)
            if (!values.TryGetValue(OwnedForce164Inputs.ThresholdKey(i), out thresholds[i])) return null;
        return new(forceId, current, (int)amount, gains[0]!.PreviousLevel, thresholds);
    }
}
