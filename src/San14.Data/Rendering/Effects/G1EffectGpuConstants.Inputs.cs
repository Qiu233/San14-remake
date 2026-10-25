using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>The live viewport, render target and selected texture rectangle.
/// These dimensions are independent even when the source chooses whole-texture UVs.</summary>
public sealed record G1EffectGpuViewport(int X, int Y, int Width, int Height,
    int TargetWidth, int TargetHeight, int TextureWidth, int TextureHeight,
    int TextureX, int TextureY, int TextureRectangleWidth, int TextureRectangleHeight,
    bool WholeTexture)
{
    public Vector4 Prepare()
    {
        if (TargetWidth <= 0 || TargetHeight <= 0 || TextureWidth <= 0 || TextureHeight <= 0)
            throw new InvalidDataException("Effect viewport requires its actual target and texture extents.");
        Vector4 source = WholeTexture ? new(.5f, .5f, -.5f, .5f) :
            new((Width * .5f) / TargetWidth, (X + Width * .5f) / TargetWidth,
                (Height * -.5f) / TargetHeight, (Y + Height * .5f) / TargetHeight);
        float iw = 1f / TextureWidth, ih = 1f / TextureHeight;
        return new(source.X * TextureRectangleWidth * iw,
            (TextureX + TextureRectangleWidth * source.Y + 0f) * iw,
            source.Z * TextureRectangleHeight * ih,
            (TextureY + TextureRectangleHeight * source.W + 0f) * ih);
    }
}

/// <summary>Current GPU camera values. WorldToProjection is the actual D3D
/// projection product, not the far-normalized effect culling projection.</summary>
public sealed record G1EffectGpuCameraConstants(Matrix4x4 WorldToProjection,
    Matrix4x4 ProjectionToView, Vector4 Eye, float Near, float Far, bool ReverseDepth,
    int DepthSamplingMode, bool ZeroToOneProjection, bool RemapViewportDepth,
    float MinimumViewportDepth, float MaximumViewportDepth,
    Vector4? DestinationPort, Vector4? SourcePort)
{
    public Vector4 PrepareDepthDot()
    {
        float first = ReverseDepth ? Far : Near, second = ReverseDepth ? Near : Far;
        float inverseSecond = 1f / second, difference = second - first,
            inverseFirst = 1f / first, inverseDifference = inverseSecond - inverseFirst;
        if (RemapViewportDepth)
        {
            float scale = 1f / (MaximumViewportDepth - MinimumViewportDepth);
            difference *= scale; inverseDifference *= scale;
            inverseFirst *= MinimumViewportDepth * difference * inverseSecond + 1f;
        }
        return DepthSamplingMode switch
        {
            1 or 2 => new(inverseDifference * 255f * .00390625f,
                inverseDifference * 255f * 1.5258789e-05f,
                inverseDifference * 256f * 5.9604645e-08f, inverseFirst),
            3 or 4 => new(inverseDifference, 0, 0, inverseFirst),
            5 or 6 => new(difference * 255f * .00390625f,
                difference * 255f * 1.5258789e-05f,
                difference * 256f * 5.9604645e-08f, inverseSecond),
            7 or 8 => new(difference, 0, 0, inverseSecond),
            // The original default dispatch explicitly clears this parameter.
            _ => Vector4.Zero
        };
    }
    public float PrepareDepthBias() => ZeroToOneProjection ? Far / (Far - Near) : (Near + Far) / (Far - Near);

    public static G1EffectGpuCameraConstants FromCurrentMatrices(Matrix4x4 worldToView,
        Matrix4x4 projection, Vector4 eye, float near, float far, bool reverseDepth,
        int depthSamplingMode, bool zeroToOneProjection, bool remapViewportDepth,
        float minimumViewportDepth, float maximumViewportDepth,
        Vector4? destinationPort, Vector4? sourcePort)
    {
        if (!Matrix4x4.Invert(projection, out Matrix4x4 inverse))
            throw new InvalidDataException("Effect GPU projection is singular.");
        return new(G1EffectDistribution.Multiply(worldToView, projection), inverse, eye, near, far,
            reverseDepth, depthSamplingMode, zeroToOneProjection, remapViewportDepth,
            minimumViewportDepth, maximumViewportDepth, destinationPort, sourcePort);
    }
}

