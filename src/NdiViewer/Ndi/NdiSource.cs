namespace NdiViewer.Ndi;

/// <summary>
/// A snapshot of an NDI source discovered on the network. Copied out of unmanaged
/// memory immediately, since the native buffers backing an NDIlib_source_t are only
/// valid until the next call into the finder.
/// </summary>
public sealed record NdiSource(string Name, string? UrlAddress)
{
    public override string ToString() => Name;
}
