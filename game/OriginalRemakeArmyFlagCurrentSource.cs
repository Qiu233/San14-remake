using System.Collections.ObjectModel;
using San14.Core;
using San14.Data;

public sealed record OriginalRemakeArmyFlagCurrentBinding(int UnitId,int ExplicitCommanderId12,
    int OwnedForceVariant,byte? RetainedSourceColor49,OriginalArmyFlagPaneNameSelection BannerName,
    OriginalArmyFlagPaneNameSelection? CarriedKind2Name,string? CarriedKind2Unavailable,string CallerSource);

/// <summary>Immutable logical-refresh inputs for the source-created flag
/// consumer. Core unit IDs are managed keys; native force selection executes
/// the commander/person/district/pointer route in an owned query receiver.</summary>
public sealed class OriginalRemakeArmyFlagCurrentFrame
{
    public IReadOnlyDictionary<int,OriginalRemakeArmyFlagCurrentBinding> Bindings {get;}
    public IReadOnlyDictionary<int,string> Unavailable {get;}
    public bool OriginalCurrentProcessStateVerified=>false;

    internal OriginalRemakeArmyFlagCurrentFrame(Dictionary<int,OriginalRemakeArmyFlagCurrentBinding> bindings,
        Dictionary<int,string> unavailable)
        =>(Bindings,Unavailable)=(new ReadOnlyDictionary<int,OriginalRemakeArmyFlagCurrentBinding>(bindings),
            new ReadOnlyDictionary<int,string>(unavailable));

    public OriginalRemakeArmyFlagMaterialInputs Select(OriginalRemakeArmyFlagActor actor)
    {
        if(!Bindings.TryGetValue(actor.UnitId,out var input))
            throw new InvalidDataException(Unavailable.GetValueOrDefault(actor.UnitId)??"Owned source flag commander/force inputs are unavailable.");
        var name=actor.Category switch
        {
            2=>input.BannerName,
            3=>input.CarriedKind2Name??throw new InvalidDataException(input.CarriedKind2Unavailable??"Source final-banner carried text is unavailable."),
            _=>throw new InvalidDataException("Source flag material input received an unsupported model category.")
        };
        string provenance=input.CallerSource+"; "+name.NativeSourceProvenance;
        var pane=new OriginalRemakeArmyFlagSourcePaneInputs(input.RetainedSourceColor49,name.SourceTextBeforeSingleUnitCopy,
            provenance,actor.Category==3){ExplicitOwnedFreshNulTextRoute=!name.HasGlyph};
        return new(input.OwnedForceVariant,pane,2,
            "owned fresh resource primary phase1; source D9E070 +B8 specularW2; no original current draw-state claim",
            true);
    }
}

/// <summary>Source fields for the playable fresh flag prefix. Force+48 and
/// Force+49 retain their immutable CE source values because this ruleset has
/// no writer for those fields. Current ruler, Person district118 and District
/// force10 come from the coherent owned campaign capture. No Core force name,
/// Core force identity shortcut or per-side-flag name is substituted.</summary>
public sealed class OriginalRemakeArmyFlagCurrentSource
{
    private readonly LoadedScenario _source;
    private readonly OriginalOwnedArmyCurrentDefinitions _definitions;
    private readonly LoadedOriginalArmyFlagPaneNames _names;

