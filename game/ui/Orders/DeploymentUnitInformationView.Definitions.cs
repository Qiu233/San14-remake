using Godot;

internal partial class DeploymentUnitInformationView
{
    private UiSourceFunctionButton formationDefinition = null!, tacticDefinition = null!;
    private Action? openFormationDefinition, openTacticDefinition;

    // The original fnc mounts own their common function artwork and child
    // icon. The parent must not also draw the disabled Art11/77/113 copies.
    private void ConfigureDefinitionButtons()
    {
        formationDefinition = Create("DeploymentFormationDefinition", 75, () => openFormationDefinition?.Invoke());
        tacticDefinition = Create("DeploymentTacticDefinition", 111, () => openTacticDefinition?.Invoke());
        UiSourceFunctionButton Create(string name, int node, Action pressed)
        {
            var button = new UiSourceFunctionButton { Name = name, Position = source.Rectangle(node).Position,
                Disabled = true, InputAdmission = () => !abandoned && IsVisibleInTree() && CanProcess() };
            AddChild(button);
            button.Configure(repository, assets, shader, "000_cmn_icon_info", pressed);
            return button;
        }
    }

    internal void ApplyDefinitionButtons(Action? formation, Action? tactic)
    {
        if (abandoned) return;
        openFormationDefinition = formation; openTacticDefinition = tactic;
        formationDefinition.Disabled = formation is null; tacticDefinition.Disabled = tactic is null;
    }

    private void AbandonDefinitionButtons()
    {
        openFormationDefinition = null; openTacticDefinition = null;
        if (formationDefinition is not null) { formationDefinition.InputAdmission = null; formationDefinition.Disabled = true; }
        if (tacticDefinition is not null) { tacticDefinition.InputAdmission = null; tacticDefinition.Disabled = true; }
    }
}
