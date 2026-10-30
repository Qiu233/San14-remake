using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Only source-time operands belonging to the real report formatter.
/// Null means that an operand was not supplied; it is never replaced by a
/// scenario snapshot or guessed from the eventual notification.</summary>
internal sealed record HistoryMessageOperands(ArmyUnitReference? Army0 = null, ArmyUnitReference? Army1 = null,
    ushort? Base0 = null, string? String0 = null, string? String1 = null, string? String2 = null,
    int Person0 = 0, int Person1 = 0, int? Force0 = null, int? Area0 = null, int[]? Integers = null,
    int? Person2 = null, int? Person3 = null, int? Force1 = null, int? Person4 = null, int? District0 = null,
    int? Force2 = null, int? Force3 = null, int? Force4 = null,
    int? ObjectDefinition0 = null, int? ObjectDefinition1 = null,
    ushort? Base1 = null, ushort? Base2 = null, ushort? Base3 = null, ushort? Base4 = null, string? String3 = null,
    int? Area1 = null, int? Area2 = null, int? Area3 = null, int? Area4 = null);
internal sealed record HistoryTextResult(GameState State, string Text)
{
    public IReadOnlyList<NarrationVoiceRequest> Voices { get; init; } = [];
    public IReadOnlyList<NarrationSpatialVoiceRequest> SpatialVoices { get; init; } = [];
}

public sealed partial class ArmyCampaignRuleAdapter
{
    private enum ReportMessageOrigin { SavedHistory, AreaWarning, ArmyTargetWarning, BaseNearbyWarning, AllianceExpiryWarning, ForceObjectiveWarning, ArmySupplyWarning, AttackRequestExpiryWarning, GarrisonFoodShortage, Governance, Duel, CaptiveRelease, Talent, Diplomacy, PersonnelAdministration, PersonnelDevelopment, MonthlyMaintenance, ArmyMapSpeech, DoctrineResearch, NormalTurn, StrategicCommands, Proposal, IntelligenceCommands, Occupation, DuelPresentation, DuelCapture, PersonnelItemTransfer, StoryStateControl }
    internal string ReadHistoryDataText(int section, int id)
    {
        var data = _repository.LoadMessages(Seed.Language, MessageKind.Data).Messages;
        if ((uint)section >= (uint)data.Sections.Count || (uint)id >= (uint)data.Sections[section].Records.Count)
            throw new InvalidDataException("campaign-history.data_message");
        return data.Sections[section].Records[id].Text ?? throw new InvalidDataException("campaign-history.data_text");
    }

    //30AEE0's1421461A8 is msgdata table0, bounded by0x1809. It is
    //not another resource family or an inferred name from the command enum.
    internal string ReadHistoryChoiceName(GameState state, int choice) => choice is 1 or 2
        ? ReadHistoryDataText(0, 0x118 + choice) : CampaignHistoryWriter.Supplemental(state)
            ?? throw new InvalidDataException("campaign-history.current_empty_text");

    internal string ReadHistoryArmyName(GameState state, SourceWorldContext current, ArmyUnitReference army) =>
        CurrentPersonListSource.ReadArmyName(state, current, army, ReadHistoryDataText);

    internal HistoryTextResult FormatHistoryReport(GameState state, SourceWorldContext current,
        int root, HistoryMessageOperands operands)
        => FormatReportMessage(state,current,root,operands,ReportMessageOrigin.SavedHistory,null);

    private HistoryTextResult FormatOccupationReport(GameState state, SourceWorldContext current,
        int root, HistoryMessageOperands operands) =>
        FormatReportMessage(state, current, root, operands, ReportMessageOrigin.Occupation, null);

    private HistoryTextResult FormatDuelHistoryReport(GameState state, SourceWorldContext current,
        int root, HistoryMessageOperands operands)
        => FormatReportMessage(state, current, root, operands, ReportMessageOrigin.Duel, null);

    private HistoryTextResult FormatCaptiveReleaseReport(GameState state, SourceWorldContext current,
        int root, HistoryMessageOperands operands)
        => FormatReportMessage(state, current, root, operands, ReportMessageOrigin.CaptiveRelease, null);

