using San14.Core;
using San14.Data;
using System.Text.Json;

namespace San14.Client.Presentation;

/// <summary>Locale operations used by reporting, independent of Godot and resource loading.</summary>
public sealed class PeriodReportText(
    Func<string, string> get,
    Func<string, object?[], string> format,
    Func<string?, string> tacticReason,
    string mapSeparator)
{
    public string Get(string key) => get(key);
    public string Format(string key, params object?[] arguments) => format(key, arguments);
    public string TacticReason(string? code) => tacticReason(code);
    public string MapSeparator { get; } = mapSeparator;
}

/// <summary>Existing UI receipt metadata, materialized during the same report projection.</summary>
public sealed record PeriodReportMetadata(int MonthlyReceiptCount, string MonthlyReceiptsJson,
    int SourceEventCount, string SourceEventsJson, string RuleReceiptsJson, string PeriodEntriesJson);

/// <summary>Immutable text and receipt values; no mutable world or locale is retained.</summary>
public sealed record PeriodReportPresentation(string Body, IReadOnlyList<string> Entries,
    PeriodReportMetadata Metadata);

/// <summary>
/// Selects the player's report streams and resolves their display identities and text.
/// Reads the current session synchronously; it never advances or copies the simulation.
/// </summary>
public static class PeriodReportPresenter
{
    public static PeriodReportPresentation Create(GameState world, int playerForce, PeriodReportText text,
        Action<PeriodReportMetadata>? publishMetadata = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(text);
        // The adapter's admission check is retained once; both body and metadata
        // consume the same ordered current-period event projection.
        CampaignEventReportView[] events = CampaignEventReportAdapter.Create(world, playerForce)
            .Where(entry => entry.Period == world.PeriodNumber).ToArray();
        PeriodReportMetadata metadata = CreateMetadata(world, playerForce, events);
        // Main has always bound receipt metadata before formatting report text.
        // Keep that observable prefix if a later missing identity/text fails.
        publishMetadata?.Invoke(metadata);
        var identity = new ReportIdentityLookup(world, text);
        string[] entries = SelectPeriodEntries(world, playerForce)
            .Select(entry => RenderPeriodEntry(world, entry, identity, text))
            .Concat((world.PersonnelReports ?? []).Where(entry => entry.ForceId == playerForce &&
                entry.ReportedInPeriod == world.PeriodNumber)
                .Select(entry => RenderPersonnelReport(entry, identity, text)))
            .Concat((world.TacticReports ?? []).Where(entry => entry.ForceId == playerForce && entry.Period == world.PeriodNumber)
                .Select(entry => RenderTacticReport(world, entry, text)))
            .Concat((world.MonthlyIncomeReports ?? []).Where(entry => entry.ForceId == playerForce && entry.Period == world.PeriodNumber)
                .Select(entry => RenderMonthlyIncomeReceipt(world, entry, text)))
            .Concat(events
                .Select(entry => RenderSourceEventReport(world, entry, text))).ToArray();
        string body = text.Get("play_report_heading") + " " +
            (entries.Length == 0 ? text.Get("play_report_empty") : string.Join(text.MapSeparator, entries));
        return new(body, Array.AsReadOnly(entries), metadata);
    }

    private static IEnumerable<PeriodReportEntry> SelectPeriodEntries(GameState world, int playerForce)
    {
        return world.PeriodReports.Where(entry => (entry.ForceId == playerForce ||
            entry.Kind == "period" || entry.Kind is ("force_defeated" or "campaign_player_defeated" or "campaign_unified" or "capture.last_city_person_freed" or "capture.last_city_personnel_unresolved" or "capture.last_city_force_unresolved") &&
            world.ForceDefeats.TryGetValue(entry.ForceId, out var defeat) && defeat.CapturingForceId == playerForce) &&
            entry.Kind is not ("item_granted" or "item_revoked" or "office_changed" or "tactic_used" or "tactic_fizzled" or "event_triggered") &&
            !(entry.Kind == "monthly_income_unavailable" && (world.MonthlyIncomeReports ?? []).Any(receipt => receipt.CityId == entry.CityId)));
    }

