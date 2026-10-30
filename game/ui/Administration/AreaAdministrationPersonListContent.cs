using San14.Core;
using San14.Data;

internal static class AreaAdministrationPersonListContent
{
    internal static IReadOnlyList<CityDomesticPersonEntry> Create(GameResourceRepository repository,
        PreviewText text, GameState world, AreaAdministrationPersonPage<AreaAdministrationPersonBasicRow> page)
    {
        var f=new CityDomesticPersonListContent.Formatting(repository,text,world);
        return page.Rows.Select(r=>f.Entry(r.Occurrence,r.Candidate,r.Name,r.RowState,
            new[]{f.Name(r.Name)}.Concat(r.Abilities.Select(a=>f.Number(a)))
                .Concat(r.Responsibilities.Select(a=>f.Responsibility(a))).Concat(new[]{f.Mark(r.AreaLocation),
                    Development(r,0),Development(r,1),Development(r,2),Occupation(r)}).ToArray(),includesName:true)).ToArray();
        string Pair(AreaAdministrationQuery<int?> normal,AreaAdministrationQuery<int?> full)
        {
            if(!normal.Available||!full.Available)return text.Get("detail_unknown");
            if(normal.Value is not int n||full.Value is not int v)return text.Get("detail_unknown");
            return n==v?f.PatternNumber(normal,0x038C):$"+{n}({n-v})";
        }
        string Development(AreaAdministrationPersonBasicRow row,int kind)
        {
            if(!row.Development.Available)return text.Get("detail_unknown");
            var d=row.Development.Value!;
            return kind switch{0=>Pair(d.Normal.CommerceIncrease,d.FullEffort.CommerceIncrease),
                1=>Pair(d.Normal.AgricultureIncrease,d.FullEffort.AgricultureIncrease),
                _=>Pair(d.Normal.BarracksIncrease,d.FullEffort.BarracksIncrease)};
        }
        string Occupation(AreaAdministrationPersonBasicRow row)=>!row.Development.Available?text.Get("detail_unknown")
            :Pair(row.Occupation,row.Development.Value!.CandidateFullEffortOccupation);
    }
    internal static IReadOnlyList<CityDomesticPersonEntry> Create(GameResourceRepository repository,
        PreviewText text, GameState world, AreaAdministrationPersonPage<AreaAdministrationPersonTraitRow> page)
    {
        var f=new CityDomesticPersonListContent.Formatting(repository,text,world);
        return page.Rows.Select(r=>f.Entry(r.Occurrence,r.Candidate,r.Name,r.RowState,r.Traits.Select(t=>f.Mark(t)).ToArray())).ToArray();
    }
    internal static IReadOnlyList<CityDomesticPersonEntry> Create(GameResourceRepository repository,
        PreviewText text, GameState world, AreaAdministrationPersonPage<CityDomesticPersonAffiliationRow> page)
    {
        var f = new CityDomesticPersonListContent.Formatting(repository, text, world);
        return page.Rows.Select(r => f.Entry(r.Occurrence, r.Candidate, r.Name, r.RowState,
            new DeploymentPersonCell[]
            {f.Force(r.ForceName), new(f.PatternNumber(r.DistrictNumber, 0x0494)), new(f.Text(r.HomeName, nullIsNotApplicable: true)),
             new(f.Text(r.CurrentLocationName)), new(f.Status(r.ServiceStatus)), new(f.Responsibility(r.PrimaryResponsibility)),
             new(f.Number(r.Loyalty, nullIsNotApplicable: true)), new(f.Text(r.OfficeName, nullIsNotApplicable: true)),
             new(f.Number(r.OtherMetric, nullIsNotApplicable: true)), new(f.Number(r.ServiceYears, nullIsNotApplicable: true))})).ToArray();
    }

    internal static IReadOnlyList<CityDomesticPersonEntry> Create(GameResourceRepository repository,
        PreviewText text, GameState world, AreaAdministrationPersonPage<CityDomesticPersonAbilityRow> page)
    {
        var f = new CityDomesticPersonListContent.Formatting(repository, text, world);
        return page.Rows.Select(r => f.Entry(r.Occurrence, r.Candidate, r.Name, r.RowState,
            [f.Status(r.ServiceStatus), f.Number(r.Leadership), f.Number(r.Strength), f.Number(r.Intelligence),
             f.Number(r.Politics), f.Number(r.Charm), f.Health(r.HealthCode), f.Number(r.ItemCount)])).ToArray();
    }

    internal static IReadOnlyList<CityDomesticPersonEntry> Create(GameResourceRepository repository,
        PreviewText text, GameState world, AreaAdministrationPersonPage<CityDomesticPersonMeritRow> page)
    {
        var f = new CityDomesticPersonListContent.Formatting(repository, text, world);
        return page.Rows.Select(r => f.Entry(r.Occurrence, r.Candidate, r.Name, r.RowState,
            [f.Text(r.RankName, nullIsNotApplicable: true), f.Number(r.Merit, nullIsNotApplicable: true), f.PatternNumber(r.CommandBonus, 0x038C),
             f.PatternNumber(r.AdditionalAreaCapacity, 0x038C), f.PatternNumber(r.CreditKind3Bonus, 0x038C)])).ToArray();
    }

    internal static IReadOnlyList<CityDomesticPersonEntry> Create(GameResourceRepository repository,
        PreviewText text, GameState world, AreaAdministrationPersonPage<CityDomesticPersonTaskRow> page)
    {
        var f = new CityDomesticPersonListContent.Formatting(repository, text, world);
        return page.Rows.Select(r => f.Entry(r.Occurrence, r.Candidate, r.Name, r.RowState,
            [f.Task(r.Task), f.Text(r.TargetName), f.Remaining(r.Remaining), f.Returning(r.Returning),
             f.Number(r.ResetValue)])).ToArray();
    }

    internal static IReadOnlyList<CityDomesticPersonEntry> Create(GameResourceRepository repository,
        PreviewText text, GameState world, AreaAdministrationPersonPage<CityDomesticPersonPersonalRow> page)
    {
        var f = new CityDomesticPersonListContent.Formatting(repository, text, world);
        return page.Rows.Select(r => f.Entry(r.Occurrence, r.Candidate, r.Name, r.RowState,
            [f.Sex(r.SexCode), f.Number(r.AgeValue, nullIsNotApplicable: true), f.Text(r.DoctrineName, nullIsNotApplicable: true), f.Text(r.FatherName),
             f.Text(r.MotherName), f.Text(r.PartnerName), f.Text(r.SwornName)])).ToArray();
    }

}
