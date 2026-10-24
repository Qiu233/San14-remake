using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Mode2's narration after Yes, before the real transport creator.
    /// The caller owns presentation random and consumes the prepared pages once.
    /// This query does not create an Army or charge stocks/command books.</summary>
    public DeploymentNarrationQuery QueryTransportNarration(GameState state,
        DispatchTransportArmyOrder order, string locale, Func<int, int> appRandom)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(appRandom);
        try
        {
            if (locale is not ("TC" or "SC") || state.NativeArmySimulation?.Parameters is not { } parameters ||
                parameters.Language != Seed.Language || parameters.RuleVersion != SimulationRuleIdentity.ForLocale(Seed.Language))
                throw TransportNarrationMissing("rule_profile");
            var current = CaptureOfficerCommandCapacityCurrent(state);
            OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
            _personAssignmentPrecallInputs.BindCurrent(state, current);
            int commanderPhysical = PersonPointer(order.OfficerId);
            if (!TransportNarrationNeed(current.ReadPersonNativeValidity(commanderPhysical), "speaker_validity"))
                throw TransportNarrationMissing("invalid_speaker");
            int speaker = TransportNarrationNeed(current.ReadPersonIdentity(commanderPhysical), "speaker_identity");

            BaseReference? targetBase = null;
            HexReference? targetHex = null;
            if (order.Action is { ActionKey: "enter", Target.Kind: DeploymentTargetKind.Location })
            {
                int raw = order.Action.Target.Id;
                targetBase = raw is >= 1 and <= 51 ? current.ReadCityTableReference(raw) :
                    raw is >= 52 and <= 61 ? current.ReadGateTableReference(raw - 51) : null;
                if (targetBase?.PhysicalSlot is null) throw TransportNarrationMissing("target_Base_pointer");
            }
            else if (order.Action is { ActionKey: "move", Target: { Kind: DeploymentTargetKind.Hex, Id: >= 0 and < 48400 } target })
            {
                targetHex = current.ReadHexTableReference(target.Id);
                if (targetHex is null || current.ReadHexPhysicalSlot(targetHex) is null)
                    throw TransportNarrationMissing("target_Hex_pointer");
            }
            else throw TransportNarrationMissing("target_mode2");

            // 7CC870 registers the commander identity, then the current ruler's
            // resolved Person identity. No ordinary deployment row sort runs.
            var force = current.ReadPersonForce(commanderPhysical) ?? throw TransportNarrationMissing("speaker_Force");
            int forcePhysical = TransportNarrationNeed(force.PhysicalSlot, "speaker_Force_pointer");
            int rawRuler = current.ReadForceRecord(forcePhysical)?.RawRuler10
                ?? throw TransportNarrationMissing("current_Force_ruler");
            int ruler = TransportNarrationNeed(current.ReadPersonIdentity(PersonPointer(rawRuler)), "ruler_identity");
            // Integer registrations0/1/2 are troops, gold, food. These original
            // two text families do not consume them; do not query extra stocks.
            int[] quantities = [order.Troops, order.Gold, order.Supplies];
            ushort? baseArgument = null, areaArgument = null;
            int root;
            if (targetBase is not null)
            {
                baseArgument = TransportNarrationNeed(current.ReadBaseObjectLink(targetBase), "target_Base4E");
                root = 0x57AD;
            }
            else
            {
                // 30DAA0 receives the real Hex12 Area key, not the Hex index.
                areaArgument = TransportNarrationNeed(current.ReadHexAreaSlot(targetHex!), "target_Hex12");
                root = 0x57BA;
            }
            var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, parameters.RuleVersion);
            var people = new DeploymentNarrationCurrentPeople(state, current, abilities, speaker, ruler,
                cityArgument: baseArgument);
            var context = new TransportNarrationCurrentContext(state, current, people, areaArgument, quantities);
            var result = new NarrationResourceMessages(_repository.LoadMessages(locale, MessageKind.Game).Messages)
                .ResolveTransport(context, appRandom, root);
            var choices = result.Choices.Where(choice => choice.MessageId == result.MessageId && choice.Arity == 3).ToArray();
            if (choices.Length != 1) throw TransportNarrationMissing("empty_or_unproved_variant");

            // The real event presentation follows complete formatting. Reuse
            // the established current portrait route; no placeholder portrait.
            if (!state.Officers.TryGetValue(speaker, out var owner)) throw TransportNarrationMissing("speaker_owner");
            string name = owner.Name ?? throw TransportNarrationMissing("speaker_name");
            ushort portrait = TransportNarrationNeed(owner.PortraitAppearanceIndex, "current_portrait");
            var route = ReadNarrationPortrait(state, current, _definitions, speaker, portrait);
            return new(speaker, name, portrait, result.MessageId, choices[0].SelectedIndex,
                result.Text, result.Pages, null) { PortraitRoute = route, RequestedVoiceId = 38 };

            int PersonPointer(int id) => current.ReadPersonLookupReference(id is >= 0 and <= 6000 ? id : 0)?.PhysicalSlot
                ?? throw TransportNarrationMissing("Person_pointer");
        }
        catch (InvalidDataException error)
        { return new(null, null, null, null, null, null, null, error.Message) { RequestedVoiceId = 38 }; }
    }

    private static T TransportNarrationNeed<T>(T? value, string field) where T : struct
        => value ?? throw TransportNarrationMissing(field);
    private static InvalidDataException TransportNarrationMissing(string field)
        => new("transport.narration." + field);

    private sealed class TransportNarrationCurrentContext(GameState state, SourceWorldContext current,
        INarrationCurrentContext people, ushort? areaArgument, IReadOnlyList<int> quantities) : INarrationCurrentContext
    {
        public int PersonValue(int argument, int field) => people.PersonValue(argument, field);
        public string PersonText(int argument, int field) => people.PersonText(argument, field);
        public int Relationship(int field, int first, int second) => people.Relationship(field, first, second);
        public int Personality(int first, Func<int> readKey) => people.Personality(first, readKey);
        public string CityText(int argument, int field) => people.CityText(argument, field);
        public int IntegerValue(int argument) => (uint)argument < (uint)quantities.Count
            ? quantities[argument] : throw TransportNarrationMissing("integer_argument");
        public string AreaText(int argument, int field)
        {
            if (argument != 0 || field != '3') throw TransportNarrationMissing("Area_text_field");
            ushort raw = areaArgument ?? throw TransportNarrationMissing("Area_argument");
            if (raw is 0 or > 500) return string.Empty;
            var selected = current.ReadAreaTableReference(raw) ?? throw TransportNarrationMissing("Area_lookup");
            if (!TransportNarrationNeed(current.ReadAreaNativeValidity(selected), "Area_validity")) return string.Empty;
            int physical = TransportNarrationNeed(selected.PhysicalSlot, "Area_pointer");
            string name = state.Areas.GetValueOrDefault(physical)?.Name ?? throw TransportNarrationMissing("Area_name");
            return CityDomesticMessageFormatting.FormatCityNameField(name);
        }
    }
}
