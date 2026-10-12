namespace San14.Data;

/// <summary>Source-table celestial colors followed by the common retained transition.</summary>
public static class SkySeasonPreparedCameraRgb
{
    public const int PrefixByteLength = PreparedCameraLighting.PrefixByteLength;

    /// <summary>Explicit encoded-camera compatibility entry point.</summary>
    public static PreparedCameraLightingPacket PrepareWithSourceTransmittanceRgb(
        CameraLightingController controller, SkylightParameters resolvedParameters,
        ReadOnlySpan<byte> priorPrefix, float frameDelta,
        SkySeasonTransmittanceSource transmittanceSource,
        NativeCameraTangentDispatch dispatch, float currentColorTemperature21C)
    {
        if (priorPrefix.Length != PrefixByteLength)
            throw new InvalidDataException("Prepared Camera lighting requires an explicit complete prior prefix.");
        var state = PrepareLighting(controller, resolvedParameters, SkyLightingEncoding.Decode(priorPrefix),
            frameDelta, transmittanceSource, dispatch, currentColorTemperature21C);
        return new(priorPrefix, state, clearAbsentTableFog:transmittanceSource.Tables is null);
    }

    /// <summary>Normal preparation consumes retained values, never encoded transition storage.</summary>
    public static SkyLightingState PrepareLighting(CameraLightingController controller,
        SkylightParameters resolvedParameters, SkyLightingState prior, float frameDelta,
        SkySeasonTransmittanceSource transmittanceSource, NativeCameraTangentDispatch dispatch,
        float currentColorTemperature21C)
    {
        ArgumentNullException.ThrowIfNull(controller); ArgumentNullException.ThrowIfNull(resolvedParameters);
        SkyLightingTransition.ValidateInputs(controller, prior, frameDelta);
        var directions = SkylightDirectionMath.Calculate(resolvedParameters);
        if(transmittanceSource.Tables is null)
        {
            if(controller.DeclaredTransmittanceTable!=0)
                throw new InvalidDataException("Absent atmosphere must come from a zero declared reference.");
            var colors=SkylightColorMath.Prepare(controller.SunHueSaturation,controller.MoonHueSaturation);
            return SkyLightingTransition.Prepare(controller,directions,colors,colors,prior,frameDelta);
        }
        if (BitConverter.SingleToUInt32Bits(currentColorTemperature21C) != 0x45CB2000)
            throw new NotSupportedException("Present-table Camera RGB requires the supported6500K color-temperature ratio branch; other temperatures require the Blackbody producer.");
        var products = SkySeasonTransmittance.PrepareBaseColors(controller, resolvedParameters,
            directions, transmittanceSource, dispatch);
        return SkyLightingTransition.Prepare(controller, directions, products.UnattenuatedBaseColors,
            products.TransmittedColors, prior, frameDelta);
    }
}
