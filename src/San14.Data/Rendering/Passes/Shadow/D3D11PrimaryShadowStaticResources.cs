using System.Globalization;
using System.Text;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>One locale's immutable ordinary primary caster programs and layouts.
/// Current World, camera, lights, admission and constants have no owner here.</summary>
public sealed class D3D11PrimaryShadowStaticResources
{
    public const string ReferenceSignature="SAN14_PRIMARY_SHADOW_MEDIA 1";
    private const byte SupportedDepthPixelFormat=0x4E;
    private const uint ShadowDepthProgramClass=5;
    public string Locale { get; }
    public string Directory { get; }
    public IReadOnlyList<D3D11PrimaryShadowStaticTemplate> Templates { get; }

    private D3D11PrimaryShadowStaticResources(string locale,string directory,
        List<D3D11PrimaryShadowStaticTemplate> templates)
        =>(Locale,Directory,Templates)=(locale,Path.GetFullPath(directory),templates.AsReadOnly());

    internal static D3D11PrimaryShadowStaticResources Prepare(GameResourceRepository repository,
        IReadOnlyList<D3D11PrimaryShadowTemplateSource> sources,IReadOnlyDictionary<string,byte[]> media,
        string locale,string directory,CancellationToken token)
    {
        var predecessor=DeferredEarlyPredecessorSource.Load(repository,locale);
        if(predecessor.ShadowDepthDeclaration.PixelFormat!=SupportedDepthPixelFormat)
            throw new NotSupportedException("Shadow startup supports its depth4E/class5 resource.");
        var queue=DeferredEarlyShadowQueueSource.Read(predecessor,WorldGeometryLateCpuSource.Load(repository,locale).Native);
        var selector=LoadedDeferredEarlyShadowSelectionSource.Load(repository,locale);
        var matrixSource=LoadedDeferredEarlyShadowVertexMatrixNativeSource.Load(repository);
        var templates=new List<D3D11PrimaryShadowStaticTemplate>();
        string root=Path.GetFullPath(directory);
        foreach(var source in sources)
        {
            token.ThrowIfCancellationRequested();
            if(source.BaseTable.Object.ClassId==0xAB9C4087)
                throw new NotSupportedException("Tree2 shadow requires its actual owned model material.");
            var matrix=source.Matrix??throw new NotSupportedException("Startup shadow needs this template's nonrigid source owner.");
            var renderer=queue.LoadRenderer(repository,queue.ReadFinalInterface(source.BaseTable));
            var state=selector.CreateOwnedRenderer(renderer);var frontend=selector.ConstructFreshFrontendFields();
            frontend.ApplyPrimitiveEntry(true);
            var program=state.PrepareCurrent(matrix,ShadowDepthProgramClass,frontend.Alpha1BE,frontend.RenderTarget0Blend1C0).ReadProgram(0);
            if(program.VertexProgram is not {} vertex||program.PixelProgram is not null)
                throw new NotSupportedException("Startup shadow template requires its separate alpha material owner.");
            var contract=DeferredEarlyShadowVertexMatrixSource.Import(matrixSource,renderer,vertex);
            if(contract.Skinned||contract.Cloth)
                throw new NotSupportedException("Startup shadow template requires its nonrigid owner.");
            var required=renderer.Shader.ReadSignatures(vertex).Parts.Single(part=>part.Kind==DxbcSignatureKind.Input).Parameters
                .Where(part=>part.RawSystemValue==0).Select(part=>(part.SemanticName.Text,part.SemanticIndex)).ToArray();
            var layout=new List<string>();
            using(var reader=new StringReader(Encoding.UTF8.GetString(media[source.LayoutPath])))
            {
                while(reader.ReadLine() is {} line)
                {
                    string[] words=line.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
                    if(required.Any(name=>name.Text==words[0]&&name.SemanticIndex==uint.Parse(words[1],CultureInfo.InvariantCulture)))
                        layout.Add(line);
                }
            }
            string output=Path.Combine(root,"template-"+source.Index.ToString(CultureInfo.InvariantCulture));
            System.IO.Directory.CreateDirectory(output);
            File.WriteAllBytes(Path.Combine(output,"vs.dxbc"),renderer.Shader.Container.GetProgramBytes(renderer.Shader.Source.Bytes,vertex).ToArray());
            File.WriteAllLines(Path.Combine(output,"layout.txt"),layout);
            templates.Add(new(source.Index,contract.ByteSize,contract.UsesLocalToProjection));
        }
        return new(locale,root,templates);
    }

    /// <summary>One bulk mapping keeps global opening template indices while
    /// borrowing local primary media. No per-template pointer files are made.</summary>
    internal void WriteOpeningReference(string gpuDirectory,int fieldTemplateCount)
    {
        System.IO.Directory.CreateDirectory(gpuDirectory);
        File.WriteAllText(Path.Combine(gpuDirectory,"primary-shadow-media.ref"),ReferenceSignature+"\n"+
            FormattableString.Invariant($"{fieldTemplateCount} {Templates.Count}\n")+Directory+"\n");
    }
}

public sealed record D3D11PrimaryShadowStaticTemplate(int Index,int Bytes,bool LocalToProjection);

/// <summary>Temporary camera-free inputs from the actual primary import.
/// The published bank keeps only media locators and source CB-layout metadata.</summary>
internal sealed record D3D11PrimaryShadowTemplateSource(int Index,NamedModelShader BaseTable,
    ModelMatrixBindingInput Matrix,string LayoutPath);
