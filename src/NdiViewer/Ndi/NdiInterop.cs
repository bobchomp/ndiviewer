using System;
using System.Runtime.InteropServices;

namespace NdiViewer.Ndi;

/// <summary>
/// Raw P/Invoke bindings for the parts of the NDI SDK's C ABI (Processing.NDI.Lib.x64.dll)
/// used by this application. Struct layouts and function signatures mirror the public
/// NDI SDK headers (Processing.NDI.Find.h / Processing.NDI.Recv.h / Processing.NDI.structs.h).
/// The NDI runtime DLL is loaded dynamically at runtime, not linked at build time -
/// see <see cref="NdiRuntimeLocator"/>.
/// </summary>
internal static class NdiInterop
{
    private const string NdiDll = "Processing.NDI.Lib.x64.dll";

    [StructLayout(LayoutKind.Sequential)]
    internal struct NDIlib_source_t
    {
        public IntPtr p_ndi_name;
        public IntPtr p_url_address;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NDIlib_find_create_t
    {
        [MarshalAs(UnmanagedType.U1)]
        public bool show_local_sources;
        public IntPtr p_groups;
        public IntPtr p_extra_ips;
    }

    internal enum NDIlib_recv_color_format_e
    {
        BGRX_BGRA = 0,
        UYVY_BGRA = 1,
        RGBX_RGBA = 2,
        UYVY_RGBA = 3,
        fastest = 100,
        best = 101,
    }

    internal enum NDIlib_recv_bandwidth_e
    {
        metadata_only = -10,
        audio_only = 10,
        lowest = 0,
        highest = 100,
    }

    internal enum NDIlib_frame_type_e
    {
        none = 0,
        video = 1,
        audio = 2,
        metadata = 3,
        error = 4,
        status_change = 100,
        source_change = 101,
    }

    internal enum NDIlib_frame_format_type_e
    {
        interleaved = 0,
        progressive = 1,
        field_0 = 2,
        field_1 = 3,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NDIlib_recv_create_v3_t
    {
        public NDIlib_source_t source_to_connect_to;
        public NDIlib_recv_color_format_e color_format;
        public NDIlib_recv_bandwidth_e bandwidth;
        [MarshalAs(UnmanagedType.U1)]
        public bool allow_video_fields;
        public IntPtr p_ndi_recv_name;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NDIlib_video_frame_v2_t
    {
        public int xres;
        public int yres;
        public int FourCC;
        public int frame_rate_N;
        public int frame_rate_D;
        public float picture_aspect_ratio;
        public NDIlib_frame_format_type_e frame_format_type;
        public long timecode;
        public IntPtr p_data;
        public int line_stride_in_bytes;
        public IntPtr p_metadata;
        public long timestamp;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NDIlib_audio_frame_v2_t
    {
        public int sample_rate;
        public int no_channels;
        public int no_samples;
        public long timecode;
        public IntPtr p_data;
        public int channel_stride_in_bytes;
        public IntPtr p_metadata;
        public long timestamp;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NDIlib_metadata_frame_t
    {
        public int length;
        public long timecode;
        public IntPtr p_data;
    }

    [DllImport(NdiDll, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool NDIlib_initialize();

    [DllImport(NdiDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void NDIlib_destroy();

    [DllImport(NdiDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr NDIlib_find_create_v2(ref NDIlib_find_create_t p_create_settings);

    [DllImport(NdiDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void NDIlib_find_destroy(IntPtr p_instance);

    [DllImport(NdiDll, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool NDIlib_find_wait_for_sources(IntPtr p_instance, uint timeout_in_ms);

    [DllImport(NdiDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr NDIlib_find_get_current_sources(IntPtr p_instance, out uint p_no_sources);

    [DllImport(NdiDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr NDIlib_recv_create_v3(ref NDIlib_recv_create_v3_t p_create_settings);

    [DllImport(NdiDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void NDIlib_recv_destroy(IntPtr p_instance);

    [DllImport(NdiDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern NDIlib_frame_type_e NDIlib_recv_capture_v2(
        IntPtr p_instance,
        ref NDIlib_video_frame_v2_t p_video_data,
        IntPtr p_audio_data,
        IntPtr p_metadata,
        uint timeout_in_ms);

    [DllImport(NdiDll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void NDIlib_recv_free_video_v2(IntPtr p_instance, ref NDIlib_video_frame_v2_t p_video_data);
}
