using San14.Core;

namespace San14.Data;

public sealed record RulerSuccessionSelection(OfficerInformationPageSet Pages,
    string Guide, string Announcement);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The pending original candidate order survives the announcement
    /// and every modal retry. Only the display reader is newly retained.</summary>
    public AreaAdministrationQuery<RulerSuccessionSelection> QueryRulerSuccessionSelection(GameState state, int forceId)
    {
        if (state.PersonDeathContinuation?.PendingSuccession is not { } request || request.ForceId != forceId)
            return AreaAdministrationQuery<RulerSuccessionSelection>.Unknown("ruler.succession_pending");
        try
        {
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            var reader = new OfficerInformationCurrent(this, state, forceId,
                rowPaletteEnabled: false, retainedCurrent: current);
            return AreaAdministrationQuery<RulerSuccessionSelection>.Known(new(
                reader.SuccessionPages(request.CandidateOfficerIds), request.PromptAtSource,
                request.EmptyChoiceWarningAtSource));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<RulerSuccessionSelection>.Unknown(error.Message); }
    }

    private sealed partial class OfficerInformationCurrent
    {
        internal OfficerInformationPageSet SuccessionPages(IReadOnlyList<int> ids)
        {
            var prepared = new Dictionary<(int Person, int Column), PreparedCell>();
            PreparedCell Read(int id, int column)
            {
                if (column is not (27 or 138)) return Cell(id, column);
                if (prepared.TryGetValue((id,column),out var saved)) return saved;
                var person = ReadPerson(id);
                saved = column == 27 ? SuccessionPolicyCost(person) : SuccessionHome(person);
                return prepared[(id,column)] = saved;
            }
            return new(Data(1,0x4AB), RulerSuccessionColumnsSource.Create(Data), ids,
                (id,column) => Read(id,column).Display with { SortUnresolvedOwner = Read(id,column).SortUnresolvedOwner },
                (id,column) => Read(id,column).SortUnresolvedOwner,
                (left,right,column,ascending) => (Read(left,column).Key ?? throw Missing("succession.sort_key"))
                    .Before(Read(right,column).Key ?? throw Missing("succession.sort_key"),ascending))
                { DynamicPaletteEnabled = false };
        }

        private PreparedCell SuccessionPolicyCost(Person person)
        {
            int RawCost()
            {
                var policy = MilitaryPolicy(person);
                int basis = policy is null ? 0 : MilitaryPolicyPersonalByte(person);
                return PolicyCosts.Calculate(basis,policy?.CostAddition ?? 0,
                    sameDoctrine:false,isRuler:false,ignoreDoctrineDiscount:true).Total;
            }
            // 360B80 includes the flag4000 display exclusion. 34DFF0's
            // independent numeric key excludes only the two special cases.
            return MakeCell(() => MilitarySuppressed(person,true) ? Data(1,0x353) : Number(RawCost()),
                () => MilitarySuppressed(person,false) ? new NumberKey(0,true) : new NumberKey(RawCost()));
        }

        private PreparedCell SuccessionHome(Person person)
        {
            ushort RawHome() => Need(current.ReadPersonFirstLocation(person.PhysicalSlot),"succession.Home");
            return MakeCell(() =>
            {
                if (!InformationNativeValid(person)) return "";
                var home = BaseLookup.Find(RawHome(),current);
                if (!Need(current.ReadBaseNativeValidity(home),"succession.Home_validity")) return "";
                int physical = Need(home.PhysicalSlot,"succession.Home_receiver");
                return home.Kind == BaseKind.City
                    ? state.Cities.GetValueOrDefault(physical)?.Name ?? throw Missing("succession.City_name")
                    : state.Gates?.GetValueOrDefault(physical+51)?.Name ?? throw Missing("succession.Gate_name");
            }, () => !InformationNativeValid(person) || RawHome() == 0
                ? new NumberKey(0,true) : new NumberKey(RawHome()));
        }
    }
}
