using San14.Core;

namespace San14.Data;

public enum OriginalBattleRemakeCurrentCityPersonDamageOutcome { StatusExcluded, PositionBusy, Eligible }
public sealed record OriginalBattleRemakeCurrentCityPersonDamageReceipt(int PhysicalSlot, ushort PersonId,
    ushort FirstLocationE2, byte StatusE6, ushort? SecondLocationE4,
    OriginalBattleRemakeCurrentCityPersonDamageOutcome Outcome, bool? NativePersonValid, int? Effect68Percent);
public sealed record OriginalBattleRemakeCurrentAttackingCityReceipt(OriginalBaseReference City,
    ushort ObjectLink4E, int NativeForceSlot, int[] RegisteredPersonOrder100,
    OriginalBattleRemakeCurrentCityPersonDamageReceipt[] OrderedCityPeople,
    OriginalAttackingCityDamageSourceInput Input, OriginalStateAdvantageSourceResult Advantage3,
    string Revision);
public sealed record OriginalBattleRemakeCurrentAttackingCityResult(
    OriginalBattleRemakeCurrentAttackingCityReceipt? Receipt, string? UnresolvedOwner);

/// <summary>TC16A260's attacking-City modifier inputs. Person order comes
/// from this operation's saved manager100, never Base88 or Officers iteration.
/// Policy21 is the current41D690 numeric aggregation; its complete policy
/// temporary-pool operation is outside this read producer. Advantage3 uses
/// the caller's same writable source context and retains its real temporary
/// pool effects there. The GameState is not modified or published.</summary>
public static class OriginalBattleRemakeCurrentAttackingCityModifiers
{
    public static OriginalBattleRemakeCurrentAttackingCityResult Resolve(GameState state,
        OriginalEventInitializationContext current, OriginalBaseReference actualCity, string revision)
    {
        if (string.IsNullOrWhiteSpace(revision)) throw new InvalidDataException("Current City modifiers need a revision.");
        if (state.NativeArmyLifecycle is not { } saved || state.NativeArmySimulation?.Parameters is not { } parameters ||
            parameters.Language != saved.Language || parameters.ExecutableSha256 != saved.ExecutableSha256 ||
            current.StaticSource is not { } source || source.ResourceUri != saved.SourceScenarioUri ||
            source.Sha256 != saved.SourceScenarioSha256 || state.ContentHashes.GetValueOrDefault(source.ResourceUri) != source.Sha256)
            return Missing("current.City_modifiers_source_binding");
        if (actualCity.Kind != OriginalBaseKind.City || actualCity.PhysicalSlot is not >= 1 or > 51 ||
            !state.Cities.ContainsKey(actualCity.PhysicalSlot.Value) || current.ReadBaseNativeValidity(actualCity) != true ||
            current.ReadBaseObjectLink(actualCity) is not ushort link || link is < 1 or > 51 ||
            current.ReadBaseForceSlot(actualCity) is not int forceId || forceId is < 1 or > 45 ||
            current.ReadForceTableReference(forceId) is not { } force || current.ReadForceNativeValidity(force) != true ||
            !state.Forces.TryGetValue(forceId, out var ownedForce)) return Missing("current.ordinary_valid_City_Force");
        //Identity is the actual manager owner; the caller captures/imports it
        //once for an operation. This producer never imports the81k arena.
        if (saved.RegistryDescriptors.GetValueOrDefault("Person")?.Identity is not int personDescriptor ||
            current.ReadRegistryDescriptor(OriginalRegistryKind.Person)?.Identity != personDescriptor ||
            current.ReadOwnedCurrentListPool() is not { } pool || pool.DescriptorCapacity != saved.Pool.DescriptorCapacity)
            return Missing("current.saved_Person_manager100_and_primary_pool");
        try
        {
            int? policy21 = OriginalBattleRemakeCurrentForcePolicies.ReadForceEffect(state, ownedForce, 21, current);
            if (policy21 is null) return Missing("current.Force_policy21_41D690_numeric");
            var chain = current.ReadRegisteredPersonPhysicalSlots();
            if (chain is null) return Missing("current.Person_manager100_chain");
            var order = new List<int>(); var people = new List<OriginalBattleRemakeCurrentCityPersonDamageReceipt>();
            var percentages = new List<int>();
            foreach (int? entry in chain)
            {
                if (entry is not int physical) return Missing("current.Person_manager100_payload");
                order.Add(physical);
                ushort? first = current.ReadPersonFirstLocation(physical);
                if (first is null) return Missing($"current.Person[{physical}].E2");
                if (first != link) continue;
                if (current.ReadPersonIdentity(physical) is not ushort personId || current.ReadPersonStatus(physical) is not byte status)
                    return Missing($"current.Person[{physical}].ID10_StatusE6");
                //3346B0 calls232F10(flag0), a status1..4 gate. It has no
                //native Person validity or per-person Force equality guard.
                if (status is < 1 or > 4)
                { Add(OriginalBattleRemakeCurrentCityPersonDamageOutcome.StatusExcluded); continue; }
                if (current.ReadPersonSecondLocation(physical) is not ushort second)
                    return Missing($"current.Person[{physical}].E4");
                if (OriginalPersonPositionRepair.DecodeFirstLocation(first.Value) != OriginalPersonPositionRepair.DecodeSecondLocation(second))
                { Add(OriginalBattleRemakeCurrentCityPersonDamageOutcome.PositionBusy, second); continue; }
                var associated = current.ReadPersonLinkedEntity(physical);
                if (associated is null || current.ReadLinkedEntityNativeValidity(associated) is not bool linkedValid)
                    return Missing($"current.Person[{physical}].associated22AC00");
                if (linkedValid)
                {
                    var command = associated.Identity is int receiver ? current.ReadOwnedPersonCommandManager()?.Commands.GetValueOrDefault(receiver) : null;
                    //The implemented Return's actual v98 is15, not16. Other
                    //valid command classes need their own current producer.
                    if (command?.Kind != "return") return Missing($"current.Person[{physical}].associated_virtual98");
                }
                if (current.ReadPersonNativeValidity(physical) is not bool valid)
                    return Missing($"current.Person[{physical}].native_validity_2B3170");
                int? effect = valid ? OriginalTacticRemakeCurrentTraits.CommanderEffectSum(current, physical, 0x44) : 0;
                if (effect is null) return Missing($"current.Person[{physical}].effect68_2B3170");
                percentages.Add(effect.Value);
                Add(OriginalBattleRemakeCurrentCityPersonDamageOutcome.Eligible, second, valid, effect.Value);

                void Add(OriginalBattleRemakeCurrentCityPersonDamageOutcome outcome, ushort? secondField = null,
                    bool? nativeValid = null, int? effectField = null) => people.Add(new(physical, personId, first.Value,
                        status, secondField, outcome, nativeValid, effectField));
            }
            var advantage = OriginalPersonReturnPathSource.ReadForceAdvantage(state, current, force, 3, 0);
            if (advantage.Value is not int query3) return Missing(advantage.UnresolvedOwner ?? "current.Force_advantage3_2ADBC0");
            //Keep every raw signed percentage in native order. The damage
            //consumer must classify raw signs and max the WRAPPED scaled
            //deltas; collapsing percentages here loses that source behavior.
            var input = new OriginalAttackingCityDamageSourceInput(policy21.Value, percentages.ToArray(), query3);
            return new(new(actualCity, link, forceId, order.ToArray(), people.ToArray(), input, advantage,
                $"{revision}:{parameters.Revision}"), null);
        }
        catch (InvalidDataException missing) { return Missing(missing.Message); }
    }
    private static OriginalBattleRemakeCurrentAttackingCityResult Missing(string field) => new(null, field);
}
