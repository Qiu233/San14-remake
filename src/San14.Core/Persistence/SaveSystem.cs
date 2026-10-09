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
    public const int CurrentSchemaVersion = 70;
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
        // Historical schemas are migrated once; normal simulation uses the current domain state.
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
        if (version < 62)
        {
            LegacyCombatSettlementSaveMigration.Apply((JsonObject)document["State"]!);
            LegacyPersonnelReturnTasksMigration.Apply((JsonObject)document["State"]!);
        }
        if (version < 63) LegacyWorldIndexesSaveMigration.Apply((JsonObject)document["State"]!);
        if (version < 64) LegacyArmyGroupsSaveMigration.Apply((JsonObject)document["State"]!);
        if (version < 65) LegacyArmyCautionsSaveMigration.Apply((JsonObject)document["State"]!);
        if (version < 66) LegacyArmyMapOccupancySaveMigration.Apply((JsonObject)document["State"]!);
        if (version < 67) LegacyOfficeBonusEncodingSaveMigration.Apply((JsonObject)document["State"]!);
        if (version < 68) LegacyForceCombatObjectiveSaveMigration.Apply((JsonObject)document["State"]!, version);
        if (version < 69) LegacyForcePolicyEffectsSaveMigration.Apply((JsonObject)document["State"]!);
        if (version < 70) LegacyArmyStoresSaveMigration.Apply((JsonObject)document["State"]!);
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
        if (state["Forces"] is JsonObject domainForces)
            foreach (JsonNode? force in domainForces.Select(pair => pair.Value))
            {
                JsonObject record = Require(force, "force state", "CombatObjective", "PolicyEffects");
                if (record["CombatObjective"] is JsonNode objective)
                    _ = Require(objective, "force combat objective", "Enabled", "TargetLocationId", "Progress");
                if (record["PolicyEffects"] is JsonNode effects)
                    _ = Require(effects, "force policy effects", "ActiveEffectIds", "SuppressedEffectIds");
            }
        if (!state.ContainsKey("ArmyMapOccupancy"))
            throw new InvalidDataException("Saved map occupancy owner is missing.");
        if (state["ArmyMapOccupancy"] is JsonNode occupancyNode)
        {
            JsonObject occupancy = Require(occupancyNode, "map occupancy", "DefaultCellKnowledge", "Cells", "Bindings");
            if (occupancy["Cells"] is not JsonObject cells || occupancy["Bindings"] is not JsonObject bindings)
                throw new InvalidDataException("Saved map occupancy directories are invalid.");
            foreach (JsonNode? cell in cells.Select(row => row.Value))
                _ = Require(cell, "map occupancy cell", "Kind", "BindingId");
            foreach (JsonNode? target in bindings.Select(row => row.Value))
                _ = Require(target, "map occupancy target", "Kind", "UnitId");
        }
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
                "SourceScenarioUri", "SourceScenarioSha256", "Indexes", "Slots", "ArmyGroups", "ArmyCautions");
            if (lifecycle.ContainsKey("HexRaw1C")) throw new InvalidDataException("Saved state contains an obsolete occupancy map.");
            if (lifecycle["Pool"] is not null) throw new InvalidDataException("Saved state contains an obsolete world membership pool.");
            if (lifecycle["Slots"] is not JsonArray slots) throw new InvalidDataException("Saved native slots are invalid.");
            foreach (JsonNode? slotNode in slots)
            {
                JsonObject slot = Require(slotNode, "native Army slot", "PhysicalSlot", "Commander12", "Status10",
                    "Priority14", "ActiveUnitId", "PendingImage", "StoredResources", "AuxiliaryAC", "AuxiliaryF4", "GroupAssignmentReady", "OccupancyBindingId");
                if (slot.ContainsKey("Gold5C") || slot.ContainsKey("Supplies60"))
                    throw new InvalidDataException("Saved Army receiver contains obsolete stores fields.");
                _ = Require(slot["StoredResources"], "retained Army stores", "Gold", "Food");
                if (slot["OverrideTarget"] is JsonNode overrideTarget)
                    _ = Require(overrideTarget, "native Army receiver override target", "Kind3C", "Identity3E");
                if (slot["PendingImage"] is JsonNode pendingNode)
                {
                    JsonObject pending = Require(pendingNode, "pending Army image", "Id", "ForceId", "OfficerId", "HexId",
                        "DestinationHexId", "Troops", "Supplies", "Morale");
                    CheckUnitModules(pending);
                }
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
            if (lifecycle["DeferredDestructionQueue"] is not null)
                throw new InvalidDataException("Saved state contains an obsolete combat queue owner.");
            if (lifecycle["ArmyGroups"] is JsonNode groupsNode)
            {
                var groups = Require(groupsNode, "army groups", "UnitAssignments", "Leaders", "ResetLeader");
                _ = Require(groups["ResetLeader"], "group reset leader", "Kind", "OfficerId");
                if (groups["UnitAssignments"] is not JsonObject assignments || groups["Leaders"] is not JsonObject leaders)
                    throw new InvalidDataException("Saved army group maps are invalid.");
                foreach (var row in assignments)
                    _ = Require(row.Value, "group assignment", "IsKnown", "GroupId");
                foreach (var row in leaders)
                {
                    var leader = Require(row.Value, "group leader", "IsKnown", "Officer");
                    _ = Require(leader["Officer"], "group officer", "Kind", "OfficerId");
                }
            }
            if (lifecycle.ContainsKey("Auxiliaries"))
                throw new InvalidDataException("Saved state contains obsolete army auxiliary tables.");
            if (lifecycle["ArmyCautions"] is JsonNode cautionsNode)
            {
                var cautions = Require(cautionsNode, "army cautions", "Relations", "GroupLabels");
                if (cautions["Relations"] is not JsonObject relations || cautions["GroupLabels"] is not JsonObject labels)
                    throw new InvalidDataException("Saved army caution maps are invalid.");
                foreach (var row in relations)
                {
                    var relation = Require(row.Value, "caution relation", "Unit", "Base");
                    _ = Require(relation["Unit"], "caution unit reference", "Kind", "UnitId");
                    _ = Require(relation["Base"], "caution base resource reference", "IsKnown", "MapObjectId");
                }
                foreach (var row in labels) _ = Require(row.Value, "caution group label", "IsKnown", "Label");
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
        if (state["NativePersonReturnCommands"] is not null)
            throw new InvalidDataException("Saved state contains an obsolete personnel command owner.");
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
                if (unit.ContainsKey("NativeReturnResources"))
                    throw new InvalidDataException("Saved Army contains obsolete carried stores.");
                if (!unit.ContainsKey("CarriedStores"))
                    throw new InvalidDataException("Saved Army stores observation is missing.");
                if (unit["CarriedStores"] is JsonNode stores)
                    _ = Require(stores, "carried Army stores", "Gold", "Food");
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
                "Int32Values", "Float32Values", "OfficeBonusEncoding");
            if (simulation["Forces"] is not JsonObject forceFields)
                throw new InvalidDataException("Owned native force field collection is invalid.");
            foreach (JsonNode? forceFieldsNode in forceFields.Select(pair => pair.Value))
            {
                _ = Require(forceFieldsNode, "owned native force fields", "Flags193", "Flags12", "Flags194", "RelationsEA");
                if (forceFieldsNode is JsonObject obsoletePolicy && (obsoletePolicy.ContainsKey("ActivePolicyBitmap1CB") ||
                    obsoletePolicy.ContainsKey("SuppressedPolicyBitmap1D5")))
                    throw new InvalidDataException("Saved force contains an obsolete policy effect owner.");
                if (forceFieldsNode is JsonObject oldForce && (oldForce.ContainsKey("ResultGate15F") ||
                    oldForce.ContainsKey("ResultDefinition160") || oldForce.ContainsKey("ResultValue162")))
                    throw new InvalidDataException("Saved force contains an obsolete combat objective owner.");
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
