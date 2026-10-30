using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed record AssociationReportTailProbe(int Slot, byte TargetForceCode, ushort PersonOperand,
    bool TargetForceValid, bool? PersonValid);
public sealed record AssociationReportTailSnapshot(bool Completed, ushort? SelectedPersonId,
    int MaximumIntelligence, ImmutableArray<AssociationReportTailProbe> ReachedAssociations);

public sealed partial class ArmyCampaignRuleAdapter
{
    public AssociationReportTailContentOperation BeginAssociationReportTailContent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, ActionReportApplicationInputs application)
        => new(this, state, current, pool, application);

    /// <summary>The complete677D00 body has no append, formatted message or
    /// saved mutation. Its real gates and current reads must still be reached.</summary>
    public sealed class AssociationReportTailContentOperation
    {
        private readonly ArmyCampaignRuleAdapter package;
        private readonly SourceWorldContext current;
        private readonly WorldIndexContext pool;
        private readonly ActionReportApplicationInputs application;
        private readonly List<AssociationReportTailProbe> associations = [];
        private bool entered, completed;
        private ushort? selected;
        private int maximum;
        public GameState State { get; }
        public AssociationReportTailSnapshot Snapshot => new(completed, selected, maximum, associations.ToImmutableArray());
        internal AssociationReportTailContentOperation(ArmyCampaignRuleAdapter package, GameState state,
            SourceWorldContext current, WorldIndexContext pool, ActionReportApplicationInputs application)
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile()) throw Missing("same_current_pool");
            this.package = package; State = state; this.current = current; this.pool = pool; this.application = application;
        }
        public AssociationReportTailSnapshot Produce(TransferPersonReference suppliedSpeaker)
        {
            if (entered) throw Missing("operation_already_entered"); entered = true;
            //570850/570840 suppression precedes the independent tutorial gate.
            if (Gate(application.SpecialModeSuppressesAssociationReport, "special_mode") ||
                Gate(application.TutorialBlocksAssociationReports, "tutorial"))
            { completed = true; return Snapshot; }
            var actor = OwnedCurrentPlayerForce.ReadCurrentReference(State, current) ?? throw Missing("World3A_Force");
            var primary = new PersonDeathHistorySelection(State, current, pool,
                new TacticRemakeCurrentAbilities(current, package._scenario, package.Seed.Language,
                    SimulationRuleIdentity.ForLocale(package.Seed.Language)), package._governorInputs,
                (force, effect) => package._baseCapacityInputs.ReadPanelModifier(State, force, effect)).PrimaryDistrict(actor);
            if (!K(current.ReadDistrictNativeValidity(primary), "primary_District_validity"))
            { completed = true; return Snapshot; }
            byte raw = K(current.ReadDistrictForceSlot(primary), "primary_District10");
            var force = current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw Missing("primary_Force");
            if (!K(current.ReadForceNativeValidity(force), "primary_Force_validity"))
            { completed = true; return Snapshot; }
            //2338E0 is the existing current-player ordinal predicate. Its
            //return is ignored here; it is not an association refresh writer.
            var freshPlayer = OwnedCurrentPlayerForce.ReadCurrentReference(State, current) ?? throw Missing("2338E0_player");
            int playerOrdinal = K(current.ReadForcePhysicalSlot(freshPlayer), "2338E0_player_virtual60");
            if (playerOrdinal != 0) _ = K(current.ReadForcePhysicalSlot(force), "2338E0_receiver_virtual60") == playerOrdinal;
            ushort chosen = 0;
            var abilities = new TacticRemakeCurrentAbilities(current, package._scenario, package.Seed.Language,
                SimulationRuleIdentity.ForLocale(package.Seed.Language));
            for (int i = 0; i < 3; i++)
            {
                var person = AssociationPerson(force, i);
                ushort identity = Identity(person, "candidate_Person10");
                if (identity != 0)
                {
                    int ability = K(abilities.ResolveDimension(State, identity, 2,
                        (f, effect) => package._baseCapacityInputs.ReadPanelModifier(State, f, effect)), "candidate_Intelligence");
                    if (maximum < ability)
                    {
                        maximum = K(abilities.ResolveDimension(State, identity, 2,
                            (f, effect) => package._baseCapacityInputs.ReadPanelModifier(State, f, effect)), "candidate_fresh_Intelligence");
                        chosen = Identity(person, "selected_Person10");
                    }
                }
            }
            if (maximum == 0 && chosen == 0) chosen = Identity(suppliedSpeaker, "fallback_speaker_Person10");
            selected = chosen;
            if (chosen != 0)
            {
                for (int i = 0; i < 3; i++)
                {
                    byte targetCode = K(current.ReadForceAssociationTargetSlot(force, i), "association_TargetForce19C");
                    var target = current.ReadForceTableReference(targetCode < 52 ? targetCode : 0) ?? throw Missing("association_TargetForce_table");
                    var person = AssociationPerson(force, i, out ushort personOperand);
                    bool valid = K(current.ReadForceNativeValidity(target), "association_TargetForce_validity");
                    bool? personValid = valid ? K(current.ReadPersonNativeValidity(P(person.PhysicalSlot, "association_Person_pointer")), "association_Person_validity") : null;
                    associations.Add(new(i, targetCode, personOperand, valid, personValid));
                }
            }
            completed = true; return Snapshot;
        }
        private TransferPersonReference AssociationPerson(ForceReference force, int index) => AssociationPerson(force, index, out _);
        private TransferPersonReference AssociationPerson(ForceReference force, int index, out ushort raw)
        {
            raw = K(current.ReadForceAssociationPersonId(force, index), "association_Person196");
            return (raw < 6001 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))
                ?? throw Missing("association_Person_lookup");
        }
        private ushort Identity(TransferPersonReference person, string field) => K(current.ReadPersonIdentity(P(person.PhysicalSlot, "Person_pointer")), field);
        private static bool Gate(Func<bool?>? predicate, string field) => predicate?.Invoke() ?? throw Missing(field);
        private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static int P(int? value, string field) => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("report.association-tail." + field);
    }
}
