using Godot;
using San14.Data;
using System.Globalization;

/// <summary>The authored Base name or shared number child; neither clip
/// completion acknowledges an ordinary effect manager node.</summary>
internal partial class UnitActionTacticNameView : UiSourceAnimationView
{
    private readonly Dictionary<int, UiSourceText> labels = [];
    private MapFont font = null!;
    private bool number;
    internal void Configure(GameResourceRepository repository, MapFont mapFont, bool numbers = false)
    {
        font = mapFont; number = numbers;
        var source = numbers ? new UiSourceLayout(MapMarkerLayout.LoadNamed(repository,"00_81_09_senpo_num_offset_00"))
            : new UiSourceLayout(repository, 0x47FB32BE);
        ConfigureAnimation(source, new HashSet<string> { numbers ? "senpo_num" : "2_senpou" },
            numbers ? new[] { 0, 1, 2 } : new[] { 11, 12, 13 });
        foreach (int index in numbers ? new[] { 1, 2 } : new[] { 12, 13 })
        {
            var node = source.Nodes[index];
            var label = new UiSourceText { Name="NameText",Size=new(node.Value44,node.Value46),MouseFilter=MouseFilterEnum.Ignore };
            AnimationPanes[index].AddChild(label); labels.Add(index,label);
        }
    }
    internal void StartName(string name,int preset)
    {
        var palette=MapTextPresets.Create()[preset];
        foreach(var (index,label) in labels)
        {
            label.Configure(font,MapFontProfile.FromNode(AnimationLayout.Nodes[index],AnimationLayout.Source.Source.ResourceUri).WithPreset(palette));
            label.Text=name;
        }
        PlayAnimation(number ? "senpo_num" : "2_senpou");
    }
    internal bool TryStartNumber(uint amount,int preset)
    {
        int signed=unchecked((int)amount);
        if(!number || IsPlaying || signed<=0)return false;
        string digits=signed.ToString(CultureInfo.InvariantCulture);
        StartName(new string(digits.Select(ch=>(char)('\uFF10'+ch-'0')).ToArray()),preset);return true;
    }
}
