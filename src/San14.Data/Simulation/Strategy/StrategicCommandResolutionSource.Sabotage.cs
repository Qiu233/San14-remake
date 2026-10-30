using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class StrategicResolutionCurrent
    {
        private int ResolveSabotage()
        {
            int executor = Executor, area = Target;
            var own = Force(executor); int ownId = ForceId(own), ownRuler = Ruler(own);
            int result = Trial();
            //Unlike the other stored-result consumers,1F9F70 does not rerun
            //a blocked predicate after the retained result overrides the trial.
            if (task.SavedResult != -1) result = task.SavedResult;
            var objects = package.ReadStrategicSabotageObjectsCurrent(State, current, area, ownId, 2).ToArray();
            if (objects.Length == 0) return 0;
            UnitSelectionGroupSourceSort.Sort(objects, result == 0 ? FailureBefore : SuccessBefore);
            var target = objects[0];
            var other = ObjectForce(target); int otherRuler = Ruler(other);
            ObjectReference? trap = null;
            if (result == 2)
            {
                var traps = package.ReadStrategicSabotageObjectsCurrent(State, current, area, ownId, 4)
                    .Where(obj => StrategyNeed(current.ReadObjectNativeValidity(obj), "sabotage_trap_validity")).ToArray();
                if (traps.Length != 0)
                {
                    UnitSelectionGroupSourceSort.Sort(traps, TrapBefore);
                    trap = traps[0];
                }
                if (trap is null || !StrategyNeed(current.ReadObjectNativeValidity(trap), "sabotage_selected_trap_validity")) result = 1;
            }
            int before = StrategyNeed(current.ReadObjectRawField14(target), "sabotage_HP14");
            int after;
            if (result != 0)
            {
                int intelligence = Ability(executor);
                int amount = unchecked(P("sabotage-loss-base", 100) + intelligence +
                    unchecked(P(result == 2 ? "sabotage-great-intelligence-percent" : "sabotage-intelligence-percent", result == 2 ? 70 : 40) * intelligence) / 100 +
                    (Trait(executor, 65) > 0 ? P("sabotage-trait-loss", 5) : 0) +
                    Sample(P("sabotage-random-bound", 31), Id(executor), area));
                after = unchecked(before - Math.Min(before, amount));
            }
            else
            {
                int intelligence = Ability(executor, trait: true);
                int amount = unchecked(Sample(unchecked(P("sabotage-failed-random-base", 3) +
                    Divide(intelligence, P("sabotage-failed-intelligence-divisor", 10))), Id(executor), area) +
                    (Trait(executor, 65) > 0 ? P("sabotage-failed-trait-loss", 1) : 0) + P("sabotage-failed-loss-base", 5));
                after = unchecked(before - Math.Min(before - 1, amount));
            }
            int definition = Definition(target), secondDefinition = trap is null ? 0 : Definition(trap);
            if (Player(own))
                History(result == 0 ? 0x33FA : result == 2 ? after < 1 ? 0x33EA : 0x33EE : after < 1 ? 0x33F2 : 0x33F6,
                    new(Person0: Id(executor), Person1: Id(ownRuler), Person2: Id(otherRuler), Area0: area,
                        ObjectDefinition0: definition, ObjectDefinition1: result == 0 ? null : secondDefinition),
                    result == 0 ? 0x3400 : 0x33FF, new(Person0: Id(executor), Area0: area),
                    StrategyNeed(current.ReadPersonDistrictSlot(executor), "sabotage_DistrictE0"), Id(executor), result != 0);
            else if (Player(other))
            {
                if (result == 0)
                    History(0x3405, new(Person0: 5012, Person1: Id(otherRuler), Person2: Id(ownRuler), Area0: area, ObjectDefinition0: definition),
                        0x3407, new(Person0: Id(ownRuler), Person1: Id(otherRuler), Area0: area), Primary(other), 5012, false, sound: 6);
                else
                {
                    int intro = trap is not null ? after < 1 ? 0x3401 : 0x3402 : after < 1 ? 0x3403 : 0x3404;
                    History(intro, new(Person0: 5012, Person1: Id(otherRuler), Person2: trap is null ? Id(ownRuler) : null,
                            Area0: area, ObjectDefinition0: definition, ObjectDefinition1: secondDefinition),
                        0x3406, new(Person0: Id(ownRuler), Person1: Id(otherRuler), Area0: area), Primary(other), 5012, false,
                        conditional: true, sound: trap is not null ? 7 : 6);
                }
            }
            if (trap is not null && StrategyNeed(current.ReadObjectNativeValidity(trap), "sabotage_trap_remove_validity")) Remove(trap);
            if (after < 1) Remove(target);
            else
            {
                int physical = StrategyNeed(target.PhysicalSlot, "sabotage_HP_receiver");
                if (physical is >= 1 and <= 51) throw StrategyMissing("sabotage_City_HP_cap1F4440");
                ushort stored = (ushort)Math.Clamp(after, 0, 9999);
                current.WriteObjectPostLoadField14(target, stored);
                State = PublishCurrentObjectDurability(State, current, target, stored);
            }
            if (result == 0) Relations(own, other, -P("sabotage-detection-relation-loss", 5));
            //1F9F70's actual domain completion writes bitmap byte4 bit6.
            //It is distinct from closing the ordinary report window.
            if (package.ReadApplicationTutorialActive() is not bool tutorial) throw StrategyMissing("sabotage_tutorial_service");
            if (tutorial)
            {
                var app = package.ReadApplicationGameplay() ?? throw StrategyMissing("sabotage_Application");
                if (app.TutorialServicePresent is not bool present) throw StrategyMissing("sabotage_tutorial_service_presence");
                if (present)
                {
                    bool bitmap = TacticRemakeCurrentBattleControl.ReadBitmapPresence(package.ReadTutorialControlState(State))
                        ?? throw StrategyMissing("sabotage_tutorial_bitmap_presence");
                    if (bitmap) State = package.SetTutorialLessonEventCurrent(State, 38, true);
                }
            }
            return result;

            ForceReference ObjectForce(ObjectReference obj) => ForceAt(StrategyNeed(current.ReadObjectForceSlot13(obj), "sabotage_Object13"));
            int ObjectOrdinal(ObjectReference obj) => StrategyNeed(obj.PhysicalSlot, "sabotage_Object_ordinal");
            int Definition(ObjectReference obj)
            {
                byte kind = current.ReadPhysicalObject(ObjectOrdinal(obj))?.RawField12 ?? throw StrategyMissing("sabotage_Object12");
                return kind <= 30 ? kind : 0;
            }
            int Hp(ObjectReference obj) => StrategyNeed(current.ReadObjectRawField14(obj), "sabotage_sort_HP14");
            byte Relation(ObjectReference obj)
            {
                int id = ForceId(ObjectForce(obj));
                if (id >= 52) return 0;
                if (current.TryReadRulerReleaseRelation13(ownId, id, out var reached))
                    return StrategyNeed(reached, "sabotage_sort_current_relation13");
                if (State.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(ownId, out var reset) == true)
                    return StrategyNeed(reset[0x13 + id], "sabotage_sort_reset_relation13");
                return State.NativeArmySimulation?.Forces.GetValueOrDefault(ownId)?.NormalAftermath?.Relations13 is { Length: 52 } values
                    ? values[id] : throw StrategyMissing("sabotage_sort_relation13");
            }
            bool Borders(ObjectReference obj) => package.StrategicForcesBorderCurrent(State, current, ownId, ForceId(ObjectForce(obj)));
            bool FailureBefore(ObjectReference first, ObjectReference second) => Hp(first) != Hp(second)
                ? Hp(first) > Hp(second) : ObjectOrdinal(first) < ObjectOrdinal(second);
            bool SuccessBefore(ObjectReference first, ObjectReference second)
            {
                byte a = Relation(first), b = Relation(second);
                if (a != b) return a < b;
                bool result = Borders(first); _ = Borders(second);
                return result; //1D8330 intentionally returns the first predicate.
            }
            bool TrapBefore(ObjectReference first, ObjectReference second)
            {
                byte a = Relation(first), b = Relation(second);
                if (a != b) return a < b;
                bool left = Borders(first), right = Borders(second);
                return left != right ? left : ObjectOrdinal(first) < ObjectOrdinal(second);
            }
            void Remove(ObjectReference obj)
            {
                //294FD0(null actor,report0): share the true removal writer and
                //visual/sound, without adding report1's visibility prerequisites.
                var removal = package.ApplyDemolitionNumbers(State, current, obj, new(null), reached => State = reached);
                if (removal is null) return;
                State = removal.State;
                string key = MapObjectSourceDirectory.DefinitionKey(removal.DefinitionSlot);
                Effects.Add(new(null, null) { MapObjectPresentation = new(MapObjectPresentationEvent.DestructionVisual,
                    removal.Receiver, removal.HexId, key) });
                Effects.Add(new(null, null) { MapObjectPresentation = new(MapObjectPresentationEvent.DestructionSound,
                    removal.Receiver, removal.HexId, key) });
            }
        }

        private int Sample(int bound, int a, int b, int c = 0, int d = 0)
        {
            if (bound < 2) return 0;
            var sampler = package.ReadArmyDestructionSampler(State, current) ?? throw StrategyMissing("resolution_pure_sampler");
            return TacticEffectSourceSamplerMath.Sample(bound, 1, a, b, c, d, sampler);
        }
    }
}
