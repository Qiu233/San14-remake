using San14.Core;

namespace San14.Data;

public static partial class PersonReturnPathSource
{
    /// <summary>2C6550(kind1,fourth0): match the selected magnitude first,
    /// then qualify that Province. This is not2ADBC0's aggregate value.</summary>
    internal static (bool? Value, string? UnresolvedOwner) ReadSpecialPersonnelEligibility(
        GameState state, SourceWorldContext current, ForceReference force, int special,
        ApplicationGameplayContext? application)
    {
        try
        {
            if (!Known(current.ReadForceNativeValidity(force), "2C6550 Force validity")) return (false, null);
            if (TutorialActive() && Player() && special == 46 && TutorialActive())
            {
                //570840 is an independent special-mode service. The saved
                //battle-control bitmap is not evidence for that object's byte6.
                var app = Known(application, "2C6550 application gameplay owner");
                if (Known(app.SpecialModeServicePresent, "2C6550 special-mode service presence"))
                    throw new MissingOwner("2C6550 active special-mode byte6 bit6 current owner");
            }
            var temporary = Known(current.CreateTemporaryHexList(), "2C6550 State temporary descriptor");
            try
            {
                current.ClearTemporaryHexList(temporary);
                var candidates = new List<int>();
                CollectAdvantageStates(state, current, 1, temporary, candidates);
                foreach (int province in candidates)
                {
                    int raw = AdvantageByte(state, PersonGrowthCurrentQueries.StateAdvantageKey(province));
                    var advantage = Known(current.ReadStaticTableReference(0x85990, raw < 18 ? raw : 0), "2C6550 Advantage pointer");
                    if (advantage.PhysicalSlot is not int type || type == 0) continue;
                    int selected = 0;
                    for (int index = 0; index < 2; index++)
                    {
                        if (AdvantageByte(state, PersonGrowthCurrentQueries.EffectKey(type, index)) != 1) continue;
                        selected = AdvantageByte(state, PersonGrowthCurrentQueries.MagnitudeKey(type, index));
                        break;
                    }
                    if (selected == special && QualifiedAdvantage(state, current, force, province).HasValue)
                        return (true, null);
                }
                return (false, null);
            }
            finally { current.ClearTemporaryHexList(temporary); current.ReleaseTemporaryHexList(temporary); }
        }
        catch (MissingOwner missing) { return (null, missing.Message); }

        bool TutorialActive()
        {
            var app = Known(application, "2C6550 application gameplay owner");
            return Known(app.TutorialServicePresent, "2C6550 tutorial service presence") &&
                Known(app.TutorialActive, "2C6550 tutorial current header");
        }
        bool Player()
        {
            var player = Known(OwnedCurrentPlayerForce.ReadCurrentReference(state, current), "2C6550 player pointer");
            int identity = Known(current.ReadForcePhysicalSlot(player), "2C6550 player identity");
            return identity != 0 && Known(current.ReadForcePhysicalSlot(force), "2C6550 Force identity") == identity;
        }
    }

    private static void CollectAdvantageStates(GameState state, SourceWorldContext current,
        int effect, HexListHandle temporary, List<int> candidates)
    {
        if (AdvantageInt(state, PersonGrowthCurrentQueries.VersionKey) != 1)
            throw new MissingOwner("2B5190 saved State/Advantage owner");
        if (AdvantageInt(state, PersonGrowthCurrentQueries.WorldMode40Key) is 2 or 4) return;
        for (int slot = 0; slot < 23; slot++)
        {
            var province = Known(current.ReadStaticTableReference(0xDC80, slot), "2B5190 State pointer");
            if (province.PhysicalSlot is not int physical || physical == 0) continue;
            int raw = AdvantageByte(state, PersonGrowthCurrentQueries.StateAdvantageKey(physical));
            var advantage = Known(current.ReadStaticTableReference(0x85990, raw < 18 ? raw : 0), "2B5190 Advantage pointer");
            if (advantage.PhysicalSlot is not int type || type == 0) continue;
            for (int index = 0; index < 2; index++)
            {
                if (AdvantageByte(state, PersonGrowthCurrentQueries.EffectKey(type, index)) != effect) continue;
                if (current.AppendReturnStateTemporary(temporary, physical)) candidates.Add(physical);
                break;
            }
        }
    }

    private static int? QualifiedAdvantage(GameState state, SourceWorldContext current,
        ForceReference force, int province)
    {
        int raw = AdvantageByte(state, PersonGrowthCurrentQueries.StateAdvantageKey(province));
        var advantage = Known(current.ReadStaticTableReference(0x85990, raw < 18 ? raw : 0), "2C6820 Advantage pointer");
        if (advantage.PhysicalSlot is not int type || type == 0) return null;
        int required = AdvantageByte(state, PersonGrowthCurrentQueries.RequirementKey(type));
        int identity = Known(current.ReadForcePhysicalSlot(force), "2AAEE0 Force identity");
        int count = 0;
        foreach (var entry in Known(current.ReadRegistryReferences(RegistryKind.City), "2AAEE0 City registry90"))
        {
            var city = Known(entry, "2AAEE0 City payload");
            if (city.Kind != RegistryKind.City || city.PhysicalSlot is not int cityId)
                throw new InvalidDataException("2AAEE0 City registry payload has another kind or a null identity.");
            if (!Known(current.ReadRegistryObjectNativeValidity(city), "2AAEE0 City validity")) continue;
            if (AdvantageByte(state, PersonGrowthCurrentQueries.CityStateKey(cityId)) != province) continue;
            if (Known(current.ReadBaseForceSlot(new(BaseKind.City, cityId)), "2AAEE0 current City Force") == identity) count++;
        }
        return count >= required ? type : null;
    }

    private static int AdvantageInt(GameState state, string key) => state.NativeArmySimulation?.Parameters is { } owner &&
        !owner.Float32Values.ContainsKey(key) && owner.Int32Values.TryGetValue(key, out int value)
            ? value : throw new MissingOwner("2C6550 current field " + key);
    private static int AdvantageByte(GameState state, string key)
    {
        int value = AdvantageInt(state, key);
        return value is >= 0 and <= byte.MaxValue ? value : throw new InvalidDataException("State/Advantage byte exceeds source width.");
    }
}
