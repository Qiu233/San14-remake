using San14.Core;

namespace San14.Data;

public sealed record AreaAdministrationMapDevelopment(AreaDevelopmentFocus Focus,
    AreaAdministrationQuery<int?> Progress, AreaAdministrationQuery<bool?> IsFocused,
    AreaAdministrationQuery<bool?> IsComplete);

public sealed record AreaAdministrationMapRow(int AreaId,
    AreaAdministrationQuery<bool?> NativeValid,
    AreaAdministrationQuery<string> AreaName,
    AreaAdministrationQuery<int?> RepresentativeHexId,
    AreaAdministrationQuery<int?> OfficerId,
    AreaAdministrationQuery<bool?> PersonValid,
    AreaAdministrationQuery<OfficerPortraitRoute?> Portrait,
    AreaAdministrationQuery<int?> ForceId,
    AreaAdministrationQuery<AreaMarkerColor?> ForceColor,
    AreaAdministrationQuery<byte?> RawFocus,
    AreaAdministrationQuery<AreaDevelopmentFocus?> Focus,
    IReadOnlyList<AreaAdministrationMapDevelopment> Development);

public sealed record AreaAdministrationMapPresentation(long Revision,
    IReadOnlyList<AreaAdministrationMapRow> Rows);

public sealed partial class AreaAdministrationDraft
{
    private AreaAdministrationMapPresentation? _mapPresentation;

