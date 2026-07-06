using System;
using System.Runtime.InteropServices;

namespace Lattice.Renderer.Text.FreeType;

internal static class FreeTypeNative
{
    public const int LoadRender = 0x4;

    private const string Lib = "freetype";

    static FreeTypeNative() => NativeLibrary.SetDllImportResolver(typeof(FreeTypeNative).Assembly, Resolve);

    [DllImport(Lib)]
    public static extern int FT_Init_FreeType(out IntPtr library);

    [DllImport(Lib)]
    public static extern int FT_Done_FreeType(IntPtr library);

    [DllImport(Lib)]
    public static extern int FT_New_Face(IntPtr library, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, CLong faceIndex, out IntPtr face);

    [DllImport(Lib)]
    public static extern int FT_Done_Face(IntPtr face);

    [DllImport(Lib)]
    public static extern int FT_Set_Pixel_Sizes(IntPtr face, uint pixelWidth, uint pixelHeight);

    [DllImport(Lib)]
    public static extern int FT_Load_Char(IntPtr face, CULong charCode, int loadFlags);

    private static IntPtr Resolve(string name, System.Reflection.Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (name != Lib)
        {
            return IntPtr.Zero;
        }

        foreach (string candidate in Candidates())
        {
            if (NativeLibrary.TryLoad(candidate, out IntPtr handle))
            {
                return handle;
            }
        }

        return IntPtr.Zero;
    }

    private static string[] Candidates()
    {
        string? overridePath = Environment.GetEnvironmentVariable("LATTICE_FREETYPE");
        return new[]
        {
            overridePath ?? string.Empty,
            "/opt/homebrew/lib/libfreetype.6.dylib",
            "/opt/homebrew/lib/libfreetype.dylib",
            "/usr/local/lib/libfreetype.6.dylib",
            "/opt/X11/lib/libfreetype.6.dylib",
            "libfreetype.6.dylib",
            "libfreetype.so.6",
            "libfreetype.so",
            "freetype6.dll",
            "freetype.dll",
        };
    }
}
