using System.Security.Cryptography;
using System.Text;
using San14.Data;

internal sealed class PreviewText
{
    private readonly LocalizedText _text;
    public SourceProvenance? OverrideSource { get; }
    public string SourceLocale { get; }
    public bool IsOverride => OverrideSource is not null;
    public string Get(string key) => _text.Get(key);
    public string Format(string key, params object?[] args) => _text.Format(key, args);
    public string CommandError(string code)
    {
        if (code.StartsWith("tactic.effect.", StringComparison.Ordinal)) return _text.Get("play_command_error_tactic.effect_unresolved");
        string key = "play_command_error_" + code;
        return _text.Keys.Contains(key) ? _text.Get(key) : code;
    }

    public string TacticReason(string? code)
    {
        if (code?.StartsWith("tactic.effect.", StringComparison.Ordinal) == true) return _text.Get("play_command_error_tactic.effect_unresolved");
        string key = "play_command_error_" + code;
        return code is not null && _text.Keys.Contains(key) ? _text.Get(key) : _text.Get("play_tactic_rule_unknown");
    }

    public PreviewText(bool traditional, ResourceOverrides? overrides)
    {
        string locale = traditional ? "TC" : "SC";
        SourceLocale = locale;
        string bundledPath = $"res://i18n/{locale}.json";
        using Godot.FileAccess bundled = Godot.FileAccess.Open(bundledPath,
            Godot.FileAccess.ModeFlags.Read)
            ?? throw new InvalidDataException($"Bundled UI text is missing: {bundledPath}");
        GameResourceId resourceId = GameResourceId.Ui(locale);
        string? overridePath = overrides?.Find(resourceId);
        string? overrideJson = null;
        if (overridePath is not null)
        {
            byte[] data = File.ReadAllBytes(overridePath);
            overrideJson = new UTF8Encoding(false, true).GetString(data);
            OverrideSource = new SourceProvenance(resourceId.OverrideRelativePath,
                data.LongLength, Convert.ToHexString(SHA256.HashData(data)))
            {
                ResourceUri = resourceId.ToString(),
                PhysicalPath = overridePath,
                IsOverride = true
            };
        }
        _text = LocalizedText.Parse(bundled.GetAsText(), overrideJson);
        foreach (string key in RequiredKeys)
            _ = _text.Get(key);
    }

