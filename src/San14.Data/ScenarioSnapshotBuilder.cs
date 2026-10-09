using San14.Data.Formats;

namespace San14.Data;

/// <summary>Projects verified S14 tables into a UI-neutral scenario view.</summary>
public static class ScenarioSnapshotBuilder
{
    public static ScenarioCityPreview Build(string sourcePath, string fixedDataPath,
        int number, string locale, S14Document scenario, S14Document fixedData,
        string decodedSha256)
    {
        if (scenario.FormatMarker != fixedData.FormatMarker)
            throw new InvalidDataException($"Scenario and fixed data editions differ: {sourcePath}");
        ScenarioReferenceReport references = ScenarioReferenceValidator.Validate(scenario, locale);
        S14CharacterTable people = S14ScenarioTables.ReadCharacterNames(scenario, locale);
        IReadOnlyList<S14CharacterPlacement> placements =
            S14ScenarioTables.ReadCharacterPlacements(scenario, locale);
        IReadOnlyList<S14CharacterAbilities> abilities =
            S14ScenarioTables.ReadCharacterAbilities(scenario, locale);
        IReadOnlyList<S14CharacterIdentity> identities =
            S14ScenarioTables.ReadCharacterIdentities(scenario, locale);
        IReadOnlyList<S14CharacterSex> sexes = S14ScenarioTables.ReadCharacterSexes(scenario, locale);
        IReadOnlyList<S14CharacterOfficeReference> offices = S14RankReader.ReadCharacterOffices(scenario, locale);
        S14CharacterPolicyTable characterPolicies = S14PolicyReader.ReadCharacterReferences(scenario, locale);
        S14CharacterDoctrineTable characterDoctrines = S14DoctrineReader.ReadCharacterReferences(scenario, locale);
        S14CharacterCreditTable? characterCredits = scenario.FormatMarker == 0x66
            ? S14CreditEffectReader.ReadCharacterReferences(scenario, locale) : null;
        Dictionary<int, int> identitySlots = identities.Where(identity => identity.CharacterId != 0)
            .ToDictionary(identity => (int)identity.CharacterId, identity => identity.Slot);
        IReadOnlyList<S14ScenarioCity> cities = S14ScenarioTables.ReadScenarioCities(scenario);
        IReadOnlyList<S14ScenarioForce> forces = S14ScenarioTables.ReadScenarioForces(scenario);
        S14RankCatalog ranks = S14RankReader.Read(fixedData);
        S14FormationCatalog combat = S14FormationReader.Read(fixedData);
        S14PolicyCatalog policies;
        try { policies = S14PolicyReader.Read(fixedData); }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Policy parameters failed for {GameResourceId.File(fixedDataPath)}. {error.Message}", error);
        }
        S14DoctrineCatalog doctrines;
        try { doctrines = S14DoctrineReader.Read(fixedData); }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Doctrine parameters failed for {GameResourceId.File(fixedDataPath)}. {error.Message}", error);
        }
        S14PersonSkillTable? personSkills = S14PersonSkillReader.ReadCharacterReferences(scenario, locale);
        // CE references are current slot7 inputs. Preserve abnormal keys and all
        // mask bits instead of applying the older display-reference constraints.
        S14CharacterCombatTable? characterCombat = personSkills is null
            ? S14FormationReader.ReadCharacterReferences(scenario, locale) : null;
        S14ItemDefinitionCatalog itemDefinitions;
        try { itemDefinitions = S14ItemReader.ReadDefinitions(fixedData); }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Item definitions failed for {GameResourceId.File(fixedDataPath)}. {error.Message}", error);
        }
        S14ScenarioItemCatalog scenarioItems;
        try { scenarioItems = S14ItemReader.ReadScenarioItems(scenario, locale); }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Scenario items failed for {GameResourceId.File(sourcePath)}. {error.Message}", error);
        }
        if (scenarioItems.Items.Count != itemDefinitions.Definitions.Count)
            throw new InvalidDataException($"Scenario and fixed item counts differ: {sourcePath}; {fixedDataPath}");
        S14ScenarioWorldDocument? worldParameters = scenario.FormatMarker == 0x66
            ? S14ScenarioWorldReader.Read(scenario, locale) : null;
        S14ScenarioObjectCatalog? objectSource = scenario.FormatMarker == 0x66
            ? S14ScenarioObjectReader.Read(scenario) : null;
        S14CreditEffectCatalog? creditEffects = null;
        S14CreditRankCatalog? creditRanks = null;
        S14DoctrinePanelCatalog? doctrinePanels = null;
        if (fixedData.FormatMarker == 0x66)
        {
            try { creditEffects = S14CreditEffectReader.Read(fixedData); }
            catch (InvalidDataException error)
            {
                throw new InvalidDataException($"Credit effect parameters failed for {GameResourceId.File(fixedDataPath)}. {error.Message}", error);
            }
            try { doctrinePanels = S14DoctrinePanelReader.Read(fixedData); }
            catch (InvalidDataException error)
            {
                throw new InvalidDataException($"Doctrine panel parameters failed for {GameResourceId.File(fixedDataPath)}. {error.Message}", error);
            }
            try { creditRanks = S14CreditRankReader.Read(fixedData); }
            catch (InvalidDataException error)
            {
                throw new InvalidDataException($"Credit rank parameters failed for {GameResourceId.File(fixedDataPath)}. {error.Message}", error);
            }
        }
        IReadOnlyList<S14ForceRankReference> forceRanks = S14RankReader.ReadForceRanks(scenario);
        IReadOnlyList<S14ScenarioDistrict> districts = S14ScenarioTables.ReadScenarioDistricts(scenario);
        IReadOnlyList<S14ScenarioGate> gateStates = S14ScenarioTables.ReadScenarioGates(scenario);
        IReadOnlyList<S14ScenarioArea> areaStates = S14ScenarioTables.ReadScenarioAreas(scenario);
        IReadOnlyList<S14CityDefinition> definitions =
            S14ScenarioTables.ReadCityDefinitions(fixedData);
        IReadOnlyList<S14GateDefinition> gates =
            S14ScenarioTables.ReadGateDefinitions(fixedData);
        IReadOnlyList<S14AreaDefinition> areas =
            S14ScenarioTables.ReadAreaDefinitions(fixedData);
        if (cities.Count != definitions.Count || gates.Count != gateStates.Count ||
            areas.Count != areaStates.Count)
            throw new InvalidDataException($"Scenario and fixed data entity counts differ: {sourcePath}");

        string? PersonName(ushort slot)
        {
            string? name = identitySlots.TryGetValue(slot, out int recordSlot)
                ? people.Names[recordSlot].DisplayName : null;
            return name is { Length: > 0 } ? name : null;
        }

        byte ForceOf(byte districtSlot)
        {
            if (districtSlot >= districts.Count)
                throw new InvalidDataException($"District reference {districtSlot} is invalid: {sourcePath}");
            return districts[districtSlot].ForceSlot;
        }

        ForcePreviewRecord[] forceRows = forces.Select(force => new ForcePreviewRecord(
            force.Slot, force.RulerSlot, PersonName(force.RulerSlot),
            force.RemainingBytesHex)
            {
                RankId = forceRanks[force.Slot].RankId,
                RawReportColor49 = force.RawReportColor49,
                RawIndexedBytes13 = force.RawIndexedBytes13,
                RawIndexedBytesEA = force.RawIndexedBytesEA
            }).ToArray();
        DistrictPreviewRecord[] districtRows = districts.Select(district =>
            new DistrictPreviewRecord(district.Slot, district.ForceSlot,
                district.Number, district.LeaderSlot, PersonName(district.LeaderSlot))).ToArray();
        var cityRows = new List<CityPreviewRecord>(cities.Count);
        foreach (S14ScenarioCity city in cities)
        {
            S14CityDefinition definition = definitions[city.Slot];
            S14AreaDefinition[] capitalMatches = city.Slot == 0 ? [] :
                areas.Where(item => item.CitySlot == city.Slot &&
                    item.Name == definition.Name).ToArray();
            if (capitalMatches.Length > 1)
                throw new InvalidDataException($"Multiple map locations match city {city.Slot}: {sourcePath}");
            cityRows.Add(new CityPreviewRecord(city.Slot, definition.Name,
                city.OfficerSlot, PersonName(city.OfficerSlot), city.DistrictSlot,
                ForceOf(city.DistrictSlot), city.Gold, city.Supplies, city.Troops,
                city.Morale, city.Durability, city.SizeCode,
                city.RemainingBytesHex, definition.SecondaryText,
                definition.LinkedLocationSlots, definition.RawLimit0,
                definition.RawLimit1, definition.RawLimit2, definition.GroupCode,
                capitalMatches.Length == 1 ? capitalMatches[0].HexId : null)
            {
                RawValidityFlag69 = city.RawValidityFlag69
            });
        }
        var characters = new List<CharacterPreviewRecord>(placements.Count);
        foreach (S14CharacterPlacement placement in placements)
        {
            S14CharacterAbilities values = abilities[placement.Slot];
            S14PersonSkillReference? skillSource = personSkills?.Characters[placement.Slot];
            IReadOnlyList<int> formations = skillSource is not null
                ? Enumerable.Range(0, 21).Where(slot => (skillSource.RawFormationMask164 & (1u << slot)) != 0).ToArray()
                : characterCombat!.Characters[placement.Slot].FormationSlots;
            IReadOnlyList<byte> tactics = skillSource is not null
                ? skillSource.RawTacticSlots168 : characterCombat!.Characters[placement.Slot].TacticSlots;
            characters.Add(new CharacterPreviewRecord(placement.Slot,
                people.Names[placement.Slot].DisplayName, placement.DistrictSlot,
                ForceOf(placement.DistrictSlot),
                placement.CitySlot, placement.SecondaryCitySlot,
                placement.StatusCode, placement.RawBytesHex, values.Leadership,
                values.Strength, values.Intelligence, values.Politics, values.Charm)
            {
                CharacterId = identities[placement.Slot].CharacterId,
                AppearanceCode = identities[placement.Slot].AppearanceCode,
                RawSexCode = sexes[placement.Slot].RawCode,
                RawOfficeId = offices[placement.Slot].OfficeId,
                RawPolicySlot = characterPolicies.Characters[placement.Slot].RawPolicySlot,
                RawPolicyBaseCost = characterPolicies.Characters[placement.Slot].RawPolicyBaseCost,
                RawPolicyFieldBytes = characterPolicies.Characters[placement.Slot].RawFieldBytes.ToArray(),
                RawDoctrineId = characterDoctrines.Characters[placement.Slot].RawDoctrineId,
                RawDoctrineWord = characterDoctrines.Characters[placement.Slot].RawDoctrineWord,
                RawDoctrineFieldBytes = characterDoctrines.Characters[placement.Slot].RawFieldBytes.ToArray(),
                CreditReference = characterCredits?.Characters[placement.Slot],
                SkillSource = skillSource,
                BaseFormationSlots = formations,
                BaseTacticSlots = tactics.Where(id => id != 0).Select(id => (int)id).ToArray()
            });
        }

        var gateRows = new List<GatePreviewRecord>(gates.Count);
        foreach (S14GateDefinition gate in gates)
        {
            S14ScenarioGate state = gateStates[gate.Slot];
            S14AreaDefinition[] matches = gate.Slot == 0 ? [] :
                areas.Where(item => item.Name == gate.Name).ToArray();
            if (matches.Length > 1)
                throw new InvalidDataException($"Multiple map locations match gate {gate.Slot}: {sourcePath}");
            gateRows.Add(new GatePreviewRecord(gate.Slot, gate.LocationSlot,
                gate.Name, gate.SecondaryText, gate.LinkedLocationSlots,
                matches.Length == 1 ? matches[0].HexId : null, state.OfficerSlot,
                PersonName(state.OfficerSlot), state.DistrictSlot, ForceOf(state.DistrictSlot),
                state.Amount0, state.Amount1, state.Troops, state.RemainingBytesHex)
                {
                    RawValidityFlag69 = state.RawValidityFlag69,
                    RawMorale44 = state.RawMorale44,
                    RawField46 = state.RawField46
                });
        }

        AreaPreviewRecord[] areaRows = areas.Select(item =>
        {
            S14ScenarioArea state = areaStates[item.Slot];
            return new AreaPreviewRecord(item.Slot, item.Name, item.SecondaryText,
                item.CitySlot, item.HexId, item.RemainingBytesHex, state.Amount0,
                state.Value2, state.ForceSlot, state.RemainingBytesHex)
            {
                RawField44 = item.RawField44,
                RawField46 = item.RawField46,
                AdjacentAreaSlots = item.AdjacentAreaSlots,
                RawNeighborSlots = item.RawNeighborSlots,
                RegionalNeighborMask = item.RegionalNeighborMask,
                RawFlags38 = state.RawFlags38,
                RawFlag74 = state.RawFlag74
            };
        }).ToArray();

        return new ScenarioCityPreview(sourcePath, decodedSha256, fixedDataPath,
            number, locale, cityRows, forceRows,
            characters.Where(person => person.DistrictSlot > 0).ToArray())
        {
            RankParameters = ranks,
            CombatParameters = combat,
            PolicyParameters = policies,
            DoctrineParameters = doctrines,
            CreditEffectParameters = creditEffects,
            CreditRankParameters = creditRanks,
            DoctrinePanelParameters = doctrinePanels,
            ItemDefinitions = itemDefinitions,
            ScenarioItems = scenarioItems,
            WorldParameters = worldParameters,
            ObjectSourceRecords = objectSource,
            Gates = gateRows,
            Areas = areaRows,
            Districts = districtRows,
            Characters = characters,
            UnresolvedCharacters = references.UnresolvedCharacters,
            UnresolvedPlacements = references.UnresolvedPlacements
        };
    }
}
