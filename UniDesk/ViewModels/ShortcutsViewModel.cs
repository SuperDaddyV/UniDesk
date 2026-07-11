using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using UniDesk.Helpers;
using UniDesk.Models;
using UniDesk.Services;

namespace UniDesk.ViewModels;

public partial class ShortcutsViewModel : ObservableObject
{
    private readonly IShortcutService _shortcutService;
    private readonly ISettingsService _settingsService;
    private readonly INotificationService _notificationService;
    private readonly ILocalizationService _localizationService;
    private int _loadGeneration;
    private int? _shortcutLimitPreview;
    private List<ShortcutItem> _allShortcuts = [];

    [ObservableProperty]
    private bool _isEditingShortcuts;

    [ObservableProperty]
    private bool _isShortcutDropTargetActive;

    [ObservableProperty]
    private ObservableCollection<ShortcutItem> _shortcuts = [];

    public ObservableCollection<object> ShortcutDisplayEntries { get; } = [];
    public bool HasShortcuts => _allShortcuts.Count > 0;

    [ObservableProperty]
    private bool _isShortcutAddMenuOpen;

    [ObservableProperty]
    private bool _isSystemAppMenuOpen;

    public IReadOnlyList<SystemAppShortcut> SystemApps => SystemAppCatalog.Apps;

    public ShortcutsViewModel(
        IShortcutService shortcutService,
        ISettingsService settingsService,
        INotificationService notificationService,
        ILocalizationService localizationService)
    {
        _shortcutService = shortcutService;
        _settingsService = settingsService;
        _notificationService = notificationService;
        _localizationService = localizationService;
    }

    [RelayCommand]
    private async Task LaunchShortcutAsync(ShortcutItem? shortcut)
    {
        if (shortcut == null || IsEditingShortcuts) return;
        await _shortcutService.LaunchShortcutAsync(shortcut.Id);
    }

    public Task LaunchSearchResultAsync(int shortcutId) =>
        _shortcutService.LaunchShortcutAsync(shortcutId);
    
    [RelayCommand]
    private void ToggleShortcutEdit()
    {
        IsEditingShortcuts = !IsEditingShortcuts;
        if (!IsEditingShortcuts)
        {
            IsShortcutAddMenuOpen = false;
            IsSystemAppMenuOpen = false;
        }
    }
    
    partial void OnIsEditingShortcutsChanged(bool value)
    {
        if (!value)
        {
            IsShortcutAddMenuOpen = false;
            IsSystemAppMenuOpen = false;
        }
    
        NotifyShortcutOrderCommandsCanExecuteChanged();
        RefreshShortcutDisplayEntries();
    }
    
    [RelayCommand]
    private void OpenShortcutAddMenu()
    {
        IsSystemAppMenuOpen = false;
        IsShortcutAddMenuOpen = !IsShortcutAddMenuOpen;
    }
    
    [RelayCommand]
    private void CloseShortcutAddMenus()
    {
        IsShortcutAddMenuOpen = false;
        IsSystemAppMenuOpen = false;
    }
    
    [RelayCommand]
    private void OpenSystemAppMenu()
    {
        IsSystemAppMenuOpen = true;
    }
    
    [RelayCommand]
    private void BackToShortcutAddMenu()
    {
        IsSystemAppMenuOpen = false;
    }
    
    [RelayCommand]
    private void AddShortcutFromFile()
    {
        CloseShortcutAddMenus();
    
        var path = ShortcutPickDialogHelper.PickFile();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }
    
