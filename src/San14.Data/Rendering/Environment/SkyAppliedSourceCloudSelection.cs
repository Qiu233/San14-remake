using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Fresh source Cloud requested variant cache, followed by the
/// actual D77B10 and11971F0 edits. The first complete source variant row is
/// copied by981F00; untouched axes are retained source bytes, not zero fill.
/// This supplies program selection, not a current CB or GPU readiness.</summary>
public sealed class SkyAppliedSourceCloudSelectionOwner
{
 private readonly LoadedShaderContainer shader;
 private readonly G1ShaderBindingTables bindings;
 private readonly string locale,recipe;
 private SkyAppliedSourceCloudSelection? current;
 private SkyAppliedSourceCloudSelectionOwner(LoadedShaderContainer shader,string locale,string recipe)
 {this.shader=shader;bindings=G1ShaderBindingReader.Read(shader.Source.Bytes);this.locale=locale;this.recipe=recipe;}
 public static SkyAppliedSourceCloudSelectionOwner CreateFresh(GameResourceRepository repository,string locale)
 {
  ArgumentNullException.ThrowIfNull(repository);
  var shader=repository.LoadShaderContainer("system",0x42ED52ED);RequireShader(shader);
  return new(shader,locale,RenderSamplerSourceAdapters.ReadAppliedCloudSelectionNativeRecipe(repository.SourceDirectory,locale));
 }
 public SkyAppliedSourceCloudSelection PrepareCurrent(MapSourceCreatedSeasonLightingFrame present,
  SkyAppliedSourceStageFrame stages,SkyAppliedSeasonCloudTextureSources textures)
 {
  ArgumentNullException.ThrowIfNull(present);ArgumentNullException.ThrowIfNull(stages);ArgumentNullException.ThrowIfNull(textures);
  RequireShader(shader);var selection=present.AcceptedSeasonCamera.Inputs.Source.Selection;
  stages.ValidateCurrent(stages.AcceptedLighting,selection,present.CurrentSkyCamera);
  if(!ReferenceEquals(stages.AcceptedPresent,present)||selection.SecondPackage is not null||
   present.AcceptedSeasonCamera.Inputs.MainFrame.Camera.ExecutableLocale!=locale)
   throw new InvalidDataException("Cloud selection requires the same applied direct source season/frame and pinned locale.");
  var canonical=SkySeasonPackageTextures.Read(selection.FirstPackage).Package;
  if(canonical.SourceEvidence!=textures.Package.SourceEvidence||canonical.Camera.ObjectId!=textures.Package.Camera.ObjectId||
   textures.Noise.ObjectId!=0xF4A532C4||textures.Noise.Entries.Count!=3||textures.Cirrus.ObjectId!=0x7E43F947||textures.Cirrus.Entries.Count!=1)
   throw new InvalidDataException("Cloud source texture groups do not belong to this exact selected source camera.");
  uint mode=Scalar(canonical.Camera,778,0x2FB5219A),samples=Scalar(canonical.Camera,779,0x0808750D);
  string finalWriter="declared-source-mode-without-Main-post-selection-receipt";
  if(present.AcceptedSeasonCamera.Inputs.MainPostSelection is { } post)
  {
   post.ValidateSameMain(present.AcceptedSeasonCamera.Inputs.MainFrame.Camera);
   post.ValidateSameSelection(selection);
   if(post.FrameNumber!=present.FrameNumber||!post.TryReadPreparedWord(0x674,out mode))
    throw new InvalidDataException("Cloud selection requires the same-frame final Main674 quality writer.");
   finalWriter=post.SourceEvidence+";actual-final-Main674;CG_QUALITY="+post.QualityOption18;
  }
  if(samples!=4||stages.ScenePresent)
   throw new NotSupportedException("Applied Cloud selection closes ray4/nullScene/Cirrus with retained feature0 axes; other scene/ray branches remain unresolved.");
  if(bindings.Variants.RecordWidth!=36||bindings.Variants.Records.Count!=288||bindings.PassCount!=6)
   throw new InvalidDataException("Cloud source variant table changed.");
  var program=SkyCloudColorProgramSelection.ReadCurrentSourceLevel(shader,mode);
  byte[] request=program.RequestedVariantBytes.ToArray();
  // Native981F00 copies the complete first row to both caches. D77B10
  // writes the four source axes;11971F0 overwrites scene and disables
  // temporal axis14 during its actual WORK color operation.
  var row=bindings.Variants.Match(request)??throw new InvalidDataException("Actual Cloud requested variant has no source row.");
  if(row.Index!=program.VariantRow)throw new InvalidDataException("Cloud selected row differs from its actual program contract.");
  return current=new(this,shader,present,stages,request,program,
   recipe+";"+finalWriter+";originalCompleteVariantRow0-retained-axes;lowCost="+program.LowCostLevel+
   "/ray4;SceneFalse/CirrusTrue;currentRow="+row.Index+"/pass0/combination="+program.Combination.Index+
   ";fresh-no-extra-variant-edits");
 }
 internal void Validate(SkyAppliedSourceCloudSelection frame)
 {if(!ReferenceEquals(frame,current))throw new InvalidDataException("Cloud current source selection was superseded.");}
 private static uint Scalar(DokObject camera,int ordinal,uint hash)
 {
  var fields=camera.Properties.Where(p=>p.Index==ordinal).ToArray();
  if(camera.ClassId!=0x23E7C64E||fields.Length!=1||fields[0].NameHash!=hash||fields[0].TypeCode!=5||
   fields[0].ElementCount!=1||fields[0].ElementSize!=4||fields[0].RawValue.Length!=4)
   throw new InvalidDataException("Cloud source mode/ray field changed its exact ordinal/type/hash.");
  return BinaryPrimitives.ReadUInt32LittleEndian(fields[0].RawValue);
 }
 internal static void RequireShader(LoadedShaderContainer shader)
 {
  var s=shader.Source;
  if(s.ResourceUri!="san14://rdb/system/42ED52ED"||s.OverrideSource is not null||
   s.DecodedSha256!="C1B780D8074F46C2C90BCB3800D941024ECAEDC9C784FD9F7C12B54B5D34A2F9"||
   Convert.ToHexString(SHA256.HashData(s.Bytes))!=s.DecodedSha256)
   throw new InvalidDataException("Cloud current selection requires its exact original shader source.");
 }
}

