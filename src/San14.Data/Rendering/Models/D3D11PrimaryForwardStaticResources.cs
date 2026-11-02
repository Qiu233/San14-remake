using System.Globalization;
using System.Text;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Resource-only Forward admission and vertex layouts. The application
/// retains this bank with its primary models; no camera constants are stored.</summary>
public sealed class D3D11PrimaryForwardStaticResources
{
    public IReadOnlyList<D3D11PrimaryForwardStaticTemplate> Templates { get; }

    private D3D11PrimaryForwardStaticResources(IEnumerable<D3D11PrimaryForwardStaticTemplate> templates)
        =>Templates=Array.AsReadOnly(templates.ToArray());

    internal static D3D11PrimaryForwardStaticResources Prepare(GameResourceRepository repository,
        IReadOnlyList<D3D11PrimaryForwardTemplateSource> sources,IReadOnlyDictionary<string,byte[]> media,
        CancellationToken token)
    {
        var shader=repository.LoadShaderContainer("system",0x8F6C9FB9);
        var signature=shader.ReadSignatures(0).Parts.Single(part=>part.Kind==DxbcSignatureKind.Input).Parameters
            .Where(value=>value.RawSystemValue==0).Select(value=>(value.SemanticName.Text,value.SemanticIndex)).ToArray();
        var templates=new SortedDictionary<int,D3D11PrimaryForwardStaticTemplate>();
        foreach(var source in sources)
        {
            token.ThrowIfCancellationRequested();
            var named=source.BaseTable;
            var state0=ModelShaderResources.ResolveType16Interface(named,0);
            if(ModelShaderResources.ResolveType16Interface(named,10).Provider is not null)
                throw new NotSupportedException("Forward state10 redirect requires its actual owner.");
            var selected=state0.Provider is {} provider?ModelShaderResources.LoadDeclaredG1S(repository,provider):null;
            var configuration=selected is null?null:selected.ReadConfigurations();
            bool eligible=selected is not null&&!selected.Container.NamePrefix.StartsWith("CNS-",StringComparison.Ordinal)&&
                configuration!.Records.Any(value=>value.Name is "AlphaTest" or "Dissolve");
            if(!eligible)
            {
                templates.Add(source.Index,new(source.Index,false,Array.Empty<string>()));
                continue;
            }
            if(named.Object.ObjectId!=0x2F9960EF||selected!.Source.FileId!=shader.Source.FileId)
                throw new NotSupportedException("Forward template selects another state0 shader family.");
            if(configuration!.Records.Any(value=>value.Name=="LightMap"))
                throw new NotSupportedException("Forward LightMap requires its current primitive Pattern-skip flag producer.");
            var matrix=source.Matrix??throw new NotSupportedException("Forward template needs its nonrigid owner.");
            if(ModelMatrixBindingSourceReader.Select(shader,matrix).Selection.RowIndex!=0)
                throw new NotSupportedException("Forward template needs its actual nonzero matrix program.");
            var layout=new List<string>();
            using(var reader=new StringReader(Encoding.UTF8.GetString(media[source.LayoutPath])))
            {
                while(reader.ReadLine() is {} line)
                {
                    string[] values=line.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
                    if(signature.Any(value=>value.Text==values[0]&&value.SemanticIndex==uint.Parse(values[1],CultureInfo.InvariantCulture)))
                        layout.Add(line);
                }
            }
            if(layout.Count!=signature.Length)
                throw new InvalidDataException("Forward source vertex layout is incomplete.");
            templates.Add(source.Index,new(source.Index,true,layout.AsReadOnly()));
        }
        if(templates.Count!=sources.Count||templates.Keys.Where((value,index)=>value!=index).Any())
            throw new InvalidDataException("Forward bank template bindings are incomplete.");
        return new(templates.Values);
    }
}

public sealed record D3D11PrimaryForwardStaticTemplate(int Index,bool Eligible,IReadOnlyList<string> Layout);

/// <summary>Temporary preparation inputs borrowed from the primary import.
/// The published Forward bank retains only eligibility and filtered layout.</summary>
internal sealed record D3D11PrimaryForwardTemplateSource(int Index,NamedModelShader BaseTable,
    ModelMatrixBindingInput Matrix,string LayoutPath);