    private static string RenderPeriodEntry(GameState world, PeriodReportEntry entry,
        ReportIdentityLookup identity, PeriodReportText text) =>
        entry.Kind switch
        {
            "period" => text.Format("play_report_period", world.PeriodNumber),
            "deploy" => text.Format("play_report_deploy", identity.OfficerName(entry.OfficerId),
                entry.FromHexId, entry.ToHexId, entry.Amount),
            "move" => text.Format("play_report_move", identity.OfficerName(entry.OfficerId),
                entry.FromHexId, entry.ToHexId, entry.Amount),
            "arrive" => text.Format("play_report_arrive", identity.OfficerName(entry.OfficerId), entry.ToHexId),
            "unit_action_complete" => text.Format("play_report_unit_action_complete", identity.OfficerName(entry.OfficerId)),
            "unit_action_cancelled" => text.Format("play_report_unit_action_cancelled", identity.OfficerName(entry.OfficerId)),
            "unit_entered" => text.Format("play_report_unit_entered", identity.OfficerName(entry.OfficerId), entry.Amount),
            "unit_enter_unresolved" => text.Format("play_report_unit_enter_unresolved", identity.OfficerName(entry.OfficerId),
                entry.LocationId is int entryLocation
                    ? world.Cities.GetValueOrDefault(entryLocation)?.Name ?? world.Gates.GetValueOrDefault(entryLocation)?.Name ?? text.Get("detail_unknown")
                    : identity.CityName(entry.CityId)),
            "officer_return" => text.Format("play_report_officer_return", identity.OfficerName(entry.OfficerId), identity.CityName(entry.CityId)),
            "army_home_changed" => text.Format("play_report_army_home_changed", identity.OfficerName(entry.OfficerId), identity.CityName(entry.CityId)),
            "person_return_unresolved" => text.Get("play_report_person_return_unresolved"),
            "unit_encounter_avoided" => text.Format("play_report_unit_encounter_avoided", identity.OfficerName(entry.OfficerId)),
            "combat_unresolved" => text.Format("play_report_combat_unresolved", identity.OfficerName(entry.OfficerId),
                entry.LocationId is int location ? world.Cities.GetValueOrDefault(location)?.Name ?? world.Gates.GetValueOrDefault(location)?.Name ?? text.Get("detail_unknown") :
                text.Format("play_report_hex_identity", entry.ToHexId)),
            "movement_unresolved" => text.Format("play_report_movement_unresolved", identity.OfficerName(entry.OfficerId)),
            "army_route_unresolved" => text.Get("play_report_army_route_unresolved"),
            "development_unresolved" or "training_unresolved" or "recruitment_unresolved" or "monthly_income_unavailable" => RenderEconomyRuleGap(world, entry, text),
            "development_supplies_cost" => text.Format("play_report_development_supplies_cost", identity.CityName(entry.CityId), entry.Amount),
            "development_morale" => text.Format("play_report_development_morale", identity.CityName(entry.CityId), entry.Amount),
            "tactic_move_blocked" => text.Format("play_report_tactic_move_blocked", identity.OfficerName(entry.OfficerId)),
            "diplomacy_goodwill" => text.Format("play_report_diplomacy_goodwill", identity.OfficerName(entry.OfficerId), identity.ForceName(entry.Amount)),
            "diplomacy_alliance" => text.Format("play_report_diplomacy_alliance", identity.OfficerName(entry.OfficerId), identity.ForceName(entry.Amount)),
            "diplomacy_alliance_cancelled" => text.Format("play_report_diplomacy_alliance_cancelled", identity.ForceName(entry.Amount)),
            "diplomacy_attack_requested" => text.Format("play_report_diplomacy_attack_requested", identity.ForceName(entry.Amount), identity.CityName(entry.CityId)),
            "diplomacy_mission_failed" => text.Format("play_report_diplomacy_mission_failed", identity.OfficerName(entry.OfficerId), identity.ForceName(entry.Amount)),
            "diplomacy_mission_cancelled" => text.Format("play_report_diplomacy_mission_cancelled", identity.OfficerName(entry.OfficerId), identity.ForceName(entry.Amount)),
            "diplomacy_treaty_expired" => text.Format("play_report_diplomacy_treaty_expired", identity.ForceName(entry.Amount)),
            "diplomacy_treaty_ended" => text.Format("play_report_diplomacy_treaty_ended", identity.ForceName(entry.Amount)),
            "diplomacy_attack_request_ended" => text.Format("play_report_diplomacy_attack_request_ended", identity.ForceName(entry.Amount), identity.CityName(entry.CityId)),
            "assault" => text.Format("play_report_assault", identity.OfficerName(entry.OfficerId),
                identity.PlaceName(entry), entry.Amount),
            "durability_damage" => text.Format("play_report_durability_damage",
                identity.OfficerName(entry.OfficerId), identity.PlaceName(entry), entry.Amount),
            "garrison_scattered" => text.Format("play_report_garrison_scattered",
                identity.PlaceName(entry), entry.Amount),
            "field_attack" => text.Format("play_report_field_attack",
                identity.OfficerName(entry.OfficerId), entry.Amount),
            "field_defense" => text.Format("play_report_field_defense",
                identity.OfficerName(entry.OfficerId), entry.Amount),
            "field_destroyed" => text.Format("play_report_field_destroyed",
                identity.OfficerName(entry.OfficerId)),
            "retreat_started" => text.Format("play_report_retreat_started",
                identity.OfficerName(entry.OfficerId), identity.RetreatPlaceName(entry)),
            "retreat_blocked" => text.Format("play_report_retreat_blocked",
                identity.OfficerName(entry.OfficerId)),
            "retreat_arrived" => text.Format("play_report_retreat_arrived",
                identity.OfficerName(entry.OfficerId), entry.Amount),
            "casualties" => text.Format("play_report_casualties", identity.OfficerName(entry.OfficerId), entry.Amount),
            "capture" => text.Format("play_report_capture", identity.OfficerName(entry.OfficerId),
                identity.PlaceName(entry)),
            "force_defeated" => text.Format("play_report_force_defeated", identity.ForceName(entry.ForceId), identity.PlaceName(entry)),
            "force_unit_withdrawn" => text.Format("play_report_force_unit_withdrawn", identity.OfficerName(entry.OfficerId), entry.Amount),
            "capture.last_city_person_freed" => text.Format("play_report_last_city_person_freed",
                identity.OfficerName(entry.OfficerId), identity.CityName(entry.Amount)),
            "capture.last_city_personnel_unresolved" => text.Format("play_report_last_city_personnel_unresolved", identity.OfficerName(entry.OfficerId)),
            "capture.last_city_force_unresolved" => text.Format("play_report_last_city_force_unresolved", world.Forces[entry.ForceId].Name),
            "campaign_player_defeated" => text.Get("play_defeated"),
            "campaign_unified" => text.Get("play_unified"),
            "campaign_unification_unresolved" => text.Get("play_report_unification_unresolved"),
            "food_source_unresolved" => text.Get("play_report_food_source_unresolved"),
            "food_mapped_shortage_unresolved" => text.Get("play_report_food_mapped_shortage_unresolved"),
            PlayableBaseCaptureAftermath.EvacuatedReportKind => text.Format("play_report_base_capture_officer_evacuated",
                identity.OfficerName(entry.OfficerId), identity.PlaceName(entry)),
            PlayableBaseCaptureAftermath.UnresolvedReportKind => text.Format("play_report_base_capture_officer_unresolved",
                identity.OfficerName(entry.OfficerId), identity.PlaceName(entry)),
            PlayableOfficerArrivalAftermath.AssignmentsClearedReportKind => text.Format("play_report_officer_arrival_assignments_cleared",
                identity.OfficerName(entry.OfficerId), identity.PlaceName(entry)),
            "transport_blocked" => text.Format("play_report_transport_blocked",
                identity.OfficerName(entry.OfficerId), identity.CityName(entry.CityId)),
            "transport_troops" => text.Format("play_report_transport_troops",
                identity.OfficerName(entry.OfficerId), identity.CityName(entry.CityId), entry.Amount),
            "transport_supplies" => text.Format("play_report_transport_supplies",
                identity.CityName(entry.CityId), entry.Amount),
            "transport_gold" => text.Format("play_report_transport_gold",
                identity.CityName(entry.CityId), entry.Amount),
            "transport_returning" => text.Format("play_report_transport_returning",
                identity.OfficerName(entry.OfficerId), identity.CityName(entry.CityId)),
            "transport_returned" => text.Format("play_report_transport_returned",
                identity.OfficerName(entry.OfficerId), identity.CityName(entry.CityId)),
            "officer_move" => text.Format("play_report_officer_move",
                identity.OfficerName(entry.OfficerId), identity.CityName(entry.CityId)),
            "officer_move_cancelled" => text.Format("play_report_officer_move_cancelled",
                identity.OfficerName(entry.OfficerId), identity.CityName(entry.CityId)),
            "deployment_cancelled" => text.Format("play_report_deployment_cancelled",
                identity.OfficerName(entry.OfficerId)),
            "talent_search_joined" => text.Format("play_report_talent_search_joined",
                identity.OfficerName(entry.OfficerId), identity.CityName(entry.CityId)),
            "talent_search_none" => text.Format("play_report_talent_search_none",
                identity.OfficerName(entry.OfficerId), identity.CityName(entry.CityId)),
            "talent_recruited" => text.Format("play_report_talent_recruited",
                identity.OfficerName(entry.OfficerId), identity.CityName(entry.CityId)),
            "talent_recruit_failed" => text.Format("play_report_talent_recruit_failed",
                identity.OfficerName(entry.OfficerId), identity.CityName(entry.CityId)),
            "talent_search_cancelled" => text.Format("play_report_talent_search_cancelled",
                identity.OfficerName(entry.OfficerId), identity.CityName(entry.CityId)),
            "talent_recruit_cancelled" => text.Format("play_report_talent_recruit_cancelled",
                identity.OfficerName(entry.OfficerId), identity.CityName(entry.CityId)),
            "food_upkeep" => text.Format("play_report_food_upkeep",
                identity.CityName(entry.CityId), entry.Amount),
            "food_source_changed" => text.Format("play_report_food_source_changed",
                identity.CityName(entry.CityId)),
            "gold_income" => text.Format("play_report_gold_income",
                identity.CityName(entry.CityId), entry.Amount),
            "supplies_income" => text.Format("play_report_supplies_income",
                identity.CityName(entry.CityId), entry.Amount),
            "development_cost" => text.Format("play_report_development_cost",
                identity.CityName(entry.CityId), entry.Amount),
            "development_unfunded" => text.Format("play_report_development_unfunded",
                identity.CityName(entry.CityId), entry.Amount),
            "food_shortage" => text.Format("play_report_food_shortage",
                identity.CityName(entry.CityId), entry.Amount),
            "food_desertion" => text.Format("play_report_food_desertion",
                identity.CityName(entry.CityId), entry.Amount),
            "food_unit_morale" => text.Format("play_report_food_unit_morale",
                identity.OfficerName(entry.OfficerId), entry.Amount),
            "supply_cut" => text.Format("play_report_supply_cut",
                identity.OfficerName(entry.OfficerId), entry.Amount),
            "territory" => text.Format("play_report_territory",
                identity.OfficerName(entry.OfficerId), entry.Amount),
            "area_capture" => text.Format("play_report_area_capture",
                identity.OfficerName(entry.OfficerId), identity.AreaName(entry.Amount), identity.CityName(entry.CityId)),
            "area_capture_unassigned" => text.Format("play_report_area_unassigned",
                identity.OfficerName(entry.OfficerId), identity.AreaName(entry.Amount)),
            "officer_territory" => text.Format("play_report_officer_territory",
                identity.OfficerName(entry.OfficerId), identity.CityName(entry.CityId), entry.Amount),
            "officer_territory_unknown" => text.Format("play_report_officer_territory_unknown",
                identity.OfficerName(entry.OfficerId), identity.AreaName(entry.Amount)),
            "occupy_complete" => text.Format("play_report_occupy_complete",
                identity.OfficerName(entry.OfficerId), identity.AreaName(entry.Amount)),
            "structure_start" => text.Format("play_report_structure_start",
                identity.OfficerName(entry.OfficerId), entry.ToHexId, identity.CityName(entry.CityId), entry.Amount),
            "structure_progress" => text.Format("play_report_structure_progress",
                entry.ToHexId, entry.Amount),
            "structure_complete" => text.Format("play_report_structure_complete", entry.ToHexId),
            "structure_blocked" => text.Format("play_report_structure_blocked", entry.ToHexId),
            "structure_repair_progress" => text.Format("play_report_structure_repair_progress",
                entry.ToHexId, entry.Amount),
            "structure_repaired" => text.Format("play_report_structure_repaired", entry.ToHexId),
            "structure_demolished" => text.Format("play_report_structure_demolished", entry.ToHexId),
            "training_cost" => text.Format("play_report_training_cost", identity.CityName(entry.CityId), entry.Amount),
            "training" => text.Format("play_report_training", identity.CityName(entry.CityId), entry.Amount),
            "recruit_cost" => text.Format("play_report_recruit_cost", identity.CityName(entry.CityId), entry.Amount),
            "recruit" => text.Format("play_report_recruit", identity.CityName(entry.CityId), entry.Amount),
            "commerce" => text.Format("play_report_commerce", identity.CityName(entry.CityId), entry.Amount),
            "agriculture" => text.Format("play_report_agriculture", identity.CityName(entry.CityId), entry.Amount),
            "barracks" => text.Format("play_report_barracks", identity.CityName(entry.CityId), entry.Amount),
            "governance_active" => text.Format("play_report_governance_active", entry.Amount),
            _ => text.Get("play_report_unknown")
        };

