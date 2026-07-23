using System;
using System.IO;
using System.Runtime.InteropServices;

namespace NdiViewer.Ndi;

/// <summary>
/// Locates the NDI Runtime on the local machine before any P/Invoke call touches
/// Processing.NDI.Lib.x64.dll. The runtime is a separate, freely redistributable
/// install (see https://ndi.video and NDI's Software Distribution guide) that our
/// installer offers to install via winget - it is intentionally not bundled inside
/// this application's own files.
/// </summary>
internal static class NdiRuntimeLocator
{
    private const string DllName = "Processing.NDI.Lib.x64.dll";

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetDllDirectory(string? lpPathName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

    [DllImport("kernel32.dll")]
    private static extern bool FreeLibrary(IntPtr hModule);

    private static readonly string[] EnvVarsToCheck =
    {
        "NDI_RUNTIME_DIR_V6",
        "NDI_RUNTIME_DIR_V5",
        "NDI_RUNTIME_DIR_V4",
        "NDI_RUNTIME_DIR",
    };

    private static readonly string[] FallbackDirectories =
    {
        @"NDI\NDI 6 Runtime\Bin\x64",
        @"NDI\NDI 5 Runtime\Bin\x64",
        @"NDI\NDI 6 Tools\Bin",
        @"NDI\NDI 5 Tools\Bin",
    };

    /// <summary>
    /// Widens the process DLL search path with any known NDI runtime install
    /// locations, then verifies the runtime DLL can actually be loaded.
    /// </summary>
    public static bool TryPrepare(out string? searchedPathsDescription)
    {
        foreach (string envVar in EnvVarsToCheck)
        {
            string? dir = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                SetDllDirectory(dir);
                if (CanLoad())
                {
                    searchedPathsDescription = dir;
                    return true;
                }
            }
        }

        foreach (string programFilesRoot in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                 })
        {
            foreach (string relative in FallbackDirectories)
            {
                string candidate = Path.Combine(programFilesRoot, relative);
                if (Directory.Exists(candidate))
                {
                    SetDllDirectory(candidate);
                    if (CanLoad())
                    {
                        searchedPathsDescription = candidate;
                        return true;
                    }
                }
            }
        }

        // Last resort: maybe it's already resolvable via PATH or the app directory.
        if (CanLoad())
        {
            searchedPathsDescription = "PATH";
            return true;
        }

        searchedPathsDescription = null;
        return false;
    }

    private static bool CanLoad()
    {
        IntPtr handle = LoadLibraryEx(DllName, IntPtr.Zero, 0);
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        FreeLibrary(handle);
        return true;
    }
}
