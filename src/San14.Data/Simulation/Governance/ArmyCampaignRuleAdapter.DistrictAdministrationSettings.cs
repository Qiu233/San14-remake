using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private static readonly int[] DistrictMilitaryMissionIds = [0, 1, 3, 4, 5, 6, 7];

    internal static DistrictSettings ReadDistrictAdministrationSettingsCurrent(SourceWorldContext current, int district)
    {
        byte Read(int offset) => DistrictNeed(current.ReadDistrictRawByte(district, offset), "settings_" + offset.ToString("X"));
        int policy = Read(0x17), kind = Read(0x19), identity = Read(0x18);
        DistrictTarget? target = kind switch
        {
            0xFF => null,
            //226DC0 writes DWORD18=FF000000: the unused target kind19
            //is known zero, while policy17 starts at1. Static2374A0 only
            //overlays10..13. Policies without a target do not turn that
            //constructor value into a target entity or an unavailable page.
            0 when policy is 0 or 1 or 4 => null,
            7 => new(DistrictTargetKind.Force, identity),
            2 => new(DistrictTargetKind.Foothold, identity),
            8 => new(DistrictTargetKind.District, identity),
            _ => throw DistrictMissing("settings_target_kind")
        };
        byte permissions = Read(0x1B);
        uint construction = (uint)(Read(0x1C) | Read(0x1D) << 8 | Read(0x1E) << 16 | Read(0x1F) << 24);
        ushort military = (ushort)(Read(0x28) | Read(0x29) << 8);
        return new(policy, target, Read(0x1A), (permissions & 4) != 0,
            (permissions & 8) != 0, (permissions & 16) != 0, (permissions & 32) != 0,
            (permissions & 64) != 0,
            Enumerable.Range(0, 31).Where(id => (construction & (1u << id)) != 0).ToImmutableHashSet(),
            new(Read(0x20), Read(0x21), Read(0x22)),
            new((military & 0x100) != 0, DistrictMilitaryMissionIds.Where(id => (military & (1 << id)) != 0).ToImmutableHashSet()))
            { Demolition = (permissions & 0x80) != 0 };
    }

    //73F460 and73E850 share these writes. Unnamed basic bits0/1 and the
    //construction word's unused high bit remain owned by their saved window.
    private static void WriteDistrictAdministrationSettingsCurrent(SourceWorldContext current, int district,
        DistrictSettings settings, ArmyReturnDistrictOperationQueries operation, bool newDistrict = false)
    {
        void Write(int offset, int value) => current.SetDistrictRawByte(district, offset, checked((byte)value));
        Write(0x17, Math.Clamp(settings.PolicyId, 0, 5));
        Write(0x18, Math.Clamp(settings.Target?.Id ?? 0, 0, 255));
        Write(0x19, settings.Target?.Kind switch
        { DistrictTargetKind.Force => 7, DistrictTargetKind.Foothold => 2, DistrictTargetKind.District => 8, _ => 0xFF });
        Write(0x1A, Math.Clamp(settings.PriorityId, 0, 4));
        byte permissions = DistrictNeed(current.ReadDistrictRawByte(district, 0x1B), "write_permissions");
        permissions = newDistrict ? (byte)3 : (byte)(permissions & 3); //761FB0 initial two unexposed permissions.
        if (settings.PolicyId == 5) permissions |= 2; //7666A0's explicit Policy5 store.
        if (settings.RecruitOfficers) permissions |= 4;
        if (settings.Attack) permissions |= 8;
        if (settings.Transport) permissions |= 16;
        if (settings.RecruitTroops) permissions |= 32;
        if (settings.Merchant) permissions |= 64;
        if (settings.Demolition) permissions |= 128;
        Write(0x1B, permissions);
        byte high = DistrictNeed(current.ReadDistrictRawByte(district, 0x1F), "write_construction_high");
        uint allowed = settings.PolicyId == 1 ? 0x7FFFFFFFu : 0;
        if (settings.PolicyId != 1)
            foreach (int id in settings.AllowedFacilityIds)
                if (id is >= 0 and < 31) allowed |= 1u << id;
        allowed |= (uint)(high & 0x80) << 24;
        for (int index = 0; index < 4; index++) Write(0x1C + index, (byte)(allowed >> (index * 8)));
        Write(0x20, Math.Clamp(settings.Resources.GoldUsageId, 0, 2));
        Write(0x21, Math.Clamp(settings.Resources.FoodUsageId, 0, 2));
        Write(0x22, Math.Clamp(settings.Resources.TroopsUsageId, 0, 2));
        ushort before = (ushort)(DistrictNeed(current.ReadDistrictRawByte(district, 0x28), "write_military_low") |
            DistrictNeed(current.ReadDistrictRawByte(district, 0x29), "write_military_high") << 8);
        ushort military = (ushort)(before & 0xFE00);
        if (settings.Military.Enabled)
        {
            military |= 0x100;
            foreach (int id in DistrictMilitaryMissionIds)
                if (settings.PolicyId == 1 ||
                    (id == 0 || settings.PolicyId is 2 or 3) && settings.Military.AllowedMissionIds.Contains(id))
                    military |= (ushort)(1 << id);
        }
        Write(0x28, (byte)military); Write(0x29, (byte)(military >> 8));
        operation.TouchedDistricts.Add(district);
    }

    internal static int ReadDistrictAdministrationIntelligenceCurrent(SourceWorldContext current, int district)
    {
        uint result = 0;
        for (int index = 0; index < 4; index++)
            result |= (uint)DistrictNeed(current.ReadDistrictRawByte(district, 0x24 + index), "stock24") << (index * 8);
        return unchecked((int)result);
    }

    private static void WriteDistrictAdministrationIntelligenceCurrent(SourceWorldContext current, int district, int value)
    {
        for (int index = 0; index < 4; index++)
            current.SetDistrictRawByte(district, 0x24 + index, unchecked((byte)(value >> (index * 8))));
    }

    internal static int DistrictAdministrationIntelligenceCapacity(GameState state, SourceWorldContext current, int district)
    {
        int force = DistrictNeed(current.ReadDistrictForceId(district), "capacity_Force");
        int rank = DistrictNeed(current.ReadMoraleWaveForceRank47(force), "capacity_Rank47");
        return rank switch { 1 or 2 or 3 or 10 or 11 or 12 => 200000, 4 or 5 => 150000, 6 or 7 => 100000, _ => 50000 };
    }

    private static T DistrictNeed<T>(T? value, string field) where T : struct => value ?? throw DistrictMissing(field);
    private static InvalidDataException DistrictMissing(string field) => new("district.administration." + field);
}
