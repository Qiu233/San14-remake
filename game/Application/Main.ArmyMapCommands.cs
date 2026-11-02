using San14.Core;
using San14.Data;

public partial class Main
{
    private void BindOrdinaryArmyMapCommands(MapSceneView map)
    {
        map.SetOrdinaryArmyMapEntryHandler((world, hex, level, groups) =>
        {
            bool opened = false;
            RunUiAction(() =>
            {
                if (_residentMap != map || _session is not { HasPendingOperation: false } session ||
                    _campaignComposition is not { } composition || !ReferenceEquals(_world, world) ||
                    !ReferenceEquals(session.World, world) || !TutorialEntryAllowed || !GameplayMapKeyboardAdmitted()) return;
                var entry = composition.PrepareArmyQueries(world).QueryOrdinaryArmyMapEntry(world, hex, level, groups);
                if (entry.CanOpen != true)
                {
                    if (entry.UnresolvedInput is { } missing) map.SetMeta("ordinary_army_entry_unknown", missing);
                    else map.RemoveMeta("ordinary_army_entry_unknown");
                    return;
                }
                map.RemoveMeta("ordinary_army_entry_unknown");
                SelectMapCommandUnit(entry.Rows[0].Army.UnitId, entry);
                opened = _unitOrderTarget is not null && ReferenceEquals(_unitOrderWorld, world) && _unitOrderMap == map;
            });
            return opened;
        });
    }
}
