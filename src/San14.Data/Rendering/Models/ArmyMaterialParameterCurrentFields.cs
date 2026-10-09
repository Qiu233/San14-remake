namespace San14.Data;

public enum ArmyMaterialStage1ParameterSlot { Normal88, Cloth90, StaticNormal98, StaticClothA0 }

/// <summary>The material's actual parameter-record fields. Call the publication
/// methods only where the corresponding original producer actually stored its
/// result. They do not grant dependency readiness or execute a type13 context.</summary>
public sealed class ArmyMaterialParameterCurrentFields<TParameterRecord> where TParameterRecord:class
{
    public TParameterRecord? Normal88 { get; private set; }
    public TParameterRecord? Cloth90 { get; private set; }
    public TParameterRecord? StaticNormal98 { get; private set; }
    public TParameterRecord? StaticClothA0 { get; private set; }
    public TParameterRecord? CurrentNormalA8 { get; private set; }
    public TParameterRecord? CurrentClothB0 { get; private set; }
    public TParameterRecord? RegularB8 { get; private set; }
    public TParameterRecord? ContextC8 { get; private set; }
    public TParameterRecord? RenderTargetE8 { get; private set; }
    public TParameterRecord? CurrentMaterialF0 { get; private set; }

    public void PublishStage1ShaderParameterRecord(ArmyMaterialStage1ParameterSlot slot,TParameterRecord? actualResult)
    {
        switch(slot) {
            case ArmyMaterialStage1ParameterSlot.Normal88:Normal88=actualResult;break;
            case ArmyMaterialStage1ParameterSlot.Cloth90:Cloth90=actualResult;break;
            case ArmyMaterialStage1ParameterSlot.StaticNormal98:StaticNormal98=actualResult;break;
            case ArmyMaterialStage1ParameterSlot.StaticClothA0:StaticClothA0=actualResult;break;
            default:throw new ArgumentOutOfRangeException(nameof(slot));
        }
    }
    /// <summary>D9D7CC result store followed by D9D7D6 mode0. The caller is
    /// inside the actual CPU8 Data-present ready block.</summary>
    public void PublishStage1RegularParameterRecord12(TParameterRecord? actualResult)
    { RegularB8=actualResult;ApplyCurrentMode(0); }
    /// <summary>D9D830 or completed D9D0B0's actual type13 result. Its source
    /// identifier is independent of this registered parameter-record owner.</summary>
    public void PublishActualContextParameterRecord13(TParameterRecord? actualResult)=>ContextC8=actualResult;
    /// <summary>D9D934 result and D9D94C..D9D998 conditional publication.
    /// Called only inside the actual CPU20 Data-present ready block.</summary>
    public void PublishStage2RenderTargetParameterRecord12(TParameterRecord? actualResult)
    {
        RenderTargetE8=actualResult;
        if(actualResult is not null)CurrentMaterialF0=actualResult;
        if(Normal88 is not null)CurrentNormalA8=Normal88;
        if(Cloth90 is not null)CurrentClothB0=Cloth90;
    }
    /// <summary>Complete parameter stores in D9D290. The mode0 static-cloth
    /// fallback really writes A8, leaving B0 unchanged; D9D3AD is not replaced
    /// by a symmetric assignment.</summary>
    public void ApplyCurrentMode(int actualMode)
    {
        switch(actualMode) {
            case 0:
                if(RegularB8 is not null)CurrentMaterialF0=RegularB8;
                if((StaticNormal98??Normal88) is {} normal)CurrentNormalA8=normal;
                if(StaticClothA0 is {} cloth)CurrentClothB0=cloth;
                else if(Cloth90 is {} fallback)CurrentNormalA8=fallback;
                break;
            case 1:
            case 2:
                if(RenderTargetE8 is not null)CurrentMaterialF0=RenderTargetE8;
                if(Normal88 is not null)CurrentNormalA8=Normal88;
                if(Cloth90 is not null)CurrentClothB0=Cloth90;
                break;
        }
    }
}
