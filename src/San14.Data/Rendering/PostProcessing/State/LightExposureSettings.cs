using System.Numerics;

namespace San14.Data;

/// <summary>Published light parameters consumed by exposure adaptation.</summary>
public readonly record struct LightExposureSettings(byte Type, byte Flags, Vector3 BaselineRgb);
