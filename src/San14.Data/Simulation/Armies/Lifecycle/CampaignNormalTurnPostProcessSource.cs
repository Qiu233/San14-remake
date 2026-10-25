using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private LoadedPolicies? normalTurnPostProcessPolicies;

    //429F30/3257F0. All handlers use this one current and its live list pool.
    //The domain owns real officers; zero-identity unused storage has no Person
    //values to publish. Named dead/free officers remain ordinary participants.
    private sealed class NormalTurnPostProcessSource(ArmyCampaignRuleAdapter package,
        GameState state, SourceWorldContext current, WorldIndexContext pool)
    {
        private readonly List<PersonAssignmentPrecallResult> assignments = [];
        private readonly List<CampaignEffectRequest> effects = [];
        private readonly HashSet<int> ownedPeople = [];

        internal GameState Run()
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw Missing("shared_current_pool");
            PersonReturnCommands.BindCurrent(state, current);
            package.PrepareNormalTurnBaseDistrictPostProcessCurrent(state, current);
            package._personAssignmentPrecallInputs.BindCurrent(state, current);
            DeploymentTacticCurrentInputs.BindCurrent(state, current);
            //Effect vectors are fixed resource definitions. This constructs the
            //operation's definition view, not a fresh/reset campaign owner.
            package.normalTurnPostProcessPolicies ??= package._repository.LoadPolicies(package._scenario.FixedDataSource.SourcePath);
            current.InitializePolicyEffectStorage();
            current.MaterializeFixedPolicyEffects(package.normalTurnPostProcessPolicies);
            BindPeople();
            var previousTransfer = current.DistrictTransferHandler;
            var previousRefresh = current.TransferServiceRefresher;
            current.DistrictTransferHandler = TransferPerson;
            current.TransferServiceRefresher = (a, b, c) =>
            {
                if (a != 1 || b != 1 || c != 0) throw Missing("transfer_service_arguments");
                state = package.PrepareApplicationServiceCurrent(state, current, service =>
                {
                    TransferService.Refresh(current, service);
                    return service;
                });
            };
            try { _ = current.ApplyOwnedTurnPostProcess(ownedPeople); }
            finally
            {
                current.DistrictTransferHandler = previousTransfer;
                current.TransferServiceRefresher = previousRefresh;
            }
            state = package.PublishNormalTurnBaseDistrictPostProcessCurrent(state, current);
            state = PublishNormalTurnCurrent(state, current, pool);
            var people = state.Officers.ToDictionary(x => x.Key, x => x.Value);
            foreach (var person in state.Officers.Values)
            {
                int physical = Physical(person.Id);
                int Read(int offset) => current.ReadPersonPostLoadByte(physical, offset)
                    ?? throw Missing("Person_field_" + offset.ToString("X"));
                int DecodeCap(int offset)
                {
                    byte raw = (byte)Read(offset);
                    return (byte)(((raw << 3) | (raw >> 5)) ^ unchecked((byte)physical));
                }
                people[person.Id] = person with
                {
                    BaseAbilities = new(Read(0x124), Read(0x125), Read(0x126), Read(0x127), Read(0x128)),
                    PolicyBaseCost = Read(0x1BA),
                    CurrentFormationMask = current.ReadPersonFormationMask164(physical),
                    AbilityState = new(DecodeCap(0x224), DecodeCap(0x225), person.AbilityState?.ObservedDoctrineId)
                };
            }
            state = state with { Officers = people, NativeNormalTurnContinuation = null };
            if (effects.Count != 0)
            {
                var owner = state.CampaignEffectRequests ?? throw Missing("effects_owner");
                state = state with { CampaignEffectRequests = owner.Append(CampaignEffectSource.MonthlyMaintenance,
                    state.NativeClock?.UpdateOrdinal ?? throw Missing("effects_revision"), effects) };
            }
            return state;
        }

        private void BindPeople()
        {
            foreach (var person in state.Officers.Values)
            {
                int physical = Physical(person.Id);
                ownedPeople.Add(physical);
                //These authoritative values may have changed during the Turn.
                //No opening-scenario value substitutes for an unknown old save.
                var abilities = person.BaseAbilities ?? throw Missing("Person_current_abilities");
                int[] values = [abilities.Leadership, abilities.Strength, abilities.Intelligence, abilities.Politics, abilities.Charm];
                for (int i = 0; i < 5; i++) current.SetPersonPostLoadByte(physical, 0x124 + i, checked((byte)values[i]));
                current.SetPersonPostLoadByte(physical, 0x1BA, person.PolicyBaseCost is int cost ? checked((byte)cost) : null);
                current.SetPersonFormationMask164(physical, person.CurrentFormationMask);
                for (int i = 0; i < 9; i++) current.SetPersonTraitSlot150(physical, i,
                    person.CurrentTraitIds is { Length: 9 } traits ? checked((ushort)traits[i]) : null);
                for (int i = 0; i < 10; i++) current.SetPersonTacticSlot168(physical, i,
                    person.CurrentTacticSlots is { Length: 10 } tactics ? tactics[i] : null);
            }
        }

        private void TransferPerson(int person, int district)
        {
            var transfer = ArmyOrdinaryReturnRuntime.CreateCurrentPersonDistrictTransfer(current, assignments,
                result => state = package.ApplyPersonAssociationRemovalReports(state, current, person, result,
                    () => package.ReadArmyDestructionSampler(state, current)?.Day450 ?? throw Missing("assignment_day450"), effects));
            int first = assignments.Count;
            PersonDistrictTransfer.Apply(person, district, new Slot7DistrictTransfer(current, transfer));
            state = PersonAssignmentPrecallInputs.PublishDeathTouched(state, current, person,
                assignments.Skip(first).ToArray());
        }

        private int Physical(int id) => current.ResolveOfficerSource(id) ?? throw Missing("Person_identity");
        private static InvalidDataException Missing(string field) => new("normal-turn.postprocess." + field);
    }

    //Keep the complete association cleanup adapter; only service publication
    //belongs to the enclosing candidate/profile transaction rather than its old
    //ordinary-return adapter's historical service frontier.
    private sealed class Slot7DistrictTransfer(SourceWorldContext current, IPersonDistrictTransferState inner)
        : IPersonDistrictTransferState
    {
        public byte? ReadPersonDistrictSlot(int person) => inner.ReadPersonDistrictSlot(person);
        public ForceReference? ReadPersonForce(int person) => inner.ReadPersonForce(person);
        public ForceReference? ReadRequestedDistrictForce(int district) => inner.ReadRequestedDistrictForce(district);
        public void RemoveForcePersonAssociation(ForceReference force, int person) => inner.RemoveForcePersonAssociation(force, person);
        public int? ReadForcePhysicalSlot(ForceReference force) => inner.ReadForcePhysicalSlot(force);
        public void WritePersonResetWord1FF(int person, ushort value) => inner.WritePersonResetWord1FF(person, value);
        public void WritePersonResetByte1FE(int person, byte value) => inner.WritePersonResetByte1FE(person, value);
        public void WritePersonDistrictSlot(int person, byte value) => inner.WritePersonDistrictSlot(person, value);
        public byte? ReadDistrictForceWithoutValidity(int district) => inner.ReadDistrictForceWithoutValidity(district);
        public ForceReference? ReadCurrentPlayerForce() => inner.ReadCurrentPlayerForce();
        public void RefreshTransferService(int a, int b, int c) => current.RefreshTransferService(a, b, c);
    }
}
