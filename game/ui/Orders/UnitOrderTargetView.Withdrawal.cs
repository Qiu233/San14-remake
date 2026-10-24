using Godot;
using San14.Data.Formats;

internal partial class UnitOrderTargetView
{
    private UiSourceSimpleTooltip? withdrawalTooltip;
    private string? withdrawalWarning;
    private bool hasAbnormalWithdrawalSelection;

    private void ConfigureSourceWithdrawalTooltip()
    {
        sourceWithdraw!.MouseEntered += RefreshWithdrawalTooltip;
        sourceWithdraw.MouseExited += HideWithdrawalTooltip;
        sourceWithdraw.Pressed += HideWithdrawalTooltip;
    }

    internal void ApplySourceWithdrawalEligibility(bool hasAbnormalSelection)
    {
        hasAbnormalWithdrawalSelection = hasAbnormalSelection;
        RefreshWithdrawalTooltip();
    }

    private bool WithdrawalTooltipAdmitted() => !abandoned && !busy && !facilitySelectionMode &&
        existingArmyMode && IsVisibleInTree() && hasAbnormalWithdrawalSelection &&
        sourceWithdraw is { } button && button.IsVisibleInTree() && button.IsHovered();

    private void RefreshWithdrawalTooltip()
    {
        if (!WithdrawalTooltipAdmitted()) { HideWithdrawalTooltip(); return; }
        if (withdrawalTooltip is null)
        {
            var tooltip = new UiSourceSimpleTooltip();
            chrome!.Canvas.AddChild(tooltip);
            tooltip.Configure(sourceRepository!, chrome.Assets, chrome.SpriteShader, chrome.Font);
            tooltip.Name = "SourceWithdrawalTooltip";
            // This source hint has no formatting arguments. Keep a missing
            // message expansion visible as unknown instead of inventing it.
            const int message = 0x569A;
            var record = sourceRepository!.LoadMessages(ui.SourceLocale, MessageKind.Game)
                .Messages.Sections[message / 10000].Records[message % 10000];
            withdrawalWarning = record.Literals.Count == 1 ? record.Literals[0].Text : ui.Get("detail_unknown");
            tooltip.SetMeta("source_message_id", message);
            tooltip.SetMeta("source_message_resolved", record.Literals.Count == 1);
            withdrawalTooltip = tooltip;
        }
        withdrawalTooltip.DisplayText(withdrawalWarning);
    }

    private void HideWithdrawalTooltip() => withdrawalTooltip?.Hide();

    public override void _Process(double delta)
    {
        if (withdrawalTooltip is not { Visible: true } tooltip) return;
        if (!WithdrawalTooltipAdmitted()) { tooltip.Hide(); return; }
        // The shown hint follows the pointer. No world query is performed.
        tooltip.UpdatePointer(chrome!.Canvas.GetLocalMousePosition());
    }
}