    private static string RenderPersonnelReport(PersonnelReportEntry entry,
        ReportIdentityLookup identity, PeriodReportText text) =>
        entry.Kind switch
        {
            PersonnelActionKind.GrantItem => text.Format("play_report_item_granted", identity.OfficerName(entry.OfficerId), identity.ItemName(entry.ItemId)),
            PersonnelActionKind.RevokeItem => text.Format("play_report_item_revoked", identity.OfficerName(entry.OfficerId), identity.ItemName(entry.ItemId)),
            PersonnelActionKind.SetOffice => text.Format("play_report_office_changed", identity.OfficerName(entry.OfficerId), identity.OfficeName(entry.NextOfficeId)),
            _ => throw new InvalidDataException("Unsupported personnel report kind.")
        };

    // This lookup is a synchronous read facade, not a detached world snapshot.
    // Only the presentation's strings and receipt JSON escape Create.
    private sealed class ReportIdentityLookup(GameState world, PeriodReportText text)
    {
        public string OfficerName(int? id) => id is int known && world.Officers.TryGetValue(known, out OfficerState? officer)
            ? officer.Name : id?.ToString() ?? "?";
        public string CityName(int? id) => id is int known && world.Cities.TryGetValue(known, out CityState? city)
            ? city.Name : id?.ToString() ?? "?";
        public string AreaName(int id) => world.Areas.TryGetValue(id, out AreaState? area) &&
            !string.IsNullOrWhiteSpace(area.Name) ? area.Name : id.ToString();
        public string ForceName(int id) => world.Forces.TryGetValue(id, out ForceState? force) ? force.Name : id.ToString();
        public string ItemName(int? id) => id is int itemId && world.Items?.TryGetValue(itemId, out ItemState? item) == true &&
            world.ItemDefinitions.TryGetValue(item.DefinitionId, out ItemDefinition? definition) ? definition.Name : id?.ToString() ?? "?";
        public string OfficeName(int? id) => id is int officeId && world.OfficerOffices.TryGetValue(officeId, out OfficerOfficeDefinition? definition)
            ? definition.Name : id?.ToString() ?? "?";
        public string PlaceName(PeriodReportEntry entry) => entry.LocationId is int locationId
            ? world.Cities.GetValueOrDefault(locationId)?.Name ?? world.Gates.GetValueOrDefault(locationId)?.Name ?? text.Get("detail_unknown")
            : entry.CityId is int cityId ? CityName(cityId) :
            world.Gates.Values.FirstOrDefault(gate => gate.HexId == entry.FromHexId)?.Name ??
            entry.FromHexId?.ToString() ?? "?";
        public string RetreatPlaceName(PeriodReportEntry entry) => entry.CityId is int cityId
            ? CityName(cityId)
            : world.Gates.Values.FirstOrDefault(gate => gate.HexId == entry.ToHexId)?.Name ??
              entry.ToHexId?.ToString() ?? "?";
    }