    public OriginalRemakeArmyFlagCurrentSource(OriginalGameRepository repository,LoadedScenario source,LoadedMap map)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(map);
        _source=source;
        _definitions=new(source,map,repository.LoadTerrainTypes(source.FixedDataSource.SourcePath),
            repository.LoadSkillDefinitions(source.FixedDataSource.SourcePath),repository.LoadPersonalities(source.FixedDataSource.SourcePath));
        _names=OriginalArmyFlagPaneNames.Load(source);
        // Name loading authenticates each complete CE row against its source
        // file. Color49's convenience projection must match that same row.
        foreach(var force in source.Snapshot.Forces)
        {
            byte[] row=Convert.FromHexString(force.RemainingBytesHex);
            if(row.Length!=327||force.RawReportColor49!=row[54])
                throw new InvalidDataException("Retained source flag color49 differs from its authenticated CE Force row56.");
        }
    }

    public static OriginalRemakeArmyFlagCurrentSource Load(OriginalGameRepository repository,GameState world,string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(world);
        var uri=GameResourceId.Parse(world.ScenarioKey);
        if(uri.Kind!="file"||locale is not("SC" or"TC"))
            throw new InvalidDataException("Source flag current inputs require an installed CE scenario and source locale.");
        var selected=repository.EffectiveScenarios.SingleOrDefault(s=>s.Locale==locale&&
            s.SourcePath.Equals(uri.Key,StringComparison.OrdinalIgnoreCase))??
            throw new InvalidDataException("Source flag scenario is not the current effective installed revision.");
        var source=repository.LoadScenario(selected);var map=repository.LoadMapForScenario(selected);
        if(source.Metadata.FormatMarker!=0x66||source.ScenarioSource.ResourceUri!=world.ScenarioKey||map.Source.Sha256!=world.MapSha256)
            throw new InvalidDataException("Source flag scenario/map profile differs from the current owned campaign.");
        return new(repository,source,map);
    }

    public OriginalRemakeArmyFlagCurrentFrame Prepare(GameState world,IEnumerable<int> explicitOwnedFlagUnitIds)
    {
        ArgumentNullException.ThrowIfNull(world);ArgumentNullException.ThrowIfNull(explicitOwnedFlagUnitIds);
        var native=_definitions.Capture(world)??throw new InvalidDataException("Source flag current definitions do not match the complete owned campaign source.");
        // The generic campaign capture keeps nullable ruler metadata unknown.
        // This fresh flag prefix explicitly retains Force51's source zero
        // header while that force remains inactive with no owned ruler. It
        // never converts an unknown active ruler or a nonzero source header.
        var lastSource=_source.Snapshot.Forces.Single(row=>row.Slot==51);
        if(lastSource.RulerSlot==0&&world.Forces.TryGetValue(51,out var currentLast)&&
            !currentLast.IsActive&&currentLast.RulerId is null)
            native.SetForceRecord(new(51,0));
        OriginalArmyFlagPaneNameSelection? carried=null;string? carriedUnavailable=null;
        try{carried=_names.ResolveCarriedKind2(native);}
        catch(InvalidDataException error){carriedUnavailable=error.Message;}
        var bindings=new Dictionary<int,OriginalRemakeArmyFlagCurrentBinding>();var unavailable=new Dictionary<int,string>();
        foreach(int id in explicitOwnedFlagUnitIds.Distinct().Order())
        {
            try
            {
                if(!world.Units.TryGetValue(id,out var unit)||(uint)unit.OfficerId>6000)
                    throw new InvalidDataException("Owned source flag unit has no complete source commander ID.");
                // An isolated owned query receiver supplies exactly the word
                // consumed by22C0A0. Its physical key1 is not Unit.Id and does
                // not assert a complete or current original ArmyUnit object.
                native.SetArmyUnitCommanderId(1,(ushort)unit.OfficerId);
                var force=native.ReadArmyUnitForceReference(new(1));
                int physical=native.ReadForcePhysicalSlot(force)??throw new InvalidDataException("Source selected force physical getter is unknown.");
                bool valid=native.ReadForceNativeValidity(force)??throw new InvalidDataException("Source selected force validity is unknown.");
                byte? color=valid?native.ReadForceReportColor(force)??throw new InvalidDataException("Retained source Force+49 is unknown."):null;
                var name=_names.Resolve(native,force);
                string provenance=_source.ScenarioSource.ResourceUri+"#"+_source.ScenarioSource.Sha256+
                    $"; owned Unit{id} commander12={unit.OfficerId}; 22C0A0/22BCB0 current Person118/District10/Forcepointer/physical={physical}; retained source Force49; fresh source-zero Force51 header retained only while inactive/no-ruler";
                bindings.Add(id,new(id,unit.OfficerId,physical,color,name,carried,carriedUnavailable,provenance));
            }
            catch(InvalidDataException error){unavailable.Add(id,error.Message);}
        }
        return new(bindings,unavailable);
    }
}
