using System;

namespace NdiViewer.Update;

internal sealed record UpdateInfo(Version Version, string DownloadUrl, string FileName);
