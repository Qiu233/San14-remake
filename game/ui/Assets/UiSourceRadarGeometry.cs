using Godot;
using San14.Data.Formats;

/// <summary>The five-axis source writer changes only the outer mesh positions;
/// its authored centre and indexed triangles remain intact.</summary>
internal static class UiSourceRadarGeometry
{
    internal static Vector2[] Positions(UiSourceLayout source,int index,IReadOnlyList<int> values,
        IReadOnlyList<int> maxima,Vector2 mount=default)
    {
        if(values.Count!=5 || maxima.Count!=5)
            throw new ArgumentException("A source radar requires five current values and five maxima.");
        var mesh=LcskPolygonNodeReader.Read(source.Source.Layout,source.Nodes[index]);
        if(mesh.Vertices.Count!=6)throw new InvalidDataException("The source radar does not have six vertices.");
        int[] axes=[0,2,3,5,4];
        var positions=mesh.Vertices.Select(v=>new Vector2(v.X,v.Y)).ToArray();
        Vector2 centre=positions[1];
        for(int axis=0;axis<axes.Length;axis++)
        {
            int maximum=maxima[axis];
            if(maximum==0)throw new ArgumentException("A populated radar requires nonzero maxima.",nameof(maxima));
            int value=values[axis];
            if(maximum<value)value=maximum;
            int remaining=unchecked(maximum-value);
            Vector2 end=positions[axes[axis]];
            positions[axes[axis]]=new(((float)value*end.X+(float)remaining*centre.X)/(float)maximum,
                ((float)value*end.Y+(float)remaining*centre.Y)/(float)maximum);
        }
        return positions.Select(point=>source.PolygonPoint(index,point,mount)).ToArray();
    }
}
