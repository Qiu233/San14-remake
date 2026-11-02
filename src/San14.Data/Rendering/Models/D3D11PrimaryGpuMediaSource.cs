using System.Globalization;
using System.Text;

namespace San14.Data;

/// <summary>Camera-free Primary GPU import declaration, published with its CPU
/// generation. The application device loads it before admitting Title; openings
/// carry only its identity and borrow the resident immutable GPU objects.</summary>
public sealed class D3D11PrimaryGpuMediaSource
{
    public string LogicalKey { get; }
    public string Identity { get; }
    public string Directory { get; }
    private readonly IReadOnlyDictionary<string,D3D11PrimaryForwardCpuResources> forward;
    private D3D11PrimaryGpuMediaSource(string key,string identity,string directory,
        IReadOnlyDictionary<string,D3D11PrimaryForwardCpuResources> forward)
        =>(LogicalKey,Identity,Directory,this.forward)=(key,identity,directory,forward);
    public D3D11PrimaryForwardCpuResources RequireForward(string locale)=>forward.TryGetValue(locale,out var source)?source:
        throw new InvalidDataException("Forward static CPU source was not prepared for locale "+locale+".");

    public static D3D11PrimaryGpuMediaSource Prepare(GameResourceRepository repository, D3D11CityGateStaticResources models,
        string logicalKey,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(models);
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalKey);
        string identity=Guid.NewGuid().ToString("N");
        var draws=models.Assets.SelectMany(asset=>asset.Draws).OrderBy(draw=>draw.Template).ToArray();
        using var writer=new StreamWriter(Path.Combine(models.Directory,"primary-gpu-media.txt"),false,new UTF8Encoding(false));
        writer.WriteLine("SAN14_PRIMARY_GPU_MEDIA 2");writer.WriteLine(logicalKey);writer.WriteLine(identity);
        writer.WriteLine(draws.Length.ToString(CultureInfo.InvariantCulture));
        for(int index=0;index<draws.Length;index++)
        {
            token.ThrowIfCancellationRequested();
            var draw=draws[index];
            if(draw.Template!=index)throw new InvalidDataException("Primary static template order is incomplete.");
            writer.WriteLine(FormattableString.Invariant($"{index} {draw.ShaderFile} {draw.VertexIndex} {draw.PixelIndex} {draw.RelativeDirectory}"));
            writer.Write(draw.DrawMetadata);
        }
        var forward=new Dictionary<string,D3D11PrimaryForwardCpuResources>(StringComparer.Ordinal);
        writer.WriteLine("2");
        foreach(string locale in new[]{"TC","SC"})
        {
            token.ThrowIfCancellationRequested();
            string relative="passes/"+locale+"/forward";
            forward.Add(locale,D3D11PrimaryPassStaticResources.PrepareForward(repository,models,locale,
                Path.Combine(models.Directory,relative),token));
            var shadow=models.RequireShadow(locale);
            writer.WriteLine(FormattableString.Invariant($"{locale} {relative} shadow/{locale} {shadow.Templates.Count}"));
        }
        return new(logicalKey,identity,models.Directory,forward);
    }

    public void WriteOpeningReference(string openingDirectory,string locale)
    {
        System.IO.Directory.CreateDirectory(openingDirectory);
        File.WriteAllText(Path.Combine(openingDirectory,"primary-gpu-media.ref"),
            $"SAN14_PRIMARY_GPU_REFERENCE 2\n{LogicalKey}\n{Identity}\n{locale}\n",new UTF8Encoding(false));
    }
}
