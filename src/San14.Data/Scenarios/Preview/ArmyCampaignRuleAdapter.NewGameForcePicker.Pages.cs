using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class ForceInformationCurrent
    {
        private readonly bool titleForcePicker;

        /// <summary>The footer14 caller borrows one actual static-load context.
        /// Source World3A resolves the observer; selected UI Force is unrelated.</summary>
        internal ForceInformationCurrent(ArmyCampaignRuleAdapter rules,GameState state,
            SourceWorldContext current,StartupWorldReference sourceWorld)
        {
            this.rules=rules;this.state=state;
            byte raw=current.ReadWorldForceSlot3A(sourceWorld)
                ??throw new InvalidDataException("new-game.force-picker.static-World3A");
            var observer=current.ReadForceTableReference(raw<=51?raw:0)
                ??throw new InvalidDataException("new-game.force-picker.static-observer-pointer");
            playerForceId=current.ReadForcePhysicalSlot(observer)
                ??throw new InvalidDataException("new-game.force-picker.static-observer-identity");
            //This is an operation-local projection of the actual static field,
            //not a player selection, native simulation epoch or World mutation.
            current.CurrentPlayerForce=observer;
            captured=new(()=>current);titleForcePicker=true;
        }
    }
}
