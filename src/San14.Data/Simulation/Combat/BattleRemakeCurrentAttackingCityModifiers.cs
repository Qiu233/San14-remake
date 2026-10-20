using San14.Core;

namespace San14.Data;

public enum BattleRemakeCurrentCityPersonDamageOutcome { StatusExcluded, PositionBusy, Eligible }
public sealed record BattleRemakeCurrentCityPersonDamageReceipt(int PhysicalSlot, ushort PersonId,
    ushort FirstLocationE2, byte StatusE6, ushort? SecondLocationE4,
    BattleRemakeCurrentCityPersonDamageOutcome Outcome, bool? NativePersonValid, int? Effect68Percent);
public sealed record BattleRemakeCurrentAttackingCityReceipt(BaseReference City,
    ushort ObjectLink4E, int NativeForceSlot, int[] RegisteredPersonOrder100,
    BattleRemakeCurrentCityPersonDamageReceipt[] OrderedCityPeople,
    AttackingCityDamageSourceInput Input, StateAdvantageSourceResult Advantage3,
    string Revision);
public sealed record BattleRemakeCurrentAttackingCityResult(
    BattleRemakeCurrentAttackingCityReceipt? Receipt, string? UnresolvedOwner);

/// <summary>TC16A260's attacking-City modifier inputs. Person order comes
/// from this operation's saved manager100, never Base88 or Officers iteration.
/// Policy21 is the current41D690 numeric aggregation; its complete policy
/// temporary-pool operation is outside this read producer. Advantage3 uses
/// the caller's same writable source context and retains its real temporary
/// pool effects there. The GameState is not modified or published.</summary>
public static class BattleRemakeCurrentAttackingCityModifiers
{
    public static BattleRemakeCurrentAttackingCityResult Resolve(GameState state,
        SourceWorldContext current, BaseReference actualCity, string revision)
    {
        if (string.IsNullOrWhiteSpace(revision)) throw new InvalidDataException("Current City modifiers need a revision.");
        if (state.ReadArmyLifecycle() is not { } saved || state.NativeArmySimulation?.Parameters is not { } parameters ||
            parameters.Language != saved.Language || parameters.RuleVersion != saved.RuleVersion ||
            current.StaticSource is not { } source || source.ResourceUri != saved.SourceScenarioUri ||
            source.Sha256 != saved.SourceScenarioSha256 || state.ContentHashes.GetValueOrDefault(source.ResourceUri) != source.Sha256)
            return Missing("current.City_modifiers_source_binding");
        if (actualCity.Kind != BaseKind.City || actualCity.PhysicalSlot is not >= 1 or > 51 ||
            !state.Cities.ContainsKey(actualCity.PhysicalSlot.Value) || current.ReadBaseNativeValidity(actualCity) != true ||
            current.ReadBaseObjectLink(actualCity) is not ushort link || link is < 1 or > 51 ||
            current.ReadBaseForceSlot(actualCity) is not int forceId || forceId is < 1 or > 45 ||
            current.ReadForceTableReference(forceId) is not { } force || current.ReadForceNativeValidity(force) != true ||
            !state.Forces.TryGetValue(forceId, out var ownedForce)) return Missing("current.ordinary_valid_City_Force");
        //Identity is the actual manager owner; the caller captures/imports it
        //once for an operation. This producer never imports the81k arena.
        if (saved.Indexes.Registries.GetValueOrDefault(WorldEntityKind.Officer) is null ||
            current.ReadRegistryDescriptor(RegistryKind.Person) is null || current.OwnedIndexes is null)
            return Missing("current.Officer_index_binding");
        try
        {
            int? policy21 = BattleRemakeCurrentForcePolicies.ReadForceEffect(state, ownedForce, 21, current);
            if (policy21 is null) return Missing("current.Force_policy21_41D690_numeric");
            var chain = current.ReadRegisteredPersonPhysicalSlots();
            if (chain is null) return Missing("current.Person_manager100_chain");
            var order = new List<int>(); var people = new List<BattleRemakeCurrentCityPersonDamageReceipt>();
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
                { Add(BattleRemakeCurrentCityPersonDamageOutcome.StatusExcluded); continue; }
                if (current.ReadPersonSecondLocation(physical) is not ushort second)
                    return Missing($"current.Person[{physical}].E4");
                if (PersonPositionRepair.DecodeFirstLocation(first.Value) != PersonPositionRepair.DecodeSecondLocation(second))
                { Add(BattleRemakeCurrentCityPersonDamageOutcome.PositionBusy, second); continue; }
                var associated = current.ReadPersonLinkedEntity(physical);
                if (associated is null || current.ReadLinkedEntityNativeValidity(associated) is not bool linkedValid)
                    return Missing($"current.Person[{physical}].associated22AC00");
                if (linkedValid)
                {
                    var command = associated.Identity is int receiver ? current.ReadOwnedPersonCommandManager()?.Returns.GetValueOrDefault(receiver) : null;
                    //The implemented Return's actual v98 is15, not16. Other
                    //valid command classes need their own current producer.
                    if (command is null) return Missing($"current.Person[{physical}].associated_virtual98");
                }
                if (current.ReadPersonNativeValidity(physical) is not bool valid)
                    return Missing($"current.Person[{physical}].native_validity_2B3170");
                int? effect = valid ? TacticRemakeCurrentTraits.CommanderEffectSum(current, physical, 0x44) : 0;
                if (effect is null) return Missing($"current.Person[{physical}].effect68_2B3170");
                percentages.Add(effect.Value);
                Add(BattleRemakeCurrentCityPersonDamageOutcome.Eligible, second, valid, effect.Value);

                void Add(BattleRemakeCurrentCityPersonDamageOutcome outcome, ushort? secondField = null,
                    bool? nativeValid = null, int? effectField = null) => people.Add(new(physical, personId, first.Value,
                        status, secondField, outcome, nativeValid, effectField));
            }
            var advantage = PersonReturnPathSource.ReadForceAdvantage(state, current, force, 3, 0);
            if (advantage.Value is not int query3) return Missing(advantage.UnresolvedOwner ?? "current.Force_advantage3_2ADBC0");
            //Keep every raw signed percentage in native order. The damage
            //consumer must classify raw signs and max the WRAPPED scaled
            //deltas; collapsing percentages here loses that source behavior.
            var input = new AttackingCityDamageSourceInput(policy21.Value, percentages.ToArray(), query3);
            return new(new(actualCity, link, forceId, order.ToArray(), people.ToArray(), input, advantage,
                $"{revision}:{parameters.Revision}"), null);
        }
        catch (InvalidDataException missing) { return Missing(missing.Message); }
    }
    private static BattleRemakeCurrentAttackingCityResult Missing(string field) => new(null, field);
}
