using Godot;
using San14.Data;

internal partial class DeploymentTargetView
{
    private GeneralUnitTargetHeaderView? generalHeaderView;

    internal void ConfigureGeneral(GameResourceRepository repository, PreviewText text,
        GeneralUnitTargetHeader header, IReadOnlyList<DeploymentTargetGroup> groups,
        IEnumerable<int> selectedOfficers, int scrollSpeedSetting,
        Action<IReadOnlyList<int>> selectionChanged, Action chooseFacility, Action requestedBack,
        Func<int>? currentScrollSpeed = null, AdministrationPageChrome.Resources? resources = null)
        => Configure(repository, text, null, header.TotalTroops, header.CommandBooks, groups,
            selectedOfficers, scrollSpeedSetting, selectionChanged, chooseFacility, requestedBack,
            currentScrollSpeed, generalHeader: header, resources: resources);

    private void ConfigureGeneralHeader(GameResourceRepository repository, PreviewText text, GeneralUnitTargetHeader header)
    {
        generalHeaderView = new() { Name = "GeneralDispatchHeader" };
        canvas.AddChild(generalHeaderView);
        generalHeaderView.Configure(repository, text, header, SourceResources);
    }

    internal void ApplyGeneralHeader(GeneralUnitTargetHeader header) => generalHeaderView?.Apply(header);
}
