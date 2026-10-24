using Godot;

public partial class MapSceneView
{
    internal void BeginFalseReportMapDisplay(IEnumerable<int> physicalHexes)
    {
        if (!IsPlayableMapReady || _commandMapDisplay is { Present: true })
            throw new InvalidOperationException("False-report display requires an available map owner.");
        var data = D3D11CommandMapData.CaptureCandidateHexes(physicalHexes);
        ulong revision = checked(++_commandMapRevision);
        _commandMapDisplay = D3D11CommandMapPublication.ForFixedCandidates(data,
            revision, revision, checked(++_commandMapProviderGeneration));
        ScheduleD3D11MapFrame();
    }

    internal void EndFalseReportMapDisplay()
    {
        if (_commandMapDisplay?.Data is not { Kind: D3D11CommandMapDisplayKind.FixedCandidateGray }) return;
        _commandMapDisplay = D3D11CommandMapPublication.ForFixedCandidates(null,
            checked(++_commandMapRevision), 0, checked(++_commandMapProviderGeneration));
        ScheduleD3D11MapFrame();
    }
}
