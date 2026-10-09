using San14.Core;

namespace San14.Data;

public sealed partial class SourceWorldContext
{
    private readonly Dictionary<int, OfficerLocationState?> _officerLocations = [];
    private OfficerLocationSourceResidues _locationResidues = new([]);
    private ArmyReceiverSourceDirectory? _locationReceivers;

    internal void BindOfficerLocations(GameState state, ArmyReceiverSourceDirectory? receivers)
    {
        _officerLocations.Clear();
        foreach (var officer in state.Officers.Values)
            _officerLocations.Add(officer.Id, officer.LocationState);
        _locationResidues = OfficerLocationSourceResidues.From(state.ResourceState);
        _locationReceivers = receivers;
    }
    internal OfficerLocationState? ReadOfficerLocation(int officerId) => _officerLocations.GetValueOrDefault(officerId);
    internal OfficerLocationSourceResidue? ReadOfficerLocationResidue(int officerId) => _locationResidues.Find(officerId);
    private int? BoundLocationOfficer(int physical)
    {
        if (_officerSources.IdentityAt(physical) is not int id || _officerSources.ResolveOfficer(id) != physical ||
            !_officerLocations.ContainsKey(id)) return null;
        return id;
    }
    private void WriteBoundOfficerPosition(int id, ushort? code)
    {
        var old = _officerLocations[id] ?? new OfficerLocationState(null, null);
        var position = OfficerLocationSourceAdapter.DecodePosition(code, _locationReceivers);
        _officerLocations[id] = old with { Position = position };
        var residue = _locationResidues.Find(id);
        _locationResidues = _locationResidues.With(new(id,
            position?.Kind == OfficerPositionKind.Unresolved ? code : null, residue?.DistrictCode));
    }
    private void WriteBoundOfficerMembership(int id, byte? code)
    {
        var old = _officerLocations[id] ?? new OfficerLocationState(null, null);
        var membership = OfficerLocationSourceAdapter.Decode(null, code, _locationReceivers).Membership;
        _officerLocations[id] = old with { Membership = membership };
        var residue = _locationResidues.Find(id);
        _locationResidues = _locationResidues.With(new(id, residue?.LocationCode,
            membership?.Kind == OfficerDistrictMembershipKind.Unresolved ? code : null));
    }
    private void CopyOfficerLocationsTo(SourceWorldContext target)
    {
        target._officerLocations.Clear();
        foreach (var pair in _officerLocations) target._officerLocations.Add(pair.Key, pair.Value);
        target._locationResidues = _locationResidues;
        target._locationReceivers = _locationReceivers;
    }
}
