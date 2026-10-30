using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Actual edit callers' current pointer-table order. This is a
    /// menu preparation, never a frame query or a re-enumerated draft owner.</summary>
    public AreaAdministrationQuery<IReadOnlyList<CurrentWorldEditTarget>> QueryCurrentWorldEditTargets(
        GameState state, CurrentWorldEditKind kind)
    {
        try
        {
            var current = OwnedArmyReturnCurrentFields.CaptureCurrent(state, _definitions);
            if (kind == CurrentWorldEditKind.Force)
            {
                //Nullable Core ruler IDs need their actual saved vacancy or
                //reset image before the original 52-slot native predicate.
                ForceUnregisterInputs.BindCurrent(state, current);
                OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
            }
            var ids = new List<int>();
            InformationListKind list;
            switch (kind)
            {
                case CurrentWorldEditKind.City:
                    list = InformationListKind.Cities;
                    for (int slot = 0; slot < 52; slot++)
                    {
                        var city = current.ReadCityTableReference(slot) ?? throw WorldEditMissing("targets_city_pointer");
                        if (!WorldEditNeed(current.ReadBaseNativeValidity(city), "targets_city_validity")) continue;
                        int physical = WorldEditNeed(city.PhysicalSlot, "targets_city_identity");
                        if (physical is >= 47 and <= 51) continue;
                        if (!state.Cities.ContainsKey(physical)) throw WorldEditMissing("targets_city_owner");
                        ids.Add(physical);
                    }
                    break;
                case CurrentWorldEditKind.Area:
                    list = InformationListKind.Areas;
                    for (int slot = 0; slot < 501; slot++)
                    {
                        var area = current.ReadAreaTableReference(slot) ?? throw WorldEditMissing("targets_area_pointer");
                        if (!WorldEditNeed(current.ReadAreaNativeValidity(area), "targets_area_validity")) continue;
                        int physical = WorldEditNeed(area.PhysicalSlot, "targets_area_identity");
                        if (physical is >= 339 and <= 343) continue;
                        if (!state.Areas.ContainsKey(physical)) throw WorldEditMissing("targets_area_owner");
                        ids.Add(physical);
                    }
                    break;
                case CurrentWorldEditKind.Force:
                    list = InformationListKind.Forces;
                    var later = new List<int>();
                    for (int slot = 0; slot < 52; slot++)
                    {
                        var force = current.ReadForceTableReference(slot) ?? throw WorldEditMissing("targets_force_pointer");
                        if (!WorldEditNeed(current.ReadForceNativeValidity(force), "targets_force_validity")) continue;
                        int physical = WorldEditNeed(current.ReadForcePhysicalSlot(force), "targets_force_identity");
                        if (physical is >= 46 and <= 50) continue;
                        ushort rulerId = WorldEditNeed(current.ReadForceRecord(physical)?.RawRuler10, "targets_force_ruler");
                        var person = (rulerId <= 6000 ? current.ReadPersonLookupReference(rulerId)
                            : current.ReadTransferPersonTableReference(0)) ?? throw WorldEditMissing("targets_ruler_pointer");
                        int ruler = WorldEditNeed(current.ReadPersonIdentity(WorldEditNeed(person.PhysicalSlot, "targets_ruler_physical")), "targets_ruler_identity");
                        if (!state.Forces.ContainsKey(physical)) throw WorldEditMissing("targets_force_owner");
                        //6178D0 concatenates a second retained list, preserving
                        //the source table order within both partitions.
                        (ruler is >= 3001 and <= 3150 or >= 2001 and <= 2088 or >= 5101 and <= 5200 ? later : ids).Add(physical);
                    }
                    ids.AddRange(later);
                    break;
                default: throw WorldEditMissing("targets_kind");
            }
            //One existing name projection for the complete retained list.
            //No per-row detail pages, income queries, sorting or World copies.
            var names = QueryInformationDetailSelection(state, list, ids);
            var result = new CurrentWorldEditTarget[names.Count];
            for (int index = 0; index < names.Count; index++)
            {
                var name = names[index];
                if (name.Caption is null) throw WorldEditMissing(name.UnresolvedOwner ?? "targets_caption");
                result[index] = new(name.Id, name.Caption);
            }
            return AreaAdministrationQuery<IReadOnlyList<CurrentWorldEditTarget>>.Known(result);
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<IReadOnlyList<CurrentWorldEditTarget>>.Unknown(error.Message); }
    }
}
