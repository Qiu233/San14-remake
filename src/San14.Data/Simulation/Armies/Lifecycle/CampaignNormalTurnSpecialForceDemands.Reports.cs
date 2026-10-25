using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class NormalTurnSpecialForceRaidsSource
    {
        private void PublishRaids(ForceReference player, ForceReference leader, HexListHandle candidates)
        {
            var native = state.NativeArmySimulation ?? throw Missing("raid_flag_owner");
            if (!native.Parameters.Int32Values.ContainsKey(OwnedArmyReturnServiceInputs.Flags16A8Key))
                throw Missing("raid_flag_saved_owner");
            uint flags = K(current.ReadWorldTransferServiceFlags(), "raid_World16A8") | 0x800000;
            current.RawTransferServiceFlags16A8 = flags;
            state = state with { NativeArmySimulation = native with { Parameters = native.Parameters with
                { Int32Values = new(native.Parameters.Int32Values) { [OwnedArmyReturnServiceInputs.Flags16A8Key] = unchecked((int)flags) } } } };
            int ruler = K(current.ReadMoraleWaveRuler22EAC0(player), "report_player_ruler");
            var homes = Bases(candidates).ToArray();
            effects.Add(new(null, null) { MonthlyMaintenance = new(MonthlyMaintenanceOperation.ControllerQueueEvent, 6, null, null) });
            if (homes.Length == 1)
                Speaker(Format(0x5EBA, new(Person0: 5012, Person1: ruler, Force0: Ordinal(leader),
                    Force1: K(current.ReadBaseForceSlot(homes[0]), "report_special_Force"))));
            else
            {
                Speaker(Format(0x5EBB, new(Person0: 5012, Person1: ruler, Force0: Ordinal(leader))));
                //The native list copy is destructive at its head. Keep the
                //real pool allocations even though five is the maximum set.
                var copy = Temporary();
                try
                {
                    foreach (var home in homes) Append(copy, home);
                    var forces = new List<int>();
                    for (int i = 0; i < 5; i++)
                    {
                        var head = Nodes(copy).FirstOrDefault();
                        if (head.Node is null) break;
                        forces.Add(K(current.ReadBaseForceSlot(Base(head.Value)), "report_enumerated_Force"));
                        current.RemoveFacilityHexTemporary(copy, head.Node);
                    }
                    if (homes.Length is >= 2 and <= 5)
                        Speaker(Format(0x5EBA + homes.Length, new(Person0: 5012, Person1: ruler,
                            Force0: forces.ElementAtOrDefault(0), Force1: forces.ElementAtOrDefault(1),
                            Force2: forces.Count > 2 ? forces[2] : null, Force3: forces.Count > 3 ? forces[3] : null,
                            Force4: forces.Count > 4 ? forces[4] : null)));
                }
                finally { Release(copy); }
            }
            foreach (var home in homes)
            {
                int force = K(current.ReadBaseForceSlot(home), "history_special_Force");
                string text = Format(0x5EC0, new(Person0: 5012, Person1: ruler, Force0: force, Force1: Ordinal(player)));
                string summary = Format(0x53B2, new(Person0: 5012, Force0: force));
                int district = K(current.ReadBaseDistrictSlot(home), "history_District");
                int hex = current.ReadBaseCurrentHexIndex(home);
                int day = (package.ReadArmyDestructionSampler(state, current) ?? throw Missing("history_Day450")).Day450;
                state = package.WriteHistory(state, current, new(1, district, hex, day, text, summary, 5012, MarkBit0: 1), effects);
            }
            state = state with { CampaignEffectRequests = (state.CampaignEffectRequests ?? throw Missing("effect_owner")).Append(
                CampaignEffectSource.MonthlyMaintenance, state.NativeClock?.UpdateOrdinal ?? throw Missing("effect_revision"), effects) };
        }
        private string Format(int root, HistoryMessageOperands operands)
        {
            var formatted = package.FormatNormalTurnReport(state, current, root, operands);
            state = formatted.State; return formatted.Text;
        }
        private void Speaker(string text) => effects.Add(new(null, null) { MonthlyMaintenance =
            new(MonthlyMaintenanceOperation.ConditionalSpeakerText, null, 5012, text) });
    }
}
