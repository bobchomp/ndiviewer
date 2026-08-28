using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace NdiViewer.Update;

/// <summary>
/// A mandatory update prompt: the only way out is clicking Update, which downloads
/// the new installer, launches it, and shuts the app down. The window refuses to
/// close any other way (X button, Alt+F4) so the app can't be used on a stale version.
/// </summary>
public partial class UpdateWindow : Window
{
    private readonly UpdateInfo _updateInfo;
    private bool _allowClose;

    internal UpdateWindow(UpdateInfo updateInfo)
    {
        InitializeComponent();
        _updateInfo = updateInfo;
        MessageText.Text = $"A newer version of NDI Viewer (v{updateInfo.Version}) is available. Please update now.";
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
        }
    }

    private async void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateButton.IsEnabled = false;
        StatusText.Visibility = Visibility.Visible;
        ProgressBarControl.Visibility = Visibility.Visible;
        StatusText.Text = "Downloading update...";

        string tempPath = Path.Combine(Path.GetTempPath(), _updateInfo.FileName);

        try
        {
            await DownloadAsync(_updateInfo.DownloadUrl, tempPath);

            StatusText.Text = "Starting installer...";
            Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });

            _allowClose = true;
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            TryDeletePartialDownload(tempPath);

            StatusText.Text = $"Update failed: {ex.Message}\nClick Update to try again.";
            ProgressBarControl.Visibility = Visibility.Collapsed;
            UpdateButton.IsEnabled = true;
        }
    }

    private static void TryDeletePartialDownload(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup only - a locked/undeletable leftover file isn't
            // worth failing the retry over; File.Create on the next attempt will
            // surface any real problem with the path.
        }
    }

    private async Task DownloadAsync(string url, string destinationPath)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("NdiViewer-Updater");

        using HttpResponseMessage response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        long? totalBytes = response.Content.Headers.ContentLength;

        await using Stream httpStream = await response.Content.ReadAsStreamAsync();
        await using FileStream fileStream = File.Create(destinationPath);

        var buffer = new byte[81920];
        long totalRead = 0;
        int read;
        while ((read = await httpStream.ReadAsync(buffer)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read));
            totalRead += read;

            if (totalBytes is > 0)
            {
                double percent = (double)totalRead / totalBytes.Value * 100;
                ProgressBarControl.Value = percent;
                StatusText.Text = $"Downloading update... {percent:0}%";
            }
        }
    }
}
