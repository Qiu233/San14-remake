using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public AreaAdministrationQuery<CurrentWorldForceEditPublication> QueryCurrentWorldForceEdit(
        GameState state, int forceId, int? districtId = null)
    {
        try
        {
            var current = CaptureCurrentWorldForceEdit(state);
            return AreaAdministrationQuery<CurrentWorldForceEditPublication>.Known(
                ReadCurrentWorldForceEdit(state, current, forceId, districtId));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<CurrentWorldForceEditPublication>.Unknown(error.Message); }
    }

    public AreaAdministrationQuery<CurrentWorldForceEditPublication> QueryCurrentWorldForceEditPreview(
        GameState state, int forceId, CurrentWorldForceEditValues values, int? districtId = null)
    {
        try
        {
            var current = CaptureCurrentWorldForceEdit(state);
            var original = CurrentWorldEditableForce(state, current, forceId);
            int originalRank = original.RankId ?? throw WorldEditMissing("force_rank");
            int originalExperience = WorldEditNeed(OwnedForce164Inputs.ReadCurrent(state, forceId), "force_doctrine_experience");
            int rank = values.RankId ?? originalRank;
            int title = values.NationalTitleId ?? (rank != originalRank && rank != 1 ? 0 : WorldEditNeed(current.ReadForceKingdomNameIndex(forceId), "force_title"));
            if (rank is < 1 or > 12 || title is < 0 or > 100 || values.DoctrineExperience is < 0 or > 9_999_999)
                throw WorldEditMissing("force_preview_range");
            var draft = state with { Forces = new(state.Forces) { [forceId] = original with { RankId = rank, KingdomNameIndex = (byte)title } } };
            current.SetForceRankSlot47(forceId, (byte)rank);
            current.SetForceKingdomNameIndex(forceId, (byte)title);
            if (values.DoctrineExperience is int experience)
                draft = CurrentWorldForceParameter(draft, OwnedForce164Inputs.ForceValueKey(forceId), experience);
            if (values.CivilReputation is int civil)
            {
                if (civil is < 0 or > 36000) throw WorldEditMissing("force_preview_civil_range");
                var owner = draft.NativeForceUnregisterCurrent ?? throw WorldEditMissing("force_civil_owner");
                var row = owner.ForceRankings.GetValueOrDefault(forceId) ?? throw WorldEditMissing("force_civil_row");
                _ = WorldEditNeed(row.Word1BC, "force_civil_reputation");
                draft = draft with { NativeForceUnregisterCurrent = owner with { ForceRankings = new(owner.ForceRankings)
                    { [forceId] = row with { Word1BC = (ushort)civil } } } };
            }
            if (values.DoctrinePoints.Count != 0)
            {
                var owner = draft.ForceCreditReserves ?? throw WorldEditMissing("force_doctrine_points_owner");
                if (!owner.PointsByForce.TryGetValue(forceId, out var points) || points.Length != 6)
                    throw WorldEditMissing("force_doctrine_points");
                var edited = points.ToBuilder();
                foreach (var row in values.DoctrinePoints)
                {
                    if (row.DoctrineId is < 1 or > 6 || row.Points is < 0 or > 99) throw WorldEditMissing("force_preview_points_range");
                    edited[row.DoctrineId - 1] = (byte)row.Points;
                }
                draft = draft with { ForceCreditReserves = owner with { PointsByForce = owner.PointsByForce.SetItem(forceId, edited.ToImmutable()) } };
            }
            foreach (int district in CurrentWorldForceDistricts(current, forceId))
            {
                int before = ReadCurrentWorldDistrictMilitary(current, district);
                int cap = PersonReturnDistrictReorganizationSource.Cap(current, draft, district);
                int? requested = values.DistrictMilitaryInformation.FirstOrDefault(x => x.DistrictId == district)?.Value;
                if (values.MilitaryDistrictId == district) requested = values.MilitaryInformation;
                if (requested is < 0) throw WorldEditMissing("force_preview_military_range");
                //A rank change clamps the retained draft, including values
                //adopted from the previous preview. Raising rank later must
                //not recover discarded military information from the World.
                WriteCurrentWorldDistrictMilitary(current, district, Math.Min(requested ?? before, cap));
            }
            if (values.Relationships is null || values.Relationships.Any(row => row is null) ||
                values.Relationships.Select(row => row.ForceId).Distinct().Count() != values.Relationships.Count)
                throw WorldEditMissing("force_preview_relation_selection");
            var relationshipEligibility = new DiplomacyCommandSource(state, current);
            foreach (var edit in values.Relationships.OrderBy(row => row.ForceId))
            {
                int other = edit.ForceId;
                if (other is < 0 or >= 52 || other == forceId || edit.Friendship is < 0 or > 200 ||
                    edit.FriendshipLevel is < 0 or > 5 || edit.AllianceMonths is < 0 or > 48)
                    throw WorldEditMissing("force_preview_relation_range");
                if (!IsCurrentWorldForceRelationshipSelectable(current, relationshipEligibility, forceId, other))
                    throw WorldEditMissing("force_preview_relation_target");
                int oldFriendship = ReadEditedEventFriendship(draft, current, new(forceId), other);
                byte oldAlliance = ReadReportForceRelationByte(draft, new(forceId), other, WorldEditMissing);
                var (friendship, months) = NormalizeCurrentWorldForceRelationshipValues(edit, oldFriendship, oldAlliance & 127);
                //The original subdialog changes the author's clone only.
                //These pure stores retain EA's high bit and do not call the
                //real alliance writer, task cancellation or territory tails.
                if (friendship != oldFriendship)
                    draft = AbsorptionWriteRelation(draft, current, forceId, other, false, (byte)friendship);
                if (months != (oldAlliance & 127))
                    draft = AbsorptionWriteRelation(draft, current, forceId, other, true, (byte)((oldAlliance & 128) | months));
            }
            return AreaAdministrationQuery<CurrentWorldForceEditPublication>.Known(ReadCurrentWorldForceEdit(
                draft, current, forceId, districtId, originalRank, originalExperience));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<CurrentWorldForceEditPublication>.Unknown(error.Message); }
    }

    private CurrentWorldForceEditPublication ReadCurrentWorldForceEdit(GameState state,
        SourceWorldContext current, int forceId, int? selectedDistrict, int? initialRank = null, int? initialExperience = null)
    {
        var force = CurrentWorldEditableForce(state, current, forceId);
        var issues = new List<string>();
        var rank = Number(() => force.RankId ?? throw WorldEditMissing("force_rank"), 1, 12);
        var title = Number(() => WorldEditNeed(current.ReadForceKingdomNameIndex(forceId), "force_title"), 0, 100);
        var experience = Number(() => WorldEditNeed(OwnedForce164Inputs.ReadCurrent(state, forceId), "force_doctrine_experience"), 0, 9_999_999);
        var level = Number(() => WorldEditNeed(experience.Value is int value ? OwnedForce164Inputs.ReadLevel(state, value) : null,
            "force_doctrine_level"), 1, 10);
        var civil = Number(() => WorldEditNeed(state.NativeForceUnregisterCurrent?.ForceRankings.GetValueOrDefault(forceId)?.Word1BC,
            "force_civil_reputation"), 0, 36000);
        var districts = new List<CurrentWorldEditOption>();
        var districtMilitary = new List<CurrentWorldForceDistrictMilitaryInformation>();
        foreach (int id in CurrentWorldForceDistricts(current, forceId))
        {
            string name = ReadDistrictInformationLeaderCaption(state, current, id);
            districts.Add(new(id, name));
            districtMilitary.Add(new(id, name, Number(() => ReadCurrentWorldDistrictMilitary(current, id), 0,
                ReadOptional(() => PersonReturnDistrictReorganizationSource.Cap(current, state, id)))));
        }
        int? districtId = selectedDistrict;
        if (districtId is null && districts.Count != 0)
        {
            int initial = WorldEditNeed(ForcePrimaryDistrictSource.Resolve(current, new(forceId)).PhysicalSlot, "force_primary_district");
            districtId = initial == 0 ? null : initial;
        }
        if (districtId is int selected && !districts.Any(x => x.Id == selected))
            throw WorldEditMissing("force_selected_district");
        var military = districtId is int district
            ? districtMilitary.First(row => row.DistrictId == district).MilitaryInformation
            : new CurrentWorldEditNumber(null, 0, null, "current-world-edit.force_no_district");
        int? firstRank = initialRank ?? rank.Value;
        var ranks = state.ForceRanks.Values.Where(row => row.Id is >= 1 and <= 9 && firstRank is int original && row.Id <= original)
            .OrderBy(row => row.Id).Select(row => new CurrentWorldEditOption(row.Id, row.Name)).ToArray();
        IReadOnlyList<CurrentWorldEditOption> titles;
        try { titles = ReadCurrentWorldNationalTitles(state, current, forceId, issues); }
        catch (InvalidDataException error) { issues.Add(error.Message); titles = []; }
        var points = new List<CurrentWorldForceDoctrinePoints>();
        for (int doctrine = 1; doctrine <= 6; doctrine++)
        {
            int index = doctrine - 1;
            string name = state.Doctrines.GetValueOrDefault(doctrine)?.Name ?? throw WorldEditMissing("force_doctrine_name");
            points.Add(new(doctrine, name, Number(() =>
            {
                if (state.ForceCreditReserves is not { } owner || !owner.PointsByForce.TryGetValue(forceId, out var row) || row.Length != 6)
                    throw WorldEditMissing("force_doctrine_points");
                return row[index];
            }, 0, 99)));
        }
        var relations = new List<CurrentWorldForceRelationship>();
        var relationshipEligibility = new DiplomacyCommandSource(state, current);
        for (int other = 0; other < 52; other++)
        {
            try
            {
                if (!IsCurrentWorldForceRelationshipSelectable(current, relationshipEligibility, forceId, other)) continue;
            }
            catch (InvalidDataException error) { issues.Add(error.Message); continue; }
            var friendship = Number(() => ReadEditedEventFriendship(state, current, new(forceId), other), 0, 200);
            var alliance = Number(() => ReadReportForceRelationByte(state, new(forceId), other, WorldEditMissing) & 127, 0, 48);
            relations.Add(new(other, state.Forces.GetValueOrDefault(other)?.Name ?? throw WorldEditMissing("relation_force_name"),
                friendship, alliance) { FriendshipLevel = friendship.Value is int value ? CurrentWorldFriendshipLevel(value) : null });
        }
        var totalMilitary = Number(() => ReadForceMilitaryInformationTotalCurrent(current, forceId), 0, null);
        var levels = new List<CurrentWorldForceDoctrineLevelOption>();
        for (int value = 1; value <= 10; value++)
        {
            int index = value;
            int? threshold = ReadOptional(() =>
            {
                var bank = state.NativeArmySimulation?.Parameters ?? throw WorldEditMissing("doctrine_threshold_owner");
                if (bank.Float32Values.ContainsKey(OwnedForce164Inputs.ThresholdKey(index)))
                    throw WorldEditMissing("doctrine_threshold_Int32_" + index);
                return bank.Int32Values.GetValueOrDefault(OwnedForce164Inputs.VersionKey) == 1 &&
                    bank.Int32Values.TryGetValue(OwnedForce164Inputs.ThresholdKey(index), out int amount)
                    ? amount : throw WorldEditMissing("doctrine_threshold_" + index);
            });
            int? firstExperience = initialExperience ?? experience.Value;
            int? firstLevel = firstExperience is int saved ? OwnedForce164Inputs.ReadLevel(state, saved) : null;
            if (threshold is int amount) levels.Add(new(value, value == firstLevel && firstExperience is int originalExperience
                ? originalExperience : Math.Clamp(amount, 0, 9_999_999)));
        }
        bool baseGate = rank.Value is int knownRank && title.Value is int knownTitle &&
            !(knownRank == 10 && knownTitle == 6) && knownRank is not (11 or 12);
        return new(forceId, force.Name, rank, title, experience, civil, districtId, military,
            ranks, titles, districts, points, relations)
        {
            FriendshipLevels = Enumerable.Range(0, 6).Select(i => new CurrentWorldEditOption(i, ReadHistoryDataText(0, 0x141 + i))).ToArray(),
            CanEditRank = baseGate && !(rank.Value == 1 && firstRank == 1),
            CanEditNationalTitle = baseGate && rank.Value == 1,
            CanEditDoctrineLevel = (initialExperience is int first ? OwnedForce164Inputs.ReadLevel(state, first) : level.Value) is int knownLevel && knownLevel != 10,
            DoctrineLevel = level, DoctrineLevels = levels, TotalMilitaryInformation = totalMilitary,
            DistrictMilitaryInformationValues = districtMilitary,
            UnresolvedFields = issues.Distinct().ToArray()
        };

        int? ReadOptional(Func<int> read)
        { try { return read(); } catch (InvalidDataException error) { issues.Add(error.Message); return null; } }
        CurrentWorldEditNumber Number(Func<int> read, int minimum, int? maximum)
        {
            try { return new(read(), minimum, maximum); }
            catch (InvalidDataException error) { issues.Add(error.Message); return new(null, minimum, maximum, error.Message); }
        }
    }

    private CommandResult ApplyCurrentWorldForceEditCurrent(GameState state, EditCurrentWorldForceOrder order)
    {
        if (order.Values is not { } values || values.DoctrinePoints is null || values.Relationships is null ||
            values.DistrictMilitaryInformation is null)
            return CommandResult.Reject("Force edit values are missing.", "world_edit.force_values");
        var current = CaptureCurrentWorldForceEdit(state);
        var original = CurrentWorldEditableForce(state, current, order.TargetForceId);
        int forceId = order.TargetForceId;
        int originalRank = original.RankId ?? throw WorldEditMissing("force_rank");
        int originalTitle = WorldEditNeed(current.ReadForceKingdomNameIndex(forceId), "force_title");
        int rank = values.RankId ?? originalRank;
        int title = values.NationalTitleId ?? (rank != originalRank && rank != 1 ? 0 : originalTitle);
        bool rankGate = !(originalRank == 10 && originalTitle == 6) && originalRank is not (1 or 11 or 12);
        if (rank != originalRank && (!rankGate || rank is < 1 or > 9 || rank > originalRank || !state.ForceRanks.ContainsKey(rank)))
            return CommandResult.Reject("Rank is outside the source selection.", "world_edit.force_rank");
        if (title != originalTitle && !(rank != originalRank && rank != 1 && title == 0))
        {
            bool titleGate = !(originalRank == 10 && originalTitle == 6) && originalRank is not (11 or 12) && rank == 1;
            var titleIssues = new List<string>();
            var titles = ReadCurrentWorldNationalTitles(state, current, forceId, titleIssues);
            if (!titleGate || !titles.Any(row => row.Id == title))
            {
                if (titleGate && title == 7 && titleIssues.Count != 0) throw new InvalidDataException(titleIssues[0]);
                return CommandResult.Reject("National title is outside the source selection.", "world_edit.force_title");
            }
        }
        if (values.DoctrineExperience is int requestedExperience)
        {
            if (requestedExperience is < 0 or > 9_999_999)
                return CommandResult.Reject("Doctrine experience is outside the source range.", "world_edit.force_experience");
            int before = WorldEditNeed(OwnedForce164Inputs.ReadCurrent(state, forceId), "force_doctrine_experience");
            if (before != requestedExperience && WorldEditNeed(OwnedForce164Inputs.ReadLevel(state, before), "force_doctrine_level") == 10)
                return CommandResult.Reject("The source level control is disabled at maximum level.", "world_edit.force_level");
        }
        if (values.CivilReputation is < 0 or > 36000 || values.DoctrinePoints.Any(row => row is null || row.DoctrineId is < 1 or > 6 || row.Points is < 0 or > 99) ||
            values.DoctrinePoints.Select(row => row.DoctrineId).Distinct().Count() != values.DoctrinePoints.Count)
            return CommandResult.Reject("Force numeric values are outside the source controls.", "world_edit.force_range");
        if (values.MilitaryDistrictId.HasValue != values.MilitaryInformation.HasValue)
            return CommandResult.Reject("A military-information edit requires its district.", "world_edit.force_district_pair");
        var military = values.DistrictMilitaryInformation.ToList();
        if (values.MilitaryDistrictId is int singleDistrict)
            military.Add(new(singleDistrict, values.MilitaryInformation!.Value));
        if (military.Any(row => row is null) || military.Select(row => row.DistrictId).Distinct().Count() != military.Count)
            return CommandResult.Reject("District edits must be distinct.", "world_edit.force_district_duplicate");
        var districtIds = CurrentWorldForceDistricts(current, forceId).ToArray();
        foreach (var row in military)
            if (!districtIds.Contains(row.DistrictId) || row.Value < 0)
                return CommandResult.Reject("District is outside the target Force.", "world_edit.force_district");
        if (values.Relationships.Any(row => row is null) || values.Relationships.Select(row => row.ForceId).Distinct().Count() != values.Relationships.Count)
            return CommandResult.Reject("Relationship edits must be distinct.", "world_edit.force_relation_duplicate");
        var relationshipEligibility = new DiplomacyCommandSource(state, current);
        foreach (var row in values.Relationships)
        {
            if ((uint)row.ForceId >= 52 || row.ForceId == forceId || row.Friendship is < 0 or > 200 ||
                row.FriendshipLevel is < 0 or > 5 || row.AllianceMonths is < 0 or > 48)
                return CommandResult.Reject("Relationship edit is outside the source controls.", "world_edit.force_relation");
            if (!IsCurrentWorldForceRelationshipSelectable(current, relationshipEligibility, forceId, row.ForceId))
                return CommandResult.Reject("Relationship target is outside the source selection.", "world_edit.force_relation_target");
        }

        var next = state;
        bool changed = false, districtChanged = false, relationChanged = false, brokeAlliance = false;
        if (rank != originalRank || title != originalTitle)
        {
            next = next with { Forces = new(next.Forces) { [forceId] = next.Forces[forceId] with { RankId = rank } } };
            current.SetForceRankSlot47(forceId, (byte)rank);
            next = CurrentWorldForceParameter(next, OwnedArmyMoraleWaveInputs.ForceRank47Key(forceId), rank);
            if (title != originalTitle) next = StoreEmpireNameCurrent(next, current, forceId, title);
            //32D1D0 resolves the emperor's actual whereabouts City, not his
            //membership Force. Clear1654 only in this source rank/title branch.
            if (rank == 1 && ReadEmpireNameImperialForceCurrent(next, current) == forceId)
            {
                _ = WorldEditNeed(CityCaptureTerritoryInputs.ReadEmperor1654(next), "emperor1654");
                next = CurrentWorldForceParameter(next, CityCaptureTerritoryInputs.Emperor1654Key, 0);
                CityCaptureTerritoryInputs.BindCurrentEmperor(next, current);
            }
            next = ApplyPlayerCommanderCapacityForRankCurrent(next, current, new(forceId), rank);
            if (rank != originalRank)
                foreach (int district in districtIds)
                {
                    int before = ReadCurrentWorldDistrictMilitary(current, district);
                    int capped = Math.Min(before, PersonReturnDistrictReorganizationSource.Cap(current, next, district));
                    if (capped != before) { WriteCurrentWorldDistrictMilitary(current, district, capped); districtChanged = true; }
                }
            changed = true;
        }
        if (values.DoctrineExperience is int experience && experience != OwnedForce164Inputs.ReadCurrent(next, forceId))
        {
            next = CurrentWorldForceParameter(next, OwnedForce164Inputs.ForceValueKey(forceId), experience);
            current.SetForce164(forceId, experience); changed = true;
        }
        foreach (var row in military)
        {
            int cap = PersonReturnDistrictReorganizationSource.Cap(current, next, row.DistrictId);
            if (row.Value > cap) return CommandResult.Reject("Military information exceeds the current rank limit.", "world_edit.force_military_cap");
            if (ReadCurrentWorldDistrictMilitary(current, row.DistrictId) == row.Value) continue;
            WriteCurrentWorldDistrictMilitary(current, row.DistrictId, row.Value); districtChanged = changed = true;
        }
        if (values.CivilReputation is int civil)
        {
            var owner = next.NativeForceUnregisterCurrent ?? throw WorldEditMissing("force_civil_owner");
            var row = owner.ForceRankings.GetValueOrDefault(forceId) ?? throw WorldEditMissing("force_civil_row");
            if (WorldEditNeed(row.Word1BC, "force_civil_reputation") != civil)
            {
                next = next with { NativeForceUnregisterCurrent = owner with { ForceRankings = new(owner.ForceRankings)
                    { [forceId] = row with { Word1BC = (ushort)civil } } } };
                changed = true;
            }
        }
        if (values.DoctrinePoints.Count != 0)
        {
            var owner = next.ForceCreditReserves ?? throw WorldEditMissing("force_doctrine_points_owner");
            if (!owner.PointsByForce.TryGetValue(forceId, out var before) || before.Length != 6)
                throw WorldEditMissing("force_doctrine_points");
            var points = before.ToBuilder();
            foreach (var row in values.DoctrinePoints)
                if (points[row.DoctrineId - 1] != row.Points) { points[row.DoctrineId - 1] = (byte)row.Points; changed = true; }
            if (!points.SequenceEqual(before))
                next = next with { ForceCreditReserves = owner with { PointsByForce = owner.PointsByForce.SetItem(forceId, points.ToImmutable()) } };
        }
        if (districtChanged) next = PublishEditedEventDistrictWindows(next, current);
        var pool = current.ReadOwnedCurrentListPool() ?? throw WorldEditMissing("force_shared_pool");
        //624890 visits the physical Force table, not the order in which
        //the user edited rows. Preserve that business-tail ordering.
        foreach (var edit in values.Relationships.OrderBy(row => row.ForceId))
        {
            int other = edit.ForceId;
            int oldFriendship = ReadEditedEventFriendship(next, current, new(forceId), other);
            int oldMonths = ReadReportForceRelationByte(next, new(forceId), other, WorldEditMissing) & 127;
            var (friendship, months) = NormalizeCurrentWorldForceRelationshipValues(edit, oldFriendship, oldMonths);
            if (friendship != oldFriendship)
            {
                next = AbsorptionWriteRelation(next, current, forceId, other, false, (byte)friendship);
                next = AbsorptionWriteRelation(next, current, other, forceId, false, (byte)friendship);
                relationChanged = changed = true;
            }
            if (months == oldMonths) continue;
            if (oldMonths != 0 && months == 0)
            {
                _personRelationInputs.BindCurrent(() => next, current);
                _baseCapacityInputs.BindCurrent(next, current);
                next = new MonthlyCampaignMaintenanceSource(this, next, current, pool)
                    .ApplyCurrentWorldEditAllianceEndCurrent(new(forceId), new(other));
                brokeAlliance = true;
            }
            else
            {
                next = WriteEditedEventAlliance(next, current, forceId, other, months);
                if (oldMonths == 0 && months != 0)
                {
                    _ = DAllianceDistrictObjectives(next, current, forceId, other);
                    next = PersonAssignmentPrecallInputs.PublishCurrent(next, current);
                    next = PublishEditedEventDistrictWindows(next, current);
                }
            }
            relationChanged = changed = true;
        }
        if (brokeAlliance)
        {
            //624890's two real maintenance calls follow all edited pairs.
            next = MaintainEditedEventAppointments(next, current);
            next = RefreshEditedEventRegionStatistics(next, current);
            next = PublishNormalTurnCurrent(next, current, pool);
        }
        if (relationChanged) next = SynchronizeDiplomacyProjection(next);
        return CommandResult.Accept(changed ? MarkCurrentWorldEdited(next, CurrentWorldEditKind.Force) : next);
    }

    private IReadOnlyList<CurrentWorldEditOption> ReadCurrentWorldNationalTitles(GameState state,
        SourceWorldContext current, int forceId, List<string>? issues = null)
    {
        var choices = new List<CurrentWorldEditOption>();
        foreach (var row in ReadEmpireNameResourcesCurrent().CustomNames.Definitions)
        {
            if (!row.NativeValidity || row.PhysicalSlot == 6) continue;
            bool allowed = (row.Field8E & 1) != 0;
            if (!allowed && row.PhysicalSlot == 7)
            {
                try
                {
                    if (ReadEmpireNameImperialForceCurrent(state, current) == forceId)
                    {
                        int minimum = ReadCurrentWorldSpecialTitleRulerPreferenceMinimum(state);
                        int id = WorldEditNeed(current.ReadForceRecord(forceId)?.RawRuler10, "title_ruler");
                        int ruler = WorldEditNeed((id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "title_ruler_pointer");
                        //The existing saved canonical17A owner is independently
                        //imported from the SC serializer's +56-byte row layout.
                        //This corresponds to the SC caller's actual Person1B2;
                        //it is not a read of original SC physical offset17A.
                        allowed = WorldEditNeed(RulerCaptureReleaseInputs.ReadCurrentWord(state, ruler, 0x17A),
                            "title_ruler_preference_current") >= minimum;
                    }
                }
                catch (InvalidDataException error) when (issues is not null) { issues.Add(error.Message); continue; }
            }
            if (allowed && !EmpireNameUsedCurrent(current, row.PhysicalSlot)) choices.Add(new(row.PhysicalSlot, row.ShortName));
        }
        return choices;
    }

    private int ReadCurrentWorldSpecialTitleRulerPreferenceMinimum(GameState state)
    {
        const string key = "simulation.current-world-edit.national-title.ruler-preference-minimum";
        var bank = state.NativeArmySimulation?.Parameters ?? throw WorldEditMissing("title_parameter_owner");
        if (bank.Language != Seed.Language || bank.RuleVersion != SimulationRuleIdentity.ForLocale(Seed.Language))
            throw WorldEditMissing("title_parameter_profile");
        if (bank.Float32Values.ContainsKey(key)) throw WorldEditMissing("title_preference_parameter_Int32");
        if (bank.Int32Values.TryGetValue(key, out int saved)) return saved;
        if (bank.Language == "SC")
        {
            //SC618560's complete special-title caller independently reads its
            //own signed Int32 source constant20. No TC value is borrowed.
            return 20;
        }
        if (bank.Language == "TC")
        {
            //TC6181D0's independent signed Int32 source constant20.
            return 20;
        }
        throw WorldEditMissing("title_parameter_locale");
    }

    //656340 admits rows before any relationship values are read. Keep the
    //same predicate at the typed UI-command boundary so stale/forged orders
    //cannot edit a row that the original selector would not expose.
    private static bool IsCurrentWorldForceRelationshipSelectable(SourceWorldContext current,
        DiplomacyCommandSource eligibility, int actualForce, int other)
    {
        var reference = current.ReadForceTableReference(other) ?? throw WorldEditMissing("relation_force_pointer");
        if (!WorldEditNeed(current.ReadForceNativeValidity(reference), "relation_force_validity")) return false;
        if (WorldEditNeed(current.ReadForceRecord(other)?.RawRuler10, "relation_force_ruler") ==
            WorldEditNeed(current.ReadForceRecord(actualForce)?.RawRuler10, "force_ruler")) return false;
        int physical = WorldEditNeed(current.ReadForcePhysicalSlot(reference), "relation_force_identity");
        if (physical is >= 46 and <= 50) return false;
        return !eligibility.CoalitionBlocks(physical, actualForce);
    }

    private static ForceState CurrentWorldEditableForce(GameState state, SourceWorldContext current, int forceId)
    {
        if (forceId is < 1 or >= 52 or >= 46 and <= 50 || !state.Forces.TryGetValue(forceId, out var owner) || owner.Id != forceId)
            throw WorldEditMissing("force_editable_identity");
        var force = current.ReadForceTableReference(forceId) ?? throw WorldEditMissing("force_pointer");
        if (force.PhysicalSlot != forceId || !WorldEditNeed(current.ReadForceNativeValidity(force), "force_validity"))
            throw WorldEditMissing("force_not_current");
        return owner;
    }

    private SourceWorldContext CaptureCurrentWorldForceEdit(GameState state)
    {
        var current = CaptureWritableTransportRouteCurrent(state);
        ForceUnregisterInputs.BindCurrent(state, current);
        OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
        foreach (var row in state.Forces.Values)
            current.SetForceRankSlot47(row.Id, row.RankId is int rank ? checked((byte)rank) : null);
        //Do not leave resource-time District24 values behind for an old save
        //whose independent current window is unknown.
        foreach (var row in current.ReadRegistryReferences(RegistryKind.District) ?? throw WorldEditMissing("district_registry"))
        {
            if (row is not { Kind: RegistryKind.District, PhysicalSlot: int id }) throw WorldEditMissing("district_registry_payload");
            var window = state.NativeForceUnregisterCurrent?.DistrictWindows10.GetValueOrDefault(id);
            for (int index = 0; index < 4; index++)
                current.SetDistrictRawByte(id, 0x24 + index, window is { Length: 28 } ? window[20 + index] : null);
        }
        _force164Inputs.BindCurrent(state, current);
        return current;
    }

    private static IEnumerable<int> CurrentWorldForceDistricts(SourceWorldContext current, int forceId)
    {
        foreach (var row in current.ReadRegistryReferences(RegistryKind.District) ?? throw WorldEditMissing("district_registry"))
        {
            if (row is not { Kind: RegistryKind.District, PhysicalSlot: int id }) throw WorldEditMissing("district_registry_payload");
            if (WorldEditNeed(current.ReadDistrictForceId(id), "district_force") == forceId) yield return id;
        }
    }
    private static int ReadCurrentWorldDistrictMilitary(SourceWorldContext current, int district)
    {
        int value = 0;
        for (int index = 0; index < 4; index++)
            value |= WorldEditNeed(current.ReadDistrictRawByte(district, 0x24 + index), "district_military") << (index * 8);
        return value;
    }
    private static void WriteCurrentWorldDistrictMilitary(SourceWorldContext current, int district, int value)
    {
        for (int index = 0; index < 4; index++) current.SetDistrictRawByte(district, 0x24 + index, (byte)((uint)value >> (index * 8)));
    }
    private static GameState CurrentWorldForceParameter(GameState state, string key, int value)
    {
        var owner = state.NativeArmySimulation ?? throw WorldEditMissing("force_parameter_owner");
        if (!owner.Parameters.Int32Values.ContainsKey(key) || owner.Parameters.Float32Values.ContainsKey(key))
            throw WorldEditMissing("force_parameter_" + key);
        return state with { NativeArmySimulation = owner with { Parameters = owner.Parameters with
            { Int32Values = new(owner.Parameters.Int32Values) { [key] = value } } } };
    }
    //Pure subdialog normalization shared by preview and the actual writer.
    //Validation belongs to their existing typed admission boundaries.
    private static (int Friendship, int Months) NormalizeCurrentWorldForceRelationshipValues(
        CurrentWorldForceRelationshipEdit edit, int oldFriendship, int oldMonths)
    {
        int friendship = edit.FriendshipLevel is int category
            ? category == CurrentWorldFriendshipLevel(oldFriendship) ? oldFriendship : CurrentWorldFriendshipCanonical[category]
            : edit.Friendship ?? oldFriendship;
        int months = edit.AllianceMonths ?? oldMonths;
        //A positive duration promotes low friendship; selecting a low radio
        //without a new positive duration clears the retained alliance.
        if (edit.AllianceMonths is > 0 && CurrentWorldFriendshipLevel(friendship) < 3) friendship = 60;
        else if ((edit.FriendshipLevel.HasValue || edit.Friendship.HasValue) && CurrentWorldFriendshipLevel(friendship) < 3) months = 0;
        return (friendship, months);
    }

    private static readonly int[] CurrentWorldFriendshipCanonical = [0, 15, 40, 60, 85, 100];
    private static int CurrentWorldFriendshipLevel(int value) => value < 1 ? 0 : value < 30 ? 1 : value < 50 ? 2 : value < 70 ? 3 : value < 100 ? 4 : 5;

    internal sealed partial class MonthlyCampaignMaintenanceSource
    {
        //The editor passes292BA0 flag0: the mature alliance tail runs, while
        //the separate flag1 Army/logistics tail is deliberately not requested.
        internal GameState ApplyCurrentWorldEditAllianceEndCurrent(ForceReference first, ForceReference second)
        { EndAlliance(first, second); return state; }
    }
}
