using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace San14.Core;

public interface ISaveCodec
{
    string FormatId { get; }
    byte[] Encode(GameState state);
    GameState Decode(ReadOnlySpan<byte> bytes);
}

public sealed record SaveEnvelope(int SchemaVersion, string FormatId, GameState State);

/// <summary>Local save format. A future original-save adapter can implement ISaveCodec.</summary>
public sealed class JsonGzipSaveCodec : ISaveCodec
{
    public const int CurrentSchemaVersion = 61;
    public string FormatId => "san14-remake-json-gzip";

    public byte[] Encode(GameState state)
    {
        StateValidator.Validate(state);
        using var output = new MemoryStream();
        using (var zip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
            JsonSerializer.Serialize(zip, new SaveEnvelope(CurrentSchemaVersion, FormatId, state));
        return output.ToArray();
    }

    public GameState Decode(ReadOnlySpan<byte> bytes)
    {
        using var input = new MemoryStream(bytes.ToArray());
        using var zip = new GZipStream(input, CompressionMode.Decompress);
        JsonObject document = JsonNode.Parse(zip) as JsonObject
            ?? throw new InvalidDataException("Save data is empty.");
        int version = document["SchemaVersion"]?.GetValue<int>()
            ?? throw new InvalidDataException("Save schema version is missing.");
        // Schema 1 stored PlayerForceId and Locale in GameState. They are ignored when
        // reading it; controller assignments and language belong to each session/view.
        // Schema 4 adds optional force-rank references and their source parameters.
        // Earlier snapshots retain unknown rank references; decoding never guesses them.
        // Schema 6 adds prepared deployments and source combat/area-link definitions.
        // Old saves default to an empty pending queue and unknown capability/adjacency arrays.
        // Schema 7 adds optional officer sex. Missing older values stay unknown;
        // decoding does not depend on an installation, a name or a portrait lookup.
        // Schema 8 adds independent office definitions and optional officer references.
        // Missing older definitions remain empty and references remain unknown.
        // Schema 9 adds optional officer presence, separate from role. Missing values
        // stay unknown; neither role nor the installed scenario supplies a default.
        // Schema 10 adds optional governance configurations and their pending accounting.
        // Missing earlier governance remains unknown; fixed source data is never used to fill it.
        // Schema 11 adds doctrine definitions and independent personal/force references.
        // Missing older definitions remain empty and references unknown; decoding never
        // derives the force's type from its current ruler or reads original resources.
        // Schema 12 adds policy definitions and independent optional personal policy
        // and base-cost fields. Missing older data remains empty/null, without source lookup.
        // Schema 13 adds independent effect definitions and ordered policy effect
        // references. Missing old tables remain empty and references null.
        // Schema 14 adds independent item definitions and optional complete inventory.
        // Missing legacy definitions stay empty; inventory, holders and presence remain
        // unknown. Loading never consults original files to replenish an old inventory.
        // Area focus None is appended at value 3. Existing 0/1/2 values retain their
        // meaning; null is never migrated into a known None value.
        // Schema 15 stores explicit new-game player options independently of their
        // effects. Missing older options remain null; DifficultyId never supplies
        // defaults for the other choices, and loading never reapplies initialization.
        // Schema16 retains typed period reports. Missing older reports are known empty.
        // Schema17 retains deployed unit configuration; older absent fields stay unknown.
        // Schema18 retains city duty appointments and current recruitment preparation.
        // Missing earlier duties remain unresolved, not verified vacant.
        // Schema19 retains typed unit missions and transport cargo. Older null missions
        // remain unknown in generic saves; playable sessions upgrade them to combat.
        // Schema20 retains pending officer relocation plans. Older missing plans stay
        // unknown in generic saves and become known empty only in playable sessions.
        // Schema21 adds an optional target area to typed occupation missions. Older
        // combat and transport missions have no occupation target.
        // Schema22 adds the explicit retreat mission. Earlier mission enum values
        // keep their numeric identities and remain readable without migration.
        // Schema23 keeps combat food in source cities. Old playable combat-unit
        // reservations are refunded once, while transport cargo stays with its unit.
        // Schema24 retains talent tasks. Missing legacy plans remain unknown in generic
        // saves and become known empty only through the playable-session upgrade.
        // Schema25 adds nullable source policy group codes; older saves keep them unknown.
        // Schema26 adds nullable map constructions and typed facility missions. Old saves
        // do not invent existing buildings; earlier mission enum values stay stable.
        // Schema27 retains sortie building types and completion separately from durability.
        // Old playable schema26 full-durability structures gain completion on session upgrade.
        // Schema28 retains candidate formation base coefficients from the selected source.
        // Older saves leave them unknown; decoding does not re-read the installation.
        // Schema29 retains two opaque CE force-row matrices. Earlier saves keep
        // them unknown; neither matrix establishes a diplomatic rule by itself.
        // Schema30 retains military waypoint cursors and terminal actions,
        // independent diplomacy missions/treaties/requests, explicit tactic queues,
        // cooldowns/statuses/detail reports, and optional city-or-gate report identity.
        // It also retains optional per-field area development and independent event
        // revisions, execution stamps, flags/counters and localized report keys.
        // Immediate personnel changes retain quoted accounting, effect revision,
        // reported-period identity and nullable current loyalty independently.
        // Missing legacy values stay null in this generic codec. Only Start or
        // UpgradeSession creates explicitly provisional playable state; decoding
        // never consults the original installation or invents diplomatic treaties.
        // Schema31 preserves independent raw area economy inputs, calculation
        // provenance on period summaries and source monthly-income receipts.
        // Missing older economy sources/receipts remain unknown; decoding never
        // computes a receipt or copies raw coefficients into development progress.
        // Schema32 preserves nullable wounded troops and explicit native subday
        // clock/Army waiting, abnormal, modifier and readiness state. Legacy
        // schema31 and earlier retain null; no original quantities or clock epoch
        // are invented on decode or playable upgrade.
        // Schema33 preserves source-created Army origin/configuration and the
        // remake's explicit current native parameter/force/world owner. Earlier
        // snapshots keep these facts null and never consult source files. It
        // also preserves the independent second/third native map-season dates
        // and their owned writer cursor; Game.Date is never copied into them.
        // Optional owned Person ability fields extend this current owner. A
        // missing record remains null, including older schema33 snapshots.
        // Optional native Army morale1A is independent of provisional Morale;
        // omitted older snapshots retain null without source reconstruction.
        // Native saved-region Hex152 is also nullable and independent of the
        // current movement Hex and Core RegionId.
        // Schema34 retains the optional normal-combat calendar observation
        // cursor. Older saves stay null; attaching at their current calendar
        // boundary never invents historical phase-local Army snapshots.
        // Schema36 retains current Person/City return fields and the owned
        // Army lifecycle arena, including exact allocation order and pending
        // images. Missing older modules remain unknown; loading never rebuilds
        // an arena from active Units or rereads the original installation.
        // Schema37 retains independent current Person XP, nullable pending
        // merit and ordered per-Person ability-growth notifications. Missing
        // older modules stay unknown; decoding does not initialize XP from
        // ability values or manufacture an empty notification vector.
        // Schema38 retains nullable current Person merit/reward counts/reset
        // alongside the existing ability stage/tail and growth pending-merit
        // owners. Old missing modules stay unknown; loading never runs fresh
        // credit initialization or clears accumulated pending rewards.
        // Schema39 adds optional Force result fields and World selection.
        // Older missing fields remain unknown; constructor reset bytes are
        // not promoted to final fresh or load-time defaults.
        // Schema40 retains optional current Gate durability, independent of
        // Base capacity. Older absent wall stocks stay unknown; decoding never
        // reads source objects or substitutes a capacity/nominal wall value.
        //Schema41 keeps remake command reports explicitly queued until the
        //next successful formal report. Earlier missing queues stay empty;
        //loading does not guess which old period entries were still unread.
        //Schema42 preserves unresolved force-defeat Army/person disposition.
        //Earlier missing ledgers stay empty; no lost cargo or prisoner history
        //is reconstructed from current officers or the installed scenario.
        //Schema43 preserves nullable native Army return resource dwords. Old
        //saves retain unknown; loading never repeats constructor/reset writes.
        //Schema46 preserves optional current Gate morale44/wounded40. Earlier
        //missing owners stay unknown; loading does not rerun the shared ctor
        //or borrow the scenario's initial morale as the current value.
        //Schema47 preserves actual Army override target3C/3E. Missing legacy
        //fields remain unknown; typed provocation owners do not synthesize raw.
        //Schema48 preserves the nullable current native Person District byte,
        //including real District0. Earlier missing values remain unknown.
        //Schema51 retains the optional source terminal result. Missing earlier
        //records stay null; decoding does not infer victory from ownership.
        //Schema52 retains independent Hex16, CityC9/CF and World1562 current
        //capture fields. Earlier missing modules stay unknown.
        //Schema53 preserves independent native Army Home sequence28 and
        //transport path/choice fields. Earlier saves keep them unknown.
        //Schema54 preserves nullable current Army byte13C in units and free
        //receivers. Missing older bytes remain unknown, never reset on load.
        //Schema55 preserves native command control140 for current units and
        //free receivers. Missing older values stay unknown on decoding.
        //Schema56 preserves separate native changed-Army work/pending owners
        //and optional current transport48. Older saves are not reinitialized.
        //Schema58 removes the calendar writer code-window proof stamp. Prior
        //saves retain the same cursor fields; the obsolete extra is ignored.
        //Schema57 preserves independent nullable native route56/1F8 on units
        //and free receivers. Decode never synthesizes a path or fresh reset.
        if (version is < 1 or > CurrentSchemaVersion ||
            document["FormatId"]?.GetValue<string>() != FormatId)
            throw new InvalidDataException("Save format or schema version is unsupported.");
        if (document["State"] is not JsonObject)
            throw new InvalidDataException("Saved game state is missing or invalid.");
        // Schema59 replaces EXE proof hashes with the version of our own rules.
        // Migrate identity metadata only; all current field values and owner links remain untouched.
        if (version < 59) MigrateRuleIdentities((JsonObject)document["State"]!);
        // Schema60 names saved parameters by their meaning. This is the only
        // boundary that recognizes historical source-address keys; values stay unchanged.
        if (version < 60) LegacySimulationParameterSaveMigration.Apply((JsonObject)document["State"]!);
        if (version < 61) LegacyAreaStatisticsSaveMigration.Apply((JsonObject)document["State"]!);
        ValidateModuleSnapshotShape((JsonObject)document["State"]!, version);
        // A present options record must contain all choices. JSON constructor
        // defaults must not turn a missing boolean into a confirmed false choice.
        if (document["State"]?["NewGameOptions"] is JsonNode optionsNode &&
            (optionsNode is not JsonObject options ||
             !options.ContainsKey("DifficultyId") ||
             options["InitialCreditEnabled"] is not JsonValue initialCredit ||
             !initialCredit.TryGetValue<bool>(out _) ||
             options["CapturedOfficersEnabled"] is not JsonValue capturedOfficers ||
             !capturedOfficers.TryGetValue<bool>(out _)))
            throw new InvalidDataException("Saved new-game options are incomplete or invalid.");
        if (version < 3 && document["State"]?["Officers"] is JsonObject officers)
            foreach (JsonNode? node in officers.Select(entry => entry.Value))
                if (node is JsonObject officer && officer.ContainsKey("CityId"))
                {
                    officer["LocationId"] = officer["CityId"]?.DeepClone();
                    officer.Remove("CityId");
                }
        // Earlier saves had only an overseer field. Preserve an explicit known
        // assignment, but never invent a focus or interpret null as verified vacancy.
        // Schema 5 district budgets also remain unknown in old saves. Legacy force
        // budgets are retained verbatim, never copied into one or several districts.
        if (document["State"]?["Areas"] is JsonObject areas)
            foreach (JsonNode? node in areas.Select(entry => entry.Value))
                if (node is JsonObject area && area.ContainsKey("OverseerId"))
                {
                    if (version >= 5)
                        throw new InvalidDataException("Current save contains a legacy overseer field.");
                    if (area.ContainsKey("Administration"))
                        throw new InvalidDataException("Legacy save contains conflicting administration fields.");
                    area["Administration"] = area["OverseerId"] is JsonNode overseer
                        ? new JsonObject { ["OverseerId"] = overseer.DeepClone(), ["DevelopmentFocus"] = null }
                        : null;
                    area.Remove("OverseerId");
                }
        SaveEnvelope envelope;
        try
        {
            envelope = document.Deserialize<SaveEnvelope>()
                ?? throw new InvalidDataException("Save data is empty.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("Saved state contains an invalid typed field or collection.", error);
        }
        GameState state = version < 23 ? MigrateCombatFood(envelope.State) : envelope.State;
        StateValidator.Validate(state);
        return state;
    }

    private static void MigrateRuleIdentities(JsonNode node)
    {
        if (node is JsonObject owner)
        {
            foreach (string oldKey in new[] { "ExecutableSha256", "SourceExecutableSha256" })
                if (owner.ContainsKey(oldKey))
                {
                    owner["RuleVersion"] = owner[oldKey] is null ? null : SimulationRuleIdentity.Version;
                    owner.Remove(oldKey);
                }
            foreach (JsonNode? child in owner.Select(pair => pair.Value).ToArray())
                if (child is not null) MigrateRuleIdentities(child);
        }
        else if (node is JsonArray children)
            foreach (JsonNode? child in children)
                if (child is not null) MigrateRuleIdentities(child);
    }

    // An absent nullable module is legacy/unknown. A present module is a complete
    // snapshot: missing constructor fields must not quietly become zero, false,
    // or an empty task queue through JSON constructor defaults.
    private static void ValidateModuleSnapshotShape(JsonObject state, int version)
    {
        if (state["NativeBaseCaptureCurrent"] is JsonNode captureNode)
        {
            JsonObject capture = Require(captureNode, "native Base capture current", "Revision", "HexWords16", "Cities", "World1562");
            if (capture["HexWords16"] is not JsonArray || capture["Cities"] is not JsonObject captureCities)
                throw new InvalidDataException("Saved Base capture current directories are missing.");
            foreach (JsonNode? city in captureCities.Select(row => row.Value))
                _ = Require(city, "native City capture current", "C9", "CF");
        }
        if (state["NativePlayableTerminal"] is JsonNode terminal)
            _ = Require(terminal, "native playable terminal", "Kind", "PlayerForcePhysical", "TriggerVictimPhysical", "RuleId");
        if (version >= 42 || state.ContainsKey("ForceDefeats"))
        {
            if (state["ForceDefeats"] is not JsonObject defeats)
                throw new InvalidDataException("Saved force defeat ledger is missing or invalid.");
            foreach (JsonNode? entry in defeats.Select(pair => pair.Value))
            {
                JsonObject record = Require(entry, "force defeat", "ForceId", "Period", "CapturedCityId", "CapturingForceId", "RuleId",
                    "WithdrawnUnits", "UnresolvedOfficerIds", "MissingRuleKeys");
                if (record["WithdrawnUnits"] is not JsonArray withdrawals || record["UnresolvedOfficerIds"] is not JsonArray || record["MissingRuleKeys"] is not JsonArray)
                    throw new InvalidDataException("Saved force defeat disposition collections are missing.");
                foreach (JsonNode? unitNode in withdrawals)
                {
                    JsonObject unit = Require(unitNode, "withdrawn Army", "Id", "ForceId", "OfficerId", "HexId", "DestinationHexId", "Troops", "Supplies", "Morale",
                        "SourceLocationId", "FormationId", "TacticIds", "Behavior", "Mission", "WoundedTroops", "MilitaryPlan", "TacticRuntime");
                    CheckUnitModules(unit);
                }
            }
        }
        if (version >= 41 || state.ContainsKey("PendingCommandReports"))
        {
            if (state["PendingCommandReports"] is not JsonArray pendingReports)
                throw new InvalidDataException("Saved pending command reports are missing or invalid.");
            foreach (JsonNode? report in pendingReports)
                _ = Require(report, "pending command report", "Kind", "ForceId", "CityId", "OfficerId",
                    "Amount", "FromHexId", "ToHexId", "LocationId", "RuleId", "MissingRuleKeys");
        }
        if (state["Officers"] is JsonObject officers)
            foreach (JsonNode? officerNode in officers.Select(pair => pair.Value))
            {
                if (officerNode is JsonObject destructionOwner && destructionOwner["NativeDestructionRuntime"] is JsonNode destruction)
                    _ = Require(destruction, "current Person destruction runtime", "Revision", "Word19A", "Byte19C",
                        "Byte19D", "Byte1A0", "Word1A4");
                if (officerNode is JsonObject creditOwner && creditOwner["NativeCreditRuntime"] is JsonNode credit)
                    _ = Require(credit, "current Person credit", "Revision", "Merit1F6",
                        "FirstRewardCount1FA", "SecondRewardCount1FB", "Reset1FC");
                if (officerNode is JsonObject growthOwner && growthOwner["NativeGrowthRuntime"] is JsonNode growthNode)
                {
                    JsonObject growth = Require(growthNode, "current Person growth", "Revision",
                        "SourceExperienceUri", "SourceExperienceSha256", "Experience13C",
                        "PendingMerit1F8", "AbilityGrowthNotices");
                    if (growth["AbilityGrowthNotices"] is JsonNode noticesNode)
                    {
                        if (noticesNode is not JsonArray notices)
                            throw new InvalidDataException("Saved Person growth notices are invalid.");
                        foreach (JsonNode? notice in notices)
                            _ = Require(notice, "Person ability growth notice", "AbilityIndex", "After4",
                                "Before5", "ReportContext8", "RawC", "Raw10");
                    }
                }
                if (officerNode is JsonObject locationOwner && locationOwner["NativeLocationRuntime"] is JsonNode location)
                    _ = Require(location, "current Person location", "Revision", "Second11C", "TraitMask1BC", "TraitExtras1BE");
                if (officerNode is JsonObject freedomOwner && freedomOwner["NativeFreedomRuntime"] is JsonNode freedom)
                    _ = Require(freedom, "current Person freedom", "Revision", "Qword18A", "Dword192", "Word19E");
                if (officerNode is JsonObject officer && officer["NativeAbilityRuntime"] is JsonNode person)
                    _ = Require(person, "owned Person ability runtime", "Revision", "Language", "RuleVersion",
                        "SourceScenarioUri", "SourceScenarioSha256", "PhysicalSlot", "Status11E", "Office11F",
                        "Doctrine184", "Flags196", "Health199", "Secondary1F4", "Extra1FD",
                        "EncodedOrdinaryCap224", "EncodedCharmCap225");
            }
        if (state["Cities"] is JsonObject cities)
            foreach (JsonNode? cityNode in cities.Select(pair => pair.Value))
                if (cityNode is JsonObject city && city["NativeReturnRuntime"] is JsonNode fields)
                    _ = Require(fields, "current City return", "Revision", "Morale44", "Wounded40", "Governor2E");
        if (state["Gates"] is JsonObject gates)
            foreach (JsonNode? gateNode in gates.Select(pair => pair.Value))
                if (gateNode is JsonObject gate && gate["NativeCombatRuntime"] is JsonNode fields)
                    _ = Require(fields, "current Gate combat", "Revision", "Morale44", "Wounded40");
        if (state["Areas"] is JsonObject areas)
            foreach (JsonNode? areaNode in areas.Select(pair => pair.Value))
            {
                if (areaNode is not JsonObject area) continue;
                if (area["Development"] is JsonNode development)
                    _ = Require(development, "area development", "Agriculture", "Commerce", "Barracks");
                if (area["EconomySource"] is JsonNode source)
                    _ = Require(source, "area economy source", "RawField44", "RawField46", "RawField52",
                        "FixedRemainingBytesHex", "ScenarioRemainingBytesHex", "FixedDataSha256", "ScenarioSha256");
                if (area["NativeNormalAftermath"] is JsonNode aftermath)
                    _ = Require(aftermath, "current Area normal aftermath", "Quantity52");
            }
        if (state["MonthlyIncomeReports"] is JsonNode incomeReportsNode)
        {
            if (incomeReportsNode is not JsonArray incomeReports)
                throw new InvalidDataException("Saved monthly income reports are invalid.");
            foreach (JsonNode? reportNode in incomeReports)
            {
                JsonObject report = Require(reportNode, "monthly income report", "CityId", "ForceId", "Period", "Quote");
                JsonObject quote = Require(report["Quote"], "monthly income quote", "Status", "RuleId", "MissingInputs", "Areas",
                    "Gold", "Supplies", "RuleVersion", "SettlementMonth", "CityId",
                    "GrossGold", "GrossSupplies", "GoldCapacity", "SuppliesCapacity", "CurrentGoldStock",
                    "CurrentSuppliesStock", "NextGoldStock", "NextSuppliesStock");
                if (quote["Areas"] is not JsonArray receipts)
                    throw new InvalidDataException("Saved monthly area receipts are invalid.");
                foreach (JsonNode? receiptNode in receipts)
                {
                    JsonObject receipt = Require(receiptNode, "monthly area receipt", "AreaId", "Status", "Inputs", "Steps", "MissingInputs");
                    if (receipt["Inputs"] is JsonArray inputs)
                        foreach (JsonNode? input in inputs) _ = Require(input, "economy input", "Name", "Value");
                    if (receipt["Steps"] is JsonArray steps)
                        foreach (JsonNode? step in steps) _ = Require(step, "economy step", "Name", "Expression", "Value");
                }
            }
        }
        if (state["Units"] is JsonObject units)
            foreach (JsonNode? unitNode in units.Select(pair => pair.Value))
            {
                if (unitNode is not JsonObject unit) continue;
                CheckUnitModules(unit);
            }
        if (state["NativeForceUnregisterCurrent"] is JsonNode forceUnregister)
            _ = Require(forceUnregister, "current Force unregister", "Revision", "DistrictWindows10",
                "ForceRankings", "ForceResetImages", "PlaceholderRelations13", "PlaceholderRelationsEA", "OtherForce1657");
        if (state["NativeArmyLifecycle"] is JsonNode lifecycleNode)
        {
            JsonObject lifecycle = Require(lifecycleNode, "native Army lifecycle", "Revision", "Language", "RuleVersion",
                "SourceScenarioUri", "SourceScenarioSha256", "Pool", "Slots", "RegistryDescriptors",
                "Pending70Descriptor", "BaseRegistryDescriptor", "BaseLists", "HexRaw1C");
            CheckSavedPoolShape(lifecycle["Pool"], "native shared pool");
            if (lifecycle["Slots"] is not JsonArray slots) throw new InvalidDataException("Saved native slots are invalid.");
            foreach (JsonNode? slotNode in slots)
            {
                JsonObject slot = Require(slotNode, "native Army slot", "PhysicalSlot", "Commander12", "Status10",
                    "Priority14", "ActiveUnitId", "PendingImage", "Gold5C", "Supplies60", "AuxiliaryAC", "AuxiliaryF4", "Trait160Descriptors");
                if (slot["OverrideTarget"] is JsonNode overrideTarget)
                    _ = Require(overrideTarget, "native Army receiver override target", "Kind3C", "Identity3E");
                if (slot["PendingImage"] is JsonNode pendingNode)
                {
                    JsonObject pending = Require(pendingNode, "pending Army image", "Id", "ForceId", "OfficerId", "HexId",
                        "DestinationHexId", "Troops", "Supplies", "Morale");
                    CheckUnitModules(pending);
                }
                if (slot["Trait160Descriptors"] is JsonArray traits)
                    foreach (JsonNode? trait in traits) _ = Require(trait, "native trait descriptor handle", "Identity");
            }
            if (lifecycle["RegistryDescriptors"] is not JsonObject registries)
                throw new InvalidDataException("Saved native registries are invalid.");
            foreach (JsonNode? handle in registries.Select(pair => pair.Value)) CheckHandle(handle);
            CheckHandle(lifecycle["Pending70Descriptor"]); CheckHandle(lifecycle["BaseRegistryDescriptor"]);
            if (lifecycle["BaseLists"] is not JsonArray bases) throw new InvalidDataException("Saved native Base lists are invalid.");
            foreach (JsonNode? baseNode in bases)
            {
                JsonObject binding = Require(baseNode, "native Base binding", "Kind", "PhysicalSlot", "HexDescriptor", "Person88Descriptor");
                CheckHandle(binding["HexDescriptor"]); CheckHandle(binding["Person88Descriptor"]);
            }
            if (lifecycle["ArmyFlags150"] is JsonNode flagsNode &&
                (flagsNode is not JsonArray armyFlags || armyFlags.Count != 501))
                throw new InvalidDataException("Saved native Army flags150 physical table is invalid.");
            if (lifecycle["Targets38"] is JsonNode targetsNode)
            {
                if (targetsNode is not JsonArray armyTargets || armyTargets.Count != 501)
                    throw new InvalidDataException("Saved native Army targets38 physical table is invalid.");
                foreach (JsonNode? target in armyTargets)
                    _ = Require(target, "native Army target", "Kind38", "Identity3A");
            }
            if (lifecycle["DeferredDestructionQueue"] is JsonNode destructionNode)
            {
                JsonObject queue = Require(destructionNode, "native destruction queue", "Revision", "Language", "RuleVersion",
                    "SourceScenarioUri", "SourceScenarioSha256", "Manager140DescriptorKey", "DescriptorFreeHead",
                    "DescriptorLiveHead", "DescriptorLiveTail", "DescriptorLiveCount", "Descriptors", "Heads10", "Tails18",
                    "Counts28", "FreeNodeHead20", "ActiveNodeCount30", "Nodes");
                foreach (string field in new[] { "Heads10", "Tails18", "Counts28" })
                    if (queue[field] is not JsonArray)
                        throw new InvalidDataException("Saved native destruction array is invalid: " + field);
                if (queue["Descriptors"] is not JsonArray destructionDescriptors || queue["Nodes"] is not JsonArray destructionNodes)
                    throw new InvalidDataException("Saved native destruction physical tables are invalid.");
                foreach (JsonNode? descriptor in destructionDescriptors)
                    _ = Require(descriptor, "native destruction descriptor", "Key", "Next8", "Previous10");
                foreach (JsonNode? node in destructionNodes)
                {
                    JsonObject row = Require(node, "native destruction node", "Payload48", "Next30", "Previous38");
                    JsonObject payload = Require(row["Payload48"], "native destruction payload", "VictimArmy0", "VictimBase8",
                        "AttackerArmy10", "AttackerBase18", "Raw20", "NativeMode24", "Cause28", "Padding2C");
                    foreach (string field in new[] { "VictimArmy0", "VictimBase8", "AttackerArmy10", "AttackerBase18" })
                        if (payload[field] is JsonNode reference)
                            _ = Require(reference, "native destruction receiver", "Kind", "PhysicalSlot");
                }
            }
            if (lifecycle["Auxiliaries"] is JsonNode auxiliariesNode)
            {
                JsonObject auxiliaries = Require(auxiliariesNode, "native Army lifecycle auxiliaries", "Revision",
                    "ArmyTroops58", "TroopsLeader2C", "CautionArmy10", "CautionArmy12", "CautionTroops18",
                    "CautionArmy30Descriptors", "AssociatedCommand18Descriptor", "InteractionPairs140");
                foreach (string field in new[] { "ArmyTroops58", "TroopsLeader2C", "CautionArmy10", "CautionArmy12", "CautionTroops18" })
                    if (auxiliaries[field] is not JsonArray)
                        throw new InvalidDataException("Saved native Army auxiliary array is invalid: " + field);
                if (auxiliaries["CautionArmy30Descriptors"] is not JsonArray cautionDescriptors)
                    throw new InvalidDataException("Saved native Caution Army descriptor array is invalid.");
                foreach (JsonNode? descriptor in cautionDescriptors) CheckHandle(descriptor);
                CheckHandle(auxiliaries["AssociatedCommand18Descriptor"]);
                if (auxiliaries["InteractionPairs140"] is JsonArray pairs)
                    foreach (JsonNode? pairNode in pairs)
                    {
                        JsonObject pair = Require(pairNode, "native Army interaction pair", "FirstArmy", "SecondArmy", "ThirdArmy");
                        foreach (string field in new[] { "FirstArmy", "SecondArmy", "ThirdArmy" })
                            if (pair[field] is JsonNode reference)
                                _ = Require(reference, "native Army interaction reference", "PhysicalSlot");
                    }
            }
            if (lifecycle["ChangedLists"] is JsonNode changedNode)
            {
                JsonObject changed = Require(changedNode, "native changed Army lists", "Revision", "Work8", "Pending18", "Busy90");
                CheckHandle(changed["Work8"]); CheckHandle(changed["Pending18"]);
            }
        }
        if (state["NativePersonCaptureWorldCandidates"] is JsonNode captureCandidatesNode)
        {
            JsonObject candidates = Require(captureCandidatesNode, "native Person capture World candidates",
                "Revision", "PersonIds165E", "Values1668");
            if (candidates["PersonIds165E"] is not JsonArray personIds || personIds.Count != 5 ||
                candidates["Values1668"] is not JsonArray candidateValues || candidateValues.Count != 5)
                throw new InvalidDataException("Saved native World capture candidate arrays are not the source five-slot windows.");
        }
        if (state["NativePersonReturnCommands"] is JsonNode commandsNode)
        {
            JsonObject manager = Require(commandsNode, "native Person return commands", "Revision", "FreeHeadEA8", "FreeNext30",
                "NextReceiverIdentity", "IdToReceiver", "Commands", "Secondary28Descriptor", "MembersPool", "TargetsPool", "TargetNodeValues");
            if (manager["FreeNext30"] is not JsonArray freeNext || freeNext.Count != 1852 ||
                manager["IdToReceiver"] is not JsonObject || manager["Commands"] is not JsonObject commands ||
                manager["TargetNodeValues"] is not JsonObject targetValues)
                throw new InvalidDataException("Saved native Person command physical owners are invalid.");
            CheckHandle(manager["Secondary28Descriptor"]);
            CheckSavedPoolShape(manager["MembersPool"], "native command member pool");
            CheckSavedPoolShape(manager["TargetsPool"], "native command target pool");
            foreach (JsonNode? commandNode in commands.Select(pair => pair.Value))
            {
                JsonObject command = Require(commandNode, "native return command", "Kind", "RawFields", "Members20", "Targets38");
                if (command["RawFields"] is not JsonArray rawFields || rawFields.Count != 128)
                    throw new InvalidDataException("Saved native return command numeric window is invalid.");
                CheckHandle(command["Members20"]); CheckHandle(command["Targets38"]);
            }
            foreach (JsonNode? value in targetValues.Select(pair => pair.Value))
                _ = Require(value, "native command union value", "FirstKind8", "FirstPhysical0", "SecondKindC", "SecondPhysical10");
        }
        static void CheckSavedPoolShape(JsonNode? poolNode, string label)
        {
            JsonObject pool = Require(poolNode, label, "DescriptorCapacity", "NodeCapacity", "ActiveNodeCount", "CreatedNodeCount",
                "FreeNodeHead", "NullDescriptorCount", "FreeDescriptors", "LiveDescriptors", "Descriptors", "Nodes");
            CheckRows(pool["Descriptors"], "native list descriptor", ["Head", "Tail", "Count"]);
            if (pool["Nodes"] is not JsonObject nodes) throw new InvalidDataException("Saved native nodes are invalid.");
            foreach (JsonNode? node in nodes.Select(pair => pair.Value))
            {
                JsonObject row = Require(node, "native list node", "Next", "Previous", "Payload");
                if (row["Payload"] is JsonNode payload) _ = Require(payload, "native list payload", "Kind", "Identity");
            }
        }
        static void CheckHandle(JsonNode? handle)
        { if (handle is not null) _ = Require(handle, "native list handle", "Identity"); }
        static void CheckUnitModules(JsonObject unit)
        {
                if (unit["MilitaryPlan"] is JsonNode planNode)
                {
                    JsonObject plan = Require(planNode, "military plan", "WaypointHexIds", "NextWaypointIndex", "Action");
                    JsonObject action = Require(plan["Action"], "military action", "ActionKey", "Target");
                    _ = Require(action["Target"], "military target", "Kind", "Id");
                }
                if (unit["TacticRuntime"] is JsonNode runtimeNode)
                {
                    JsonObject runtime = Require(runtimeNode, "tactic runtime", "Pending", "ReadyAtPeriod", "Statuses");
                    if (runtime["Pending"] is JsonNode pending)
                        _ = Require(pending, "queued tactic", "TacticId", "TargetUnitId", "IssuedAtPeriod", "ProfileRevision");
                    if (runtime["Statuses"] is JsonArray statuses)
                        foreach (JsonNode? status in statuses)
                            _ = Require(status, "tactic status", "Kind", "Amount", "SourceUnitId", "TacticId",
                                "ExpiresAtPeriod", "ProfileRevision");
                }
                if (unit["NativeTacticRuntime"] is JsonNode nativeNode)
                {
                    JsonObject native = Require(nativeNode, "native Army tactic runtime", "Status10", "Flags26",
                        "Waiting20", "Waiting21", "Waiting22", "Abnormal23", "Abnormal24", "Abnormal25",
                        "StatModifiers4A", "StatDurations50", "ProvocationOwner", "PrimaryWaitingReadyAtUpdateOrdinal");
                    if (native["ProvocationOwner"] is JsonNode owner)
                        _ = Require(owner, "native provocation owner", "ForceId", "Kind", "Id");
                }
                if (unit["NativeArmyOrigin"] is JsonNode armyOrigin)
                    _ = Require(armyOrigin, "source-created Army origin", "Revision", "Language", "RuleVersion", "NativeSlotIndex");
                if (unit["NativeOverrideTarget"] is JsonNode overrideTarget)
                    _ = Require(overrideTarget, "current native Army override target", "Kind3C", "Identity3E");
                if (unit["NativeMarch"] is JsonNode nativeMarch)
                    _ = Require(nativeMarch, "native Army march fields", "Credit14", "CurrentHex2A", "TargetHex2C",
                        "NextHex48", "OverrideTargetType3C");
                if (unit["NativeArmyConfiguration"] is JsonNode armyConfiguration)
                    _ = Require(armyConfiguration, "native Army configuration", "Formation1B", "Formation1C", "Action37", "Category40", "TraitSlots64");
                if (unit["NativeReturnResources"] is JsonNode returnResources)
                    _ = Require(returnResources, "native Army return resources", "Gold5C", "Supplies60");
                if (unit["NativeTransport"] is JsonNode transport)
                    _ = Require(transport, "native transport fields", "Waypoints2C", "Choice41",
                        "InitialNextHex48", "WrapperNextHex48");
        }
        if (state["NativeClock"] is JsonNode nativeClock)
        {
            JsonObject clock = Require(nativeClock, "native clock", "AnchorDate", "UpdateOrdinal", "UpdatesIntoPeriod", "Revision");
            _ = Require(clock["AnchorDate"], "native period anchor", "Year", "Month", "Day");
        }
        if (state["NativeSubdayReports"] is JsonNode nativeReportNode)
        {
            if (nativeReportNode is not JsonArray nativeReports)
                throw new InvalidDataException("Native subday report collection is invalid.");
            foreach (JsonNode? report in nativeReports)
                _ = Require(report, "native subday report", "Kind", "UpdateOrdinal", "UnitId", "ForceId", "NativeIndex", "Amount");
        }
        if (state["NativeArmySimulation"] is JsonNode nativeSimulationNode)
        {
            JsonObject simulation = Require(nativeSimulationNode, "owned native Army simulation", "Parameters", "Forces", "World");
            _ = Require(simulation["Parameters"], "owned native parameters", "Revision", "Language", "RuleVersion",
                "Int32Values", "Float32Values");
            if (simulation["Forces"] is not JsonObject forceFields)
                throw new InvalidDataException("Owned native force field collection is invalid.");
            foreach (JsonNode? forceFieldsNode in forceFields.Select(pair => pair.Value))
            {
                _ = Require(forceFieldsNode, "owned native force fields", "Flags193", "Flags12", "Flags194", "RelationsEA",
                    "ActivePolicyBitmap1CB", "SuppressedPolicyBitmap1D5");
                if (version >= 39)
                    _ = Require(forceFieldsNode, "owned native force result fields",
                        "ResultGate15F", "ResultDefinition160", "ResultValue162");
                if (forceFieldsNode is JsonObject forceFieldsObject && forceFieldsObject["NormalAftermath"] is JsonNode aftermath)
                    _ = Require(aftermath, "current Force normal aftermath", "Relations13", "Development1BE");
            }
            JsonObject world = Require(simulation["World"], "owned native world fields", "SelectedCommanderCount165D");
            if (version >= 39) _ = Require(world, "owned native world result selection", "SelectedForce1656");
            if (world["SamplerEpoch"] is JsonNode sampler)
                _ = Require(sampler, "owned native sampler epoch", "SourceScenarioUri", "SourceScenarioSha256",
                    "SourceWorldSha256", "Day450", "Salt45C", "NativeClockRevision", "Revision");
            if (world["ObjectPairs"] is JsonArray objectPairs)
                foreach (JsonNode? objectPair in objectPairs)
                    _ = Require(objectPair, "owned native object pair", "FirstArmyId", "BaseLocationId", "SecondArmyId");
        }
        if (state["NativeMapSeason"] is JsonNode nativeMapSeasonNode)
        {
            JsonObject season = Require(nativeMapSeasonNode, "owned native map season", "SourceScenarioUri", "SourceScenarioSha256",
                "SourceWorldSha256", "SecondDate", "ThirdDate", "ProcessedNativeOrdinal", "NativeClockRevision", "WriterRevision",
                "Phase", "AdvancedCalendarSteps", "AdvancedDays", "AdvancedPeriods", "AdvancedMonths");
            _ = Require(season["SecondDate"], "native second date", "Year", "Month", "Day", "Hour");
            _ = Require(season["ThirdDate"], "native third date", "Year", "Month", "Day", "Hour");
        }
        if (state["NativeNormalArmyCombatCalendar"] is JsonNode combatCalendarNode)
        {
            JsonObject cursor = Require(combatCalendarNode, "normal Army combat calendar cursor",
                "ProfileId", "SourceScenarioUri", "SourceScenarioSha256", "SourceWorldSha256", "NativeClockRevision",
                "StartCalendarCycleOrdinal", "NextCalendarCycleOrdinal", "ReachedDuePhaseCount",
                "LastAdvanceDiagnostics", "Revision");
            if (cursor["LastAdvanceDiagnostics"] is not JsonArray diagnostics)
                throw new InvalidDataException("Normal Army phase diagnostics are invalid.");
            foreach (JsonNode? node in diagnostics)
            {
                JsonObject diagnostic = Require(node, "normal Army phase diagnostic", "CalendarCycleOrdinal",
                    "PreWriterThirdDate", "Outcome", "OrderedArmyPairCount", "Reason", "SourceRevision");
                _ = Require(diagnostic["PreWriterThirdDate"], "normal Army pre-calendar date", "Year", "Month", "Day", "Hour");
            }
        }
        if (state["Diplomacy"] is JsonNode diplomacyNode)
        {
            JsonObject diplomacy = Require(diplomacyNode, "diplomacy", "Origin", "Relationships", "Treaties",
                "Missions", "AttackRequests", "NextMissionId");
            CheckRows(diplomacy["Relationships"], "diplomatic relationship",
                ["FirstForceId", "SecondForceId", "Level", "Origin"]);
            CheckRows(diplomacy["Treaties"], "diplomatic treaty",
                ["FirstForceId", "SecondForceId", "Kind", "StartedPeriod", "ExpiresPeriod", "Origin"]);
            CheckRows(diplomacy["Missions"], "diplomatic mission",
                ["Id", "ForceId", "SourceCityId", "DistrictId", "ExecutorOfficerId", "TargetForceId", "Kind",
                    "SubmittedPeriod", "ArrivalPeriod", "TreatyPeriods", "RequestPeriods", "TargetCityId",
                    "TargetCityForceId", "PaidGold", "PaidCommandBooks", "PolicyId"]);
            CheckRows(diplomacy["AttackRequests"], "diplomatic attack request",
                ["Id", "RequestingForceId", "RequestedForceId", "TargetCityId", "TargetForceId",
                    "AcceptedPeriod", "ExpiresPeriod", "Origin"]);
        }
        if (state["TacticReports"] is JsonArray reports)
            foreach (JsonNode? report in reports)
                _ = Require(report, "tactic report", "Kind", "Period", "ForceId", "UnitId", "OfficerId", "TacticId");
        if (state["EventRuntime"] is JsonNode eventRuntime)
        {
            JsonObject runtime = Require(eventRuntime, "event runtime", "LastSettledPeriod", "SourceRevisions",
                "Executions", "Flags", "Counters");
            CheckRows(runtime["Executions"], "event execution", ["Revision", "LastPeriod", "ExecutionCount"]);
        }
        if (state["EventReports"] is JsonArray eventReports)
            foreach (JsonNode? eventReport in eventReports)
            {
                JsonObject report = Require(eventReport, "event report", "Kind", "EventKey", "Revision", "Period");
                if (report["Source"] is JsonNode source)
                    _ = Require(source, "event source", "ResourceUri", "ResourceSha256", "DirectoryIndex", "EventId");
            }
        if (state["PersonnelReports"] is JsonArray personnelReports)
            foreach (JsonNode? report in personnelReports)
                _ = Require(report, "personnel report", "Kind", "Period", "ForceId", "OfficerId", "ItemId",
                    "PreviousHolderId", "NextHolderId", "PreviousOfficeId", "NextOfficeId", "CommandBooks",
                    "Gold", "LoyaltyDelta", "PolicyRevision", "DistrictId", "GoldCityId", "ReportedInPeriod");

        static JsonObject Require(JsonNode? node, string label, params string[] keys)
        {
            if (node is not JsonObject record || keys.Any(key => !record.ContainsKey(key)))
                throw new InvalidDataException($"Saved {label} is incomplete or invalid.");
            return record;
        }
        static void CheckRows(JsonNode? node, string label, string[] keys)
        {
            if (node is not JsonObject rows)
                throw new InvalidDataException($"Saved {label} collection is missing or invalid.");
            foreach (JsonNode? row in rows.Select(pair => pair.Value)) _ = Require(row, label, keys);
        }
    }

    private static GameState MigrateCombatFood(GameState state)
    {
        if (!PlayableSkeleton.IsPlayableRuleset(state.RulesetId)) return state;
        var cities = new Dictionary<int, CityState>(state.Cities);
        var units = new Dictionary<int, UnitState>(state.Units);
        foreach (UnitState unit in state.Units.Values.OrderBy(unit => unit.Id))
        {
            if (unit.Mission?.Kind == UnitMissionKind.Transport || unit.Supplies == 0)
                continue;
            if (unit.SourceLocationId is not int sourceId ||
                !cities.TryGetValue(sourceId, out CityState? source) ||
                source.ForceId != unit.ForceId)
                throw new InvalidDataException($"Legacy combat food for unit {unit.Id} has no owned source city; migration cannot refund it safely.");
            cities[sourceId] = source with { Supplies = checked(source.Supplies + unit.Supplies) };
            units[unit.Id] = unit with { Supplies = 0 };
        }
        return state with { Cities = cities, Units = units };
    }
}

public sealed class SaveStore(ISaveCodec codec)
{
    public void Save(string path, GameState state)
    {
        byte[] bytes = codec.Encode(state);
        AtomicSaveFiles.Write(path, bytes);
    }

    public GameState Load(string path) => codec.Decode(File.ReadAllBytes(path));
}

internal static class AtomicSaveFiles
{
    public static void Write(string path, byte[] bytes)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(fullPath)) File.Replace(temporary, fullPath, null);
            else File.Move(temporary, fullPath);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