    private static string RenderTacticReport(GameState world, TacticReportEntry entry, PeriodReportText text)
    {
        string caster = world.Officers.GetValueOrDefault(entry.OfficerId)?.Name ?? entry.OfficerId.ToString();
        string tactic = world.Tactics.GetValueOrDefault(entry.TacticId)?.Name ?? entry.TacticId.ToString();
        string target = entry.TargetUnitId is int id && world.Units.TryGetValue(id, out UnitState? unit)
            ? world.Officers.GetValueOrDefault(unit.OfficerId)?.Name ?? id.ToString() : entry.TargetUnitId?.ToString() ?? text.Get("detail_unknown");
        string effect = entry.Effect is { } kind ? text.Get("play_tactic_effect_" + kind.ToString().ToLowerInvariant()) : text.Get("detail_unknown");
        return entry.Kind switch
        {
            "used" => text.Format("play_report_tactic_used", caster, tactic, target),
            "fizzled" => text.Format("play_report_tactic_fizzled", caster, tactic, text.TacticReason(entry.Reason)),
            "effect" => text.Format("play_report_tactic_effect", tactic, target, effect, entry.Amount) +
                (entry.ExpiresAtPeriod is int expiry ? "  " + text.Format("play_tactic_effect_until", expiry) : ""),
            "expired" => text.Format("play_report_tactic_expired", target, tactic, effect),
            _ => throw new InvalidDataException("Unsupported tactic report kind.")
        };
    }

