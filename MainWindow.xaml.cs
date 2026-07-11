using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ProfanityFilterEditor.Models;
using ProfanityFilterEditor.Services;

namespace ProfanityFilterEditor;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<string> _words = new();
    private ICollectionView _view = null!;

    private MinecraftInstance? _instance;
    private WordListService? _service;
    private bool _hasUnsavedChanges;

    public MainWindow()
    {
        InitializeComponent();

        _view = CollectionViewSource.GetDefaultView(_words);
        WordListBox.ItemsSource = _view;
        _words.CollectionChanged += (_, _) => UpdateCountText();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var config = ConfigService.Load();

        MinecraftInstance? instance = null;

        if (!string.IsNullOrWhiteSpace(config.MinecraftInstallPath))
        {
            instance = TryBuildInstanceFromSavedPath(config.MinecraftInstallPath!);
        }

        if (instance == null)
        {
            instance = await ResolveInstanceViaDiscoveryAsync();
        }

        if (instance == null)
        {
            StatusText.Text = "No Minecraft install was selected.";
            SetEditingEnabled(false);
            return;
        }

        SetInstance(instance);
        ConfigService.Save(new Models.AppConfig { MinecraftInstallPath = instance.InstallPath });
        LoadWordListFromDisk();
    }

    private MinecraftInstance? TryBuildInstanceFromSavedPath(string installPath)
    {
        try
        {
            var exePath = Path.Combine(installPath, "Minecraft.Windows.exe");
            if (!File.Exists(exePath)) return null;

            var dataFolder = Path.Combine(installPath, "data");
            var wordListPath = Path.Combine(dataFolder, "profanity_filter.wlist");
            return new MinecraftInstance(installPath, exePath, dataFolder, wordListPath);
        }
        catch
        {
            return null;
        }
    }

    private async Task<MinecraftInstance?> ResolveInstanceViaDiscoveryAsync()
    {
        StatusText.Text = "Looking for Minecraft on this PC...";

        var instances = await Task.Run(MinecraftLocator.FindInstances);

        if (instances.Count == 0)
        {
            MessageBox.Show(this,
                "Couldn't find any Minecraft.Windows.exe on this PC.\n\n" +
                "Make sure Minecraft Bedrock is installed, then use \"Change install\" to try again.",
                "Minecraft not found", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        if (instances.Count == 1)
        {
            return instances[0];
        }

        var picker = new SelectInstanceWindow(instances) { Owner = this };
        var result = picker.ShowDialog();
        return result == true ? picker.SelectedInstance : null;
    }

    private void SetInstance(MinecraftInstance instance)
    {
        _instance = instance;
        InstancePathText.Text = instance.InstallPath;
        RefreshFilterState();
        SetEditingEnabled(true);
    }

    private void RefreshFilterState()
    {
        if (_instance == null) return;

        var state = FilterToggleService.GetState(_instance);
        var activePath = FilterToggleService.GetActivePath(_instance);

        _service = activePath != null ? new WordListService(activePath) : null;

        switch (state)
        {
            case FilterState.EnabledInData:
                FilterStateText.Text = "Enabled";
                ToggleFilterButton.Content = "DISABLE";
                ToggleFilterButton.Background = (Brush)FindResource("DangerStoneBrush");
                ToggleFilterButton.IsEnabled = true;
                break;

            case FilterState.DisabledInRoot:
                FilterStateText.Text = "Disabled";
                ToggleFilterButton.Content = "ENABLE";
                ToggleFilterButton.Background = (Brush)FindResource("PlayGreenBrush");
                ToggleFilterButton.IsEnabled = true;
                break;

            default:
                FilterStateText.Text = "profanity_filter.wlist not found";
                ToggleFilterButton.Content = "N/A";
                ToggleFilterButton.Background = (Brush)FindResource("StoneMidBrush");
                ToggleFilterButton.IsEnabled = false;
                break;
        }
    }

    private void ToggleFilter_Click(object sender, RoutedEventArgs e)
    {
        if (_instance == null) return;

        if (_hasUnsavedChanges)
        {
            var choice = MessageBox.Show(this,
                "You have unsaved changes to the word list. Toggling will move the file as-is; " +
                "save first if you want those changes kept. Continue toggling anyway?",
                "Unsaved changes", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (choice != MessageBoxResult.Yes) return;
        }

        try
        {
            var newState = FilterToggleService.Toggle(_instance);
            RefreshFilterState();

            StatusText.Text = newState == FilterState.DisabledInRoot
                ? "Filter disabled - file moved out of data\\, Minecraft won't load it."
                : "Filter enabled - file moved back into data\\.";

            LoadWordListFromDisk();
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show(this,
                "Windows blocked moving that file. Try running this app as Administrator, " +
                "or take ownership of the Minecraft install folder first.",
                "Access denied", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Couldn't toggle the filter: {ex.Message}", "Toggle failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadWordListFromDisk()
    {
        if (_instance == null) return;

        if (_service == null)
        {
            StatusText.Text = $"No profanity_filter.wlist found in either location under:\n{_instance.InstallPath}";
            _words.Clear();
            SetEditingEnabled(false);
            return;
        }

        try
        {
            var words = _service.Load();
            _words.Clear();
            foreach (var w in words) _words.Add(w);

            StatusText.Text = $"Loaded {words.Count} word(s) from {_service.FilePath} - detected format: {DescribeFormat(_service.DetectedFormat)}";
            _hasUnsavedChanges = false;
            SetEditingEnabled(true);
        }
        catch (UnauthorizedAccessException)
        {
            StatusText.Text = "Access denied reading the file. Try running this app as Administrator.";
            SetEditingEnabled(false);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Couldn't read the word list: {ex.Message}";
            SetEditingEnabled(false);
        }
    }

    private static string DescribeFormat(WordListFormat format) => format switch
    {
        WordListFormat.WholeFileBase64 => "whole-file Base64",
        WordListFormat.PerLineBase64 => "per-line Base64",
        _ => "plain text",
    };

    private void SetEditingEnabled(bool enabled)
    {
        NewWordBox.IsEnabled = enabled;
        SearchBox.IsEnabled = enabled;
        WordListBox.IsEnabled = enabled;
        SaveButton.IsEnabled = enabled;
    }

    private void UpdateCountText()
    {
        CountText.Text = $"{_words.Count} word(s)" + (_hasUnsavedChanges ? " - unsaved changes" : "");
    }

    // --- Add / remove -------------------------------------------------

    private void AddWord_Click(object sender, RoutedEventArgs e) => AddWordFromInput();

    private void NewWordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) AddWordFromInput();
    }

    private void AddWordFromInput()
    {
        var word = NewWordBox.Text.Trim();
        if (word.Length == 0) return;

        if (_words.Any(w => string.Equals(w, word, StringComparison.OrdinalIgnoreCase)))
        {
            StatusText.Text = $"\"{word}\" is already in the list.";
            NewWordBox.Clear();
            return;
        }

        _words.Add(word);
        NewWordBox.Clear();
        _hasUnsavedChanges = true;
        UpdateCountText();
    }

    private void RemoveWord_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string word })
        {
            _words.Remove(word);
            _hasUnsavedChanges = true;
            UpdateCountText();
        }
    }

    // --- Search ---------------------------------------------------------

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchPlaceholder.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        var filter = SearchBox.Text.Trim();
        _view.Filter = filter.Length == 0
            ? null
            : obj => obj is string s && s.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private void NewWordBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        NewWordPlaceholder.Visibility = NewWordBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // --- Save / change instance ------------------------------------------

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_service == null) return;

        try
        {
            _service.Save(_words);
            _hasUnsavedChanges = false;
            StatusText.Text = $"Saved {_words.Count} word(s) to {_service.FilePath} (backup kept as .bak).";
            UpdateCountText();
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show(this,
                "Windows blocked writing to that file. Try running this app as Administrator, " +
                "or take ownership of the Minecraft install folder first.",
                "Access denied", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Couldn't save: {ex.Message}", "Save failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ChangeInstance_Click(object sender, RoutedEventArgs e)
    {
        if (_hasUnsavedChanges)
        {
            var choice = MessageBox.Show(this,
                "You have unsaved changes. Switch installs anyway?",
                "Unsaved changes", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (choice != MessageBoxResult.Yes) return;
        }

        var instance = await ResolveInstanceViaDiscoveryAsync();
        if (instance == null) return;

        SetInstance(instance);
        ConfigService.Save(new Models.AppConfig { MinecraftInstallPath = instance.InstallPath });
        LoadWordListFromDisk();
    }
}
