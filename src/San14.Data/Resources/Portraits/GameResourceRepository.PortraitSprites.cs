using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class GameResourceRepository
{
    public AreaAdministrationQuery<bool> ReadPortraitResourceAdmission(int appearance)
    {
        if (appearance is >= 7000 and <= 7999)
            return AreaAdministrationQuery<bool>.Known(LoadUserPortraits().IsRegistered(appearance));
        return LoadPortraitContent().ReadAdmission(appearance);
    }

    internal string? UserPortraitResourceIssue(int appearance)
    {
        if (!LoadUserPortraits().IsRegistered(appearance))
            return "officer-editor.portrait.user_not_registered:" + appearance;
        foreach (int bank in new[] { 9, 10, 11 })
        {
            string relative = UserPortraitRegistration.RelativePath(appearance - 7000, bank);
            if (!LoadUserPortraits().HasResource(appearance, bank)) return "officer-editor.portrait.user_resource:" + relative;
        }
        return null;
    }

    public ArmyMapPortraitNames ResolveUserFacePortrait(int appearance)
    {
        if (!LoadUserPortraits().IsRegistered(appearance))
            throw new InvalidDataException("officer-editor.portrait.user_not_registered:" + appearance);
        return new(appearance, appearance,
            GameResourceId.File(UserPortraitRegistration.RelativePath(appearance - 7000, 10)).ToString(), "portrait");
    }

    public DeploymentNarrationPortraitRoute ResolveUserStandingPortrait(int appearance)
    {
        if (!LoadUserPortraits().IsRegistered(appearance))
            throw new InvalidDataException("officer-editor.portrait.user_not_registered:" + appearance);
        //841F00 binds all three authored bank9 targets.8AA0C0 kind1 ignores
        //the part selector, so each target uses the same M image and UV.
        return new(checked((ushort)appearance), appearance,
            GameResourceId.File(UserPortraitRegistration.RelativePath(appearance - 7000, 9)).ToString(),
            ["portrait", "portrait", "portrait"]);
    }

    internal bool ContainsUserPortraitResource(string resourceName)
    {
        return TryUserPortraitResource(resourceName, out _, out int bank, out int appearance)
            && LoadUserPortraits().HasResource(appearance, bank);
    }

    internal static bool TryUserPortraitResource(string resourceName, out string relative,
        out int bank, out int appearance)
    {
        relative = ""; bank = 0; appearance = 0;
        if (!resourceName.StartsWith("san14://file/ADD_FACE/", StringComparison.Ordinal)) return false;
        var id = GameResourceId.Parse(resourceName);
        string[] parts = id.Key.Split('/');
        if (parts.Length != 3) throw new InvalidDataException("officer.portrait.user_resource_identity");
        bank = parts[1] switch { "EDIT_KAO_M" => 9, "EDIT_KAO_S" => 10, "EDIT_KAO_L" => 11,
            _ => throw new InvalidDataException("officer.portrait.user_resource_bank") };
        string prefix = parts[1] + "_";
        const string suffix = ".s14.zp";
        if (!parts[2].StartsWith(prefix, StringComparison.Ordinal) || !parts[2].EndsWith(suffix, StringComparison.Ordinal)
            || !int.TryParse(parts[2].AsSpan(prefix.Length, parts[2].Length - prefix.Length - suffix.Length),
                System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int slot)
            || slot is < 0 or >= 1000 || id.Key != UserPortraitRegistration.RelativePath(slot, bank))
            throw new InvalidDataException("officer.portrait.user_resource_slot");
        relative = id.Key; appearance = slot + 7000;
        return true;
    }

    internal LoadedSprite LoadUserPortraitSprite(string resourceName, string spriteName)
    {
        if (!TryUserPortraitResource(resourceName, out string relative, out int bank, out int appearance)
            || spriteName != "portrait") throw new InvalidDataException("officer.portrait.user_resource_identity");
        if (!LoadUserPortraits().IsRegistered(appearance))
            throw new InvalidDataException("officer.portrait.user_not_registered:" + appearance);
        var source = ReadUserPortraitFile(relative);
        //8AC8E0 uses308B90's zp1 decompression then9525F0/955C20's GT1G
        //texture loader. The first image is the source8AA0C0 binding.
        byte[] decoded = Zp1Reader.Read(source.Bytes).DecodedPayload;
        RgbaTexture pixels = G1TextureReader.Decode(decoded);
        string hash = Convert.ToHexString(SHA256.HashData(decoded));
        var texture = new LoadedTexture(pixels, source.Provenance.PhysicalPath, 0, hash, source.Provenance)
            { OverrideSource = source.Provenance.IsOverride ? source.Provenance : null,
                SourceImage = G1TextureContainerReader.Read(decoded).Textures[0] };
        //8AA0C0's type1 bank9/10/11 UV constants. Padded texels are never
        //silently stretched across the normal Face04 or standing geometry.
        float right = bank switch { 9 => 636f / 1024f, 10 => 242f / 256f, _ => 1f };
        float bottom = bank == 9 ? 900f / 1024f : 1f;
        var definition = new SpriteDefinition(spriteName, 0, new(0, 0), new(right, 0),
            new(0, bottom), new(right, bottom), 0);
        _ = definition.GetRectangle(pixels.Width, pixels.Height);
        var asset = new LoadedRdbAsset(decoded, source.Provenance.ResourceUri, source.Provenance.PhysicalPath,
            0, hash, source.Provenance, source.Provenance.IsOverride ? source.Provenance : null);
        return new(definition, texture, asset);
    }
}
