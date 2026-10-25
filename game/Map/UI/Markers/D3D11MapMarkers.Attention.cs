using Godot;
using San14.Data;

internal partial class D3D11MapMarkers
{
    private sealed class CityAttention(Control root, Control merchant, Control food,
        Control foreignProvince, Control playerProvince)
    {
        internal Control Root { get; } = root;
        internal Control Merchant { get; } = merchant;
        internal Control Food { get; } = food;
        internal Control ForeignProvince { get; } = foreignProvince;
        internal Control PlayerProvince { get; } = playerProvince;
        internal bool Player, Gain, Loss, MerchantPredicate, MerchantFlag;
        internal int MerchantTick, Selected = -1, CarouselTick, EnabledCount;

        internal void Advance(bool merchantEnabled, bool foodWarning, int rate)
        {
            // 1ABE20 updates the cached merchant flag on the source logical
            // frame interval. Business predicates are published separately.
            if (!merchantEnabled) MerchantFlag = false;
            else if (MerchantTick < rate * 20 / 60) MerchantTick++;
            else { MerchantTick = 0; MerchantFlag = MerchantPredicate; }
            if (!Player)
            {
                Merchant.Hide(); Food.Hide(); PlayerProvince.Hide();
                ForeignProvince.Visible = Gain;
                return;
            }
            bool first = MerchantFlag, second = foodWarning, third = Loss;
            int count = (first ? 1 : 0) + (second ? 1 : 0) + (third ? 1 : 0);
            if (Selected < 0 || count != EnabledCount)
                Selected = first ? 0 : second ? 1 : third ? 2 : 3;
            EnabledCount = count;
            if (count > 1)
            {
                if (CarouselTick < (float)rate + rate) CarouselTick++;
                else
                {
                    CarouselTick = 0;
                    do { Selected = (Selected + 1) % 3; }
                    while (!(Selected == 0 ? first : Selected == 1 ? second : third));
                }
            }
            Merchant.Visible = Player && Selected == 0;
            Food.Visible = Player && Selected == 1;
            PlayerProvince.Visible = Player && Selected == 2;
            ForeignProvince.Visible = !Player && Gain;
        }
    }

    private readonly Dictionary<(Kind, int), CityAttention> cityAttention = [];
    private UiSourceLayout? cityAttentionLayout;
    private Shader? cityAttentionShader;
    private Vector2 cityAttentionMount;
    private Func<bool>? merchantAttentionEnabled;

    private void ConfigureCityAttention(GameResourceRepository repository)
    {
        cityAttentionLayout = new(MapMarkerLayout.LoadNamed(repository, "00_02_00_mein_offset_24"));
        cityAttentionMount = new UiSourceLayout(new LoadedLayout(_citySources!.Full.Layout,
            _citySources.Full.Source)).Rectangle(84).Position;
        cityAttentionShader = UiSourceSprite.CreateShader();
        SynchronizeCityAttention();
    }

    private void SynchronizeCityAttention()
    {
        if (cityAttentionLayout is not { } layout) return;
        foreach (var key in cityAttention.Keys.Where(key => !_markers.ContainsKey(key)).ToArray())
        {
            var view = cityAttention[key]; cityAttention.Remove(key);
            RemoveChild(view.Root); view.Root.QueueFree();
        }
        foreach (var (key, marker) in _markers)
        {
            if (marker.Kind == Kind.Area) continue;
            if (!cityAttention.TryGetValue(key, out var view))
            {
                var root = new Control { Name = $"BaseAttention{marker.Kind}{marker.Id}",
                    MouseFilter = MouseFilterEnum.Ignore, Visible = false };
                AddChild(root);
                Control Art(string? sprite = null, MapCornerColors? colors = null, bool expansion = false)
                {
                    var art = layout.Art(root, 1, layout.Rectangle(1), _assets!, cityAttentionShader!, colors,
                        commonSpriteOverride: sprite, expansionCommonSprite: expansion);
                    art.Visible = false;
                    return art;
                }
                // Both replacements belong to the source's registered PK
                // common bank; the food sprite keeps its authored bank zero.
                view = new(root, Art("000_cmn_icon_shounin_fuchi", _spritePalettes![11], true), Art(),
                    Art("000_cmn_icon_gadvantage_fuchi", _spritePalettes[11], true),
                    Art("000_cmn_icon_gadvantage_fuchi", _spritePalettes[12], true));
                cityAttention.Add(key, view);
            }
            view.Player = _playerForce is int player && marker.Force == player;
        }
        PositionCityAttention();
    }

    internal bool ApplyProvinceAttention(IReadOnlyList<CityProvinceAdvantageIndicators> rows, out string? missing)
    {
        foreach (var row in rows)
        {
            if (!cityAttention.ContainsKey((Kind.City, row.CityId)))
            { missing = "city.attention.actual_map_receiver"; return false; }
            if ((row.IsPlayerCity ? row.LossWarning : row.GainOpportunity) is null)
            { missing = row.UnresolvedOwner ?? "city.attention.province_predicate"; return false; }
        }
        foreach (var row in rows)
        {
            var view = cityAttention[(Kind.City, row.CityId)];
            view.Player = row.IsPlayerCity;
            view.Gain = row.GainOpportunity == true;
            view.Loss = row.LossWarning == true;
        }
        missing = null;
        return true;
    }

    internal bool ApplyMerchantAttention(IReadOnlyList<CityMerchantAttention> rows,
        Func<bool> enabled, out string? missing)
    {
        foreach (var row in rows)
        {
            if (!cityAttention.ContainsKey((Kind.City, row.CityId)))
            { missing = "city.attention.actual_map_receiver"; return false; }
            if (row.Attention is null)
            { missing = row.UnresolvedOwner ?? "city.attention.merchant_predicate"; return false; }
        }
        foreach (var row in rows) cityAttention[(Kind.City, row.CityId)].MerchantPredicate = row.Attention!.Value;
        merchantAttentionEnabled = enabled;
        missing = null;
        return true;
    }

    internal void AdvanceCityAttention(int framesPerSecond)
    {
        bool enabled = merchantAttentionEnabled?.Invoke() == true;
        foreach (var (key, view) in cityAttention)
            view.Advance(enabled, foodWarnings.ContainsKey(key), framesPerSecond);
        PositionCityAttention();
    }

    private void PositionCityAttention()
    {
        foreach (var (key, view) in cityAttention)
        {
            if (!_markers.TryGetValue(key, out var marker)) { view.Root.Hide(); continue; }
            view.Root.Visible = marker.OnScreen && marker.CityPresentation.Display != 2 &&
                (_baseInformationVisible || campaignMarkerHexes is not null) && CampaignMarkerVisible(marker);
            view.Root.Scale = Size / CanonicalSize;
            view.Root.Position = (marker.Screen + cityAttentionMount) * view.Root.Scale;
            view.Root.Modulate = new Color(1, 1, 1, marker.CityPresentation.PanelAlpha);
        }
    }
}
