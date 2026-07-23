using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace NdiViewer.Ndi;

public sealed class NdiVideoFrame
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int Stride { get; init; }
    public required byte[] Pixels { get; init; }
    public double FrameRate { get; init; }
}

/// <summary>
/// Connects to a single NDI source and streams decoded video frames from a background
/// capture thread. Audio and metadata are intentionally not received - this app is a
/// video preview only.
/// </summary>
public sealed class NdiReceiver : IDisposable
{
    private readonly IntPtr _handle;
    private readonly Thread _thread;
    private volatile bool _running;

    public event Action<NdiVideoFrame>? FrameReceived;
    public event Action<string>? ConnectionError;

    public NdiReceiver(NdiSource source)
    {
        IntPtr namePtr = Utf8Marshal.StringToUtf8Ptr(source.Name);
        IntPtr urlPtr = Utf8Marshal.StringToUtf8Ptr(source.UrlAddress);
        IntPtr recvNamePtr = Utf8Marshal.StringToUtf8Ptr("NDI Viewer");

        try
        {
            var createSettings = new NdiInterop.NDIlib_recv_create_v3_t
            {
                source_to_connect_to = new NdiInterop.NDIlib_source_t
                {
                    p_ndi_name = namePtr,
                    p_url_address = urlPtr,
                },
                // Always deliver 8-bit interleaved BGRA/BGRX so we can hand raw bytes
                // straight to a WPF WriteableBitmap without a YUV conversion step.
                color_format = NdiInterop.NDIlib_recv_color_format_e.BGRX_BGRA,
                bandwidth = NdiInterop.NDIlib_recv_bandwidth_e.highest,
                allow_video_fields = false,
                p_ndi_recv_name = recvNamePtr,
            };

            _handle = NdiInterop.NDIlib_recv_create_v3(ref createSettings);
        }
        finally
        {
            Utf8Marshal.Free(namePtr);
            Utf8Marshal.Free(urlPtr);
            Utf8Marshal.Free(recvNamePtr);
        }

        if (_handle == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Failed to connect to NDI source '{source.Name}'.");
        }

        _running = true;
        _thread = new Thread(CaptureLoop) { IsBackground = true, Name = "NdiReceiver" };
        _thread.Start();
    }

    private void CaptureLoop()
    {
        while (_running)
        {
            var videoFrame = new NdiInterop.NDIlib_video_frame_v2_t();
            NdiInterop.NDIlib_frame_type_e frameType;

            try
            {
                frameType = NdiInterop.NDIlib_recv_capture_v2(_handle, ref videoFrame, IntPtr.Zero, IntPtr.Zero, 1000);
            }
            catch (Exception ex)
            {
                ConnectionError?.Invoke(ex.Message);
                break;
            }

            if (!_running)
            {
                break;
            }

            if (frameType != NdiInterop.NDIlib_frame_type_e.video || videoFrame.p_data == IntPtr.Zero)
            {
                continue;
            }

            try
            {
                int stride = videoFrame.line_stride_in_bytes > 0
                    ? videoFrame.line_stride_in_bytes
                    : videoFrame.xres * 4;
                int byteCount = stride * videoFrame.yres;
                if (byteCount <= 0)
                {
                    continue;
                }

                var pixels = new byte[byteCount];
                Marshal.Copy(videoFrame.p_data, pixels, 0, byteCount);

                double frameRate = videoFrame.frame_rate_D > 0
                    ? (double)videoFrame.frame_rate_N / videoFrame.frame_rate_D
                    : 0;

                FrameReceived?.Invoke(new NdiVideoFrame
                {
                    Width = videoFrame.xres,
                    Height = videoFrame.yres,
                    Stride = stride,
                    Pixels = pixels,
                    FrameRate = frameRate,
                });
            }
            finally
            {
                NdiInterop.NDIlib_recv_free_video_v2(_handle, ref videoFrame);
            }
        }
    }

    public void Dispose()
    {
        if (!_running)
        {
            return;
        }

        _running = false;
        _thread.Join(2000);
        NdiInterop.NDIlib_recv_destroy(_handle);
    }
}
