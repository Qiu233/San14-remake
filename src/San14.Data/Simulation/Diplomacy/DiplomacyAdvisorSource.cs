using San14.Core;

namespace San14.Data;

internal sealed record DiplomacyAdvisorResult(GameState State, string? Text, int? AdvisorOfficerId);

public sealed partial class ArmyCampaignRuleAdapter
{
    //2F39D0 kinds4/5/6/9. Called by preparation input events, never by
    //drawing, card queries or submission. The borrowed current is page-owned.
    internal DiplomacyAdvisorResult ReadDiplomacyAdvisorCurrent(GameState state, SourceWorldContext current,
        int forceId, int executorId, int recipientId, DiplomaticMissionKind kind, int months,
        int? targetCity, DiplomacyOffer offer)
    {
        if (Seed.Language != "TC") throw DiplomacyUiSource.Missing("advisor_SC_owner");
        var force = current.ReadForceTableReference(forceId) ?? throw Missing("Force_pointer");
        var advisor = Advisor(force);
        if (!Valid(advisor)) return new(state, null, null);
        int accuracy = 0;
        if (Need(current.ReadForceNativeValidity(force), "Force_validity"))
        {
            var fresh = Advisor(force);
            if (Valid(fresh))
            {
                var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
                    SimulationRuleIdentity.ForLocale(Seed.Language));
                int id = Need(current.ReadPersonIdentity(Need(fresh.PhysicalSlot, "fresh_pointer")), "fresh_ID10");
                int first = Need(abilities.ResolveDimension(state, id, 2,
                    (f, effect) => _baseCapacityInputs.ReadPanelModifier(state, f, effect)), "first_INT");
                int second = Need(abilities.ResolveDimension(state, id, 2,
                    (f, effect) => _baseCapacityInputs.ReadPanelModifier(state, f, effect)), "second_INT");
                accuracy = Math.Clamp(unchecked(60 + Math.Max(0, first - 70) + Math.Max(0, second - 90)), 0, 100);
            }
        }
        var q = new DiplomacyQuery(this, state, current, executorId, recipientId,
            DiplomacyCommandSource.NativeKind(kind), months, targetCity);
        TacticSourceSamplerWorld Sampler() => ReadArmyDestructionSampler(state, current) ?? throw Missing("sampler");
        bool accurate = TacticEffectSourceSamplerMath.Sample(100, 10, executorId, recipientId, 0, 0, Sampler()) < accuracy;
        int advisorId = Need(current.ReadPersonIdentity(Need(advisor.PhysicalSlot, "pointer")), "ID10");
        int ownRuler = q.Id(q.Ruler(q.Own)), otherRuler = q.Id(q.Ruler(q.Other));
        int root;
        HistoryMessageOperands operands;
        if (kind == DiplomaticMissionKind.CancelAlliance)
        {
            bool trespass = false;
            foreach (var army in current.ReadRegistryReferences(RegistryKind.Army) ?? throw Missing("Army_registry60"))
            {
                int physical = Need(army?.PhysicalSlot, "Army_pointer");
                if (current.ReadArmyUnitRawForceSlot(new(physical)) != q.OwnOrdinal) continue;
                int raw = Need(current.ReadArmyUnitRawHex2A(new(physical)), "Army_Hex2A");
                var hex = current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw Missing("Army_Hex_pointer");
                if (Need(current.ReadHexForceSlot(hex), "Army_Hex14") == q.OtherOrdinal)
                { trespass = true; break; }
            }
            root = trespass ? 0x2D14 : 0x2D1A;
            operands = new(Person0: advisorId, Person1: ownRuler, Person2: otherRuler);
        }
        else
        {
            //The original evaluates322B80 before3254D0's shortcut gates.
            int probability = q.Probability(offer);
            var trial = ResolveDiplomacyTrialQuery(q, offer, probability, Sampler);
            bool positive = trial.Accepted == accurate;
            operands = new(Person0: advisorId, Person1: ownRuler, Person2: executorId, Person3: otherRuler);
            if (kind == DiplomaticMissionKind.Goodwill) root = positive ? 0x2CD3 : 0x2CE3;
            else if (kind == DiplomaticMissionKind.Alliance)
            {
                bool renewing = ReadDiplomacyRelationCurrent(state, current, q.OwnOrdinal, q.OtherOrdinal).AllianceMonths != 0;
                root = renewing ? positive ? 0x337C : 0x338C : positive ? 0x2CF3 : 0x2D04;
                if (renewing) operands = operands with { Integers = [months] };
            }
            else if (kind == DiplomaticMissionKind.RequestAttack)
            {
                if (trial.Branch == "request-attack-blocked")
                { root = 0x3469; operands = new(Person0: advisorId, Force0: q.OtherOrdinal); }
                else
                {
                    root = positive ? 0x2D7E : 0x2D8E;
                    operands = operands with { Person4 = q.Id(q.Ruler(q.BaseForce(q.AttackBase()))),
                        Base0 = checked((ushort)(targetCity ?? throw Missing("target_Base4E"))) };
                }
            }
            else throw Missing("kind");
        }
        var formatted = FormatDiplomacyReportCurrent(state, current, root, operands);
        return new(formatted.State, formatted.Text, string.IsNullOrEmpty(formatted.Text) ? null : advisorId);

        TransferPersonReference Advisor(ForceReference f)
        {
            ushort raw = Need(current.ReadAssignmentForceAdvisor52(Need(current.ReadForcePhysicalSlot(f), "Force_physical")), "Force52");
            return (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))
                ?? throw Missing("Person_lookup");
        }
        bool Valid(TransferPersonReference p) => p.PhysicalSlot is int physical && Need(current.ReadPersonNativeValidity(physical), "Person_validity");
        static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        static InvalidDataException Missing(string field) => DiplomacyUiSource.Missing("advisor_" + field);
    }
}