public sealed class SkyAppliedSourceCloudSelection
{
 private readonly SkyAppliedSourceCloudSelectionOwner owner;
 internal SkyAppliedSourceCloudSelectionOwner SourceCreatedRendererOwner => owner;
 private readonly byte[] requested;
 internal SkyAppliedSourceCloudSelection(SkyAppliedSourceCloudSelectionOwner owner,LoadedShaderContainer shader,
  MapSourceCreatedSeasonLightingFrame present,SkyAppliedSourceStageFrame stages,byte[] requested,
  SkyCloudColorProgramSelection program,string provenance)
 {this.owner=owner;Shader=shader;AcceptedPresent=present;SourceStages=stages;this.requested=requested.ToArray();ProgramSelection=program;SourceProvenance=provenance;}
 public LoadedShaderContainer Shader{get;}
 public MapSourceCreatedSeasonLightingFrame AcceptedPresent{get;}
 public SkyAppliedSourceStageFrame SourceStages{get;}
 public SkyCloudColorProgramSelection ProgramSelection{get;}
 public int VariantRow=>ProgramSelection.VariantRow;
 public int ColorCombination=>ProgramSelection.Combination.Index;
 public int ColorVertexProgram=>ProgramSelection.VertexProgram;
 public int ColorPixelProgram=>ProgramSelection.PixelProgram;
 public int PixelConstantByteLength=>ProgramSelection.PixelConstantByteLength;
 public bool BinderTextureAssignmentsEnabled=>true;
 public ReadOnlyMemory<byte> RequestedVariantBytes=>requested.ToArray();
 public string SourceProvenance{get;}
 public void ValidateCurrent(MapSourceCreatedSeasonLightingFrame present,SkyAppliedSourceStageFrame stages)
 {
  owner.Validate(this);SkyAppliedSourceCloudSelectionOwner.RequireShader(Shader);
  if(!ReferenceEquals(present,AcceptedPresent)||!ReferenceEquals(stages,SourceStages))
   throw new InvalidDataException("Cloud current variant receipt belongs to another accepted source frame.");
  stages.ValidateCurrent(stages.AcceptedLighting,present.AcceptedSeasonCamera.Inputs.Source.Selection,present.CurrentSkyCamera);
 }
}
