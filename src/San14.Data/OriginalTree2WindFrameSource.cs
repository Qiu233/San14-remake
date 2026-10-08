using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>Original D15260 inputs for the normal PB*Tree2 renderer. Values
/// come from the current View/Main/graphics state, after D12A80 preparation.</summary>
public readonly record struct OriginalTree2WindFrameInputs(
    Vector3 WindDirection, float WindStrength, float WindPhase, float AllWindScale,
    Vector2 InteractionOriginXZ, float InteractionExtent, Vector3 WindOrigin,
    float WindExtent, int WindSelector);

/// <summary>Material fields imported by the original Tree2 material loader.
/// The material's ObjectHeight is distinct from the instance transform scale.</summary>
public readonly record struct OriginalTree2MaterialParameters(
    float FixedHeight, float ObjectHeight, Vector2 ObjectDeform,
    float AnimationSpeed, float BendRate, uint InteractionType);

public readonly record struct OriginalTree2WindResourceSelection(
    int InteractionSide, int WindSelector, int WindPhaseSide);

public sealed record OriginalTree2WindResourceBindings<T>(
    T InteractionVectorT0, T InteractionHeightT1, T? CurrentViewInteractionT2,
    T WindPhaseT3, T WindTextureT8, T WindRippleTextureT9,
    OriginalTree2WindResourceSelection Selection) where T : class;

/// <summary>Actual D01580 owner fields24F0/2500. WindStrength2480 is produced
/// by AB54C0, and its target direction by AB5380, before this frame update.</summary>
public readonly record struct OriginalTree2SceneWindState(
    Vector2 PhasePair, Vector4 SmoothedDirection);

public sealed record OriginalTree2WindCpuResource(
    string Name, uint NativeFormat, uint ResourceFormat, uint ViewFormat,
    int Width, int Height, int Depth, int ElementByteCount,
    uint BindFlags, byte[] InitialBytes)
{
    public int ViewElementCount => InitialBytes.Length / ElementByteCount;
}

public sealed record OriginalTree2InitialWindCpuResources(
    OriginalTree2WindCpuResource[] InteractionVectorSides,
    OriginalTree2WindCpuResource[] InteractionHeightSides,
    OriginalTree2WindCpuResource[] WindPhaseSides,
    OriginalTree2WindCpuResource WindTexture,
    OriginalTree2WindCpuResource WindRippleTexture);

/// <summary>CE normal Tree2 VS parameter setters and required initialization
/// producers. These CPU operations do not synthesize current scene wind,
/// replace the interaction writer, or run the Tree2Wind GPU update.</summary>
public static class OriginalTree2WindFrameSource
{
    public const int VsConstantBufferByteCount = 272;
    public const int InteractionResolution = 512;
    public const int WindResolution = 64;
    public const string SourceEvidence =
        "CE_TC:D12A80/View680+690+694;D15260/Main141C+1438+143C/graphics4D0+4D8+4F0;" +
        "14117A530+14117A490+14117A820;141179660;D20700+14119BCB0;D207F0+141178720;D20880+1411788E0";

    public static OriginalTree2WindFrameInputs ReadCurrentFrame(
        ReadOnlySpan<byte> currentView, ReadOnlySpan<byte> currentMain,
        ReadOnlySpan<byte> currentGraphicsState, uint groupFlags)
    {
        int selector = (int)((groupFlags >> 1) & 15u);
        int windOriginOffset = 0x4F0 + selector * 16;
        return new(new(F(currentView, 0x680), F(currentView, 0x684), F(currentView, 0x688)),
            F(currentView, 0x690), F(currentView, 0x694), F(currentMain, 0x143C),
            new(F(currentGraphicsState, 0x4D0), F(currentGraphicsState, 0x4D8)),
            F(currentMain, 0x141C),
            new(F(currentGraphicsState, windOriginOffset), F(currentGraphicsState, windOriginOffset + 4),
                F(currentGraphicsState, windOriginOffset + 8)), F(currentMain, 0x1438), selector);
    }

    public static OriginalTree2WindResourceSelection ReadCurrentResourceSelection(
        ReadOnlySpan<byte> currentGraphicsState, uint groupFlags)
    {
        int selector = (int)((groupFlags >> 1) & 15u);
        uint interactionSide = U(currentGraphicsState, 0x404);
        uint windSide = U(currentGraphicsState, 0x40C + selector * 4);
        if (interactionSide > 1 || windSide > 1)
            throw new InvalidDataException("Tree2 current ping-pong side exceeds its source pair.");
        return new((int)interactionSide, selector, (int)windSide);
    }

