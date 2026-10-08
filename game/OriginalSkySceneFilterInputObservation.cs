/// <summary>Closed numeric observation at the chosen generation's first
/// actual filter boundary. It creates no GPU readiness receipt.</summary>
public sealed class OriginalSkySceneFilterInputObservation
{
    private readonly OriginalSkyScene scene;
    internal OriginalSkySceneFilterInputObservation(OriginalSkyScene scene,uint generation,long completed,uint flat,
        OriginalSkyCubeDraw draw,OriginalSkySceneBaseStages stages,int lightFaces,bool sourceDrawCallHasReturned=false)
        =>(this.scene,GenerationIndex,CompletedGenerations,FilterFlat,FilterDraw,ActualBaseStages,ActualOptionalLightFaceCount,SourceDrawCallHasReturned)
            =(scene,generation,completed,flat,draw,stages,lightFaces,sourceDrawCallHasReturned);
    public uint GenerationIndex{get;}
    public long CompletedGenerations{get;}
    public uint FilterFlat{get;}
    public OriginalSkyCubeDraw FilterDraw{get;}
    public OriginalSkySceneBaseStages ActualBaseStages{get;}
    public int ActualOptionalLightFaceCount{get;}
    public bool SourceDrawCallHasReturned{get;}
    public bool ProvesTextureReadiness=>false;
    public void RequireCurrent()=>scene.RequireCurrentFilterInputObservation(this);
}
