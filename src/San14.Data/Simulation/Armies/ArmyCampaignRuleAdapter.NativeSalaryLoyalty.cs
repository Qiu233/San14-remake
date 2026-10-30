using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private readonly record struct SalaryLoyaltyResult(GameState State, bool ReportShortage,
        bool SpecialRemains, bool SpecialDeparts);

    private SalaryLoyaltyResult ApplyNativeSalaryLoyaltyShortageCurrent(GameState state,
        SourceWorldContext current, int person)
    {
        var force = current.ReadPersonForce(person) ?? throw SalaryMissing("shortage_Person_Force");
        int rawRuler = K(current.ReadForceRecord(K(force.PhysicalSlot, "shortage_Force_pointer"))?.RawRuler10,
            "shortage_Force_ruler10");
        int ruler = K((rawRuler <= 6000 ? current.ReadPersonLookupReference(rawRuler)
            : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "shortage_ruler_lookup");
        if (Protected()) return new(state, false, false, false);

        bool special = !K(OfficerBehaviorRules.UsesOrdinaryPersonRules(current.ReadPersonBehavior(person)),
            "shortage_flags14");
        int loss = special ? 60 : NativeMonthlySalaryShortageRules.ForLanguage(Seed.Language)
            .LoyaltyLoss(K(current.ReadAllegianceDisposition(person), "shortage_Person16C"));
        byte before = K(ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, person, _governorInputs),
            "shortage_PersonE8");
        byte after = (byte)Math.Clamp(unchecked(before - unchecked((byte)loss)), 0, 255);
        if (K(OfficerBehaviorRules.PreservesMaximumLoyalty(current.ReadPersonBehavior(person)), "shortage_flags13"))
            after = 255;
        current.SetPersonDestructionLoyaltyE8(person, after);
        state = ArmyDestructionLoyalty.PublishCurrentRawE8(state, current, person, after);
        return new(state, true, special && after != 0, special && after == 0);

        bool Valid(int p) => K(current.ReadPersonNativeValidity(p), "shortage_relation_validity");
        int Id(int p) => K(current.ReadPersonIdentity(p), "shortage_relation_identity");
        bool Pair() => Valid(person) && Valid(ruler) && Id(person) != Id(ruler);
        bool Relative(int a, int b, int field) => Valid(a) && Valid(b) && Id(a) != Id(b) &&
            OfficeWord(state, a, field) is var value && value != 0 && value == Id(b);
        bool Protected()
        {
            if (Pair() && OfficeWord(state, person, 0xBA) is var spouse && spouse != 0 && spouse == Id(ruler)) return true;
            if (Pair() && OfficeWord(state, person, 0xBC) is var sworn && sworn != 0 && sworn == OfficeWord(state, ruler, 0xBC)) return true;
            if (Relative(person, ruler, 0xB4) || Relative(person, ruler, 0xB6) ||
                Relative(ruler, person, 0xB4) || Relative(ruler, person, 0xB6)) return true;
            return OfficeRelatedCurrent(state, current, person, ruler) &&
                !OfficeRelationList(state, current, person, ruler, 0xD0);
        }
        static T K<T>(T? value, string field) where T : struct => value ?? throw SalaryMissing(field);
    }
}