    private static string RenderMonthlyIncomeReceipt(GameState world, MonthlyIncomeReportEntry receipt, PeriodReportText text)
    {
        string city = world.Cities[receipt.CityId].Name;
        MonthlyIncomeQuote quote = receipt.Quote;
        if (quote.Status == PeriodEconomyCalculationStatus.Available)
            return text.Format("play_report_monthly_receipt_available", city, quote.Gold, quote.Supplies);
        return text.Format(quote.Status == PeriodEconomyCalculationStatus.PartialCurrentContext
                ? "play_report_monthly_receipt_partial" : "play_report_monthly_receipt_unavailable",
            city, quote.MissingInputs.Length);
    }

    private static string RenderEconomyRuleGap(GameState world, PeriodReportEntry entry, PeriodReportText text)
    {
        string city = entry.CityId is int id && world.Cities.TryGetValue(id, out CityState? place)
            ? place.Name : text.Get("detail_unknown");
        return text.Format("play_report_" + entry.Kind, city) +
            (entry.MissingRuleKeys is { Length: > 0 } missing ? "  " + text.Format("play_report_missing_rule_count", missing.Length) : "");
    }

    private static string RenderSourceEventReport(GameState world, CampaignEventReportView entry, PeriodReportText text)
    {
        string identity = entry.EventId is int eventId ? text.Format("play_report_source_event_identity", eventId) : text.Get("play_report_event_identity");
        if (entry.Kind == "triggered") return text.Format("play_report_event_triggered", identity);
        if (entry.Kind == nameof(PlayableEventEffectKind.Message))
            return text.Format("play_report_event_message", identity);
        string? effect = entry.Kind switch
        {
            nameof(PlayableEventEffectKind.SetAgriculture) => "agriculture_set",
            nameof(PlayableEventEffectKind.AddAgriculture) => "agriculture_add",
            nameof(PlayableEventEffectKind.SetCommerce) => "commerce_set",
            nameof(PlayableEventEffectKind.AddCommerce) => "commerce_add",
            nameof(PlayableEventEffectKind.SetBarracks) => "barracks_set",
            nameof(PlayableEventEffectKind.AddBarracks) => "barracks_add",
            nameof(PlayableEventEffectKind.SetSecurity) => "security_set",
            nameof(PlayableEventEffectKind.AddSecurity) => "security_add",
            _ => null
        };
        if (effect is not null)
        {
            string area = entry.AreaId is int id && world.Areas.TryGetValue(id, out AreaState? place) && !string.IsNullOrWhiteSpace(place.Name)
                ? place.Name : text.Get("detail_unknown");
            return text.Format("play_report_event_area_effect", identity, area, text.Get("play_report_event_effect_" + effect), entry.Amount);
        }
        if (entry.Kind == nameof(PlayableEventEffectKind.SetFlag)) return text.Format("play_report_event_flag", identity);
        if (entry.Kind == nameof(PlayableEventEffectKind.AddCounter)) return text.Format("play_report_event_counter", identity);
        return text.Format("play_report_event_effect_unknown", identity);
    }

    private static PeriodReportMetadata CreateMetadata(GameState world, int playerForce,
        CampaignEventReportView[] events)
    {
        // Income and period receipt metadata intentionally include every period;
        // the visible income/event streams have their own current-period filter.
        MonthlyIncomeReportEntry[] income = (world.MonthlyIncomeReports ?? [])
            .Where(entry => entry.ForceId == playerForce).ToArray();
        return new(income.Length, JsonSerializer.Serialize(income), events.Length,
            JsonSerializer.Serialize(events),
            JsonSerializer.Serialize(world.PeriodReports.Where(entry => entry.ForceId == playerForce &&
                (entry.RuleId is not null || entry.MissingRuleKeys is not null))),
            JsonSerializer.Serialize(world.PeriodReports.Where(entry => entry.ForceId == playerForce)));
    }
}