    /// <summary>14117A300/14117A650 ->141179660. The optional t2 comes from
    /// the selected current View +1B58 resource owner +30. The caller retains
    /// its existing resource lifecycle; these are owned resources, not raw pointers.</summary>
    public static OriginalTree2WindResourceBindings<T> BindCurrentResources<T>(
        OriginalTree2WindResourceSelection selection,
        IReadOnlyList<T> interactionVectorSides, IReadOnlyList<T> interactionHeightSides,
        T? selectedCurrentViewInteraction, IReadOnlyList<T> allWindPhasePairs,
        T windTexture, T windRippleTexture) where T : class
    {
        if ((uint)selection.InteractionSide >= 2 || (uint)selection.WindPhaseSide >= 2 ||
            (uint)selection.WindSelector >= 16 || interactionVectorSides.Count < 2 ||
            interactionHeightSides.Count < 2 || allWindPhasePairs.Count < (selection.WindSelector + 1) * 2)
            throw new ArgumentOutOfRangeException(nameof(selection), "Tree2 source resource pair is unavailable.");
        return new(interactionVectorSides[selection.InteractionSide],
            interactionHeightSides[selection.InteractionSide], selectedCurrentViewInteraction,
            allWindPhasePairs[selection.WindSelector * 2 + selection.WindPhaseSide],
            windTexture, windRippleTexture, selection);
    }

    /// <summary>Write the normal Tree2 RDEF variables160..259. Retains the
    /// caller's matrices and padding; current per-material fields are required.</summary>
    public static void WriteVsParameters(Span<byte> actualVsConstantBuffer,
        OriginalTree2WindFrameInputs frame, OriginalTree2MaterialParameters material)
    {
        if (actualVsConstantBuffer.Length < VsConstantBufferByteCount)
            throw new ArgumentException("The original normal Tree2 VS constant buffer requires272 bytes.", nameof(actualVsConstantBuffer));
        float factor = MathF.Sqrt(Mul(Mul(frame.WindStrength, frame.AllWindScale), 0.01f));
        W(actualVsConstantBuffer, 160, frame.AllWindScale);
        W(actualVsConstantBuffer, 164, frame.WindDirection.X);
        W(actualVsConstantBuffer, 168, frame.WindDirection.Y);
        W(actualVsConstantBuffer, 172, frame.WindDirection.Z);
        W(actualVsConstantBuffer, 176, Mul(factor, 0.1f));
        W(actualVsConstantBuffer, 180, frame.WindPhase);
        W(actualVsConstantBuffer, 184, material.FixedHeight);
        W(actualVsConstantBuffer, 188, material.ObjectHeight);
        W(actualVsConstantBuffer, 192, material.ObjectDeform.X);
        W(actualVsConstantBuffer, 196, material.ObjectDeform.Y);
        W(actualVsConstantBuffer, 200, material.AnimationSpeed);
        W(actualVsConstantBuffer, 204, material.BendRate);
        W(actualVsConstantBuffer, 208, frame.InteractionOriginXZ.X);
        W(actualVsConstantBuffer, 212, frame.InteractionOriginXZ.Y);
        float interactionExtent = SpanAtLeastOne(frame.InteractionExtent);
        W(actualVsConstantBuffer, 216, interactionExtent);
        W(actualVsConstantBuffer, 220, interactionExtent);
        BinaryPrimitives.WriteUInt32LittleEndian(actualVsConstantBuffer[224..], InteractionResolution);
        BinaryPrimitives.WriteUInt32LittleEndian(actualVsConstantBuffer[228..], material.InteractionType);
        float windExtent = SpanAtLeastOne(frame.WindExtent);
        W(actualVsConstantBuffer, 232, frame.WindOrigin.X);
        W(actualVsConstantBuffer, 236, windExtent);
        W(actualVsConstantBuffer, 240, frame.WindOrigin.Y);
        W(actualVsConstantBuffer, 244, windExtent);
        W(actualVsConstantBuffer, 248, frame.WindOrigin.Z);
        W(actualVsConstantBuffer, 252, windExtent);
        BinaryPrimitives.WriteUInt32LittleEndian(actualVsConstantBuffer[256..], WindResolution);
    }

    /// <summary>D01580→141179580 and the explicit phase-pair zero store.
    /// This is fresh owner state, before its loaded weather wind is advanced.</summary>
    public static OriginalTree2SceneWindState CreateFreshSceneWindState()
        => new(Vector2.Zero, new Vector4(1, 0, 0, 0));

