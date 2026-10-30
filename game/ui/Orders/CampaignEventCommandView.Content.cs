using Godot;
using San14.Data;

internal partial class CampaignEventCommandView
{
    private void PublishContent()
    {
        foreach(Node child in contentHost.GetChildren()){contentHost.RemoveChild(child);child.QueueFree();}
        contentHost.RemoveMeta("unresolved_owner");
        if(contentScroll is not null){contentScroll.Hide();contentScroll.QueueFree();contentScroll=null;}
        for(int i=0;i<detailTabs.Count;i++)detailTabs[i].Selected=i==detailTab;
        SetMeta("detail_tab",detailTab);if(selected is null)return;
        var rowSource=detailSource;
        var contentRows=new Control{Name="CampaignEventDescriptionRows",Size=contentHost.Size,MouseFilter=MouseFilterEnum.Ignore};contentHost.AddChild(contentRows);
        int count=0;
        if(detailTab==0)
        {
            var conditions=selected.Conditions;
            if(conditions is not null)
            {
                foreach(var line in conditions.Lines)
                {
                    var row=new UiSourceEventConditionRow{Name="CampaignEventCondition"+line.OperationIndex,Position=new(0,count++*ConditionPitch)};
                    contentRows.AddChild(row);row.Configure(rowSource,assets,shader,font,line.Text,line.IndentPixels,line.ShowBox,line.Passed==true,line.TextPreset);
                    row.SetMeta("passed_known",line.Passed.HasValue);
                }
                if(conditions.UnresolvedOwners.Count!=0)
                {contentHost.SetMeta("unresolved_owner",string.Join(";",conditions.UnresolvedOwners));Unknown();}
            }
            else{contentHost.SetMeta("unresolved_owner","campaign_event.conditions."+selected.EventId);Unknown();}
        }
        else foreach(string value in selected.ResultLines)
        {
            var label=rowSource.Text(contentRows,22,new(0,count++*ConditionPitch),"",font,MapTextPresets.Create()[0]);label.SetKnownColourLine(value);
        }
        var box=source.Rectangle(106);contentScroll=new(){Name="CampaignEventContentScrollbar",Position=box.Position,Size=box.Size};canvas.AddChild(contentScroll);
        contentScroll.Configure(repository,assets,shader,Math.Max(0,count*ConditionPitch-(int)contentHost.Size.Y),scrollSpeed(),
            value=>contentRows.Position=new(0,-value),currentScrollSpeed:scrollSpeed);
        contentScroll.ProcessMode=busy?ProcessModeEnum.Disabled:ProcessModeEnum.Inherit;
        void Unknown()=>rowSource.Text(contentRows,22,new(0,count++*ConditionPitch),text.Get("detail_unknown"),font,MapTextPresets.Create()[0]);
    }
    private void PublishPeople()
    {
        foreach(Node child in peopleHost.GetChildren()){peopleHost.RemoveChild(child);child.QueueFree();}
        peopleHost.RemoveMeta("unresolved_owner");
        if(selected is null)return;
        var strip=detailSource;
        int slot=0;
        foreach(int id in selected.RelatedPersonIds.Where(id=>id is>=1 and<=6000).Take(4))
        {
            var person=personProvider(id);
            if(person is null)
            {
                peopleHost.SetMeta("unresolved_owner","campaign_event.person.publication."+id);
                continue;
            }
            var mount=CampaignEventCommandPresentation.PersonSlots[slot];
            var box=strip.Rectangle(mount.PortraitNode);
            var host=new Control{Name="CampaignEventPerson"+slot++,Position=box.Position,Size=box.Size,MouseFilter=MouseFilterEnum.Ignore};peopleHost.AddChild(host);
            host.SetMeta("person_id",id);host.SetMeta("person_name",person.Name);
            if(person.Portrait is{} portrait)
            {
                var face=new UiSourcePortrait{Name="Face04",Size=box.Size};host.AddChild(face);
                face.Configure(repository,assets,shader,portrait,personFaceSource);
            }
            else host.SetMeta("unresolved_owner","campaign_event.person.portrait."+id);
            strip.Art(peopleHost,mount.FrameNode,strip.Rectangle(mount.FrameNode),assets,shader);
            strip.Text(peopleHost,mount.NameNode,default,person.Name,font);
        }
    }
}
