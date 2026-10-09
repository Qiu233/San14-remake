using System.Buffers.Binary;
using System.Collections.ObjectModel;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalCityOpeningPostLoadRecord(int Slot,int ObjectSlot,int HexId,int SizeCode,
    int ForcePhysicalSlot,int SourceDurability,int Durability,int Capacity,int DamageTier,int Variant,int Category,
    bool SpecialCapacity);

/// <summary>Source-created ordinary opening visual fields, derived from this
/// locale's source rather than another language's process observation. No
/// full-world readiness, current runtime policy or original-process capture is
/// claimed. Load/reload owns verification; lookups reuse the prepared values.</summary>
public sealed class LoadedOriginalCityOpeningPostLoadSource
{
    internal LoadedOriginalCityOpeningPostLoadSource(LoadedScenario scenario,OriginalCityOpeningPostLoadNativeRecipe native,
        OriginalCeCityVariantTable variants,NewGameOptions options,int[] effects,Dictionary<int,OriginalCityOpeningPostLoadRecord> cities)
    {
        Locale=scenario.Snapshot.Locale;ScenarioSource=scenario.ScenarioSource;FixedDataSource=scenario.FixedDataSource;
        ImplementationVersion=variants.ImplementationVersion;Options=options;ForcePolicyEffects32=Array.AsReadOnly(effects);
        Cities=new ReadOnlyDictionary<int,OriginalCityOpeningPostLoadRecord>(cities);
        Evidence=new(scenario.ScenarioSource.ResourceUri+"#source-created-ordinary-City66",scenario.ScenarioSource.Sha256,
            "source-created-ordinary-postload;"+native.SourceUri+";implementation="+native.ImplementationVersion+
            ";fixed="+scenario.FixedDataSource.ResourceUri+"@"+scenario.FixedDataSource.Sha256+
            ";native-object-registry/Area35/linked-Object14/capacity/effect32/float-ceil;derived-not-observed");
    }
    public string Locale{get;}
    public SourceProvenance ScenarioSource{get;}
    public SourceProvenance FixedDataSource{get;}
    public string ImplementationVersion{get;}
    public NewGameOptions Options{get;}
    public IReadOnlyList<int> ForcePolicyEffects32{get;}
    public IReadOnlyDictionary<int,OriginalCityOpeningPostLoadRecord> Cities{get;}
    public CityVisualEvidence Evidence{get;}

    /// <summary>Creates the owned current descriptors once. Their lifetime
    /// follows actual descriptor writers, not the calendar or render frame.</summary>
    public OriginalCityVisualDescriptorOwner CreateCurrentDescriptorOwner(OriginalCeCityVariantTable variants)
        => new(this,variants);

    /// <summary>Only for this source-created opening. Later policy, ownership,
    /// size or durability writers need the current state producer.</summary>
    public CityVisualSelectionResult SelectSourceCreatedOpening(OriginalCeCityVariantTable variants,CityVisualState city)
    {
        ArgumentNullException.ThrowIfNull(variants);
        if(variants.Locale!=Locale||variants.ImplementationVersion!=ImplementationVersion)
            throw new InvalidDataException("Derived opening and current city variant source differ.");
        if(!Cities.TryGetValue(city.Slot,out var record)||city.HexId!=record.HexId||city.SizeCode!=record.SizeCode||
            city.Durability!=record.Durability||city.ForceId!=(record.ForcePhysicalSlot==0?null:(int?)record.ForcePhysicalSlot))
            return new(null,"source-created-opening-city-fields-differ");
        return CityVisualSelector.Select(variants,city,CityVisualDamageInput.FromTier(record.DamageTier,Evidence));
    }
}