    private static readonly string[] RequiredKeys =
    [
        "title", "hint", "language", "locale_sc", "locale_tc", "scenarios",
        "new_game_difficulty", "new_game_difficulty_beginner",
        "new_game_difficulty_intermediate", "new_game_difficulty_advanced",
        "new_game_difficulty_super", "new_game_difficulty_top",
        "new_game_initial_credit", "new_game_captured_officers",
        "new_game_yes", "new_game_no",
        "new_game_options_pending", "new_game_options_unknown",
        "force_selection", "cities_unit", "map_title", "map_hint",
        "map_surface_fallback", "scene_preview_use_target",
        "play_terrain_map_title", "play_terrain_map_notice", "asset_hint",
        "play_map_unit_here", "play_map_units_here", "play_map_structure_here",
        "play_formation_source_base", "play_formation_source_unknown",
        "tile_prompt", "loading", "source_file", "decoded_bytes", "bytes",
        "cell", "surface", "raw_fields", "city_records", "force", "officer",
        "officer_list", "raw_values", "raw_amounts", "source_hash", "colon",
        "linked_locations", "city_hex", "raw_limits", "group_code", "location",
        "list_separator", "map_separator", "date_inline", "date_line",
        "scenario_item", "force_item", "loaded_status", "load_error",
        "districts", "district_number", "district_leader", "governor", "gold",
        "supplies", "troops", "morale", "durability", "size", "size_small",
        "size_medium", "size_large", "size_giant", "size_unknown", "gates", "troops_includes_gates",
        "base_abilities", "base_abilities_hint", "leadership", "strength",
        "intelligence", "politics", "charm", "land_controlled", "land_rate",
        "territory_map", "encoding_map", "region", "neutral", "unresolved_references",
        "portrait_source", "portrait_error", "portrait_unavailable",
        "save_initial_state", "load_initial_state", "initial_state_hint",
        "initial_state_saved", "initial_state_loaded", "state_file_error",
        "startup_failed", "startup_failed_help", "client_failed", "client_failed_help",
        "play_title", "play_commands_administration", "play_commands_military",
        "play_commands_personnel", "play_commands_governance",
        "play_map_show_commands", "play_map_hide_commands", "play_map_reset_camera",
        "play_start", "play_city", "play_force_information", "play_city_information_item", "play_city_orders_item", "play_area_orders_item", "play_unit_orders_item", "play_map_next_unit",
        "play_area", "play_focus",
        "play_focus_commerce", "play_focus_agriculture", "play_focus_barracks",
        "play_admin_officer", "play_officer", "play_formation", "play_tactic_slot",
        "play_tactic_none", "play_troops", "play_target", "play_admin",
        "play_deploy", "play_deploy_occupy", "play_deploy_build", "play_advance", "play_finish_report", "play_save",
        "play_continue", "play_save_slot", "play_save_slot_item", "play_save_slot_used", "play_not_started", "play_active", "play_reporting",
        "save_choose_slot", "load_choose_slot", "save_slot_unreadable", "save_file_failed", "save_time_unknown",
        "save_overwrite_question", "load_replace_question",
        "play_governance", "play_governance_apply", "play_governance_advisor", "play_governance_group",
        "play_governance_vacant", "play_governance_status", "play_governance_pending",
        "play_governance_no_pending", "play_governance_unavailable", "play_report_governance_active",
        "play_orders", "play_map_date", "play_map_orders", "play_map_advance",
        "play_map_force_gold", "play_map_force_supplies", "play_map_force_troops",
        "play_map_force_cities", "play_map_force_officers", "play_map_force_districts",
        "play_target_none", "play_started", "play_saved",
        "play_loaded", "play_order_accepted", "play_order_rejected",
        "play_error", "play_report_empty", "play_report_period",
        "play_report_deploy", "play_report_commerce", "play_report_agriculture",
        "play_report_barracks", "play_report_other", "play_report_heading",
        "play_unit", "play_unit_item", "play_retarget", "play_occupy_area", "play_withdraw",
        "play_report_occupy_complete", "play_report_area_capture",
        "play_report_area_unassigned", "play_disband", "play_build_formation",
        "play_structure_kind", "play_repair_structure", "play_demolish_structure",
        "play_report_structure_start", "play_report_structure_progress",
        "play_report_structure_complete", "play_report_structure_blocked",
        "play_report_structure_repair_progress", "play_report_structure_repaired",
        "play_report_structure_demolished",
        "play_report_officer_territory", "play_report_officer_territory_unknown",
        "play_report_supply_cut",
        "play_duty_officer", "play_duty_none", "play_duty_unknown", "play_duty_status",
        "play_assign_recruiter", "play_assign_trainer", "play_unified", "play_defeated", "play_command_error_command.campaign_ended", "play_report_unification_unresolved", "play_report_food_source_unresolved", "play_report_food_mapped_shortage_unresolved",
        "play_report_move", "play_report_arrive", "play_report_assault",
        "play_report_durability_damage", "play_report_garrison_scattered",
        "play_report_field_attack", "play_report_field_defense", "play_report_field_destroyed",
        "play_report_retreat_started", "play_report_retreat_blocked",
        "play_report_retreat_arrived",
        "play_report_casualties", "play_report_capture", "play_report_training_cost",
        "play_report_base_capture_officer_evacuated", "play_report_base_capture_officer_unresolved",
        "play_report_last_city_person_freed", "play_report_last_city_personnel_unresolved", "play_report_last_city_force_unresolved",
        "play_command_error_rules.army_creation", "play_command_error_deployment.native_creation_denied",
        "play_report_officer_arrival_assignments_cleared",
        "play_report_training", "play_report_recruit_cost", "play_report_recruit",
        "play_transport_city", "play_transport_supplies", "play_transport_gold",
        "play_transport_dispatch", "play_transport_hint", "play_report_transport_blocked",
        "play_report_transport_troops", "play_report_transport_supplies",
        "play_report_transport_gold", "play_transport_arrival", "play_transport_enter_city",
        "play_transport_return_city", "play_report_transport_returning",
        "play_report_transport_returned", "play_move_officer", "play_move_destination",
        "play_move_dispatch", "play_summon_officer", "play_move_summon",
        "play_summon_item", "play_move_pending", "play_report_officer_move",
        "play_report_officer_move_cancelled", "play_report_deployment_cancelled",
        "play_talent_executor", "play_talent_target", "play_talent_search",
        "play_talent_recruit", "play_talent_pending",
        "play_report_talent_search_joined", "play_report_talent_search_none",
        "play_report_talent_recruited", "play_report_talent_recruit_failed",
        "play_report_talent_search_cancelled", "play_report_talent_recruit_cancelled",
        "play_prepared_deployment", "play_prepared_item", "play_prepared_build_item",
        "play_cancel_deployment",
        "play_report_food_upkeep", "play_report_food_source_changed", "play_report_gold_income",
        "play_report_supplies_income", "play_report_food_shortage",
        "play_report_development_cost", "play_report_development_unfunded",
        "play_report_food_desertion", "play_report_food_unit_morale",
        "play_report_territory", "play_commands_diplomacy",
        "play_military_title", "play_military_attack_nearby", "play_military_allow_retreat",
        "play_military_pursuit", "play_military_after_action", "play_military_stay",
        "play_military_retreat", "play_military_waypoints", "play_military_waypoint",
        "play_military_add_selected_hex", "play_military_remove", "play_military_clear",
        "play_military_apply_waypoints", "play_military_apply_behavior",
        "play_diplomacy_title", "play_diplomacy_executor", "play_diplomacy_target_force",
        "play_diplomacy_months", "play_diplomacy_attack_city", "play_diplomacy_goodwill",
        "play_diplomacy_alliance", "play_diplomacy_cancel_alliance", "play_diplomacy_request_attack",
        "play_diplomacy_unknown", "play_diplomacy_no_treaty", "play_diplomacy_treaty_alliance",
        "play_diplomacy_treaty_truce", "play_diplomacy_relation_status", "play_diplomacy_pending",
        "play_diplomacy_relation_hostile", "play_diplomacy_relation_treacherous",
        "play_diplomacy_relation_discordant", "play_diplomacy_relation_neutral",
        "play_diplomacy_relation_friendly", "play_diplomacy_relation_close",
        "play_report_unit_action_complete", "play_report_unit_action_cancelled",
        "play_report_unit_entered", "play_report_unit_encounter_avoided",
        "play_report_diplomacy_goodwill", "play_report_diplomacy_alliance",
        "play_report_diplomacy_alliance_cancelled", "play_report_diplomacy_attack_requested",
        "play_report_diplomacy_mission_failed", "play_report_diplomacy_mission_cancelled",
        "play_report_diplomacy_treaty_expired", "play_report_diplomacy_treaty_ended",
        "play_report_diplomacy_attack_request_ended", "play_event_rule_unavailable",
        "play_personnel_title", "play_personnel_officer",
        "play_personnel_item", "play_personnel_office",
        "play_personnel_grant", "play_personnel_revoke",
        "play_personnel_appoint", "play_personnel_status",
        "play_personnel_inventory_unknown", "play_personnel_inventory_known",
        "play_personnel_office_unverified", "play_command_error_personnel.scope",
        "play_command_error_personnel.ruler_office", "play_command_error_personnel.officer_busy",
        "play_command_error_personnel.item_revoke_first", "play_command_error_personnel.item_policy",
        "play_command_error_personnel.office_policy", "play_command_error_personnel.item_state",
        "play_command_error_personnel.item_force", "play_command_error_personnel.loyalty",
        "play_command_error_personnel.item_absent", "play_command_error_personnel.item_control",
        "play_command_error_personnel.item_holder_changed", "play_command_error_personnel.office_missing",
        "play_command_error_personnel.officer_missing", "play_command_error_personnel.item_missing",
        "play_command_error_personnel.inventory", "play_command_error_personnel.ruler",
        "play_command_error_personnel.officer_control", "play_command_error_personnel.ineligible",
        "play_command_error_personnel.policy", "play_command_error_personnel.insufficient_orders",
        "play_command_error_personnel.insufficient_gold", "play_command_error_personnel.current_context",
        "play_command_error_personnel.hired_status", "play_command_error_personnel.command_cost",
        "play_command_error_personnel.loyalty_guards", "play_command_error_personnel.loyalty_sentinel",
        "play_command_error_personnel.item_loyalty_value", "play_command_error_personnel.confiscation_penalty",
        "play_command_error_personnel.loyalty_result", "play_command_error_personnel.headquarters_district",
        "play_report_item_granted", "play_report_item_revoked",
        "play_report_office_changed",
        "play_military_attack_target", "play_military_enter_target",
        "play_military_attack", "play_military_enter",
        "play_military_stay_action", "play_military_stay_hint",
        "play_military_target_unit", "play_military_target_location",
        "play_military_no_action", "play_military_action_current",
        "play_military_action_complete", "play_military_action_move",
        "play_military_action_stay", "play_military_action_attack",
        "play_military_action_enter", "play_military_action_occupy",
        "play_military_action_build", "play_military_action_repair",
        "play_military_action_demolish", "play_tactic_active_title",
        "play_tactic_choice", "play_tactic_target",
        "play_tactic_use", "play_tactic_cancel",
        "play_tactic_no_equipped", "play_tactic_target_item",
        "play_tactic_status", "play_tactic_state_unresolved",
        "play_tactic_state_ready", "play_tactic_state_queued",
        "play_tactic_state_coolingdown", "play_tactic_state_disabled",
        "play_tactic_cooldown_until", "play_tactic_profile",
        "play_tactic_relation_self", "play_tactic_relation_friendly",
        "play_tactic_relation_hostile", "play_tactic_queued",
        "play_tactic_queue_empty", "play_tactic_effect_until",
        "play_report_tactic_used", "play_report_tactic_fizzled",
        "play_report_tactic_effect", "play_report_tactic_expired",
        "play_tactic_effect_armydamage", "play_tactic_effect_moralechange",
        "play_tactic_effect_attackpercent", "play_tactic_effect_defensepercent",
        "play_tactic_effect_immobilized", "play_tactic_effect_confused",
        "play_tactic_effect_provoked", "play_tactic_effect_clearharmfulstatuses",
        "play_tactic_effect_clearabnormalstatuses", "play_tactic_effect_armyhealing",
        "play_tactic_effect_nativeabnormalchange", "play_tactic_effect_nativemoralechange",
        "play_command_error_unit.action_mission",
        "play_command_error_unit.behavior_mission", "play_command_error_unit.batch",
        "play_command_error_tactic.phase", "play_command_error_tactic.control",
        "play_command_error_tactic.unit_missing", "play_command_error_tactic.tactic_missing",
        "play_command_error_tactic.district", "play_command_error_tactic.not_equipped",
        "play_command_error_tactic.runtime", "play_command_error_tactic.runtime_unresolved",
        "play_command_error_tactic.formation", "play_command_error_tactic.formation_unresolved",
        "play_command_error_tactic.formation_denied", "play_command_error_tactic.unit_inactive",
        "play_command_error_tactic.cooldown", "play_command_error_tactic.confused",
        "play_command_error_tactic.target_missing", "play_command_error_tactic.target_runtime",
        "play_command_error_tactic.target_runtime_unresolved", "play_command_error_tactic.target_relation",
        "play_command_error_tactic.target_range", "play_command_error_tactic.eligibility",
        "play_command_error_tactic.eligibility_unresolved", "play_command_error_tactic.eligibility_denied",
        "play_command_error_tactic.pending", "play_command_error_tactic.pending_other",
        "play_command_error_tactic.profile_changed", "play_command_error_tactic.source_revision",
        "play_command_error_tactic.use",
        "scene_audio_loading", "scene_audio_ready_unresolved",
        "scene_audio_unavailable", "scene_audio_selection_unknown",
        "play_report_combat_unresolved", "play_report_movement_unresolved", "play_report_army_route_unresolved", "play_report_hex_identity",
        "play_report_development_unresolved", "play_report_training_unresolved",
        "play_report_recruitment_unresolved", "play_report_monthly_income_unavailable",
        "play_report_missing_rule_count", "play_report_monthly_receipt_available",
        "play_report_monthly_receipt_partial", "play_report_monthly_receipt_unavailable",
        "play_report_development_supplies_cost", "play_report_development_morale",
        "play_report_tactic_move_blocked", "play_report_source_event_identity",
        "play_report_event_identity", "play_report_event_triggered",
        "play_report_event_message", "play_report_event_area_effect",
        "play_report_event_effect_agriculture_set", "play_report_event_effect_agriculture_add",
        "play_report_event_effect_commerce_set", "play_report_event_effect_commerce_add",
        "play_report_event_effect_barracks_set", "play_report_event_effect_barracks_add",
        "play_report_event_effect_security_set", "play_report_event_effect_security_add",
        "play_report_event_flag", "play_report_event_counter",
        "play_report_event_effect_unknown", "play_report_unknown",
        "play_military_unit_summary", "play_tactic_rule_unknown",
        "play_command_error_tactic.effect_unresolved",
        "ui_audio_loading", "ui_audio_ready", "ui_audio_unavailable"
    ];

