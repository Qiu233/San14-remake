using System.Collections.ObjectModel;
using San14.Core;

namespace San14.Data;

public sealed partial class GameResourceRepository
{
    private ApplicationSpecialPersonAvailability? _specialPersonCapabilities;

    /// <summary>Bind the metadata writer's two person capability bits separately
    /// from profile unlocks and from the definition loader's subscribed-app gate.</summary>
    public CollectionOfficerAdmission LoadCollectionOfficerAdmission(CollectionOfficerAdmission? application=null)
    {
        var metadata=LoadPortraitContent();
        var first=new Dictionary<int,bool?>();var second=new Dictionary<int,bool?>();
        foreach(int id in Enumerable.Range(5101,100).Concat(Enumerable.Range(2078,5)))
        {first.Add(id,false);second.Add(id,false);}
        foreach(var file in metadata.Files)
            foreach(var record in file.Records)
            {
                if(record.Type!=2||!first.ContainsKey(record.Id))continue;
                first[record.Id]=Union(first[record.Id],file.Admitted);
                if(record.WritesSecondaryBitmap)second[record.Id]=Union(second[record.Id],file.Admitted);
            }
        var content=first.ToDictionary(pair=>pair.Key,pair=>new CollectionOfficerContentAvailability(pair.Value,second[pair.Key]));
        var subscribed=metadata.Platform.Where(pair=>pair.Key>0&&pair.Key<=int.MaxValue).ToDictionary(pair=>(int)pair.Key,pair=>pair.Value.Owned);
        var installed=metadata.Platform.Where(pair=>pair.Key>0&&pair.Key<=int.MaxValue).ToDictionary(pair=>(int)pair.Key,pair=>pair.Value.Installed);
        application??=new(new Dictionary<int,bool>());
        //Explicit application facts retain their owner; the usual profile-only
        //input supplies just AllowedOfficerIds, leaving metadata facts here.
        foreach(var pair in application.Content)content[pair.Key]=pair.Value;
        foreach(var pair in application.SubscribedDlcAppIds)subscribed[pair.Key]=pair.Value;
        foreach(var pair in application.InstalledDlcAppIds)installed[pair.Key]=pair.Value;
        return application with
        {
            Content=new ReadOnlyDictionary<int,CollectionOfficerContentAvailability>(content),
            SubscribedDlcAppIds=new ReadOnlyDictionary<int,bool?>(subscribed),
            InstalledDlcAppIds=new ReadOnlyDictionary<int,bool?>(installed)
        };
        static bool? Union(bool? previous,bool? next)=>previous==true||next==true?true:previous is null||next is null?null:false;
    }

    /// <summary>The independent original-person directory, preserving all100
    /// physical slots. Absent files retain the real zeroed reset definition.</summary>
    public ApplicationSpecialPersonAvailability LoadApplicationSpecialPersonCapabilities()
    {
        if(_specialPersonCapabilities is not null)return _specialPersonCapabilities;
        var admission=LoadCollectionOfficerAdmission();
        var directory=ReadCollectionSpecialOfficerDirectory(admission);
        var people=new ApplicationSpecialPersonDefinition[100];bool catalogKnown=true;
        foreach(var slot in directory)
        {
            if(slot.Person is {} definition)
            {
                //The original loads this positive-App payload only after the
                //subscribed-app callback. Presence is not a substitute fact.
                if(slot.AppId>0&&slot.Subscribed is null)catalogKnown=false;
                people[slot.Slot]=new(definition.Id,Array.AsReadOnly((ushort[])definition.Traits.Clone()),
                    Array.AsReadOnly((byte[])definition.Tactics.Clone()));
            }
            else people[slot.Slot]=new(0,Array.AsReadOnly(new ushort[9]),Array.AsReadOnly(new byte[10]));
        }
        return _specialPersonCapabilities=new(
            new ReadOnlyDictionary<int,bool?>(admission.Content.ToDictionary(pair=>pair.Key,pair=>pair.Value.Owned)),
            catalogKnown?Array.AsReadOnly(people):null);
    }
}
