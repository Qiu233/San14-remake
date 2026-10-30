using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Reads the Item selected by an actual information/belongings
    /// caller. That caller owns admission, its candidate order and initial row.
    /// No title library, opening Item flags or discovery defaults are used.</summary>
    public ItemInformationPage QueryItemInformation(GameState state, int itemId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (itemId is < 1 or > 201) throw new ArgumentOutOfRangeException(nameof(itemId));
        var text = new Dictionary<int, string>();
        var unresolved = new Dictionary<int, string>();
        var page = new ItemInformationPage(itemId, "", text);
        foreach (var caption in ItemInformationSources.Captions)
            Text(caption.Node, () => ReadHistoryDataText(1, caption.Message));
        //The body initializes this ruby control but never writes a ruby name.
        text[11] = "";
        var item = state.Items?.GetValueOrDefault(itemId);
        var definition = item is null ? null : state.ItemDefinitions.GetValueOrDefault(item.DefinitionId);
        if (definition is null)
        {
            foreach (int node in new[] { 2, 7, 10, 17, 22, 27, 32, 37, 42, 47, 50, 53 })
                unresolved[node] = Missing("Item_current_definition").Message;
            return page with { UnresolvedFields = unresolved };
        }
        text[10] = definition.Name;
        page = page with { Name = definition.Name };
        Text(27, () => Math.Min(definition.LoyaltyValue ?? throw Missing("Item_value49"), 50)
            .ToString(CultureInfo.InvariantCulture));
        Text(32, () => ReadHistoryDataText(1,
            ((item!.Lifecycle?.RetainedFlags ?? throw Missing("Item_flags4F")) & 1) != 0 ? 0x338 : 0x339));

        if (definition.Appearance is { } appearance)
        {
            page = page with { ImageIndex = appearance.ImageIndex,
                CanAppreciatePoem = appearance.Kind == 26 && itemId is >= 132 and <= 151 };
            Text(22, () => appearance.Kind is >= 1 and <= 26 ? ReadHistoryDataText(0, 0x81E + appearance.Kind) : "");
            Text(37, () => appearance.AbilityKind == 0 ? "" :
                ReadHistoryDataText(0, 0x183 + appearance.AbilityKind - 1) + "+" +
                Math.Min(appearance.AbilityValue, 10).ToString(CultureInfo.InvariantCulture));
            Text(42, () => appearance.EffectKind is 1 or 2 ? ReadHistoryDataText(0, 0x839 + appearance.EffectKind) : "");
            //Historical resource text and authored custom text were captured
            //by the fresh Item definition producer, and survive save/restore.
            text[47] = appearance.Description;
        }
        else
            foreach (int node in new[] { 2, 22, 37, 42, 47 })
                unresolved[node] = Missing("Item_current_appearance").Message;

        Read(53, () =>
        {
            var holder = item!.Holder ?? throw Missing("Item_holder4A");
            int raw = holder.OfficerId ?? 0;
            page = page with { HolderOfficerId = raw };
            //One existing lightweight current capture serves the selected
            //holder, ruler and flag. There is no item/army list recapture.
            var current = CaptureDetailForceInputs(state) ?? throw Missing("current_Person_Force_owner");
            var person = (raw <= 6000 ? current.ReadPersonLookupReference(raw)
                : current.ReadTransferPersonTableReference(0)) ?? throw Missing("holder_lookup");
            int physical = Need(person.PhysicalSlot, "holder_receiver");
            bool holderValid = Need(current.ReadPersonNativeValidity(physical), "holder_validity");
            page = page with { HolderValid = holderValid };
            Text(53, () => holderValid ? PersonName(physical) : "");
            if (!holderValid) text[17] = "";
            Read(50, () =>
            {
                ushort appearance = holderValid ? Need(current.ReadPersonPortraitAppearance(physical), "holder_appearanceA6")
                    : (ushort)ItemInformationSources.InvalidHolderAppearanceId;
                int identity = holderValid ? Need(current.ReadPersonIdentity(physical), "holder_identity") : 0;
                var portrait = ReadOfficerPortraitAppearance(state, current, _definitions, identity, appearance, ArmyMapUiSources.Create());
                page = page with { HolderPortraitAppearanceId = appearance, HolderPortrait = portrait };
                if (portrait.Kind == OfficerPortraitRouteKind.Unresolved)
                    unresolved[50] = portrait.UnresolvedInput ?? Missing("holder_portrait").Message;
            });

            Read(7, () =>
            {
                var force = current.ReadPersonForce(physical) ?? throw Missing("holder_District_Force");
                int forceId = Need(force.PhysicalSlot, "Force_receiver");
                page = page with { ForceId = forceId };
                bool forceValid = Need(current.ReadForceNativeValidity(force), "Force_validity");
                page = page with { ForceFlagValid = forceValid };
                Read(7, () =>
                {
                    page = page with { ForceColorIndex = Need(current.ReadForceReportColor(force), "Force_color") };
                    _mapInformationNames ??= ArmyFlagPaneNames.Load(_scenario);
                    page = page with { ForceFlag = ReadDetailForceAppearance(state, current,
                        _mapInformationNames, force, forceValid, MapSpritePalettes.Create()) ?? throw Missing("Force_flag") };
                });
                Text(17, () =>
                {
                    if (!holderValid || !forceValid) return "";
                    ushort rulerId = Need(current.ReadForceRecord(forceId)?.RawRuler10, "Force_ruler10");
                    var ruler = (rulerId <= 6000 ? current.ReadPersonLookupReference(rulerId)
                        : current.ReadTransferPersonTableReference(0)) ?? throw Missing("Force_ruler_lookup");
                    int rulerPhysical = Need(ruler.PhysicalSlot, "Force_ruler_receiver");
                    return Need(current.ReadPersonNativeValidity(rulerPhysical), "Force_ruler_validity") ? PersonName(rulerPhysical) : "";
                });
            });
            //Known invalid holder uses appearance896 at node50. Unknown
            //validity must not be turned into that known-invalid appearance.
            string PersonName(int receiver)
            {
                int identity = Need(current.ReadPersonIdentity(receiver), "Person_identity");
                return state.Officers.GetValueOrDefault(identity)?.Name ?? throw Missing("Person_current_name12_20");
            }
        });
        //A failure before a shared holder/Force dependency is known affects
        //these controls too; retain every independent Item field above.
        if (!text.ContainsKey(17) && !unresolved.ContainsKey(17))
            unresolved[17] = unresolved.GetValueOrDefault(7) ?? unresolved.GetValueOrDefault(53) ?? Missing("holder_Force_name").Message;
        if (page.HolderPortrait is null && !unresolved.ContainsKey(50))
            unresolved[50] = unresolved.GetValueOrDefault(53) ?? Missing("holder_portrait").Message;
        if (page.ForceFlag is null && !unresolved.ContainsKey(7))
            unresolved[7] = unresolved.GetValueOrDefault(17) ?? unresolved.GetValueOrDefault(53) ?? Missing("holder_Force_color").Message;
        return page with { UnresolvedFields = unresolved };

        void Text(int node, Func<string> read) => Read(node, () => text[node] = read());
        void Read(int node, Action read)
        {
            try { read(); }
            catch (InvalidDataException error) { unresolved[node] = error.Message; }
        }
        static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        static InvalidDataException Missing(string field) => new("information.Item." + field);
    }

    /// <summary>The ordinary Item's Poem button opens the normal static poem
    /// detail. Its metadata is keyed by the selected current Item identity;
    /// only its name is mutable. This does not open title appreciation media.</summary>
    public CollectionPoemDetail ReadItemPoemInformation(GameState state, int itemId)
    {
        if (itemId is < 132 or > 151) throw new ArgumentOutOfRangeException(nameof(itemId));
        var item = state.Items?.GetValueOrDefault(itemId)
            ?? throw new InvalidDataException("information.Item.poem_current_Item");
        var definition = state.ItemDefinitions.GetValueOrDefault(item.DefinitionId)
            ?? throw new InvalidDataException("information.Item.poem_current_definition");
        if (definition.Appearance is not { } appearance)
            throw new InvalidDataException("information.Item.poem_current_appearance");
        if (appearance.Kind != 26) throw new InvalidOperationException("The selected Item is not a poem.");
        //The same offline-closed normal8CF3B0 map used by CollectionCatalog;
        //these are immutable art choices, not a title collection's admission.
        ReadOnlySpan<int> images = [1, 2, 3, 11, 4, 5, 6, 12, 7, 8, 9, 9, 9, 9, 9, 10, 10, 10, 10, 10];
        int image = images[itemId - 132];
        return new(itemId, definition.Name, ReadHistoryDataText(0, itemId + 0x16E2),
            ReadHistoryDataText(0, itemId + 0x16F7), ReadHistoryDataText(0, itemId + 0x170C),
            new($"texture_stl_poems_{image:D3}", $"stl_event_p_{image:D3}_trim", 28, image));
    }
}