    public string Title => _text.Get("title");
    public string Hint => _text.Get("hint");
    public string Language => _text.Get("language");
    public string LocaleSimplified => _text.Get("locale_sc");
    public string LocaleTraditional => _text.Get("locale_tc");
    public string Scenarios => _text.Get("scenarios");
    public string ForceSelection => _text.Get("force_selection");
    public string CitiesUnit => _text.Get("cities_unit");
    public string MapTitle => _text.Get("map_title");
    public string MapHint => _text.Get("map_hint");
    public string AssetHint => _text.Get("asset_hint");
    public string TilePrompt => _text.Get("tile_prompt");
    public string Loading => _text.Get("loading");
    public string SourceFile => _text.Get("source_file");
    public string DecodedBytes => _text.Get("decoded_bytes");
    public string Bytes => _text.Get("bytes");
    public string Cell => _text.Get("cell");
    public string Surface => _text.Get("surface");
    public string RawFields => _text.Get("raw_fields");
    public string CityRecords => _text.Get("city_records");
    public string Force => _text.Get("force");
    public string Officer => _text.Get("officer");
    public string OfficerList => _text.Get("officer_list");
    public string RawValues => _text.Get("raw_values");
    public string RawAmounts => _text.Get("raw_amounts");
    public string LinkedLocations => _text.Get("linked_locations");
    public string CityHex => _text.Get("city_hex");
    public string RawLimits => _text.Get("raw_limits");
    public string GroupCode => _text.Get("group_code");
    public string Location => _text.Get("location");
    public string SourceHash => _text.Get("source_hash");
    public string Colon => _text.Get("colon");
    public string ListSeparator => _text.Get("list_separator");
    public string MapSeparator => _text.Get("map_separator");
    public string Districts => _text.Get("districts");
    public string DistrictLeader => _text.Get("district_leader");
    public string Governor => _text.Get("governor");
    public string Gold => _text.Get("gold");
    public string Supplies => _text.Get("supplies");
    public string Troops => _text.Get("troops");
    public string Morale => _text.Get("morale");
    public string Durability => _text.Get("durability");
    public string Size => _text.Get("size");
    public string Gates => _text.Get("gates");
    public string TroopsIncludesGates => _text.Get("troops_includes_gates");
    public string BaseAbilities => _text.Get("base_abilities");
    public string BaseAbilitiesHint => _text.Get("base_abilities_hint");
    public string Leadership => _text.Get("leadership");
    public string Strength => _text.Get("strength");
    public string Intelligence => _text.Get("intelligence");
    public string Politics => _text.Get("politics");
    public string Charm => _text.Get("charm");
    public string LandControlled => _text.Get("land_controlled");
    public string LandRate => _text.Get("land_rate");
    public string TerritoryMap => _text.Get("territory_map");
    public string EncodingMap => _text.Get("encoding_map");
    public string Region => _text.Get("region");
    public string Neutral => _text.Get("neutral");
    public string UnresolvedReferences(int identities, int placements) =>
        _text.Format("unresolved_references", identities, placements);
    public string PortraitSource(string name) => _text.Format("portrait_source", name);
    public string PortraitError(string name, string error) => _text.Format("portrait_error", name, error);
    public string PortraitUnavailable => _text.Get("portrait_unavailable");
    public string SaveInitialState => _text.Get("save_initial_state");
    public string LoadInitialState => _text.Get("load_initial_state");
    public string InitialStateHint => _text.Get("initial_state_hint");
    public string InitialStateSaved => _text.Get("initial_state_saved");
    public string InitialStateLoaded => _text.Get("initial_state_loaded");
    public string StateFileError(string error) => _text.Format("state_file_error", error);
    public string StartupFailed => _text.Get("startup_failed");
    public string StartupFailedHelp => _text.Get("startup_failed_help");
    public string ClientFailed => _text.Get("client_failed");
    public string ClientFailedHelp => _text.Get("client_failed_help");

    public string DateInline(int year, int month, int day) => _text.Format("date_inline", year, month, day);
    public string DateLine(int year, int month, int day) => _text.Format("date_line", year, month, day);
    public string DistrictNumber(int number) => _text.Format("district_number", number);
    public string CitySize(int code) => code switch
    {
        1 => _text.Get("size_small"),
        2 => _text.Get("size_medium"),
        3 => _text.Get("size_large"),
        4 => _text.Get("size_giant"),
        _ => _text.Format("size_unknown", code)
    };
    public string ScenarioItem(int number, string date, string? title) =>
        _text.Format("scenario_item", number, date, title);
    public string ForceItem(int slot, string ruler, int cities) =>
        _text.Format("force_item", slot, ruler, cities, CitiesUnit);
    public string LoadedStatus(int scenarios) => _text.Format("loaded_status", scenarios);
    public string LoadError(string detail) => _text.Format("load_error", detail);
}
