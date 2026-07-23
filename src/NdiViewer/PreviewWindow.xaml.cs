using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NdiViewer.Ndi;

namespace NdiViewer;

public partial class PreviewWindow : Window
{
    private readonly NdiReceiver _receiver;
    private WriteableBitmap? _bitmap;
    private int _bitmapWidth;
    private int _bitmapHeight;

    public PreviewWindow(NdiSource source)
    {
        InitializeComponent();
        Title = $"NDI Viewer - {source.Name}";

        _receiver = new NdiReceiver(source);
        _receiver.FrameReceived += OnFrameReceived;
        _receiver.ConnectionError += OnConnectionError;

        Closed += (_, _) => _receiver.Dispose();
    }

    private void OnFrameReceived(NdiVideoFrame frame)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_bitmap is null || frame.Width != _bitmapWidth || frame.Height != _bitmapHeight)
            {
                // 32bpp, no-alpha pixel format: NDI's BGRX/BGRA delivery always has
                // valid B/G/R bytes in this byte order, and ignoring the 4th byte
                // sidesteps undefined alpha in BGRX frames.
                _bitmap = new WriteableBitmap(frame.Width, frame.Height, 96, 96, PixelFormats.Bgr32, null);
                VideoImage.Source = _bitmap;
                _bitmapWidth = frame.Width;
                _bitmapHeight = frame.Height;
            }

            var rect = new Int32Rect(0, 0, frame.Width, frame.Height);
            _bitmap.WritePixels(rect, frame.Pixels, frame.Stride, 0);

            OverlayText.Visibility = Visibility.Collapsed;
            InfoText.Text = frame.FrameRate > 0
                ? $"{frame.Width}x{frame.Height} @ {frame.FrameRate:0.##} fps"
                : $"{frame.Width}x{frame.Height}";
        });
    }

    private void OnConnectionError(string message)
    {
        Dispatcher.BeginInvoke(() =>
        {
            OverlayText.Text = $"Connection lost: {message}";
            OverlayText.Visibility = Visibility.Visible;
        });
    }
}
