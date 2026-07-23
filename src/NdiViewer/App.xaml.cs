using System;
using System.Windows;
using NdiViewer.Ndi;

namespace NdiViewer;

public partial class App : Application
{
    private bool _ndiInitialized;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!NdiRuntimeLocator.TryPrepare(out _))
        {
            MessageBox.Show(
                "The NDI Runtime was not found on this computer.\n\n" +
                "NDI Viewer needs the free NDI Runtime to discover and receive NDI video. " +
                "Re-run the NDI Viewer installer and allow it to install the runtime, or " +
                "install it yourself from https://ndi.link/NDIRedistV6, then restart NDI Viewer.",
                "NDI Runtime Not Found",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Shutdown(1);
            return;
        }

        if (!NdiInterop.NDIlib_initialize())
        {
            MessageBox.Show(
                "Failed to initialize the NDI runtime. Your CPU may not meet NDI's minimum " +
                "requirements (SSE4.1 support is required).",
                "NDI Initialization Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        _ndiInitialized = true;

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ndiInitialized)
        {
            NdiInterop.NDIlib_destroy();
        }

        base.OnExit(e);
    }
}
