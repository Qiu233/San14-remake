using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Fresh source Cloud requested variant cache, followed by the
/// actual D77B10 and11971F0 edits. The first complete source variant row is
/// copied by981F00; untouched axes are retained source bytes, not zero fill.
/// This supplies program selection, not a current CB or GPU readiness.</summary>
public sealed class SkyAppliedSourceCloudSelectionOwner
{
 private readonly SkyCloudProgramResources programs;
 private readonly string locale,recipe;
 private SkyAppliedSourceCloudSelection? current;
 private SkyAppliedSourceCloudSelectionOwner(SkyCloudProgramResources programs,string locale,string recipe)
 {this.programs=programs;this.locale=locale;this.recipe=recipe;}
 public static SkyAppliedSourceCloudSelectionOwner CreateFresh(GameResourceRepository repository,string locale,
  SkyCloudProgramResources programs)
 {
  ArgumentNullException.ThrowIfNull(repository);
  ArgumentNullException.ThrowIfNull(programs);
  return new(programs,locale,RenderSamplerSourceAdapters.ReadAppliedCloudSelectionNativeRecipe(repository.SourceDirectory,locale));
 }
 public SkyAppliedSourceCloudSelection PrepareCurrent(MapSourceCreatedSeasonLightingFrame present,
  SkyAppliedSourceStageFrame stages,SkyAppliedSeasonCloudTextureSources textures)
 {
  ArgumentNullException.ThrowIfNull(present);ArgumentNullException.ThrowIfNull(stages);ArgumentNullException.ThrowIfNull(textures);
  var selection=present.AcceptedSeasonCamera.Inputs.Source.Selection;
  stages.ValidateCurrent(stages.AcceptedLighting,selection,present.CurrentSkyCamera);
  if(!ReferenceEquals(stages.AcceptedPresent,present)||selection.SecondPackage is not null||
   present.AcceptedSeasonCamera.Inputs.MainFrame.Camera.ExecutableLocale!=locale)
   throw new InvalidDataException("Cloud selection requires the same applied direct source season/frame and pinned locale.");
  var canonical=selection.FirstPackage;
  textures.RequireSelectedPackage(canonical);
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
  var program=programs.SelectColor(mode);
  return current=new(this,present,stages,program,
   recipe+";"+finalWriter+";originalCompleteVariantRow0-retained-axes;lowCost="+program.LowCostLevel+
   "/ray4;SceneFalse/CirrusTrue;currentRow="+program.VariantRow+"/pass0/combination="+program.Combination.Index+
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
}

public sealed class SkyAppliedSourceCloudSelection
{
 private readonly SkyAppliedSourceCloudSelectionOwner owner;
 internal SkyAppliedSourceCloudSelectionOwner SourceCreatedRendererOwner => owner;
 internal SkyAppliedSourceCloudSelection(SkyAppliedSourceCloudSelectionOwner owner,
  MapSourceCreatedSeasonLightingFrame present,SkyAppliedSourceStageFrame stages,
  SkyCloudColorProgramSelection program,string provenance)
 {this.owner=owner;AcceptedPresent=present;SourceStages=stages;ProgramSelection=program;SourceProvenance=provenance;}
 public LoadedShaderContainer Shader=>ProgramSelection.Shader;
 public MapSourceCreatedSeasonLightingFrame AcceptedPresent{get;}
 public SkyAppliedSourceStageFrame SourceStages{get;}
 public SkyCloudColorProgramSelection ProgramSelection{get;}
 public int VariantRow=>ProgramSelection.VariantRow;
 public int ColorCombination=>ProgramSelection.Combination.Index;
 public int ColorVertexProgram=>ProgramSelection.VertexProgram;
 public int ColorPixelProgram=>ProgramSelection.PixelProgram;
 public int PixelConstantByteLength=>ProgramSelection.PixelConstantByteLength;
 public bool BinderTextureAssignmentsEnabled=>true;
 public ReadOnlyMemory<byte> RequestedVariantBytes=>ProgramSelection.RequestedVariantBytes;
 public string SourceProvenance{get;}
 public void ValidateCurrent(MapSourceCreatedSeasonLightingFrame present,SkyAppliedSourceStageFrame stages)
 {
  owner.Validate(this);
  if(!ReferenceEquals(present,AcceptedPresent)||!ReferenceEquals(stages,SourceStages))
   throw new InvalidDataException("Cloud current variant receipt belongs to another accepted source frame.");
  stages.ValidateCurrent(stages.AcceptedLighting,present.AcceptedSeasonCamera.Inputs.Source.Selection,present.CurrentSkyCamera);
 }
}
