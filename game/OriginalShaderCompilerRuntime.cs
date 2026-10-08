using Godot;
using San14.Data;

/// <summary>Resolves the separate Windows compiler library without depending on
/// the working directory or extracting native code from a resource pack.</summary>
public static class OriginalShaderCompilerRuntime
{
    public const string LibraryName = "libvkd3d-shader-1.dll";
    public const string VerifiedLibrarySha256 = "37885422AD210365C8FFCDA9F40A7F321842139698C6E1E6134DB20BA5D65780";

    public static Vkd3dShaderCompiler Open()
    {
        string directory = OS.HasFeature("editor")
            ? Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "runtime", "windows-x64"))
            : Path.Combine(Path.GetDirectoryName(OS.GetExecutablePath())!, "native", "windows-x64");
        var compiler = new Vkd3dShaderCompiler(Path.Combine(directory, LibraryName));
        if (compiler.LibrarySha256 != VerifiedLibrarySha256)
        {
            compiler.Dispose();
            throw new InvalidDataException("Bundled shader compiler differs from the verified Windows build.");
        }
        return compiler;
    }
}