public static class OriginalCityOpeningPostLoadSource
{
    /// <summary>Load once for a new ordinary source-created opening. This is
    /// the Object14/City66 dependency projection of native postload; gold,
    /// troop and morale outputs cannot change these visual fields and are not
    /// filled with guessed runtime providers.</summary>
    public static LoadedOriginalCityOpeningPostLoadSource LoadSourceCreatedOrdinary(OriginalGameRepository repository,
        LoadedScenario scenario,LoadedMap map,NewGameOptions options,OriginalCeCityVariantTable variants)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(map);ArgumentNullException.ThrowIfNull(options);ArgumentNullException.ThrowIfNull(variants);
        if(scenario.Metadata.FormatMarker!=0x66||scenario.Snapshot.Locale is not("SC" or "TC")||
            options.DifficultyId is not("beginner" or "intermediate" or "advanced" or "super" or "top")||
            scenario.Snapshot.Cities.Count!=52||scenario.Snapshot.Forces.Count!=52||map.Map.Cells.Length!=48400||
            scenario.Snapshot.WorldParameters?.SourceBoundary?.Settings is not { } settings)
            throw new NotSupportedException("Opening City66 needs complete ordinary CE sources and actual new-game choices.");
        var native=OriginalDeferredSamplerSources.LoadCityOpeningPostLoadNative(repository.SourceDirectory,scenario.Snapshot.Locale);
        if(variants.Locale!=native.Locale)
            throw new InvalidDataException("City postload and selected variant locales differ.");
        // ENV source serialization precedes City slot7. The ordinary page
        // also writes1, but it cannot replace an unproved static source gate.
        if(settings.FieldAtNativeMember(0xE58).SignedValue!=1)
            throw new NotSupportedException("Ordinary source-created City slot7 needs actual source selector E58=1.");
        // Advanced key39 is present in this source but unused by that gate.
        _=settings.FieldAtNativeMember(0x68C);
        var effects=ReadSourceCreatedForceEffects32(scenario);
        var current=new OriginalEventInitializationContext();
        current.InitializeStaticTablePointers();current.ResetCityObjectLinks();current.MaterializeStaticSource(scenario);
        var objects=scenario.Snapshot.ObjectSourceRecords??throw new InvalidDataException("Opening object source is absent.");
        OriginalObjectLinkReconstruction.ApplyRegisteredObjects(objects.Objects.Select(o=>
            new S14ConditionNativeObject(o.PhysicalSlot,o.RawField10,o.RawField12)).ToArray(),1,current.WriteObjectLink);
        // Native3306E0 static1 writes Area35 from fixed Area34 before the
        // capacity special-base map query. Project only that consumed field.
        foreach(var cell in map.Map.Cells)
        {current.SetHexTablePointer(cell.Id,cell.Id);current.SetHexAreaSlot(cell.Id,cell.AreaSlot);}
        foreach(var area in scenario.Snapshot.Areas)
        {
            current.SetAreaTablePointer(area.Slot,area.Slot);current.SetAreaValidityCode(area.Slot,area.CitySlot);
            if(area.Slot!=0&&area.CitySlot!=0)current.SetAreaCitySlot(area.Slot,(byte)Math.Clamp((int)area.CitySlot,0,51));
        }
        int Parameter(string key)=>native.BaseParameters.Values[key];
        var result=new Dictionary<int,OriginalCityOpeningPostLoadRecord>();
        foreach(var city in scenario.Snapshot.Cities.Where(c=>c.Slot!=0))
        {
            var reference=new OriginalBaseReference(OriginalBaseKind.City,city.Slot);
            if(current.ReadBaseNativeValidity(reference)!=true||current.ReadBaseObjectLink(reference) is not ushort link||
                current.ReadBaseForceSlot(reference) is not int forceSlot||forceSlot is <0 or >51||
                current.ReadCitySizeB8(reference) is not byte size||size is <1 or >4||city.HexId is not int hex)
                throw new InvalidDataException("Opening city has an unresolved actual object/district/size source: "+city.Slot);
            bool special=current.ReadSpecialBaseCapacity(reference);
            int capacity=ReadCityCapacity(size,special,effects[forceSlot],native.BaseParameters,false,null);
            int sourceDurability=current.ReadObjectRawField14(new(link))??throw new InvalidDataException("Linked opening Object14 is unknown.");
            // Native linked setter first clamps by actual base capacity,
            // then Object14 setter applies its own source maximum.
            int durable=sourceDurability<0?0:Math.Min(sourceDurability,capacity);
            durable=durable<0?0:Math.Min(durable,Parameter(SimulationParameterKeys.BaseCapacity.MaximumDurability));
            current.SetObjectRawField14(link,unchecked((ushort)durable));
            int actualDurable=current.ReadObjectRawField14(new(link))!.Value;
            byte tier=ReadFlag66(actualDurable,capacity,native.BaseParameters);
            current.SetBasePostLoadFlag66(reference,tier);
            int variant=variants.VariantForCitySlot(city.Slot);
            result.Add(city.Slot,new(city.Slot,link,hex,size,forceSlot,sourceDurability,actualDurable,capacity,tier,variant,
                OriginalCeCityModels.CategoryFor(variant,size,tier),special));
        }
        return new(scenario,native,variants,options,effects,result);
    }

    /// <summary>The fourth capacity output for a real city-object binding.
    /// Other three capacity outputs and their troop provider are not consumed.
    /// Special bases skip the ordinary percentage and final-min branch.</summary>
    public static int ReadCityCapacity(int actualSizeCode,bool specialBase,int actualPolicyEffect32,
        OriginalBaseParameterCatalog parameters,bool actualPercentageEnabled,int? actualPercentage)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        int P(string key)=>parameters.Values[key];
        if(specialBase)return P(SimulationParameterKeys.BaseCapacity.MaximumDurability);
        int capacity=unchecked(P(SimulationParameterKeys.BaseCapacity.BaseDurability)+(actualSizeCode-1)*P(SimulationParameterKeys.BaseCapacity.DurabilityPerAdditionalSize)+actualPolicyEffect32*P(SimulationParameterKeys.BaseCapacity.DurabilityPolicyScale));
        if(actualPercentageEnabled)capacity=unchecked((actualPercentage??throw new InvalidDataException("Enabled city capacity percentage is unknown."))*capacity)/100;
        return Math.Min(capacity,9999);
    }

    /// <summary>Native float32 divide/multiply, ceil and CVTTSS2SI bands.
    /// This does not substitute source City46 for current capacity.</summary>
    public static byte ReadFlag66(int linkedDurability,int actualCapacity,OriginalBaseParameterCatalog parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        float ratio=(float)linkedDurability/(float)actualCapacity;
        float percent=ratio*BitConverter.Int32BitsToSingle(parameters.Values[SimulationParameterKeys.Shared.PercentDenominator]);
        float ceiling=MathF.Ceiling(percent);
        int n=!float.IsFinite(ceiling)||ceiling<int.MinValue||ceiling>=2147483648f?int.MinValue:(int)ceiling;
        int high=parameters.Values[SimulationParameterKeys.BaseCapacity.DamageTierFirstThreshold],middle=parameters.Values[SimulationParameterKeys.BaseCapacity.DamageTierSecondThreshold],low=parameters.Values[SimulationParameterKeys.BaseCapacity.DamageTierThirdThreshold];
        if(n>high&&n<=100)return 0;
        if(n>middle&&n<=high)return 1;
        if(n>low&&n<=middle)return 2;
        return(byte)(n>=0&&n<=low?3:0);
    }

    private static int[] ReadSourceCreatedForceEffects32(LoadedScenario scenario)
    {
        var source=scenario.Snapshot;
        var catalog=source.PolicyParameters??throw new InvalidDataException("Opening policy definitions are absent.");
        var effect=catalog.Effects.Single(e=>e.Slot==32);
        if(effect.RawValues.Count!=10)throw new InvalidDataException("Opening effect32 needs its actual ten-level definition.");
        var persons=source.Characters.Where(p=>p.CharacterId!=0).ToDictionary(p=>p.CharacterId);
        var policies=catalog.Policies.ToDictionary(p=>p.Slot);var result=new int[52];
        foreach(var force in source.Forces)
        {
            if(OriginalNativeValidity.Force(force.Slot,force.RulerSlot)!=true)continue;
            byte[] row=new byte[329];BinaryPrimitives.WriteUInt16LittleEndian(row,force.RulerSlot);
            Convert.FromHexString(force.RemainingBytesHex).CopyTo(row,2);
            ushort Word(int at)=>BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(at,2));
            var members=new List<(int Person,int Filter)>{(force.RulerSlot,0),(Word(59),Word(324))};
            for(int group=0;group<5;group++)for(int i=0;i<7;i++)members.Add((Word(61+group*29+i*2),Word(75+group*29+i*2)));
            int level=0;
            foreach(var member in members)
            {
                if(member.Filter!=0||member.Person==0)continue;
                if(!persons.TryGetValue(member.Person,out var person)||person.CreditReference is not { } credit)
                    throw new InvalidDataException("Opening policy contributor source is absent.");
                bool valid=OriginalNativeValidity.Person(person.Slot,(ushort)person.CharacterId,credit.RawFlagsWord,credit.RawTailCode,person.StatusCode)
                    ??throw new InvalidDataException("Opening contributor validity fields are unknown.");
                if(!valid)continue;
                byte raw=person.RawPolicySlot??throw new InvalidDataException("Opening contributor policy byte185 is unknown.");
                int policySlot=raw<=120?raw:0;
                var policy=policies[policySlot];int multiplicity=policy.RawEffectSlots.Count(e=>e==32);
                if(multiplicity==0)continue;
                // Original kind3: status1 is zero; stage0 returns zero; on
                // the remaining branch, zero actual counts make both effect
                // loops empty. Other reached credit profiles need their producer.
                if(person.StatusCode!=1&&credit.RawStageCode!=0&&credit.RawSelectionCodes.Any(c=>c is >=1 and <=6))
                    throw new NotSupportedException("Opening effect32 contributor has a reached nonzero kind3 credit profile.");
                // City slot7 precedes Person slot7's clamp-to6. Consume the
                // actual serializer byte here; saturation-to10 belongs to
                // this original contribution query itself.
                int personal=person.RawPolicyBaseCost
                    ??throw new InvalidDataException("Opening contributor base1BA is unknown.");
                for(int i=0;i<multiplicity;i++)level=Math.Min(level+Math.Min(personal,10),10);
            }
            result[force.Slot]=level==0?0:effect.RawValues[level-1];
        }
        return result;
    }
}

internal sealed record OriginalCityOpeningPostLoadNativeRecipe(string Locale,string ImplementationVersion,string SourceUri,
    OriginalBaseParameterCatalog BaseParameters);
