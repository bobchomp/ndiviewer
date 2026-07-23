using System;
using System.Runtime.InteropServices;
using System.Text;

namespace NdiViewer.Ndi;

/// <summary>
/// The NDI SDK uses UTF-8 C strings. Marshal.StringToHGlobalAnsi uses the system's
/// default ANSI code page instead, which can mangle non-ASCII source names, so we
/// encode/allocate UTF-8 buffers ourselves.
/// </summary>
internal static class Utf8Marshal
{
    public static IntPtr StringToUtf8Ptr(string? value)
    {
        if (value is null)
        {
            return IntPtr.Zero;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(value);
        IntPtr ptr = Marshal.AllocHGlobal(bytes.Length + 1);
        Marshal.Copy(bytes, 0, ptr, bytes.Length);
        Marshal.WriteByte(ptr, bytes.Length, 0);
        return ptr;
    }

    public static void Free(IntPtr ptr)
    {
        if (ptr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    public static string? PtrToString(IntPtr ptr)
    {
        return ptr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(ptr);
    }
}