    /// <summary>D12A80→14117AA30/14117A950. The source AB54C0 generator must
    /// already supply the actual current strength and AB5380 target direction.</summary>
    public static OriginalTree2SceneWindState AdvanceSceneWind(
        OriginalTree2SceneWindState current, Vector4 actualAb5380Direction,
        float actualWindStrength2480, float deltaSeconds3350, float tree2WindScaler)
    {
        Vector2 phase = current.PhasePair;
        float scaled = Mul(Mul(tree2WindScaler, 0.01f), actualWindStrength2480);
        float admitted = scaled < 30f ? (0f <= scaled ? scaled : 0f) : 30f;
        float speed = MathF.Sqrt(admitted);
        if (1f < speed)
        {
            float delta = Sub(speed, 1f);
            speed = Add(Add(delta, delta) / Sub(MathF.Sqrt(30f), 1f), 1f);
        }
        phase.X = Add(Mul(Mul(speed, deltaSeconds3350), 6.2831855f), phase.X);
        if (3600f < phase.X)
        {
            phase.X = Sub(phase.X, 3600f);
            phase.Y = Add(phase.Y, 1f);
        }
        float t = BitConverter.Int32BitsToSingle(0x3C23D70A);
        float retained = Sub(1f, t);
        Vector4 old = current.SmoothedDirection, target = actualAb5380Direction;
        Vector4 direction = new(Add(Mul(t, target.X), Mul(retained, old.X)),
            Add(Mul(t, target.Y), Mul(retained, old.Y)),
            Add(Mul(t, target.Z), Mul(retained, old.Z)),
            Add(Mul(t, target.W), Mul(retained, old.W)));
        float length = MathF.Sqrt(Add(Add(Mul(direction.X, direction.X), Mul(direction.Y, direction.Y)),
            Mul(direction.Z, direction.Z)));
        float reciprocal = 1f / length;
        direction.X = Mul(direction.X, reciprocal);
        direction.Y = Mul(direction.Y, reciprocal);
        direction.Z = Mul(direction.Z, reciprocal);
        return new(phase, direction);
    }

    public static void WritePreparedViewWind(Span<byte> actualView,
        OriginalTree2SceneWindState current, float actualWindStrength2480)
    {
        W(actualView, 0x680, current.SmoothedDirection.X);
        W(actualView, 0x684, current.SmoothedDirection.Y);
        W(actualView, 0x688, current.SmoothedDirection.Z);
        W(actualView, 0x68C, current.SmoothedDirection.W);
        W(actualView, 0x690, actualWindStrength2480);
        W(actualView, 0x694, Add(Mul(current.PhasePair.Y, 3600f), current.PhasePair.X));
    }

    /// <summary>Only actual D20700/D207F0/D20880 initialization contents.
    /// Each wind selector lazily creates its own phase pair. Later interaction
    /// and Tree2Wind writes must use their real current resource generations.</summary>
    public static OriginalTree2InitialWindCpuResources CreateInitialCpuResources()
    {
        int interactionCells = InteractionResolution * InteractionResolution;
        int windCells = WindResolution * WindResolution * WindResolution;
        OriginalTree2WindCpuResource VectorSide(int side)
        {
            byte[] bytes = new byte[interactionCells * 4];
            bytes.AsSpan().Fill(0x7F);
            return new($"InteractionVector[{side}]", 0x24, 62, 62,
                InteractionResolution, InteractionResolution, 1, 1, 0x88, bytes);
        }
        OriginalTree2WindCpuResource HeightSide(int side) => new($"InteractionHeight[{side}]",
            0x1E, 54, 54, InteractionResolution, InteractionResolution, 1, 2, 0x88,
            new byte[interactionCells * 2]);
        OriginalTree2WindCpuResource PhaseSide(int side) => new($"WindPhase[{side}]",
            0x17, 41, 41, WindResolution, WindResolution, WindResolution, 4, 0x88,
            new byte[windCells * 2 * 4]);
        OriginalTree2WindCpuResource Wind(string name)
        {
            byte[] bytes = new byte[windCells * 8];
            for (int i = 0; i < bytes.Length; i += 8)
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i), 0x3C00);
            // 1411788E0→94F520: mipLevels1/nativeFormat3; 953A40 maps
            // native texture3 to typeless9 and float shader view10.
            return new(name, 3, 9, 10, WindResolution, WindResolution, WindResolution,
                8, 0x88, bytes);
        }
        return new([VectorSide(0), VectorSide(1)], [HeightSide(0), HeightSide(1)],
            [PhaseSide(0), PhaseSide(1)], Wind("WindTexture"), Wind("WindRippleTexture"));
    }

    private static float SpanAtLeastOne(float value) => value <= 1f ? 1f : value;
    private static float F(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadSingleLittleEndian(bytes[offset..]);
    private static uint U(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    private static void W(Span<byte> bytes, int offset, float value) => BinaryPrimitives.WriteSingleLittleEndian(bytes[offset..], value);
    private static float Mul(float left, float right) => OriginalNativeFloat.Multiply(left, right);
    private static float Add(float left, float right) => OriginalNativeFloat.Add(left, right);
    private static float Sub(float left, float right) => OriginalNativeFloat.Subtract(left, right);
}
