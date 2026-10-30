using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Identity of the project's calendar rules. No original executable is loaded.</summary>
public sealed class MapSeasonNativeSource
{
    public string Locale { get; }
    public string RuleVersion { get; }
    public string SourceProvenance => $"san14://rules/ce/calendar-writer/v1@{RuleVersion}";
    private MapSeasonNativeSource(string locale)
    { Locale = locale; RuleVersion = SimulationRuleIdentity.ForLocale(locale); }

    public static MapSeasonNativeSource Load(string sourceDirectory, string locale) => new(locale);
}

public sealed record MapSeasonCalendarStep(NativeMapSeasonDate Date,
    bool ContinueNativeCycle,bool DayAdvanced,bool PeriodAdvanced,bool MonthAdvanced);
/// <summary>The date boundary consumed by rendering. Strategy clocks and
/// read-only scenario previews retain distinct concrete owners.</summary>
public abstract record MapSeasonCalendarFrame(long FrameNumber,
    MapSeasonSelection Selection,string SourceProvenance)
{
    public abstract NativeMapSeasonDate ThirdDate { get; }
}
public sealed record MapSeasonRuntimeFrame(long FrameNumber,NativeMapSeasonState State,
    MapSeasonSelection Selection,string SourceProvenance)
    : MapSeasonCalendarFrame(FrameNumber,Selection,SourceProvenance)
{
    public override NativeMapSeasonDate ThirdDate => State.ThirdDate;
}
/// <summary>A scenario resource date, without an owned strategy clock epoch.</summary>
public sealed record MapSeasonScenarioPreviewFrame(long FrameNumber,LoadedScenarioWorld Source,
    MapSeasonSelection Selection,string SourceProvenance)
    : MapSeasonCalendarFrame(FrameNumber,Selection,SourceProvenance)
{
    public override NativeMapSeasonDate ThirdDate
    {
        get
        {
            var date=Source.Document.RawThirdDate;
            return new(date.RawYear,date.RawMonth,date.RawDay,date.RawSubdayPart);
        }
    }
}
public sealed record MapSeasonRuntimeAdvance(bool Available,NativeMapSeasonState State,
    long CalendarStepsApplied,string? UnavailableReason);
public sealed record MapSeasonResolvedSourceFrame(MapSeasonCalendarFrame Calendar,
    MapSeasonSourceResources Resources);

/// <summary>Owned source World+34 producer. It never takes date values from
/// Core PeriodDate, World+2E or the Renderer Camera clock. Native waiting
/// ordinal supplies only elapsed inspected32-stage calendar cycles:3 two-hour
/// calendar writes per ordinary waiting update. Returned phase is post writer.</summary>
public static class MapSeasonRuntime
{
    public const string WriterRevision="ce-world-third-calendar-3steps-per-owned-waiting-update-20261002";
    public const int CalendarStepsPerOwnedUpdate=3;