        _ = AddFromPathsAsync([path]);
    }
    
    [RelayCommand]
    private void AddShortcutFromFolder()
    {
        CloseShortcutAddMenus();
    
        var folderDialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = L("Shortcut.SelectFolder")
        };
    
        if (folderDialog.ShowDialog() != true)
        {
            return;
        }
    
        _ = AddFromPathsAsync([folderDialog.FolderName]);
    }
    
    [RelayCommand]
    private void AddShortcutFromSystemApp(SystemAppShortcut? app)
    {
        if (app == null)
        {
            return;
        }
    
        CloseShortcutAddMenus();
        _ = CreateShortcutAndReloadAsync(new ShortcutItem
        {
            Name = app.Name,
            Path = app.Path,
            LaunchArguments = app.LaunchArguments,
            IconLookupPath = app.IconLookupPath ?? app.Path,
            BundledIconFileName = app.BundledIconFileName,
            Type = app.Type
        });
    }
    
    public void SetShortcutLimitPreview(int? limit)
    {
        _shortcutLimitPreview = limit;
        RefreshVisibleShortcuts();
    }

    public void SetLimitPreview(int? limit) => SetShortcutLimitPreview(limit);
    
    private int GetShortcutMaxCount() =>
        _shortcutLimitPreview
        ?? ShortcutLimitHelper.ParseLimit(_settingsService.GetValue("ShortcutMaxCount", ShortcutLimitHelper.DefaultLimit.ToString()));
    
    private static bool IsDuplicateShortcut(IEnumerable<ShortcutItem> existing, ShortcutItem candidate)
    {
        var candidatePath = NormalizeShortcutPath(candidate.Path);
        return existing.Any(s =>
            string.Equals(NormalizeShortcutPath(s.Path), candidatePath, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(s.LaunchArguments ?? string.Empty, candidate.LaunchArguments ?? string.Empty, StringComparison.Ordinal));
    }
    
    private async Task CreateShortcutAndReloadAsync(ShortcutItem shortcut)
    {
        var maxCount = GetShortcutMaxCount();
        var allShortcuts = await _shortcutService.GetAllShortcutsAsync();
    
        if (allShortcuts.Count >= maxCount)
        {
            _notificationService.ShowWarningMessage(_localizationService.Format("Shortcut.LimitExceededFormat", maxCount));
            return;
        }
    
        if (IsDuplicateShortcut(allShortcuts, shortcut))
        {
            _notificationService.ShowWarningMessage(L("Shortcut.Duplicate"));
            return;
        }
    
        shortcut.SortOrder = GetNextShortcutSortOrder(allShortcuts);
        var id = await _shortcutService.CreateShortcutAsync(shortcut);
        if (id <= 0)
        {
            _notificationService.ShowWarningMessage(L("Shortcut.AddFailed"));
            return;
        }
    
        await LoadShortcutsAsync();
    }
    
    public async Task<ShortcutImportResult> AddFromPathsAsync(IEnumerable<string>? paths)
    {
        var result = new ShortcutImportResult();
        var incomingPaths = paths?
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    
        if (incomingPaths.Count == 0)
        {
            result.InvalidCount++;
            return result;
        }
    
        try
        {
            var allShortcuts = await _shortcutService.GetAllShortcutsAsync();
            var maxCount = GetShortcutMaxCount();
            var existingPaths = allShortcuts
                .Select(shortcut => NormalizeShortcutPath(shortcut.Path))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
    
            foreach (var path in incomingPaths)
            {
                if (!ShortcutPathHelper.IsSupportedPath(path))
                {
                    result.InvalidCount++;
                    continue;
                }
    
                var normalizedPath = NormalizeShortcutPath(path);
                if (existingPaths.Contains(normalizedPath))
                {
                    result.DuplicateCount++;
                    continue;
                }
    
                if (allShortcuts.Count >= maxCount)
                {
                    result.LimitSkippedCount++;
                    continue;
                }
    
                try
                {
                    var shortcut = ShortcutPathHelper.CreateFromPath(path, GetNextShortcutSortOrder(allShortcuts));
                    var id = await _shortcutService.CreateShortcutAsync(shortcut);
                    if (id <= 0)
                    {
                        result.InvalidCount++;
                        continue;
                    }
    
                    shortcut.Id = id;
                    allShortcuts.Add(shortcut);
                    existingPaths.Add(normalizedPath);
                    result.AddedCount++;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, $"ShortcutsViewModel.AddShortcutFromPath: {path}");
                    result.InvalidCount++;
                }
            }
    
            if (result.HasChanges)
            {
                await LoadShortcutsAsync();
            }
    
            if (result.AddedCount > 0)
            {
                _notificationService.ShowSuccessMessage(result.ToUserMessage(_localizationService));
            }
            else if (result.DuplicateCount > 0 || result.InvalidCount > 0 || result.LimitSkippedCount > 0)
            {
                _notificationService.ShowWarningMessage(result.ToUserMessage(_localizationService));
            }
    
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "MainWindowViewModel.AddFromPathsAsync");
            _notificationService.ShowErrorMessage(L("Shortcut.DropFailed"));
            result.InvalidCount += incomingPaths.Count;
            return result;
        }
    }
    
    private static int GetNextShortcutSortOrder(IReadOnlyCollection<ShortcutItem> shortcuts) =>
        shortcuts.Count == 0 ? 0 : shortcuts.Max(shortcut => shortcut.SortOrder) + 1;
    
    private static string NormalizeShortcutPath(string path)
    {
        try
        {
            var expanded = Environment.ExpandEnvironmentVariables(path).Trim();
            var fullPath = Path.GetFullPath(expanded);
            return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return path.Trim();
        }
    }
    
    private void RefreshShortcutDisplayEntries()
    {
        ShortcutDisplayEntries.Clear();
        foreach (var shortcut in Shortcuts)
        {
            ShortcutDisplayEntries.Add(shortcut);
        }
    
        if (IsEditingShortcuts && Shortcuts.Count < GetShortcutMaxCount())
        {
            ShortcutDisplayEntries.Add(AddShortcutPlaceholder.Instance);
        }
    
        OnPropertyChanged(nameof(HasShortcuts));
        NotifyShortcutOrderCommandsCanExecuteChanged();
    }
    
    private void RefreshVisibleShortcuts()
    {
        Shortcuts.Clear();
        foreach (var shortcut in _allShortcuts
                     .OrderBy(shortcut => shortcut.SortOrder)
                     .ThenBy(shortcut => shortcut.CreatedAt)
                     .ThenBy(shortcut => shortcut.Id)
                     .Take(GetShortcutMaxCount()))
        {
            Shortcuts.Add(shortcut);
        }
    
        RefreshShortcutDisplayEntries();
    }
    
    [RelayCommand]
    private async Task DeleteShortcutAsync(ShortcutItem? shortcut)
    {
        if (shortcut == null) return;
    
        await _shortcutService.DeleteShortcutAsync(shortcut.Id);
        await LoadShortcutsAsync();
    }
    
    [RelayCommand(CanExecute = nameof(CanMoveShortcutUp))]
    private async Task MoveShortcutUpAsync(ShortcutItem? shortcut)
    {
        var index = GetShortcutIndex(shortcut);
        if (index <= 0)
        {
            return;
        }
    
        await MoveShortcutToIndexAsync(shortcut, index - 1);
    }
    
    private bool CanMoveShortcutUp(ShortcutItem? shortcut) =>
        GetShortcutIndex(shortcut) > 0;
    
    [RelayCommand(CanExecute = nameof(CanMoveShortcutDown))]
    private async Task MoveShortcutDownAsync(ShortcutItem? shortcut)
    {
        var index = GetShortcutIndex(shortcut);
        if (index < 0 || index >= _allShortcuts.Count - 1)
        {
            return;
        }
    
        await MoveShortcutToIndexAsync(shortcut, index + 1);
    }
    
    private bool CanMoveShortcutDown(ShortcutItem? shortcut)
    {
        var index = GetShortcutIndex(shortcut);
        return index >= 0 && index < _allShortcuts.Count - 1;
    }
    
    [RelayCommand(CanExecute = nameof(CanMoveShortcutToFirst))]
    private async Task MoveShortcutToFirstAsync(ShortcutItem? shortcut)
    {
        if (GetShortcutIndex(shortcut) <= 0)
        {
            return;
        }
    
        await MoveShortcutToIndexAsync(shortcut, 0);
    }
    
    private bool CanMoveShortcutToFirst(ShortcutItem? shortcut) =>
        GetShortcutIndex(shortcut) > 0;
    
    [RelayCommand(CanExecute = nameof(CanMoveShortcutToLast))]
    private async Task MoveShortcutToLastAsync(ShortcutItem? shortcut)
    {
        var index = GetShortcutIndex(shortcut);
        if (index < 0 || index >= _allShortcuts.Count - 1)
        {
            return;
        }
    
        await MoveShortcutToIndexAsync(shortcut, _allShortcuts.Count - 1);
    }
    
    private bool CanMoveShortcutToLast(ShortcutItem? shortcut)
    {
        var index = GetShortcutIndex(shortcut);
        return index >= 0 && index < _allShortcuts.Count - 1;
    }
    
    public async Task MoveShortcutAsync(ShortcutItem? source, ShortcutItem? target)
    {
        if (source == null || target == null || source.Id == target.Id)
        {
            return;
        }
    
        var sourceIndex = GetShortcutIndex(source);
        var targetIndex = GetShortcutIndex(target);
        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
        {
            return;
        }
    
        await MoveShortcutToIndexAsync(source, targetIndex);
    }
    
    private int GetShortcutIndex(ShortcutItem? shortcut)
    {
        if (shortcut == null)
        {
            return -1;
        }
    
        return _allShortcuts.FindIndex(item => item.Id == shortcut.Id);
    }
    
    private async Task MoveShortcutToIndexAsync(ShortcutItem? shortcut, int targetIndex)
    {
        var sourceIndex = GetShortcutIndex(shortcut);
        if (shortcut == null || sourceIndex < 0)
        {
            return;
        }
    
        targetIndex = Math.Clamp(targetIndex, 0, _allShortcuts.Count - 1);
        if (sourceIndex == targetIndex)
        {
            return;
        }
    
        var ordered = _allShortcuts.ToList();
        ordered.RemoveAt(sourceIndex);
        ordered.Insert(targetIndex, shortcut);
        await ApplyShortcutOrderAsync(ordered);
    }
    
    private async Task ApplyShortcutOrderAsync(List<ShortcutItem> orderedShortcuts)
    {
        for (var i = 0; i < orderedShortcuts.Count; i++)
        {
            orderedShortcuts[i].SortOrder = i;
        }
    
        _allShortcuts = orderedShortcuts;
        RefreshVisibleShortcuts();
        await _shortcutService.UpdateSortOrderAsync(_allShortcuts.Select(shortcut => shortcut.Id).ToList());
        NotifyShortcutOrderCommandsCanExecuteChanged();
    }
    
    private void NotifyShortcutOrderCommandsCanExecuteChanged()
    {
        MoveShortcutUpCommand.NotifyCanExecuteChanged();
        MoveShortcutDownCommand.NotifyCanExecuteChanged();
        MoveShortcutToFirstCommand.NotifyCanExecuteChanged();
        MoveShortcutToLastCommand.NotifyCanExecuteChanged();
    }
    
    private async Task LoadShortcutsAsync()
    {
        var generation = Interlocked.Increment(ref _loadGeneration);
        try
        {
            await _shortcutService.NormalizeSortOrderAsync();
            var shortcuts = await _shortcutService.GetAllShortcutsAsync();
            if (generation != _loadGeneration) return;
    
            void Apply()
            {
                _allShortcuts = shortcuts
                    .OrderBy(shortcut => shortcut.SortOrder)
                    .ThenBy(shortcut => shortcut.CreatedAt)
                    .ThenBy(shortcut => shortcut.Id)
                    .ToList();
                RefreshVisibleShortcuts();
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess()) Apply();
            else await dispatcher.InvokeAsync(Apply);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "ShortcutsViewModel.Reload");
            if (generation == _loadGeneration)
            {
                _notificationService.ShowWarningMessage(L("Shortcut.LoadFailed"));
            }
        }
    }

    public Task ReloadAsync() => LoadShortcutsAsync();

    public Task<ShortcutImportResult> AddShortcutsFromPathsAsync(IEnumerable<string>? paths) =>
        AddFromPathsAsync(paths);

    private string L(string key) => _localizationService.GetString(key);
}
