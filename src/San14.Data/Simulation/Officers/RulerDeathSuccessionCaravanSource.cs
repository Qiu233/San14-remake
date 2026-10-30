using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal sealed partial class PersonDeathSource
    {
        //28C5D0, after28DFB0 transferred the successor and before24E110
        //sorts retained persons. Keep the old command identity across the
        //return initializer: that initializer installs a new associated task.
        private void CancelRulerSuccessorCaravanCurrent(int successor, ForceReference originalForce,
            PersonLinkedEntityReference associated)
        {
            int commandId = Known(associated.Identity, "successor_caravan_command_identity");
            var manager = current.ReadOwnedPersonCommandManager() ?? throw Missing("successor_caravan_manager");
            var task = manager.Caravans?.GetValueOrDefault(commandId)
                ?? throw Missing("successor_caravan_current_owner");
            if (IsPlayer(originalForce))
            {
                //2299C0 supplies the associated Country(kind8), whose normal
                //name becomes String0 after the message manager's reset.
                string country = package.ReadForeignTradeCountry(task.CountryId).Name;
                var message = package.FormatForeignTradeMessage(latest, current, 0x32FE,
                    new(String0: country));
                latest = message.State;
                var district = ForcePrimaryDistrictSource.Resolve(current, originalForce);
                var effects = new List<CampaignEffectRequest>();
                latest = package.WriteHistory(latest, current, new(0,
                    Known(district.PhysicalSlot, "successor_caravan_history_District"), 0,
                    DeathHistoryDay(), message.Text, string.Empty, Identity(successor)), effects);
                AppendRulerSuccessionEffects(effects);
            }
            var date = latest.NativeMapSeason?.ThirdDate ?? throw Missing("successor_caravan_current_date");
            latest = PersonReturnCommands.CancelAssociatedCommandCurrent(latest, current, commandId, date, package);
            //335CD0's virtual110(...,1) immediately consumes this old receiver;
            //it is not the manager's later param0 retirement-queue cut.
            latest = PersonReturnCommands.RetireAssociatedCommandImmediatelyCurrent(latest, current, commandId);
        }
    }
}
