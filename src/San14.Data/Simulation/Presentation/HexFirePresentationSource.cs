using San14.Core;

namespace San14.Data;

public sealed record HexFirePresentationRow(int HexId,ushort RemainingDuration);
public sealed record HexFirePresentationPublication(IReadOnlyList<HexFirePresentationRow> Fires);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>One map publication for the owned fire-duration root. The
    /// caller caches it by that root, never invokes this scan from redraw,
    /// and retains the previous publication when an input is unresolved.</summary>
    public AreaAdministrationQuery<HexFirePresentationPublication> QueryHexFirePresentation(GameState state)
    {
        try
        {
            var owner=state.NativeBaseCaptureCurrent??throw HexFirePresentationMissing("duration_owner");
            if(owner.HexWords16 is not {Length:48400})throw HexFirePresentationMissing("duration_table");
            var current=CaptureWritableTransportRouteCurrent(state);
            var fires=new List<HexFirePresentationRow>();
            for(int id=0;id<owner.HexWords16.Length;id++)
            {
                ushort remaining=owner.HexWords16[id]??throw HexFirePresentationMissing("duration_value");
                if(remaining==0)continue;
                var hex=current.ReadHexTableReference(id)??throw HexFirePresentationMissing("Hex_pointer");
                if(hex.PhysicalSlot is not int physical)continue;
                byte raw=current.ReadHexSurfaceCode(hex)??throw HexFirePresentationMissing("Hex_surface");
                int surface=(current.ReadSurfaceTypeTableReference(raw<=30?raw:0)
                    ??throw HexFirePresentationMissing("surface_pointer")).PhysicalSlot
                    ??throw HexFirePresentationMissing("surface_identity");
                //18AEB0 does not create the terrestrial loop on water.
                if((_terrain.Catalog.Types[surface].RawField2C&2)!=0)continue;
                fires.Add(new(physical,remaining));
            }
            return new(new(fires.AsReadOnly()),null);
        }
        catch(InvalidDataException error){return new(null,error.Message);}
    }
    private static InvalidDataException HexFirePresentationMissing(string field)=>new("Hex-fire.presentation."+field);
}
