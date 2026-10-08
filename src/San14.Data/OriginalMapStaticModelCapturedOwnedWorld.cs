using System.Collections.ObjectModel;
using San14.Core;

namespace San14.Data;

/// <summary>Current static-model fields from an already detached owned render
/// capture. It keeps complete current records and copies their small lookup
/// dictionaries; the campaign's terrain/officer/event data is not a Model
/// input. This is not a reusable current population or visibility cache.</summary>
public sealed class OriginalMapStaticModelCapturedOwnedWorld
{
    private OriginalMapStaticModelCapturedOwnedWorld(GameState world, string provenance)
    {
        MapWidth=world.MapWidth;MapHeight=world.MapHeight;SourceProvenance=provenance;
        Forces=new ReadOnlyDictionary<int,ForceState>(new Dictionary<int,ForceState>(world.Forces));
        Cities=new ReadOnlyDictionary<int,CityState>(new Dictionary<int,CityState>(world.Cities));
        Gates=new ReadOnlyDictionary<int,GateState>(new Dictionary<int,GateState>(world.Gates));
        Structures=new ReadOnlyDictionary<int,StructureState>(new Dictionary<int,StructureState>(world.Structures??
            throw new InvalidDataException("Current owned facility population is unresolved.")));
    }
    public int MapWidth { get; }
    public int MapHeight { get; }
    public string SourceProvenance { get; }
    public IReadOnlyDictionary<int,ForceState> Forces { get; }
    public IReadOnlyDictionary<int,CityState> Cities { get; }
    public IReadOnlyDictionary<int,GateState> Gates { get; }
    public IReadOnlyDictionary<int,StructureState> Structures { get; }

    /// <summary>The caller owns this previously detached capture throughout
    /// preparation. The method does not take a live campaign across an await.</summary>
    public static OriginalMapStaticModelCapturedOwnedWorld FromCapturedOwnedWorld(GameState capturedOwnedWorld,
        string captureProvenance)
    {
        ArgumentNullException.ThrowIfNull(capturedOwnedWorld);
        ArgumentException.ThrowIfNullOrWhiteSpace(captureProvenance);
        return new(capturedOwnedWorld,captureProvenance);
    }
}
