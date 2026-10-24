using System.Collections.Immutable;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal InformationList ReadInformationDestinyList(GameState state, SourceWorldContext current,
        int playerForceId, string title)
    {
        //8CC3B0 reads the actual Force table, not just the player's Force or a
        //registered-person count. Its three pairs are1/6,2/3,4/5.
        var rows = ImmutableArray.CreateBuilder<InformationRow>();
        var issues = new HashSet<string>(StringComparer.Ordinal);
        var sums = new long[3]; var counts = new int[3]; var complete = new[] { true, true, true };
        var representatives = new InformationDestinyRepresentative?[7];
        var representativeComplete = Enumerable.Repeat(true, 7).ToArray();
        for (int slot = 0; slot < 52; slot++)
        {
            try
            {
                var force = current.ReadForceTableReference(slot) ?? throw InfoDefinitionMissing("destiny.Force_lookup");
                if (!InfoDefinitionKnown(current.ReadForceNativeValidity(force), "destiny.Force_validity")) continue;
                int id = InfoDefinitionKnown(current.ReadForcePhysicalSlot(force), "destiny.Force_identity");
                if (id is 0 or 51 or >= 46 and <= 50) continue;
                ushort rawRuler = InfoDefinitionKnown(current.ReadForceRecord(id)?.RawRuler10, "destiny.Force_ruler");
                var ruler = (rawRuler < 6001 ? current.ReadPersonLookupReference(rawRuler)
                    : current.ReadTransferPersonTableReference(0)) ?? throw InfoDefinitionMissing("destiny.ruler_lookup");
                int person = InfoDefinitionKnown(ruler.PhysicalSlot, "destiny.ruler_receiver");
                int rulerId = InfoDefinitionKnown(current.ReadPersonIdentity(person), "destiny.ruler_identity");
                var officer = state.Officers.GetValueOrDefault(rulerId) ?? throw InfoDefinitionMissing("destiny.ruler_current");
                int rawDoctrine = InfoDefinitionKnown(OfficerDoctrineRules.ReadDoctrineId(officer.DoctrineId, officer.AbilityState), "destiny.ruler_doctrine");
                int? doctrinePointer = (current.ReadStaticTableReference(0x78CF8, (uint)rawDoctrine < 7 ? rawDoctrine : 0)
                    ?? throw InfoDefinitionMissing("destiny.doctrine_lookup")).PhysicalSlot;
                if (doctrinePointer is null) continue;
                int doctrine = doctrinePointer.Value;
                //233F10 doctrine validity is its nonempty current name.
                string doctrineName = CurrentDoctrineName(doctrine);
                if (doctrineName.Length == 0) continue;
                int doctrineAnchor = InfoDefinitionKnown((current.ReadStaticTableReference(0x78CF8, 0)
                    ?? throw InfoDefinitionMissing("destiny.doctrine_anchor_lookup")).PhysicalSlot, "destiny.doctrine_anchor_receiver");
                int relativeDoctrine = doctrine - doctrineAnchor;
                int group = relativeDoctrine switch { 1 or 6 => 0, 2 or 3 => 1, 4 or 5 => 2, _ => -1 };
                if (group < 0) continue;
                string name = state.Forces.GetValueOrDefault(id)?.Name ?? officer.Name;
                ushort? value = state.NativeForceUnregisterCurrent?.ForceRankings.GetValueOrDefault(id)?.Word1BC;
                counts[group]++;
                if (value.HasValue) sums[group] += value.Value;
                else { complete[group] = false; issues.Add("information.destiny.Force1BC"); }
                if (!value.HasValue) representativeComplete[relativeDoctrine] = false;
                else if (representatives[relativeDoctrine]?.Popularity is not long maximum || value.Value > maximum)
                {
                    string rulerName = officer.CurrentSurname is {} surname && officer.CurrentGivenName is {} given
                        ? surname + given : officer.Name;
                    representatives[relativeDoctrine] = new(relativeDoctrine, doctrineName, id, rulerId,
                        rulerName, current.ReadPersonPortraitAppearance(person), value.Value, null);
                }
                var cells = ImmutableDictionary.CreateBuilder<string, InformationCell>(StringComparer.Ordinal);
                cells["name"] = new(name, SortText: name);
                cells["doctrine"] = new(doctrineName, Number: relativeDoctrine);
                cells["popularity"] = value.HasValue ? new(Number: value.Value)
                    : new(UnresolvedOwner: "information.destiny.Force1BC");
                rows.Add(new((int)InformationListKind.Destiny, id, name, cells.ToImmutable()));
            }
            catch (InvalidDataException error)
            { issues.Add(error.Message); Array.Fill(complete, false); Array.Fill(representativeComplete, false); }
        }

        var groups = ImmutableArray.CreateBuilder<InformationDestinyGroup>();
        int[][] pairs = [[1, 6], [2, 3], [4, 5]];
        string totalCaption = ReadHistoryDataText(1, 0x8AE);
        string countCaption = ReadHistoryDataText(1, 0x8AF);
        for (int group = 0; group < pairs.Length; group++)
        {
            ImmutableArray<string> names;
            var members = ImmutableArray.CreateBuilder<InformationDestinyRepresentative>();
            try
            {
                names = pairs[group].Select(raw => CurrentDoctrineName(
                    InfoDefinitionKnown((current.ReadStaticTableReference(0x78CF8, raw)
                        ?? throw InfoDefinitionMissing("destiny.summary_doctrine_lookup")).PhysicalSlot,
                        "destiny.summary_doctrine_receiver"))).ToImmutableArray();
            }
            catch (InvalidDataException error) { issues.Add(error.Message); names = ["", ""]; }
            for (int index = 0; index < 2; index++)
            {
                int doctrine = pairs[group][index];
                members.Add(!representativeComplete[doctrine]
                    ? new(doctrine, names[index], null, null, null, null, null, "information.destiny.representative_current")
                    : representatives[doctrine] ?? new(doctrine, names[index], null, null, null, null, null, null));
            }
            for (int index = 0; index < members.Count; index++)
            {
                var member = members[index];
                if (member.RulerId is not int ruler) continue;
                try
                {
                    ushort appearance = checked((ushort)(member.PortraitAppearanceCode
                        ?? throw InfoDefinitionMissing("destiny.representative_appearance")));
                    var route = ReadOfficerPortraitAppearance(state, current, _definitions, ruler, appearance, ArmyMapUiSources.Create());
                    if (route.Kind != OfficerPortraitRouteKind.StockNamed)
                        throw new InvalidDataException(route.UnresolvedInput ?? "information.destiny.representative_portrait");
                    var sprite = new LocalizedUiSpriteSource(_repository).LoadTexturePackSprite(route.ResourceName!, route.SpriteName!);
                    members[index] = member with { Portrait = sprite };
                }
                catch (InvalidDataException error) { members[index] = member with { UnresolvedOwner = error.Message }; }
            }
            groups.Add(new(group, names, totalCaption, complete[group] ? sums[group] : null,
                countCaption, complete[group] ? counts[group] : null, members.ToImmutable()));
        }
        //8BBDD0 orders total descending, then first doctrine identity ascending.
        //Without a total the original placement and winning pair remain unknown.
        var ordered = complete.All(value => value)
            ? groups.OrderByDescending(group => group.Total).ThenBy(group => pairs[group.Index][0]).ToImmutableArray()
            : groups.ToImmutable();
        var topNames = complete.All(value => value) && sums.Count(value => value == sums.Max()) == 1
            ? ordered[0].DoctrineNames : ImmutableArray<string>.Empty;

        int[][] messagesByChannel =
        [
            [0x34CD, 0, 0x34CE, 0x34CF, 0x34D0, 0x34D1, 0x34D2, 0x34D3, 0x34D4,
                0, 0x34D5, 0x34D6, 0x34D7, 0x34D8, 0x34D9, 0x34DA, 0x34DB],
            [0x34DC, 0, 0x34DD, 0x34DE, 0x34DF, 0x34E0, 0x34E1, 0,
                0x34E2, 0x34E3, 0x34E4, 0x34E5, 0x34E6, 0x34E7, 0x34E8],
            [0x34E9, 0, 0x34EA, 0x34EB]
        ];
        var channels = ImmutableArray.CreateBuilder<InformationDestinyExplanationChannel>();
        var formatter = new NarrationResourceMessages(_repository.LoadMessages(Seed.Language, MessageKind.Game).Messages);
        for (int channel = 0; channel < messagesByChannel.Length; channel++)
        {
            var explanations = ImmutableArray.CreateBuilder<InformationDestinyExplanationRow>();
            for (int index = 0; index < messagesByChannel[channel].Length; index++)
            {
                int message = messagesByChannel[channel][index];
                if (message == 0) { explanations.Add(new(index, null, "", null)); continue; }
                try { explanations.Add(new(index, message, formatter.ResolveInformationExplanation(message), null)); }
                catch (InvalidDataException error)
                { issues.Add(error.Message); explanations.Add(new(index, message, null, error.Message)); }
            }
            channels.Add(new(channel, 0xCD + channel, ReadHistoryDataText(1, 0xCD + channel), explanations.ToImmutable()));
        }
        string? unresolved = issues.Count == 0 ? null : string.Join(";", issues);
        var summary = new InformationDestinyPage(ReadHistoryDataText(1, 0x83F), ReadHistoryDataText(1, 0x8AC),
            topNames, ordered, channels.ToImmutable(), unresolved);
        return new(title, [InformationColumnFor("name", ReadHistoryDataText(1, 0x34F)),
            InformationColumnFor("doctrine"), InformationColumnFor("popularity")], rows.ToImmutable(),
            unresolved) { DestinySummary = summary };

        string CurrentDoctrineName(int physical)
        {
            if (state.Doctrines.TryGetValue(physical, out var definition)) return definition.Name;
            var catalog = _scenario.Snapshot.DoctrineParameters?.Doctrines
                ?? throw InfoDefinitionMissing("destiny.doctrine_catalog");
            if ((uint)physical >= catalog.Count || physical is >= 1 and <= 6)
                throw InfoDefinitionMissing("destiny.doctrine_current");
            return catalog[physical].Name;
        }
    }
}
