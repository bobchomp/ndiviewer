using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using NdiViewer.Ndi;

namespace NdiViewer;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<NdiSource> _sources = new();
    private readonly NdiFinder _finder;
    private readonly Dictionary<string, PreviewWindow> _openPreviews = new();

    public MainWindow()
    {
        InitializeComponent();
        SourcesListBox.ItemsSource = _sources;

        _finder = new NdiFinder();
        _finder.SourcesChanged += OnSourcesChanged;

        Closed += (_, _) => _finder.Dispose();
    }

    private void OnSourcesChanged(IReadOnlyList<NdiSource> sources)
    {
        Dispatcher.BeginInvoke(() =>
        {
            string? previouslySelected = (SourcesListBox.SelectedItem as NdiSource)?.Name;

            _sources.Clear();
            foreach (var source in sources.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
            {
                _sources.Add(source);
            }

            if (previouslySelected is not null)
            {
                SourcesListBox.SelectedItem = _sources.FirstOrDefault(s => s.Name == previouslySelected);
            }

            StatusText.Text = _sources.Count switch
            {
                0 => "No NDI sources found yet - still scanning...",
                1 => "1 source found",
                _ => $"{_sources.Count} sources found",
            };
        });
    }

    private void SourcesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        OpenButton.IsEnabled = SourcesListBox.SelectedItem is not null;
    }

    private void SourcesListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (SourcesListBox.SelectedItem is NdiSource source)
        {
            OpenPreview(source);
        }
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        if (SourcesListBox.SelectedItem is NdiSource source)
        {
            OpenPreview(source);
        }
    }

    private void OpenPreview(NdiSource source)
    {
        if (_openPreviews.TryGetValue(source.Name, out PreviewWindow? existing))
        {
            existing.Activate();
            return;
        }

        PreviewWindow window;
        try
        {
            window = new PreviewWindow(source);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not open '{source.Name}':\n{ex.Message}", "Connection Failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        window.Closed += (_, _) => _openPreviews.Remove(source.Name);
        _openPreviews[source.Name] = window;
        window.Show();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
