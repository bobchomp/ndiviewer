using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace NdiViewer.Ndi;

/// <summary>
/// Continuously scans the local network for NDI sources on a background thread and
/// raises <see cref="SourcesChanged"/> with the current source list. Callers are
/// responsible for marshaling the event back to the UI thread.
/// </summary>
public sealed class NdiFinder : IDisposable
{
    private readonly IntPtr _handle;
    private readonly Thread _thread;
    private volatile bool _running;

    public event Action<IReadOnlyList<NdiSource>>? SourcesChanged;

    public NdiFinder()
    {
        var createSettings = new NdiInterop.NDIlib_find_create_t
        {
            show_local_sources = true,
            p_groups = IntPtr.Zero,
            p_extra_ips = IntPtr.Zero,
        };

        _handle = NdiInterop.NDIlib_find_create_v2(ref createSettings);
        if (_handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create the NDI find instance.");
        }

        _running = true;
        _thread = new Thread(PollLoop) { IsBackground = true, Name = "NdiFinder" };
        _thread.Start();
    }

    private void PollLoop()
    {
        while (_running)
        {
            // Blocks for up to 1s, returning true only if the source list actually
            // changed - skip the refresh entirely otherwise to avoid needless UI
            // churn (list rebuilds, selection loss) on a static network.
            bool changed = NdiInterop.NDIlib_find_wait_for_sources(_handle, 1000);

            if (!_running)
            {
                break;
            }

            if (!changed)
            {
                continue;
            }

            try
            {
                SourcesChanged?.Invoke(ReadCurrentSources());
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    private List<NdiSource> ReadCurrentSources()
    {
        IntPtr arrayPtr = NdiInterop.NDIlib_find_get_current_sources(_handle, out uint count);
        var result = new List<NdiSource>((int)count);

        if (arrayPtr == IntPtr.Zero)
        {
            return result;
        }

        int structSize = Marshal.SizeOf<NdiInterop.NDIlib_source_t>();
        for (int i = 0; i < count; i++)
        {
            IntPtr itemPtr = IntPtr.Add(arrayPtr, i * structSize);
            var native = Marshal.PtrToStructure<NdiInterop.NDIlib_source_t>(itemPtr);
            string name = Utf8Marshal.PtrToString(native.p_ndi_name) ?? "(unnamed source)";
            string? url = Utf8Marshal.PtrToString(native.p_url_address);
            result.Add(new NdiSource(name, url));
        }

        return result;
    }

    public void Dispose()
    {
        if (!_running)
        {
            return;
        }

        _running = false;

        // See the equivalent comment in NdiReceiver.Dispose: only destroy the native
        // instance once we're certain the poll thread has left its P/Invoke call,
        // to avoid a use-after-free race.
        if (_thread.Join(5000))
        {
            NdiInterop.NDIlib_find_destroy(_handle);
        }
    }
}