    internal HistoryTextResult FormatActionAreaReport(GameState state, SourceWorldContext current,
        AreaReportFormattingInputs input, Action<GameState> publishReached)
        => FormatReportMessage(state,current,input.MessageId,
            new(Person0:input.SpeakerPersonOperand0,Person1:input.RulerPersonOperand1,
                Base0:input.CityBaseOperand0),ReportMessageOrigin.AreaWarning,publishReached);

    internal HistoryTextResult FormatActionArmyTargetReport(GameState state,SourceWorldContext current,
        ArmyTargetReportFormattingInputs input,Action<GameState> publishReached)
        => FormatReportMessage(state,current,0x5024,
            new(Person0:input.SpeakerPersonOperand0,Person1:input.RulerPersonOperand1,
                Force0:input.EnemyForceOperand0,Base0:input.TargetBaseOperand0),ReportMessageOrigin.ArmyTargetWarning,publishReached);

    internal HistoryTextResult FormatActionBaseNearbyReport(GameState state,SourceWorldContext current,
        BaseNearbyEnemyReportFormattingInputs input,Action<GameState> publishReached)
        => FormatReportMessage(state,current,0x502b,
            new(Person0:input.SpeakerPersonOperand0,Person1:input.RulerPersonOperand1,
                Base0:input.FootholdBaseOperand0),ReportMessageOrigin.BaseNearbyWarning,publishReached);

    internal HistoryTextResult FormatActionAllianceExpiryReport(GameState state,SourceWorldContext current,
        AllianceExpiryReportFormattingInputs input,Action<GameState> publishReached)
        => FormatReportMessage(state,current,0x5046,
            new(Person0:input.SpeakerPersonOperand0,Person1:input.RulerPersonOperand1,
                Force0:input.OtherForceOperand0,Integers:[input.RemainingAllianceCounterInteger0]),
            ReportMessageOrigin.AllianceExpiryWarning,publishReached);

    internal HistoryTextResult FormatActionForceObjectiveReport(GameState state, SourceWorldContext current,
        ForceObjectiveReportFormattingInputs input, Action<GameState> publishReached)
        => FormatReportMessage(state, current, 0x50c1,
            new(Person0: input.SpeakerPersonOperand0, Person1: input.RulerPersonOperand1,
                Force0: input.OtherForceOperand0, Base0: input.ObjectiveBaseOperand0),
            ReportMessageOrigin.ForceObjectiveWarning, publishReached);

    internal HistoryTextResult FormatActionArmySupplyReport(GameState state, SourceWorldContext current,
        ArmySupplyReportFormattingInputs input, Action<GameState> publishReached)
        => FormatReportMessage(state, current, input.MessageId,
            new(Person0: input.SpeakerPersonOperand0, Person1: input.RulerPersonOperand1,
                Army0: input.ArmyOperand0, Base0: input.HomeBaseOperand0, Integers: [input.ArmyCountInteger0]),
            ReportMessageOrigin.ArmySupplyWarning, publishReached);

    internal HistoryTextResult FormatActionAttackRequestExpiryReport(GameState state, SourceWorldContext current,
        ForceAttackRequestExpiryFormattingInputs input, Action<GameState> publishReached)
        => FormatReportMessage(state, current, 0x50c8,
            new(Person0: input.SpeakerPersonOperand0, Person1: input.RulerPersonOperand1,
                Force0: input.RequestingForceOperand0, Integers: [input.RemainingMonthsInteger0]),
            ReportMessageOrigin.AttackRequestExpiryWarning, publishReached);

    private HistoryTextResult FormatGarrisonFoodShortageReport(GameState state, SourceWorldContext current,
        int messageId, int speakerPersonOperand0, int rulerPersonOperand1, ushort footholdBaseOperand0,
        Action<GameState> publishReached)
        => FormatReportMessage(state, current, messageId,
            new(Person0: speakerPersonOperand0, Person1: rulerPersonOperand1, Base0: footholdBaseOperand0),
            ReportMessageOrigin.GarrisonFoodShortage, publishReached);

    private HistoryTextResult FormatGovernanceResponse(GameState state, SourceWorldContext current,
        int messageId, int speaker, int ruler, string appointment)
        => FormatReportMessage(state, current, messageId,
            new(Person0: speaker, Person1: ruler, String0: appointment), ReportMessageOrigin.Governance, null);