public sealed record G1EffectGpuFieldConstant(int ApplyType, Vector4 Frequency);
public sealed record G1EffectGpuEffectConstants(bool AlphaTestEnabled, byte AlphaReference,
    Vector2 SaturationFade, bool SaturationFadeEnabled, Vector2 FadeDistance, int DebugParticleId,
    float? MotionVectorScale, IReadOnlyList<G1EffectGpuFieldConstant>? Fields)
{
    /// <summary>Only after creating the actual fresh Effect2 owner and native
    /// parameters, before any current View override changes these named fields.</summary>
    public static G1EffectGpuEffectConstants CreateFreshOwned(G1EffectDrawPacket packet,
        bool alphaTestEnabled, IReadOnlyList<G1EffectGpuFieldConstant>? currentFields = null) =>
        new(alphaTestEnabled, packet.AlphaReference, new(.25f, 0), false, new(100, 0), 0, null,
            (packet.Flags & 0x400) == 0 ? DisabledFields : currentFields);

    public G1EffectGpuEffectConstants ForPacket(G1EffectDrawPacket packet, bool alphaTestEnabled,
        IReadOnlyList<G1EffectGpuFieldConstant>? currentFields) => this with
        { AlphaTestEnabled = alphaTestEnabled, AlphaReference = packet.AlphaReference,
            Fields = (packet.Flags & 0x400) == 0 ? DisabledFields : currentFields };

    /// <summary>The real row-field table consumed by the mesh callback. Function
    /// type zero carries the frequency; other function types explicitly write zero.</summary>
    public static IReadOnlyList<G1EffectGpuFieldConstant> ReadFields(ReadOnlyMemory<byte> table)
    {
        int I(int at)
        {
            if (at < 0 || at > table.Length - 4) throw new InvalidDataException("Effect field table reference escapes its resource.");
            return BinaryPrimitives.ReadInt32LittleEndian(table.Span[at..]);
        }
        float F(int at) => BitConverter.Int32BitsToSingle(I(at));
        int count = I(8), start = checked(0xC + I(0xC));
        if (count < 0) throw new InvalidDataException("Effect field table has a negative count.");
        var result = new G1EffectGpuFieldConstant[Math.Min(count, 4)];
        for (int index = 0; index < result.Length; index++)
        {
            int slot = checked(start + index * 4), at = checked(slot + I(slot));
            Vector4 frequency = I(at + 4) == 0 ? new(F(at + 0x10), F(at + 0x14), F(at + 0x18), F(at + 0x1C)) : Vector4.Zero;
            result[index] = new(I(at + 8), frequency);
        }
        return Array.AsReadOnly(result);
    }

    private static readonly IReadOnlyList<G1EffectGpuFieldConstant> DisabledFields =
        Array.AsReadOnly(new[] { new G1EffectGpuFieldConstant(0, default), new(0, default), new(0, default), new(0, default) });
}

/// <summary>The current selected packed-light Flex color is independent from
/// Effect2's own white color multiplier. Scene groups are actual prepared owners.</summary>
public sealed record G1EffectGpuLightConstants(Vector4 FlexColor, DeferredSceneInputs Scene);
public sealed record G1EffectGpuPatternConstants(Matrix4x4 WorldToPattern, float Pattern,
    IReadOnlyList<Vector4>? PatternPlaneSub = null);
public sealed record G1EffectGpuShadowConstants(D3D11PrimaryForwardShadowFields Current,
    IReadOnlyList<bool>? ShadowModes = null, Vector4? SecondaryScale = null);

public sealed record G1EffectGpuConstantInputs(G1EffectGpuCameraConstants? Camera = null,
    G1EffectGpuEffectConstants? Effect = null, G1EffectGpuLightConstants? Light = null,
    G1EffectGpuPatternConstants? Pattern = null, G1EffectGpuShadowConstants? Shadow = null,
    DeferredClusterInputs? Cluster = null,
    Func<G1EffectGpuConstantRequest, ReadOnlyMemory<byte>?>? AdditionalProvider = null);
