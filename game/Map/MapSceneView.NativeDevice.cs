public partial class MapSceneView
{
    private D3D11RenderSession.Native11DeviceOwner? applicationNativeMapDevice;

    internal void SetApplicationNativeMapDevice(D3D11RenderSession.Native11DeviceOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (applicationNativeMapDevice is { } current && !ReferenceEquals(current, owner))
            throw new InvalidOperationException("A map's live Native11 device generation cannot be replaced in place.");
        applicationNativeMapDevice = owner;
    }

    private D3D11RenderSession.Native11DeviceOwner RequireApplicationNativeMapDevice() =>
        applicationNativeMapDevice ?? throw new InvalidOperationException(
            "The application Native11 device must finish startup before opening a map.");
}