    internal HistoryTextResult FormatMonthlyMaintenanceReport(GameState state, SourceWorldContext current,
        int root, HistoryMessageOperands operands, Action<GameState> publishAfterDraw) =>
        FormatReportMessage(state, current, root, operands, ReportMessageOrigin.MonthlyMaintenance, publishAfterDraw);

    internal HistoryTextResult FormatNormalTurnReport(GameState state, SourceWorldContext current,
        int root, HistoryMessageOperands operands, Action<GameState>? publishAfterDraw = null) =>
        FormatReportMessage(state, current, root, operands, ReportMessageOrigin.NormalTurn, publishAfterDraw,
            _personnelNarrationVoice);

    private HistoryTextResult FormatReportMessage(GameState state, SourceWorldContext current,
        int root, HistoryMessageOperands operands, ReportMessageOrigin origin, Action<GameState>? publishReached,
        NarrationVoiceSettings? voiceSettings = null,
        Func<int, NarrationSpatialVoicePosition?>? spatialPosition = null,
        Func<NarrationSpatialVoiceRequest, bool>? spatialAdmission = null)
    {
        var latest = state;
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
            SimulationRuleIdentity.ForLocale(Seed.Language));
        var voices = voiceSettings is null ? null : new NarrationVoiceRequests(voiceSettings, Draw,
            spatialPosition: spatialPosition, spatialAdmission: spatialAdmission);
        var people = new DeploymentNarrationCurrentPeople(state, current, abilities,
            operands.Person0, operands.Person1, area: operands.Area0,
            panelReader: (force, effect) => _baseCapacityInputs.ReadPanelModifier(latest, force, effect),
            cityArgument: operands.Base0, appearanceRepository: _repository,
            appearanceScenario: _scenario, appearanceLanguage: Seed.Language, forceArgument: operands.Force0,
            personArgument2: operands.Person2, personArgument3: operands.Person3, forceArgument1: operands.Force1,
            personArgument4: operands.Person4, voiceRequests: voices);
        var context = new ReportCurrent(this, () => latest, current, people, operands,
            origin is ReportMessageOrigin.MonthlyMaintenance or ReportMessageOrigin.NormalTurn or ReportMessageOrigin.Occupation);
        int Draw(int bound)
        {
            if (bound < 2) return 0;
            var saved = latest.CampaignAppRandom32 ?? throw HistoryMissing("App32_draw_state");
            var random = new SharedSourceRandom32(saved.State);
            int value = random.Next(bound);
            latest = latest with { CampaignAppRandom32 = new(random.State) };
            publishReached?.Invoke(latest);
            return value;
        }
        var vm = new NarrationResourceMessages(_repository.LoadMessages(Seed.Language, MessageKind.Game).Messages);
        string text = origin switch
        {
            ReportMessageOrigin.AreaWarning=>vm.ResolveActionAreaReport(context,Draw,root),
            ReportMessageOrigin.ArmyTargetWarning=>vm.ResolveActionArmyTargetReport(context,Draw),
            ReportMessageOrigin.BaseNearbyWarning=>vm.ResolveActionBaseNearbyReport(context,Draw),
            ReportMessageOrigin.AllianceExpiryWarning=>vm.ResolveActionAllianceExpiryReport(context,Draw),
            ReportMessageOrigin.ForceObjectiveWarning=>vm.ResolveActionForceObjectiveReport(context,Draw),
            ReportMessageOrigin.ArmySupplyWarning=>vm.ResolveActionArmySupplyReport(context,Draw,root),
            ReportMessageOrigin.AttackRequestExpiryWarning=>vm.ResolveActionAttackRequestExpiryReport(context,Draw),
            ReportMessageOrigin.GarrisonFoodShortage=>vm.ResolveGarrisonFoodShortageReport(context,Draw,root),
            ReportMessageOrigin.Governance=>vm.ResolveGovernanceNarration(context,Draw,root),
            ReportMessageOrigin.Duel=>vm.ResolveDuelHistoryReport(context,Draw,root),
            ReportMessageOrigin.DuelPresentation=>vm.ResolveDuelPresentation(context,Draw,root),
            ReportMessageOrigin.DuelCapture=>vm.ResolveDuelCapture(context,Draw,root),
            ReportMessageOrigin.CaptiveRelease=>vm.ResolveCaptiveReleaseReport(context,Draw,root),
            ReportMessageOrigin.Talent=>vm.ResolveTalentReport(context,Draw,root),
            ReportMessageOrigin.PersonnelAdministration=>vm.ResolvePersonnelAdministrationReport(context,Draw,root),
            ReportMessageOrigin.PersonnelItemTransfer=>vm.ResolvePersonnelItemTransfer(context,Draw,root),
            ReportMessageOrigin.StoryStateControl=>vm.ResolveStoryStateControl(context,Draw,root),
            ReportMessageOrigin.PersonnelDevelopment=>vm.ResolvePersonnelDevelopmentReport(context,Draw,root),
            ReportMessageOrigin.MonthlyMaintenance=>vm.ResolveMonthlyMaintenance(context,Draw,root),
            ReportMessageOrigin.NormalTurn=>vm.ResolveNormalTurnReport(context,Draw,root),
            ReportMessageOrigin.ArmyMapSpeech=>vm.ResolveArmyMapSpeech(context,Draw,root),
            ReportMessageOrigin.DoctrineResearch=>vm.ResolveDoctrineResearch(context,Draw,root),
            ReportMessageOrigin.StrategicCommands=>vm.ResolveStrategicCommandReport(context,Draw,root),
            ReportMessageOrigin.Diplomacy=>vm.ResolveDiplomacyReport(context,Draw,root),
            ReportMessageOrigin.Proposal=>vm.ResolveProposalReport(context,Draw,root),
            ReportMessageOrigin.IntelligenceCommands=>vm.ResolveIntelligenceReport(context,Draw,root),
            ReportMessageOrigin.Occupation=>vm.ResolveOccupationReport(context,Draw,root),
            _=>vm.ResolveHistoryReport(context,Draw,root)
        };
        return new(latest, text) { Voices = voices?.Pending ?? [], SpatialVoices = voices?.PendingSpatial ?? [] };
    }

    private static string HeadHistory(string text) => text.IndexOf('\0') is int n && n >= 0 ? text[..n] : text;
    private static T HistoryKnown<T>(T? value, string field) where T : struct => value ?? throw HistoryMissing(field);
    private static InvalidDataException HistoryMissing(string field) => new("campaign-history.text." + field);

    private sealed class ReportCurrent(ArmyCampaignRuleAdapter package, Func<GameState> latest,
        SourceWorldContext current, INarrationCurrentContext people, HistoryMessageOperands operands, bool monthlyForceFields = false) : INarrationCurrentContext
    {
        public int PersonValue(int a, int f) => people.PersonValue(a, f);
        public string PersonText(int a, int f) => people.PersonText(a, f);
        public int Relationship(int f, int a, int b)
        {
            if (f is not ('P' or 'Q')) return people.Relationship(f, a, b);
            var force = current.ReadForceTableReference(a is >= 0 and < 52 ? a : 0)
                ?? throw HistoryMissing("Function_Force_lookup");
            bool valid = HistoryKnown(current.ReadForceNativeValidity(force), "Function_Force_validity");
            if (f == 'P') return valid ? 1 : 0;
            if (!valid || b is < 0 or >= 52) return 0;
            int physical = HistoryKnown(force.PhysicalSlot, "Function_Force_pointer");
            var state = latest();
            if (state.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(physical, out var reset) == true)
                return HistoryKnown(reset[0x13 + b], "FunctionQ_Force_reset_relation13");
            var values = physical == 0 ? state.NativeForceUnregisterCurrent?.PlaceholderRelations13 :
                state.NativeArmySimulation?.Forces.GetValueOrDefault(physical)?.NormalAftermath?.Relations13;
            return values is { Length: 52 } ? values[b] : throw HistoryMissing("FunctionQ_Force_relation13");
        }
        public int Personality(int a, Func<int> key) => people.Personality(a, key);
        public string AreaText(int a, int f)
        {
            if (f != '3') throw HistoryMissing("Area_field");
            int raw = AreaArgument(a);
            if (raw is < 1 or > 500) return "";
            var selected = current.ReadAreaTableReference(raw) ?? throw HistoryMissing("Area_lookup");
            if (!HistoryKnown(current.ReadAreaNativeValidity(selected), "Area_validity")) return "";
            int physical = HistoryKnown(selected.PhysicalSlot, "Area_physical");
            var name = latest().Areas.GetValueOrDefault(physical)?.Name ?? throw HistoryMissing("Area_current_name");
            return CityDomesticMessageFormatting.FormatCityNameField(name);
        }
        //310770 returns the unformatted saved argument before native
        //validity for a bare selector. Field2 requires a valid Area; field3
        //returns the same fully formatted UTF16 length as its text callback.
        public int AreaValue(int argument, int field)
        {
            int raw = AreaArgument(argument);
            if (field == 0) return raw;
            if (field == '3') return AreaText(argument, field).Length;
            if (field != '2') throw HistoryMissing("Area_numeric_field");
            if (raw is < 1 or > 500) return 0;
            var area = current.ReadAreaTableReference(raw) ?? throw HistoryMissing("Area_lookup");
            return HistoryKnown(current.ReadAreaNativeValidity(area), "Area_validity") ? raw : 0;
        }
        private int AreaArgument(int argument) => (argument switch
        {
            0 => operands.Area0, 1 => operands.Area1, 2 => operands.Area2,
            3 => operands.Area3, 4 => operands.Area4, _ => null
        }) ?? throw HistoryMissing("Area_argument");
        //Bare City argument is read without the display-field range gate.
        public int CityValue(int a, int f)
        {
            if (f != 0) throw HistoryMissing("Base_numeric_field");
            return BaseArgument(a);
        }
        public int BaseArgumentValidity(int raw) => HistoryKnown(
            current.ReadBaseNativeValidity(BaseLookup.Find(raw, current)), "Base_argument_validity") ? 1 : 0;
        public string CityText(int a, int f)
        {
            if (f != '2') throw HistoryMissing("Base_text_field");
            ushort raw = BaseArgument(a);
            if (raw is 0 or > 61) return "";
            var selected = BaseLookup.Find(raw, current);
            int physical = HistoryKnown(selected.PhysicalSlot, "Base_text_pointer");
            var state = latest();
            string? name = selected.Kind switch
            {
                BaseKind.City => state.Cities.GetValueOrDefault(physical)?.Name,
                BaseKind.Gate => state.Gates.GetValueOrDefault(physical + 51)?.Name,
                _ => null
            };
            return CityDomesticMessageFormatting.FormatCityNameField(name ?? throw HistoryMissing("Base_text_name"));
        }
        private ushort BaseArgument(int argument) => (argument switch
        {
            0 => operands.Base0, 1 => operands.Base1, 2 => operands.Base2,
            3 => operands.Base3, 4 => operands.Base4,
            _ => throw HistoryMissing("Base_argument_index")
        }) ?? throw HistoryMissing("Base_argument");
        public int IntegerValue(int a) => operands.Integers is { } values && (uint)a < (uint)values.Length
            ? values[a] : throw HistoryMissing("Integer_argument");
        public int ForceValue(int a, int f)
        {
            if (a is < 0 or > 4 || f != 0) throw HistoryMissing("Force_numeric_field");
            int raw = ForceArgument(a);
            var force = current.ReadForceTableReference(raw is >= 0 and < 52 ? raw : 0)
                ?? throw HistoryMissing("Force_numeric_pointer");
            return HistoryKnown(current.ReadForcePhysicalSlot(force), "Force_numeric_ordinal");
        }
        public string DistrictText(int argument, int field)
        {
            if (argument != 0 || field != '2') throw HistoryMissing("District_field");
            int raw = operands.District0 ?? throw HistoryMissing("District_argument");
            if (raw is < 1 or > 51) return "";
            var district = current.ReadDistrictTableReference(raw) ?? throw HistoryMissing("District_lookup");
            int physical = HistoryKnown(district.PhysicalSlot, "District_physical");
            ushort id = HistoryKnown(current.ReadDistrictLeader(physical), "District_leader12");
            var person = (id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))
                ?? throw HistoryMissing("District_leader_lookup");
            int at = HistoryKnown(person.PhysicalSlot, "District_leader_physical");
            string name = "";
            if (HistoryKnown(current.ReadPersonNativeValidity(at), "District_leader_validity"))
            {
                if (physical is >= 46 and <= 50) throw HistoryMissing("District_special_name231160");
                int identity = HistoryKnown(current.ReadPersonIdentity(at), "District_leader_identity");
                var owner = latest().Officers.GetValueOrDefault(identity) ?? throw HistoryMissing("District_leader_owner");
                name = HeadHistory(owner.CurrentSurname ?? throw HistoryMissing("District_leader_surname")) +
                    HeadHistory(owner.CurrentGivenName ?? throw HistoryMissing("District_leader_given_name")) +
                    HeadHistory(package.ReadHistoryDataText(1, 0x350));
            }
            return CityDomesticMessageFormatting.FormatPersonNameField(name);
        }
        public string ForceText(int a, int f)
        {
            if (monthlyForceFields) return MonthlyForceText(a, f);
            if (f == '4') return people.ForceText(a, f);
            if (a is not (0 or 1) || f != '3') throw HistoryMissing("Force_field");
            int raw = (a == 0 ? operands.Force0 : operands.Force1) ?? throw HistoryMissing("Force_argument");
            if (raw is < 1 or > 51) return "";
            var value = TrapCounterReportForceNameSource.Capture(latest(), current, checked((byte)raw));
            string name = value.Form switch
            {
                TrapCounterForceNameForm.Empty => "",
                TrapCounterForceNameForm.CustomKingdom => Custom(value.CustomKingdomIndex!.Value) +
                    HeadHistory(package.ReadHistoryDataText(1, 0x34F)),
                TrapCounterForceNameForm.PersonWithArmySuffix => HeadHistory(value.PersonNameOperandAtSource
                    ?? throw HistoryMissing("Force_name")) + HeadHistory(package.ReadHistoryDataText(1, 0x34F)),
                TrapCounterForceNameForm.PersonWithoutArmySuffix => HeadHistory(value.PersonNameOperandAtSource
                    ?? throw HistoryMissing("Force_name")),
                _ => throw HistoryMissing("Force_name_form")
            };
            return CityDomesticMessageFormatting.FormatCityNameField(name);
        }
        //30EB60's monthly force fields read a custom kingdom name before
        //native validity, unlike the ordinary trap-counter display contract.
        private string MonthlyForceText(int argument, int field)
        {
            if (argument is < 0 or > 4 || field is not ('3' or '4')) throw HistoryMissing("monthly_Force_field");
            int raw = ForceArgument(argument);
            if (raw is < 1 or > 51) return "";
            var force = current.ReadForceTableReference(raw) ?? throw HistoryMissing("monthly_Force_lookup");
            int physical = HistoryKnown(force.PhysicalSlot, "monthly_Force_pointer");
            byte kingdom = HistoryKnown(current.ReadForceKingdomNameIndex(physical), "monthly_Force_kingdom");
            string suffix = HeadHistory(package.ReadHistoryDataText(1, 0x34F));
            string name;
            if (kingdom is >= 1 and <= 100) name = Custom(kingdom) + (field == '3' ? suffix : "");
            else
            {
                if (!HistoryKnown(current.ReadForceNativeValidity(force), "monthly_Force_validity")) return "";
                int selected = HistoryKnown(current.ReadForcePhysicalSlot(force), "monthly_Force_identity");
                int ruler = selected == 0 ? 5014 : HistoryKnown(current.ReadForceRecord(selected)?.RawRuler10, "monthly_Force_ruler");
                var person = (ruler <= 6000 ? current.ReadPersonLookupReference(ruler) : current.ReadTransferPersonTableReference(0))
                    ?? throw HistoryMissing("monthly_ruler_lookup");
                int personPhysical = HistoryKnown(person.PhysicalSlot, "monthly_ruler_pointer");
                if (!HistoryKnown(current.ReadPersonNativeValidity(personPhysical), "monthly_ruler_validity")) return "";
                int identity = HistoryKnown(current.ReadPersonIdentity(personPhysical), "monthly_ruler_identity");
                if (current.ResolveOfficerSource(identity) != personPhysical || !latest().Officers.TryGetValue(identity, out var owner))
                    throw HistoryMissing("monthly_ruler_owner");
                selected = HistoryKnown(current.ReadForcePhysicalSlot(force), "monthly_Force_fresh_identity");
                name = selected is < 46 or > 50 ? HeadHistory(owner.Name) + suffix :
                    HeadHistory(identity is >= 5101 and <= 5200 or >= 2062 and <= 2066 ? owner.Name : owner.CurrentSurname ?? throw HistoryMissing("monthly_ruler_surname"));
            }
            return CityDomesticMessageFormatting.FormatCityNameField(name);
        }
        private int ForceArgument(int argument) => (argument switch
        {
            0 => operands.Force0, 1 => operands.Force1, 2 => operands.Force2,
            3 => operands.Force3, 4 => operands.Force4, _ => null
        }) ?? throw HistoryMissing("Force_argument");

        private string Custom(byte id)
        {
            var defs = package._repository.LoadForceNameDefinitions(package._scenario.FixedDataSource.SourcePath).Catalog.Definitions;
            if ((uint)id >= (uint)defs.Count) throw HistoryMissing("Force_name_catalog");
            return HeadHistory(defs[id].ShortName);
        }
        private ArmyUnitReference Army(int a) => (a == 0 ? operands.Army0 : a == 1 ? operands.Army1 : null)
            ?? throw HistoryMissing("Army_argument");
        public int ArmyValue(int a, int f)
        {
            var arg = Army(a);
            if (f == 0) return HistoryKnown(arg.PhysicalSlot, "Army_argument_identity");
            if(f is >= 'I' and <= 'L')
            {
                int lookup=HistoryKnown(arg.PhysicalSlot,"Army_argument_identity");
                if(lookup is <1 or >500)return 0;
                var army=current.ReadArmyUnitTableReference(lookup)??throw HistoryMissing("Army_argument_lookup");
                if(!HistoryKnown(current.ReadArmyUnitNativeValidity(army),"Army_argument_validity"))return 0;
                int id=ArmyDestructionQueue.ResolveUnitId(latest(),army.PhysicalSlot)??throw HistoryMissing("Army_construction_image");
                var selection=latest().ReadArmyLifecycle()?.Receivers.FindUnit(id)?.FacilityConstruction
                    ??throw HistoryMissing("Army_construction_selection46");
                int definition=selection.DefinitionKey is null?0:MapObjectSourceDirectory.DefinitionSlot(selection.DefinitionKey);
                //30E350 compares raw Army46. It does not dereference a
                //potentially aliased definition table before this predicate.
                return definition==(f switch {'I'=>8,'J'=>11,'K'=>12,'L'=>14,_=>0})?1:0;
            }
            if (f != '2') throw HistoryMissing("Army_field_" + f.ToString("X"));
            int raw = HistoryKnown(arg.PhysicalSlot, "Army_argument_identity");
            if (raw is < 1 or > 500) return 0;
            var looked = current.ReadArmyUnitTableReference(raw) ?? throw HistoryMissing("Army_argument_lookup");
            _ = HistoryKnown(current.ReadArmyUnitNativeValidity(looked), "Army_argument_validity");
            //30E350 only calls22ECB0 when a display buffer was supplied.
            return 0;
        }
        public string ArmyText(int a, int f)
        {
            var arg = Army(a);
            int raw = HistoryKnown(arg.PhysicalSlot, "Army_argument_identity");
            if (f != '2') throw HistoryMissing("Army_field_" + f.ToString("X"));
            if (raw is < 1 or > 500) return "";
            var looked = current.ReadArmyUnitTableReference(raw) ?? throw HistoryMissing("Army_argument_lookup");
            if (!HistoryKnown(current.ReadArmyUnitNativeValidity(looked), "Army_argument_validity")) return "";
            return CityDomesticMessageFormatting.FormatPersonNameField(package.ReadHistoryArmyName(latest(), current, looked));
        }
        private string String(int a) => (a switch { 0 => operands.String0, 1 => operands.String1, 2 => operands.String2,
            3 => operands.String3, _ => null }) ?? throw HistoryMissing("String_argument");
        public string StringText(int a, int f)
        {
            string value = HeadHistory(String(a));
            if (f is not (0 or '2' or '3')) throw HistoryMissing("String_field");
            return f == '3' ? "" : CityDomesticMessageFormatting.FormatAbilityField(value);
        }
        public int StringValue(int a, int f)
        {
            string value = HeadHistory(String(a));
            if (f is not (0 or '2' or '3')) throw HistoryMissing("String_field");
            return f == '3' ? value.Length : CityDomesticMessageFormatting.FormatAbilityField(value).Length;
        }
    }
}
