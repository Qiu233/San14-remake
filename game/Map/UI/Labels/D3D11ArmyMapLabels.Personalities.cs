using Godot;
using San14.Core;

internal partial class D3D11ArmyMapLabels
{
    internal void RefreshDeparturePersonalities(int unitId,IReadOnlyList<ArmyDeparturePersonalitySnapshot> source)
    {
        if(!_cards.TryGetValue(unitId,out var card))
            throw new InvalidDataException("army-personalities.source_card");
        card.RefreshPersonalities(source);
    }

    internal partial class ArmyMapCard
    {
        private Control? _personalityView;

        internal void RefreshPersonalities(IReadOnlyList<ArmyDeparturePersonalitySnapshot> source)
        {
            var next=new Control {Name="Personalities",MouseFilter=Control.MouseFilterEnum.Ignore};
            AddChild(next);
            try
            {
                var layout=new UiSourceLayout(new San14.Data.LoadedLayout(_owner._source.Normal.Layout,_owner._source.Normal.Source));
                int displayed=0;
                foreach(var trait in source.Where(value=>value.Valid&&value.Eligible).Take(5))
                {
                    if(trait.Active is not bool active||trait.Style is not byte style)
                        throw new InvalidDataException(trait.Missing??"army-personalities.source_state");
                    string kind=style==2?"debuff":style==3?"unique":"buff";
                    int node=0x3C-displayed*2;
                    layout.Art(next,node,layout.Rectangle(node),_owner._assets,_owner._speechShader,
                        commonSpriteOverride:"000_cmn_prt_sign_unit_kosei_"+kind+"_off");
                    var overlay=layout.Art(next,node+1,layout.Rectangle(node+1),_owner._assets,_owner._speechShader,
                        commonSpriteOverride:"000_cmn_prt_sign_unit_kosei_"+kind+"_on");
                    overlay.Visible=active;
                    displayed++;
                }
                var previous=_personalityView;_personalityView=next;
                if(previous is not null){RemoveChild(previous);previous.QueueFree();}
                UpdatePersonalityVisibility();
            }
            catch{RemoveChild(next);next.QueueFree();throw;}
        }

        private void UpdatePersonalityVisibility()
        {
            if(_personalityView is not {} view)return;
            view.Visible=!IsGroup&&!IsSimple;
            view.Modulate=Alpha(Colors.White,Presentation.DetailsAlpha);
        }
    }
}
