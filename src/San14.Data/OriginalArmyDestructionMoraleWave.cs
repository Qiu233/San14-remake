using San14.Core;

namespace San14.Data;

/// <summary>Actual local1410/13F8/1400 pointer owners captured before259360
/// callbacks. Suppressed is the source's already-computed bVar2, not a UI flag.</summary>
public sealed record OriginalArmyDestructionMoraleWaveOwners(OriginalTransferPersonReference VictimPerson,
    OriginalForceReference VictimForce, OriginalForceReference WinnerForce, bool Suppressed);
public sealed record OriginalArmyDestructionMoraleWaveEffect(OriginalArmyUnitReference Army, int NativeEffectKind,
    OriginalArmyMoraleAdjustmentReceipt? Morale);
public sealed record OriginalArmyDestructionMoraleWaveResult(int? EffectCount,
    IReadOnlyList<OriginalArmyDestructionMoraleWaveEffect> Effects, int? NearbyCount, string? UnresolvedOwner);

/// <summary>25B0C9→25B56x in the caller's writable operation-local world.
/// Uses real2A78D0 radius3 and2709A0 stores. The complete destruction caller
/// discards this context on unknown/zero-routing; no GameState is scanned or
/// published here and no Core unit list replaces a native pointer chain.</summary>
public static class OriginalArmyDestructionMoraleWave
{
    public static OriginalArmyDestructionMoraleWaveResult Apply(GameState state,
        OriginalEventInitializationContext current, OriginalArmyUnitReference victim,
        OriginalArmyDestructionMoraleWaveOwners owners, OriginalArmyUnitReference? winner = null,
        OriginalBaseReference? winnerBase = null)
    {
        var effects = new List<OriginalArmyDestructionMoraleWaveEffect>(); int? nearbyCount = null;
        try
        {
            if (owners.Suppressed) return new(0, effects, 0, null);
            int person = Get(owners.VictimPerson.PhysicalSlot, "captured victim Person pointer");
            bool personValid = Get(current.ReadPersonNativeValidity(person), "captured victim Person validity");
            int? post = personValid ? Get(current.ReadMoraleWavePersonPostE7(person), "current victim PostE7") : null;
            bool postValid = post.HasValue && Get(OriginalOwnedArmyMoraleWaveInputs.ReadPostNativeValidity(state, post.Value), "current CPostData validity");
            int order = postValid ? Get(OriginalOwnedArmyMoraleWaveInputs.ReadPost37(state, post!.Value), "current CPostData37") : 0;
            bool lowPost = postValid && order <= P(0x1A4B5D8);
            bool ruler = false;
            if (!lowPost)
                ruler = Get(current.ReadPersonIdentity(person), "current victim ID10") == Get(current.ReadMoraleWaveRuler22EAC0(owners.VictimForce), "victim Force ruler22EAC0");
            if (!lowPost && !ruler) return new(0, effects, 0, null);
            var temporary = current.CreateNearbyTemporary();
            try
            {
                nearbyCount = OriginalNearbyArmyTemporarySource.FillNearbyArmyTemporary(current, victim, 3, temporary);
                bool hasWinner = winner?.PhysicalSlot.HasValue == true && Get(current.ReadArmyUnitNativeValidity(winner), "winner Army validity");
                if (!hasWinner && winnerBase?.PhysicalSlot.HasValue == true)
                    hasWinner = Get(current.ReadBaseNativeValidity(winnerBase), "winner Base validity");
                //Source tests ruler AGAIN after building the actual list,
                //even when the first OR short-circuited on a valid low Post.
                ruler = Get(current.ReadPersonIdentity(person), "repeated victim ID10") ==
                    Get(current.ReadMoraleWaveRuler22EAC0(owners.VictimForce), "repeated victim Force ruler22EAC0");
                int rankFactor = 0, postFactor = 0, postBonus = 0;
                if (ruler)
                {
                    int force = Get(current.ReadForcePhysicalSlot(owners.VictimForce), "victim Force physical");
                    byte rank = Get(current.ReadMoraleWaveForceRank47(force), "current victim Force47");
                    rankFactor = unchecked((10 - (int)Get(OriginalOwnedArmyMoraleWaveInputs.ReadRankAC(state, rank), "current RankAC")) * 2);
                }
                else
                {
                    //The captured Post pointer stays the same, but25B256
                    //reads its current byte37 again after the nearby fill.
                    int currentOrder = Get(OriginalOwnedArmyMoraleWaveInputs.ReadPost37(state, post!.Value), "repeated current CPostData37");
                    postFactor = unchecked(10 - currentOrder);
                    postBonus = Get(OriginalOwnedArmyMoraleWaveInputs.ReadPost38(state, current, post!.Value), "current CPostData38 slot9");
                }
                int factor = unchecked(postBonus + postFactor + rankFactor);
                var node = current.ReadSkillTemporaryHead(temporary) ?? throw Missing("nearby current head");
                while (node.Identity.HasValue)
                {
                    var payload = current.ReadRegistryNodePayload(new(node.Identity)) ?? throw Missing("nearby Army pointer payload");
                    if (payload.Kind != OriginalRegistryKind.Army || payload.PhysicalSlot is not int armySlot)
                        throw Missing("nearby typed Army receiver");
                    var army = new OriginalArmyUnitReference(armySlot);
                    if (winner?.PhysicalSlot != armySlot)
                    {
                        //22CD80 returns the District's raw byte10. It does not
                        //map an out-of-table byte through the Force0 fallback.
                        int force = current.ReadArmyUnitRawForceSlot(army);
                        if (hasWinner && force == Get(current.ReadForcePhysicalSlot(owners.WinnerForce), "captured winner Force physical"))
                            ApplyOne(army, Math.Min(unchecked(factor * P(0x1A4B5F0)) / 100, P(0x1A4B5E4)), 0x41);
                        else if (force == Get(current.ReadForcePhysicalSlot(owners.VictimForce), "captured victim Force physical"))
                        {
                            int effective = EffectiveMorale(army);
                            if (effective > (hasWinner ? 0 : 1))
                            {
                                int loss = Math.Min(unchecked(factor * P(0x1A4B608)) / 100, P(0x1A4B5FC));
                                if (!hasWinner) loss = Math.Min(loss, unchecked(EffectiveMorale(army) - 1));
                                ApplyOne(army, unchecked(-loss), 0x42);
                            }
                        }
                    }
                    //Callbacks precede the source's Next read. Do not prebuild
                    //an Army array, sort it or deduplicate repeated pointers.
                    node = current.ReadHexListNodeNext(node) ?? throw Missing("nearby current next");
                }
                return new(effects.Count, effects, nearbyCount, null);
            }
            finally { current.ClearNearbyTemporary(temporary, true); }

            int EffectiveMorale(OriginalArmyUnitReference army)
            {
                //22EB30 invokes current Force callbacks before the source
                //reads byte1A; those callbacks may change the same Army.
                int cap = OriginalBasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(army), current);
                return Math.Min(cap, (int)Get(current.ReadArmyReturnResourceFields(army)?.Morale1A, "nearby Army current raw1A"));
            }
            void ApplyOne(OriginalArmyUnitReference army, int amount, int nativeEffect)
            {
                var result = OriginalArmyMoraleNonzeroCurrent.Apply(state, current, army, amount);
                if (result.NativeReturn is null)
                    throw new InvalidDataException(result.UnresolvedOwner ?? "current.MoraleWave: nearby2709A0");
                effects.Add(new(army, nativeEffect, result.Receipt));
            }
            int P(int tcRva) => Get(OriginalArmyDestructionParameterSeed.ReadCurrentTc(state, tcRva), $"morale wave parameter{tcRva:X}");
        }
        catch (InvalidDataException unknown) { return new(null, effects, nearbyCount, unknown.Message); }
    }
    private static T Get<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("current.MoraleWave: " + field);
}
