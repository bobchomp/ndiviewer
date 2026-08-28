using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using NdiViewer.Ndi;
using NdiViewer.Update;

namespace NdiViewer;

public partial class App : Application
{
    private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromHours(4);

    private bool _ndiInitialized;
    private Timer? _updateCheckTimer;
    private volatile bool _updatePromptShowing;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Backstop so an unexpected exception on the UI thread (e.g. in the update
        // flow) shows an error instead of silently freezing the app.
        DispatcherUnhandledException += OnDispatcherUnhandledException;

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

        StartUpdateChecks();
    }

    private void StartUpdateChecks()
    {
        _ = CheckForUpdatesAsync();

        _updateCheckTimer = new Timer(
            _ => _ = CheckForUpdatesAsync(),
            null,
            UpdateCheckInterval,
            UpdateCheckInterval);
    }

    private async Task CheckForUpdatesAsync()
    {
        if (_updatePromptShowing)
        {
            return;
        }

        UpdateInfo? info = await UpdateChecker.CheckForUpdateAsync();
        if (info is null)
        {
            return;
        }

        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        await Dispatcher.InvokeAsync(() => ShowUpdatePrompt(info));
    }

    private void ShowUpdatePrompt(UpdateInfo info)
    {
        if (_updatePromptShowing)
        {
            return;
        }

        _updatePromptShowing = true;

        // Block every other open window (main + any preview windows) so the app is
        // unusable until the update is installed - this is a mandatory update prompt.
        // Wrapped in try/finally: if constructing or showing UpdateWindow throws for
        // any reason, we must still re-enable those windows and clear the flag,
        // otherwise the app is left permanently frozen with no way to recover and no
        // future update check will ever run again.
        var otherWindows = Windows.OfType<Window>().ToList();
        foreach (Window window in otherWindows)
        {
            window.IsEnabled = false;
        }

        try
        {
            var updateWindow = new UpdateWindow(info) { Owner = MainWindow };
            updateWindow.ShowDialog();
        }
        finally
        {
            // Reached once the dialog closes (normally only via a successful update,
            // which shuts the whole app down anyway) or if showing it failed outright.
            foreach (Window window in otherWindows)
            {
                window.IsEnabled = true;
            }

            _updatePromptShowing = false;
        }
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"NDI Viewer hit an unexpected error and needs to close it:\n\n{e.Exception.Message}",
            "Unexpected Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
        Shutdown(1);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _updateCheckTimer?.Dispose();

        if (_ndiInitialized)
        {
            NdiInterop.NDIlib_destroy();
        }

        base.OnExit(e);
    }
}
