using San14.Data.Formats;

namespace San14.Data;

/// <summary>Current district affiliation and leadership. Missing input remains unknown.</summary>
public sealed class DistrictIdentityState
{
    private readonly Dictionary<int, Identity> _identities = [];
    private sealed record Identity(byte? ForceId = null, byte? Number = null,
        byte? LeaderLow = null, byte? LeaderHigh = null)
    {
        public ushort? LeaderId => LeaderLow is byte low && LeaderHigh is byte high
            ? (ushort)(low | high << 8) : null;
    }

    public byte? ReadForceId(int districtId) => Get(districtId).ForceId;
    public byte? ReadNumber(int districtId) => Get(districtId).Number;
    public ushort? ReadLeaderId(int districtId) => Get(districtId).LeaderId;

    public void SetAffiliation(int districtId, byte? forceId, byte? number) =>
        _identities[districtId] = Get(districtId) with { ForceId = forceId, Number = number };

    public void SetLeaderId(int districtId, ushort leaderId) => _identities[districtId] = Get(districtId)
        with { LeaderLow = (byte)leaderId, LeaderHigh = (byte)(leaderId >> 8) };

    public bool? IsActive(int districtId)
    {
        if (districtId == 0) return false;
        Identity identity = Get(districtId);
        if (identity.ForceId is not byte force) return null;
        if (force == 0) return false;
        if (identity.Number is not byte number) return null;
        if (number == 0) return false;
        return identity.LeaderId is ushort leader ? leader != 0 : null;
    }

    internal byte? ReadSourceField(int districtId, DistrictIdentitySourceField field)
    {
        Identity identity = Get(districtId);
        return field switch
        {
            DistrictIdentitySourceField.Force => identity.ForceId,
            DistrictIdentitySourceField.Number => identity.Number,
            DistrictIdentitySourceField.LeaderLow => identity.LeaderLow,
            DistrictIdentitySourceField.LeaderHigh => identity.LeaderHigh,
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };
    }

    internal void SetSourceField(int districtId, DistrictIdentitySourceField field, byte? value)
    {
        Identity identity = Get(districtId);
        _identities[districtId] = field switch
        {
            DistrictIdentitySourceField.Force => identity with { ForceId = value },
            DistrictIdentitySourceField.Number => identity with { Number = value },
            DistrictIdentitySourceField.LeaderLow => identity with { LeaderLow = value },
            DistrictIdentitySourceField.LeaderHigh => identity with { LeaderHigh = value },
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };
    }

    internal void CopyTo(DistrictIdentityState target)
    { foreach (var identity in _identities) target._identities.Add(identity.Key, identity.Value); }

    private Identity Get(int districtId) => _identities.GetValueOrDefault(districtId) ?? new();
}