    public static NativeMapSeasonState CreateFromScenario(LoadedScenarioWorld source,
        NativeSubdayClock explicitOwnedFreshClock,MapSeasonNativeSource native)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(explicitOwnedFreshClock);
        ArgumentNullException.ThrowIfNull(native);
        ValidateSource(source);
        if(explicitOwnedFreshClock.UpdateOrdinal!=0||explicitOwnedFreshClock.UpdatesIntoPeriod!=0||
            string.IsNullOrWhiteSpace(explicitOwnedFreshClock.Revision))
            throw new InvalidDataException("Source season creation requires the explicit fresh owned native clock epoch, not an inferred old-save date.");
        var third=Date(source.Document.RawThirdDate);
        OrdinaryDate(third);
        if(third.Hour!=0||third.Day is not (1 or 11 or 21))
            throw new NotSupportedException("This ordinary source campaign date must begin on its exact ten-day command boundary.");
        return new(source.Source.ResourceUri,source.Source.Sha256,source.Document.WorldSha256,
            Date(source.Document.RawSecondDate),third,0,explicitOwnedFreshClock.Revision,
            WriterRevision,NativeMapSeasonPhase.PostCalendarWriter,0,0,0,0);
    }

    /// <summary>Fresh campaign convenience: resolve its actual source world and
    /// content generation. A missing native clock cannot create a strategy epoch.</summary>
    public static GameState CreateForFreshCampaign(GameResourceRepository repository,GameState campaign,
        MapSeasonNativeSource native)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(campaign);
        if(campaign.NativeMapSeason is not null)throw new InvalidOperationException("The campaign already has an owned season generation.");
        var clock=campaign.NativeClock??throw new NotSupportedException("Campaign native clock is unresolved; Core.Date cannot supply a World third-date epoch.");
        var id=GameResourceId.Parse(campaign.ScenarioKey);
        if(id.Kind!="file")throw new InvalidDataException("Source campaign must name its actual scenario file URI.");
        var world=repository.LoadScenarioWorld(id.Key,native.Locale);
        if(!campaign.ContentHashes.TryGetValue(world.Source.ResourceUri,out string? hash)||hash!=world.Source.Sha256)
            throw new InvalidDataException("Campaign and season world belong to different scenario source generations.");
        return campaign with{NativeMapSeason=CreateFromScenario(world,clock,native)};
    }

    /// <summary>One exact reached source leaf. Signed byte/word storage and
    /// native30-day/12-month carry are retained; no Gregorian calendar applies.</summary>
    public static MapSeasonCalendarStep StepCalendarWriter(NativeMapSeasonDate current)
    {
        OrdinaryDate(current);
        sbyte hour=unchecked((sbyte)(current.Hour+2));
        if(hour<24)return new(current with{Hour=hour},true,false,false,false);
        sbyte day=unchecked((sbyte)(current.Day+1)),month=current.Month;short year=current.Year;
        bool monthAdvanced=day>30;
        if(monthAdvanced)
        {
            day=1;month=unchecked((sbyte)(month+1));
            if(month>12){year=unchecked((short)(year+1));month=1;}
        }
        bool period=day%10==1;
        return new(new(year,month,day,0),!period,true,period,monthAdvanced);
    }

    public static MapSeasonRuntimeAdvance AdvanceToOwnedClock(NativeMapSeasonState previous,
        NativeSubdayClock? ownedClock)
    {
        ArgumentNullException.ThrowIfNull(previous);ValidateOwned(previous);
        if(ownedClock is null)return new(false,previous,0,"Owned native simulation clock is unresolved; preserved independent World dates.");
        if(ownedClock.Revision!=previous.NativeClockRevision||ownedClock.UpdateOrdinal<previous.ProcessedNativeOrdinal||
            ownedClock.UpdatesIntoPeriod is <0 or >40||ownedClock.UpdateOrdinal<ownedClock.UpdatesIntoPeriod||
            (ownedClock.UpdateOrdinal-ownedClock.UpdatesIntoPeriod)%40!=0)
            throw new InvalidDataException("Season advancement requires the same monotonic owned native simulation clock generation.");
        long updates=ownedClock.UpdateOrdinal-previous.ProcessedNativeOrdinal;
        if(updates==0)return new(true,previous,0,null);
        if(updates>1000000)throw new NotSupportedException("Owned calendar catch-up exceeds its bounded ordinary replay contract.");
        long steps=checked(updates*CalendarStepsPerOwnedUpdate),days=0,periods=0,months=0;
        NativeMapSeasonDate date=previous.ThirdDate;
        for(long i=0;i<steps;i++)
        {
            var next=StepCalendarWriter(date);date=next.Date;
            if(next.DayAdvanced)days++;if(next.PeriodAdvanced)periods++;if(next.MonthAdvanced)months++;
        }
        var result=previous with{ThirdDate=date,ProcessedNativeOrdinal=ownedClock.UpdateOrdinal,
            AdvancedCalendarSteps=checked(previous.AdvancedCalendarSteps+steps),
            AdvancedDays=checked(previous.AdvancedDays+days),AdvancedPeriods=checked(previous.AdvancedPeriods+periods),
            AdvancedMonths=checked(previous.AdvancedMonths+months)};
        ValidateOwned(result);return new(true,result,steps,null);
    }

    public static GameState AdvanceCampaign(GameState campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        if(campaign.NativeMapSeason is null)return campaign;
        var result=AdvanceToOwnedClock(campaign.NativeMapSeason,campaign.NativeClock);
        return ReferenceEquals(result.State,campaign.NativeMapSeason)?campaign:campaign with{NativeMapSeason=result.State};
    }

    public static MapSeasonRuntimeFrame PrepareFrame(NativeMapSeasonState state,long explicitFrameNumber)
    {
        ArgumentNullException.ThrowIfNull(state);ValidateOwned(state);
        if(explicitFrameNumber<1)throw new ArgumentOutOfRangeException(nameof(explicitFrameNumber));
        return new(explicitFrameNumber,state,MapSeasonMath.SelectMonth(state.ThirdDate.Month),
            $"owned-native-world-third;{state.SourceScenarioUri}@{state.SourceScenarioSha256};world={state.SourceWorldSha256};"+
            $"third={state.ThirdDate.Year}/{state.ThirdDate.Month}/{state.ThirdDate.Day}:{state.ThirdDate.Hour};"+
            $"processed-native-ordinal={state.ProcessedNativeOrdinal};phase={state.Phase};writer={state.WriterRevision};frame={explicitFrameNumber}");
    }

    /// <summary>Read the actual scenario third date for pre-game rendering.
    /// This does not create or advance a NativeMapSeasonState or NativeClock.</summary>
    public static MapSeasonScenarioPreviewFrame PrepareScenarioPreviewFrame(
        LoadedScenarioWorld source,long explicitFrameNumber)
    {
        ArgumentNullException.ThrowIfNull(source);ValidateSource(source);
        if(explicitFrameNumber<1)throw new ArgumentOutOfRangeException(nameof(explicitFrameNumber));
        if(!Hash(source.Source.Sha256)||!Hash(source.Document.WorldSha256))
            throw new InvalidDataException("Scenario preview requires its actual source and World resource identities.");
        var third=Date(source.Document.RawThirdDate);OrdinaryDate(third);
        return new(explicitFrameNumber,source,MapSeasonMath.SelectMonth(third.Month),
            $"scenario-preview-world-third;{source.Source.ResourceUri}@{source.Source.Sha256};world={source.Document.WorldSha256};"+
            $"third={third.Year}/{third.Month}/{third.Day}:{third.Hour};frame={explicitFrameNumber}");
    }

    /// <summary>Reframe an already loaded preview without reloading, hashing or
    /// copying its scenario source. Source identity is checked at initial load.</summary>
    public static MapSeasonScenarioPreviewFrame PrepareScenarioPreviewFrame(
        MapSeasonScenarioPreviewFrame preview,long explicitFrameNumber)
    {
        ArgumentNullException.ThrowIfNull(preview);ValidateFrame(preview);
        if(explicitFrameNumber<1)throw new ArgumentOutOfRangeException(nameof(explicitFrameNumber));
        var source=preview.Source;var third=preview.ThirdDate;
        return new(explicitFrameNumber,source,preview.Selection,
            $"scenario-preview-world-third;{source.Source.ResourceUri}@{source.Source.Sha256};world={source.Document.WorldSha256};"+
            $"third={third.Year}/{third.Month}/{third.Day}:{third.Hour};frame={explicitFrameNumber}");
    }

    /// <summary>Validate only the concrete calendar's rendering boundary.
    /// Preview source format and identity belong to its initial load.</summary>
    public static void ValidateFrame(MapSeasonCalendarFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        switch(frame)
        {
            case MapSeasonRuntimeFrame live:
                if(PrepareFrame(live.State,live.FrameNumber)!=live)
                    throw new InvalidDataException("Season frame differs from its owned calendar source.");
                break;
            case MapSeasonScenarioPreviewFrame preview:
                if(preview.FrameNumber<1)throw new InvalidDataException("Scenario preview needs a positive rendering frame.");
                var third=preview.ThirdDate;OrdinaryDate(third);
                if(preview.Selection!=MapSeasonMath.SelectMonth(third.Month))
                    throw new InvalidDataException("Scenario preview selection differs from its resource third date.");
                break;
            default:
                throw new NotSupportedException("Rendering requires an owned live calendar or an actual scenario preview date.");
        }
    }

    public static MapSeasonResolvedSourceFrame ResolveScenarioPreviewSourceFrame(
        GameResourceRepository repository,LoadedScenarioWorld source,long explicitFrameNumber)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var frame=PrepareScenarioPreviewFrame(source,explicitFrameNumber);
        var resources=MapSeasonResourceConsumer.Load(repository,frame.ThirdDate.Month);
        if(resources.Selection!=frame.Selection)throw new InvalidDataException("Scenario preview date and resolved source season keys disagree.");
        return new(frame,resources);
    }

    /// <summary>Resolve the actual named season's SceneDatabase and effect
    /// source object on demand. This is a typed submission input; native action
    /// interpolation and final material/light writes are separate consumers.</summary>
    public static MapSeasonResolvedSourceFrame ResolveSourceFrame(GameResourceRepository repository,
        NativeMapSeasonState state,long explicitFrameNumber)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var frame=PrepareFrame(state,explicitFrameNumber);
        var resources=MapSeasonResourceConsumer.Load(repository,state.ThirdDate.Month);
        if(resources.Selection!=frame.Selection)throw new InvalidDataException("Owned calendar and resolved source season keys disagree.");
        return new(frame,resources);
    }

    public static void ValidateOwned(NativeMapSeasonState state)
    {
        ArgumentNullException.ThrowIfNull(state);OrdinaryDate(state.ThirdDate);
        if(state.SecondDate is null||state.WriterRevision!=WriterRevision||state.Phase!=NativeMapSeasonPhase.PostCalendarWriter||
            string.IsNullOrWhiteSpace(state.NativeClockRevision)||string.IsNullOrWhiteSpace(state.SourceScenarioUri)||
            !Hash(state.SourceScenarioSha256)||!Hash(state.SourceWorldSha256)||state.ProcessedNativeOrdinal<0||
            state.ProcessedNativeOrdinal>long.MaxValue/CalendarStepsPerOwnedUpdate||
            state.AdvancedCalendarSteps!=checked(state.ProcessedNativeOrdinal*CalendarStepsPerOwnedUpdate)||
            state.AdvancedDays!=state.ProcessedNativeOrdinal/4||
            state.AdvancedPeriods!=state.ProcessedNativeOrdinal/40||state.AdvancedMonths<0||
            state.ThirdDate.Hour!=state.ProcessedNativeOrdinal%4*6)
            throw new InvalidDataException("Owned source season snapshot has an inconsistent post-writer cursor or provenance.");
    }

    private static void ValidateSource(LoadedScenarioWorld source)
    {
        if(source.Document.RawBytes.Length<43||source.Source.ResourceUri!=GameResourceId.File(source.Source.SourcePath).ToString())
            throw new InvalidDataException("Source season world differs from its complete independent source bytes.");
    }
    private static NativeMapSeasonDate Date(S14ScenarioSourceDate date)=>new(date.RawYear,date.RawMonth,date.RawDay,date.RawSubdayPart);
    private static void OrdinaryDate(NativeMapSeasonDate date)
    {
        ArgumentNullException.ThrowIfNull(date);
        if(date.Month is <1 or >12||date.Day is <1 or >30||date.Hour is <0 or >22||(date.Hour&1)!=0)
            throw new InvalidDataException("Ordinary native World third date is outside its exact signed30-day/two-hour writer range.");
    }
    private static bool Hash(string? hash)=>hash is{Length:64}&&hash.All(Uri.IsHexDigit);
}
