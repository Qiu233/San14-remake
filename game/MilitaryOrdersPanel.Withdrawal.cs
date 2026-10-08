using Godot;
using San14.Core;

public partial class MilitaryOrdersPanel
{
    private readonly Button _withdrawSelection = new() { Name = "MilitaryWithdrawSelection" };

    private void AddWithdrawalControls()
    {
        AddChild(_withdrawSelection);
        _withdrawSelection.Pressed += () => RequestWithdrawal();
    }

    private void ConfigureWithdrawal()
    {
        if (_text is null) return;
        _withdrawSelection.Text = _text("play_military_withdraw_selection");
        _withdrawSelection.TooltipText = _text("play_military_withdraw_selection_hint");
    }

    /// <summary>The normal military selection controls both single and grouped
    /// withdrawals. No representative unit supplies the other armies' home.</summary>
    public bool RequestWithdrawal()
    {
        int[] units = CommandUnitIdsSnapshot();
        if (!_strategy || _forceId is not int force || units.Length == 0 || _issue is null)
            return false;
        _issue(units.Length == 1
            ? new WithdrawUnitOrder(force, units[0])
            : new WithdrawUnitsOrder(force, units));
        return true;
    }

    private void UpdateWithdrawalEnabled()
    {
        _withdrawSelection.Disabled = !_strategy || CommandUnitIdsSnapshot().Length == 0;
    }
}