    /// <summary>199AB0's held-area map buttons. Reads this draft's current
    /// appointments once per revision; never opens a child or borrows a World.</summary>
    public AreaAdministrationQuery<AreaAdministrationMapPresentation> QueryMapPresentation()
    {
        try
        {
            EnsureOpen();
            if (_mapPresentation is { } cached && cached.Revision == Revision)
                return AreaAdministrationQuery<AreaAdministrationMapPresentation>.Known(cached);
            var rows = new List<AreaAdministrationMapRow>(_held.Length);
            foreach (int area in _held)
            {
                var valid = Read<bool?>(() => K(_current.ReadAreaNativeValidity(new(area)), "map_area_validity"));
                var name = ParentAreaName(area, _state.Areas.GetValueOrDefault(area));
                if (!valid.Available || valid.Value != true)
                {
                    string missing = valid.UnresolvedInput ?? "area-draft.map_invalid_area_not_drawn";
                    rows.Add(new(area, valid, name,
                        AreaAdministrationQuery<int?>.Unknown(missing), AreaAdministrationQuery<int?>.Unknown(missing),
                        AreaAdministrationQuery<bool?>.Unknown(missing), AreaAdministrationQuery<OfficerPortraitRoute?>.Unknown(missing),
                        AreaAdministrationQuery<int?>.Unknown(missing), AreaAdministrationQuery<AreaMarkerColor?>.Unknown(missing),
                        AreaAdministrationQuery<byte?>.Unknown(missing), AreaAdministrationQuery<AreaDevelopmentFocus?>.Unknown(missing),
                        Array.Empty<AreaAdministrationMapDevelopment>()));
                    continue;
                }
                var hex = Read<int?>(() =>
                {
                    int raw = K(_current.ReadAreaHexIndex(new(area)), "map_representative_hex");
                    var pointer = _current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw Missing("map_representative_hex_pointer");
                    return K(pointer.PhysicalSlot, "map_representative_hex_identity");
                });
                var physical = Read<int?>(() => PersonAreaAppointmentSource.Resolve(_current, Person(area)));
                var personValid = physical.Available
                    ? Read<bool?>(() => K(_current.ReadPersonNativeValidity(physical.Value!.Value), "map_person_validity"))
                    : AreaAdministrationQuery<bool?>.Unknown(physical.UnresolvedInput!);
                var identity = personValid.Available && personValid.Value == false
                    ? AreaAdministrationQuery<int?>.Known(null)
                    : physical.Available ? Read<int?>(() => K(_current.ReadPersonIdentity(physical.Value!.Value), "map_person_identity"))
                    : AreaAdministrationQuery<int?>.Unknown(physical.UnresolvedInput!);
                AreaAdministrationQuery<OfficerPortraitRoute?> portrait;
                if (!personValid.Available) portrait = AreaAdministrationQuery<OfficerPortraitRoute?>.Unknown(personValid.UnresolvedInput!);
                else if (personValid.Value == false) portrait = AreaAdministrationQuery<OfficerPortraitRoute?>.Known(null);
                else
                {
                    portrait = Read<OfficerPortraitRoute?>(() => _officerCardSource.ReadMapPortrait(physical.Value!.Value));
                    if (portrait.Value is { Kind: OfficerPortraitRouteKind.Unresolved } unresolved)
                        portrait = AreaAdministrationQuery<OfficerPortraitRoute?>.Unknown(unresolved.UnresolvedInput ?? "area-draft.map_portrait");
                }
                var force = Read<int?>(() => K(ParentAreaForce(area).PhysicalSlot, "map_force_identity"));
                var color = Read<AreaMarkerColor?>(() => MapSpritePalettes.ForceFill(
                    K(_current.ReadForceReportColor(ParentAreaForce(area)), "map_force_color49")));
                var rawFocus = Read<byte?>(() => Focus(area));
                var focus = rawFocus.Available
                    ? Read<AreaDevelopmentFocus?>(() => AreaAdministration.DecodeFocus(rawFocus.Value!.Value))
                    : AreaAdministrationQuery<AreaDevelopmentFocus?>.Unknown(rawFocus.UnresolvedInput!);
                var development = new List<AreaAdministrationMapDevelopment>(3);
                foreach (var kind in new[] { AreaDevelopmentFocus.Commerce, AreaDevelopmentFocus.Agriculture, AreaDevelopmentFocus.Barracks })
                {
                    var progress = Read<int?>(() =>
                    {
                        var source = _state.Areas.GetValueOrDefault(area) ?? throw Missing("map_area_owner");
                        int value = Progress(source, kind) ?? throw Missing("map_development_progress");
                        if (value is < 0 or > ushort.MaxValue) throw Missing("map_development_progress");
                        return value;
                    });
                    var complete = progress.Available ? AreaAdministrationQuery<bool?>.Known(progress.Value >= 1000)
                        : AreaAdministrationQuery<bool?>.Unknown(progress.UnresolvedInput!);
                    AreaAdministrationQuery<bool?> highlighted;
                    if (complete.Available && complete.Value == true || personValid.Available && personValid.Value == false)
                        highlighted = AreaAdministrationQuery<bool?>.Known(false);
                    else if (!personValid.Available) highlighted = AreaAdministrationQuery<bool?>.Unknown(personValid.UnresolvedInput!);
                    else if (!rawFocus.Available) highlighted = AreaAdministrationQuery<bool?>.Unknown(rawFocus.UnresolvedInput!);
                    else if (rawFocus.Value != EncodeFocus(kind)) highlighted = AreaAdministrationQuery<bool?>.Known(false);
                    else if (!complete.Available) highlighted = AreaAdministrationQuery<bool?>.Unknown(complete.UnresolvedInput!);
                    else highlighted = AreaAdministrationQuery<bool?>.Known(true);
                    development.Add(new(kind, progress, highlighted, complete));
                }
                rows.Add(new(area, valid, name, hex, identity, personValid, portrait, force, color, rawFocus, focus, development.AsReadOnly()));
            }
            _mapPresentation = new(Revision, rows.AsReadOnly());
            return AreaAdministrationQuery<AreaAdministrationMapPresentation>.Known(_mapPresentation);
        }
        catch (InvalidDataException unknown)
        { return AreaAdministrationQuery<AreaAdministrationMapPresentation>.Unknown(unknown.Message); }

        static AreaAdministrationQuery<T> Read<T>(Func<T> read)
        {
            try { return AreaAdministrationQuery<T>.Known(read()); }
            catch (InvalidDataException unknown) { return AreaAdministrationQuery<T>.Unknown(unknown.Message); }
        }
    }
}
