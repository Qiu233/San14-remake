using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class EditedEventResultsCurrent
    {
        private int ChangeLord(IReadOnlyList<int> a)
        {
            var force = EditedForce(a[0]);
            int next = EditedForcePerson(a[1]), old = EditedForcePerson(a[0]);
            if (!Need(current.ReadPersonNativeValidity(old), "lord_old_validity") || !ForceValid(force) ||
                !Need(current.ReadPersonNativeValidity(next), "lord_new_validity")) return 0;
            ushort identity = Need(current.ReadPersonIdentity(next), "lord_new10");
            ushort home = Need(current.ReadPersonFirstLocation(next), "lord_newE2");
            int affiliation = DuelAftermathForce(current, next);
            byte status = Need(current.ReadPersonStatus(next), "lord_newE6");
            if (unchecked((ushort)(identity - 4001)) <= 1099 ||
                (home is < 1 or > 61) && affiliation is >= 46 and <= 50 || DuelAftermathSpecialPerson(current, next) ||
                (Need(current.ReadOwnedReturnPersonFlags(next), "lord_new15E") & 0x4000) != 0 || status is 6 or 7 or 9 ||
                Need(current.ReadPersonStatus(old), "lord_oldE6") != 1) return 0;
            if (affiliation != ForceSlot(force)) return 0;
            int own = ForceSlot(force);
            byte originalKingdom = Need(current.ReadForceKingdomNameIndex(own), "lord_original_Force48");
            var names = rules.ReadEmpireNameResourcesCurrent().CustomNames.Definitions;
            int requested = a[2];
            int definition = requested < 101 ? requested : 0;
            if (definition < 0) throw Missing("lord_negative_kingdom_definition");
            bool available = true;
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Force) ?? throw Missing("lord_ForceC0"))
                if (Need(current.ReadForceKingdomNameIndex(Need(entry?.PhysicalSlot, "lord_Force_payload")), "lord_Force48") ==
                    names[definition].PhysicalSlot) available = false;
            if (requested is >= 1 and <= 100 && requested != 43 && requested is not (>= 38 and <= 42) && available)
                State = StoreEmpireNameCurrent(State, current, own, requested);
            _ = rules.BorrowOwnedArmyReturnWorld(State, current, ForceResultPool);
            State = new PersonDeathSource(rules, State, current, ForceResultPool).ApplyEditedEventRulerTransfer(old, next);
            var death = rules.ApplyEditedEventPersonDeathTailCurrent(State, current, old);
            if (death.NativeReturn is null || death.State.PersonDeathContinuation is not null)
                throw Missing("lord_death_continuation");
            State = death.State;
            if (death.NativeReturn == 0)
            {
                //The source failure tail restores48 only; it does not undo
                //the earlier successful succession/item statements.
                State = StoreEmpireNameCurrent(State, current, own, originalKingdom);
                return 0;
            }
            //17E670 invalidates the application's Force banner textures.
            //It is not274D70 Army maintenance and is not a presentation ACK.
            State = rules.ApplyEditedEventRulerCoalitionTail(State, current, ForceResultPool, own, old, next);
            State = rules.MaintainEditedDeathArmyCommandsCurrent(State, current);
            State = PublishNormalTurnCurrent(State, current, ForceResultPool);
            return 1;
        }
    }
    private GameState ApplyEditedEventRulerCoalitionTail(GameState state, SourceWorldContext current, WorldIndexContext pool,
        int force, int oldRuler, int newRuler)
    {
        if (EditedForceNeed(current.ReadPersonStatus(newRuler), "coalition_successorE6") != 1) return state;
        var effects = new List<CampaignEffectRequest>();
        if (state.NativeForceUnregisterCurrent?.OtherForce1657 == force)
            Announce(0x2A37, EditedForceNeed(state.NativeArmySimulation?.World.SelectedForce1656, "coalition_selected"));
        if (state.NativeArmySimulation?.World.SelectedForce1656 == force) Announce(0x2A3A, force);
        return AppendEditedForceResultEffects(state, effects);
        void Announce(int root, int namedForce)
        {
            var text = FormatNormalTurnReport(state, current, root,
                new(Person0: EditedForceNeed(current.ReadPersonIdentity(oldRuler), "coalition_old_identity"), Force0: namedForce));
            state = text.State;
            effects.Add(new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.Announcement, null, null, null, null, text.Text) });
            state = DissolveCampaignCoalitionCurrent(state, current, pool, effects);
        }
    }
    internal sealed partial class PersonDeathSource
    {
        internal GameState ApplyEditedEventRulerTransfer(int oldRuler, int newRuler)
        {
            var force = Force(oldRuler);
            var retained = Temporary();
            try
            {
                //28DFB0's editor caller passes1: the existing transfer writer
                //is the shared prefix, followed immediately by26D140(...,1).
                ApplyRulerSuccessionTransfer(oldRuler, newRuler, retained);
                ApplyRulerSuccessionForceTail(force, newRuler, oldRuler);
            }
            finally { ClearRelease(retained); }
            //3BA650 changes only item flags4F and holder4A. Do not call the
            //ordinary item-transfer helper, which also changes status4E.
            foreach (var item in PersonItems(oldRuler))
            {
                ClearItemFlag3(item);
                WriteItemHolder(item, (ushort)Math.Min((int)Identity(newRuler), 6000));
            }
            return latest;
        }
    }
}
