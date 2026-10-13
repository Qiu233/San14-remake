using San14.Data;
using San14.Data.Formats;

public sealed partial class RemakeArmyFlagNativeRenderer
{
    private ArmyWorldQueuePreparedPackets? _worldPackets;

    /// <summary>Requires every actual pole/cloth material and producer image.
    /// Missing cloth fails this complete preparation instead of returning a
    /// partial successful World inventory. Each actual final full table selects
    /// its own49/50 partition and the corresponding native alpha input.</summary>
    public Task<ArmyWorldQueuePreparedPackets> PrepareWorldQueuePacketsAsync(
        NativeMapArmyPreparedFrame actualArmy,
        IReadOnlyList<ModelBlendAttachment> actualNativeSixColorStates,
        NativeWorldShaderTableRegistry actualSharedTableRegistry,
        IArmyWorldFlagMaterialTablePublication? actualMaterialPublication)
    {
        ArmyWorldQueuePreparedPackets.RequireFrame(actualArmy);
        ArgumentNullException.ThrowIfNull(actualSharedTableRegistry);
        ArmyWorldQueuePreparedPackets.RequireColorInputScope(actualArmy, actualNativeSixColorStates);
        if (actualArmy.SourceMaterials is null && actualArmy.FlagActors != 0)
            throw new InvalidDataException("Army World flag queues need the actual current source force/text/material provider.");
        if (actualMaterialPublication is null && (actualArmy.FlagActors != 0 || actualArmy.Flags.Count != 0))
            throw new NotSupportedException("Nonempty Army flag preparation requires its actual current material publication.");
        actualMaterialPublication?.RequireCurrent(actualArmy);
        Accept(); return PrepareWorldAcceptedAsync(actualArmy, actualNativeSixColorStates, actualSharedTableRegistry, actualMaterialPublication);
    }
    private async Task<ArmyWorldQueuePreparedPackets> PrepareWorldAcceptedAsync(
        NativeMapArmyPreparedFrame actualArmy, IReadOnlyList<ModelBlendAttachment> actualNativeSixColorStates,
        NativeWorldShaderTableRegistry actualSharedTableRegistry,
        IArmyWorldFlagMaterialTablePublication? actualMaterialPublication)
    {
        await _gate.WaitAsync();
        try
        {
            _worldPackets = null;
            var packets = new List<ModelGbufferInstancePacket>();
            var selections = new List<ArmyWorldQueuePrimitiveSource>();
            var tables = new List<ArmyWorldPrimitiveTableRow>();
            var pending = new Dictionary<string,ArmyDrawMaterialState>(StringComparer.Ordinal);
            ModelRasterInputs? raster = null;
            float seconds = actualArmy.Animation.CallerSeconds;
            foreach (var (unit, frame) in actualArmy.Flags)
            foreach (var actor in frame.Actors)
            {
                var materialPublication = actualMaterialPublication ?? throw new NotSupportedException("An actual flag actor needs its current material publication.");
                if (!_epochs.TryGetValue(unit, out var epoch) || !ReferenceEquals(epoch.Initialization, actor.Initialization))
                    _epochs[unit] = epoch = (actor.Initialization, seconds);
                var sources=_sources.Get(actor.Source);
                foreach (var draw in actor.Source.Draws)
                {
                    raster ??= ArmyWorldQueuePreparedPackets.PreparationRaster(actualNativeSixColorStates);
                    var source=sources.GetDraw(draw.Index);
                    string packet = actor.InstanceKey + $"/draw{draw.Index}";
                    string writer = sources.WorldTables.ImportFreshNullWrapper(actor.Source.Catalog, actor.Category, actor.Source.Model.Geometry, draw.Index);
                    if (source.RequiredRuntimeProxyIds.Count == 0)
                    {
                        materialPublication.RequireCurrent(actualArmy);
                        var poleMaterial = materialPublication.ReadActualFinalTable(actor, draw.Index, null);
                        var finalPoleTable = poleMaterial.FinalTable;
                        actualSharedTableRegistry.RequireCurrent(finalPoleTable);
                        var actualPoleInterface=actualSharedTableRegistry.ReadSelectedInterface(finalPoleTable,18);
                        if(string.IsNullOrWhiteSpace(materialPublication.SourceEvidence)||
                            !ReferenceEquals(poleMaterial.ActualActor,actor)||poleMaterial.SourceRawDrawIndex!=draw.Index||
                            string.IsNullOrWhiteSpace(poleMaterial.SourceWriterEvidence)||
                            actualPoleInterface.Provider?.ShaderFileHash!=source.Shader.Source.FileId)
                            throw new NotSupportedException("Flag pole current full table needs its actual matching program/inputs producer.");
                        if(poleMaterial.ActualParameterRoute==ArmyWorldPrimitiveParameterRoute.Unknown)
                            throw new NotSupportedException("Flag pole needs the actual prior/current28/40 parameter selection.");
                        if(poleMaterial.ActualParameterRoute==ArmyWorldPrimitiveParameterRoute.ActualCurrentMaterial&&
                            (poleMaterial.ActualMaterialRecord64 is null||poleMaterial.ActualMaterialId is null||poleMaterial.ActualCurrentTextures is null))
                            throw new NotSupportedException("Flag pole current material path needs the actual28/40 and38 records.");
                        var selection = sources.WorldQueues.Read(packet, source.GraphSource.Graph,
                            actor.Source.ShaderBindings, draw.Index, actualPoleInterface, source.Shader);
                        if (!selection.Opaque49.SourceQueueSelectsShader)
                            throw new NotSupportedException("Source flag pole did not select original opaque queue49.");
                        var previous=_materials.Read(packet,source,actor.Initialization);
                        var pole = source.Prepare(packet, new(seconds - epoch.Start, actor.World,
                            actualArmy.Context.PreparedCamera!, new byte[6256], previous?.Pixel ?? new byte[80],
                            previous?.Flags??0, raster, new Dictionary<uint,RemakeArmyFlagRuntimeTexture>())
                            {ActualCurrentTextureGroup=poleMaterial.ActualCurrentTextures,
                                ActualMaterialRecord64=poleMaterial.ActualParameterRoute==ArmyWorldPrimitiveParameterRoute.ActualCurrentMaterial?poleMaterial.ActualMaterialRecord64:null,
                                ActualMaterialId=poleMaterial.ActualParameterRoute==ArmyWorldPrimitiveParameterRoute.ActualCurrentMaterial?poleMaterial.ActualMaterialId:null,
                                ActualBackendAlphaEnabled=selection.Opaque49.Queue.BackendAlphaEnabled});
                        packets.Add(pole.Packet); selections.Add(selection);
                        tables.Add(new(packet, finalPoleTable, writer + ";" + materialPublication.SourceEvidence + ";" + poleMaterial.SourceWriterEvidence +
                            ";group=" + poleMaterial.ActualContainerGroupOrdinal + ";material=" + poleMaterial.ActualMaterialObjectId.ToString("X8") +
                            ";actual-pole-final-full-table;WorldPQ-default-container-materials-not-assumed-null") { ActualFlagMaterial = poleMaterial });
                        pending[packet] = new(source,actor.Initialization,pole.NextCachedMaterialFlags, pole.Packet.Pixel.Constants[0].ToArray());
                        continue;
                    }
                    var input = actualArmy.SourceMaterials!.Select(actor);
                    var variant=sources.GetVariant(draw.Index,input.ExplicitOwnedForceVariant);
                    var producer=sources.GetProducer(actor.Initialization.CodeSource.Language);
                    var primary=await producer.PrepareAsync(variant.Variation,variant.TextureUses.Single(t=>t.TextureRegister==0),input.SourcePane);
                    var regularGroup=sources.GetTextureGroup(variant,primary);
                    if(materialPublication is IArmyWorldFlagMaterialResourceReceiver reachedMaterialResources)
                        reachedMaterialResources.ReceiveActualReadyRegularTextureGroup(actualArmy,actor,draw.Index,variant.Variation,regularGroup);
                    materialPublication.RequireCurrent(actualArmy);
                    var currentMaterial = materialPublication.ReadActualFinalTable(actor, draw.Index, variant.Variation);
                    var finalTable = currentMaterial.FinalTable;
                    actualSharedTableRegistry.RequireCurrent(finalTable);
                    if (string.IsNullOrWhiteSpace(materialPublication.SourceEvidence) ||
                        !ReferenceEquals(currentMaterial.ActualActor, actor) || currentMaterial.SourceRawDrawIndex != draw.Index ||
                        currentMaterial.ActualMaterialObjectId != variant.Variation.SelectedMaterialObjectId ||
                        string.IsNullOrWhiteSpace(currentMaterial.SourceWriterEvidence) ||
                        currentMaterial.ActualContainerGroupOrdinal != actor.Source.MaterialGroups.ReadDeclaredDraw(draw.Index).DeclaredContainerGroupOrdinal)
                        throw new InvalidDataException("Flag final Material78/80 publication belongs to another current actor/draw/group/material.");
                    var actualTextureGroup=currentMaterial.ActualCurrentTextures??
                        throw new NotSupportedException("Flag current material requires its actual E0 texture-group record; C0 or a primary image is not an inferred replacement.");
                    var actualVariant=sources.GetFinalVariant(draw.Index,variant,finalTable);
                    var selected=actualSharedTableRegistry.ReadSelectedInterface(finalTable,18);
                    var clothSelection = sources.WorldQueues.Read(packet, source.GraphSource.Graph,
                        actor.Source.ShaderBindings, draw.Index, selected, actualVariant.Shader);
                    if (clothSelection.Alpha50.SourceQueueSelectsShader == clothSelection.Opaque49.SourceQueueSelectsShader ||
                        input.ExplicitForcedAlphaReference is not null)
                        throw new NotSupportedException("Actual final flag shader needs exactly one original49/50 partition and an unforced native alpha reference.");
                    bool actualAlpha=clothSelection.Alpha50.SourceQueueSelectsShader;
                    if(currentMaterial.ActualParameterRoute==ArmyWorldPrimitiveParameterRoute.ActualPriorRetained)
                    {
                        var prior=sources.WorldTables.ReadActualFreshPriorTable(actor.Source.Catalog,actor.Category,actor.Source.Model.Geometry,draw.Index,
                            actualSharedTableRegistry.RetainImportedTable(source.GraphSource.Draw.Binding.Shader));
                        if(!ReferenceEquals(prior,finalTable)||actualVariant.Shader.Source.FileId!=source.Shader.Source.FileId)
                            throw new InvalidDataException("Flag prior28/40 route must retain its actual fresh prior full table/program.");
                        var previous=_materials.Read(packet,source,actor.Initialization);
                        var prepared=source.Prepare(packet,new(seconds-epoch.Start,actor.World,actualArmy.Context.PreparedCamera!,
                            new byte[6256],previous?.Pixel??new byte[80],previous?.Flags??0,raster,
                            new Dictionary<uint,RemakeArmyFlagRuntimeTexture>())
                            {ActualCurrentTextureGroup=actualTextureGroup,
                                ActualBackendAlphaEnabled=actualAlpha?clothSelection.Alpha50.Queue.BackendAlphaEnabled:clothSelection.Opaque49.Queue.BackendAlphaEnabled});
                        packets.Add(prepared.Packet);selections.Add(clothSelection);
                        tables.Add(new(packet,finalTable,writer+";"+materialPublication.SourceEvidence+";"+currentMaterial.SourceWriterEvidence+
                            ";DB8BBB-actual-prior18/28/40-retained;actual-current38-textures") {ActualFlagMaterial=currentMaterial});
                        pending[packet]=new(source,actor.Initialization,prepared.NextCachedMaterialFlags,prepared.Packet.Pixel.Constants[0].ToArray());
                        continue;
                    }
                    if(currentMaterial.ActualParameterRoute!=ArmyWorldPrimitiveParameterRoute.ActualCurrentMaterial)
                        throw new NotSupportedException("Flag needs its actual prior/current28/40 parameter selection.");
                    var actualMaterialRecord=currentMaterial.ActualMaterialRecord64??
                        throw new NotSupportedException("Flag current primitive28 requires its actual type12 material parameter record.");
                    var actualMaterialId=currentMaterial.ActualMaterialId??
                        throw new NotSupportedException("Flag current primitive40 material ID requires its actual type15 property mapping.");
                    var previousCloth=_materials.Read(packet,actualVariant,actor.Initialization);
                    var cloth = actualVariant.Prepare(packet, new(seconds - epoch.Start, actor.World,
                        actualArmy.Context.PreparedCamera!, new byte[6256], previousCloth?.Pixel ?? new byte[80],
                        previousCloth?.Flags??0, raster, new Dictionary<uint,RemakeArmyFlagRuntimeTexture>
                            { [primary.ProxyObjectId] = primary }, input.ExplicitSpecularW, input.ExplicitSpecularSource,
                        actualAlpha ? clothSelection.Alpha50.Queue.BackendAlphaEnabled : clothSelection.Opaque49.Queue.BackendAlphaEnabled)
                        {ActualCurrentTextureGroup=actualTextureGroup,ActualMaterialRecord64=actualMaterialRecord,ActualMaterialId=actualMaterialId,
                            ActualMaterialParameterSource=currentMaterial.SourceWriterEvidence});
                    packets.Add(cloth.Packet); selections.Add(clothSelection);
                    tables.Add(new(packet, finalTable, writer + ";" + materialPublication.SourceEvidence + ";" + currentMaterial.SourceWriterEvidence +
                        ";group=" + currentMaterial.ActualContainerGroupOrdinal +
                        ";actual-force-row=" + variant.Variation.SelectedSourceVariationRow +
                        ";material=" + variant.Variation.SelectedMaterialObjectId.ToString("X8") + ";actual-final-full-table;Primitive50-source-null") { ActualFlagMaterial = currentMaterial });
                    pending[packet] = new(actualVariant,actor.Initialization,cloth.NextCachedMaterialFlags, cloth.Packet.Pixel.Constants[0].ToArray());
                }
            }
            ArmyWorldQueuePreparedPackets? result = null;
            result = new(actualArmy, "ArmyFlag", packets, selections, tables, () =>
            {
                ObjectDisposedException.ThrowIf(_retiring, this);
                actualMaterialPublication?.RequireCurrent(actualArmy);
                if (!ReferenceEquals(_worldPackets, result))
                    throw new InvalidDataException("Army flag packet preparation was superseded or retired.");
            });
            _materials.Commit(pending);
            foreach (int old in _epochs.Keys.Where(k=>!actualArmy.Flags.ContainsKey(k)).ToArray())_epochs.Remove(old);
            return _worldPackets = result;
        }
        finally { _gate.Release(); CompleteAccepted(); }
    }
}
