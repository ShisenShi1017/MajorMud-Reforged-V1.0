using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using Reforged.App.Models;
using Reforged.App.Services;

namespace Reforged.App.Views;

/// <summary>
/// Play-client game-data surface. It is intentionally not a world editor.
/// Reforged worlds are read-only and server-selected. External BBSes edit only
/// their local field-level overlay over Classic.
/// </summary>
public sealed class MudPlayGameDataOverlayWindow : Window
{
    private readonly MudPlayClientGameDataOverlayService _service = new();
    private readonly TextBlock _authorityText;
    private readonly TextBlock _overlayText;
    private readonly ComboBox _family;
    private readonly TextBox _search;
    private readonly DataGrid _records;
    private readonly DataGrid _fields;
    private readonly TextBox _fieldPath;
    private readonly TextBox _fieldValue;
    private readonly Button _saveOverride;
    private readonly Button _resetField;
    private readonly Button _resetRecord;
    private readonly Button _clearOverlay;
    private readonly Button _importMdb;
    private readonly TextBlock _importStatus;
    private MudPlayGameDataContext? _context;
    private MudPlayGameDataRecord? _selectedRecord;
    private MudPlayGameDataField? _selectedField;
    private IReadOnlyList<MudPlayGameDataRecord> _loadedRecords = [];

    public MudPlayGameDataOverlayWindow(ClientSessionConnection session)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));

        Title = "Client Game Data — RealmGate";
        Width = 1180;
        Height = 780;
        MinWidth = 880;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = SystemColors.ControlBrush;
        FontFamily = new FontFamily("Tahoma");
        FontSize = 12;

        var root = new DockPanel { Margin = new Thickness(10) };

        var header = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(36, 42, 48)),
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 0, 0, 10)
        };
        DockPanel.SetDock(header, Dock.Top);
        var headerStack = new StackPanel();
        headerStack.Children.Add(new TextBlock
        {
            Text = "Client Game Data",
            Foreground = Brushes.White,
            FontSize = 20,
            FontWeight = FontWeights.SemiBold
        });
        _authorityText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(200, 209, 218)),
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        _overlayText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(200, 209, 218)),
            Margin = new Thickness(0, 2, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        _importStatus = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(246, 211, 107)),
            Margin = new Thickness(0, 2, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        headerStack.Children.Add(_authorityText);
        headerStack.Children.Add(_overlayText);
        headerStack.Children.Add(_importStatus);
        header.Child = headerStack;
        root.Children.Add(header);

        var footer = new TextBlock
        {
            Text = "RealmGate servers: active RealmGate world data is read-only here. External BBSes: Classic remains the immutable base; importing an MDB stores only its differences and manual corrections. The MDB itself is never copied. This is a client data overlay, not the RealmGate world Editor.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.DimGray,
            Margin = new Thickness(2, 8, 2, 0)
        };
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        var toolbar = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(toolbar, Dock.Top);

        var reload = new Button { Content = "Reload", Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(8, 0, 0, 0) };
        reload.Click += (_, _) => ReloadContextAndFamily();
        DockPanel.SetDock(reload, Dock.Right);
        toolbar.Children.Add(reload);

        _importMdb = new Button
        {
            Content = "Import MDB → Overlay...",
            Padding = new Thickness(12, 5, 12, 5),
            Margin = new Thickness(8, 0, 0, 0),
            ToolTip = "Compare an external BBS MDB/ACCDB with Classic and store only differences in this BBS overlay."
        };
        _importMdb.Click += async (_, _) => await ImportMdbAsync();
        DockPanel.SetDock(_importMdb, Dock.Right);
        toolbar.Children.Add(_importMdb);

        _family = new ComboBox { Width = 190, Margin = new Thickness(0, 0, 8, 0) };
        foreach (var family in _service.Families)
            _family.Items.Add(family);
        _family.SelectedIndex = 0;
        _family.SelectionChanged += (_, _) => LoadSelectedFamily();
        DockPanel.SetDock(_family, Dock.Left);
        toolbar.Children.Add(_family);

        _search = new TextBox { MinWidth = 220, ToolTip = "Search record name or id" };
        _search.TextChanged += (_, _) => ApplySearch();
        toolbar.Children.Add(_search);
        root.Children.Add(toolbar);

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.42, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.58, GridUnitType.Star) });

        _records = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            CanUserAddRows = false,
            CanUserDeleteRows = false
        };
        _records.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(MudPlayGameDataRecord.DisplayName)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _records.Columns.Add(new DataGridTextColumn { Header = "ID", Binding = new Binding(nameof(MudPlayGameDataRecord.Id)), Width = new DataGridLength(145) });
        _records.Columns.Add(new DataGridTextColumn { Header = "Overrides", Binding = new Binding(nameof(MudPlayGameDataRecord.OverrideCount)), Width = new DataGridLength(75) });
        _records.SelectionChanged += (_, _) => RecordSelectionChanged();
        Grid.SetColumn(_records, 0);
        body.Children.Add(_records);

        var right = new Grid();
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _fields = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            CanUserAddRows = false,
            CanUserDeleteRows = false
        };
        _fields.Columns.Add(new DataGridTextColumn { Header = "Field", Binding = new Binding(nameof(MudPlayGameDataField.Path)), Width = new DataGridLength(0.42, DataGridLengthUnitType.Star) });
        _fields.Columns.Add(new DataGridTextColumn { Header = "Effective value", Binding = new Binding(nameof(MudPlayGameDataField.Value)), Width = new DataGridLength(0.58, DataGridLengthUnitType.Star) });
        _fields.Columns.Add(new DataGridCheckBoxColumn { Header = "Overlay", Binding = new Binding(nameof(MudPlayGameDataField.IsOverridden)), Width = new DataGridLength(65) });
        _fields.SelectionChanged += (_, _) => FieldSelectionChanged();
        right.Children.Add(_fields);

        var editor = new Border
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8),
            Margin = new Thickness(0, 8, 0, 0)
        };
        Grid.SetRow(editor, 1);
        var editorGrid = new Grid();
        editorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        editorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        editorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        editorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) });
        editorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        editorGrid.Children.Add(new TextBlock { Text = "Field path", VerticalAlignment = VerticalAlignment.Center });
        _fieldPath = new TextBox { IsReadOnly = true, Margin = new Thickness(4, 2, 0, 2) };
        Grid.SetColumn(_fieldPath, 1);
        editorGrid.Children.Add(_fieldPath);

        var valueLabel = new TextBlock { Text = "JSON value", VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(valueLabel, 1);
        editorGrid.Children.Add(valueLabel);
        _fieldValue = new TextBox { Margin = new Thickness(4, 2, 0, 2), MinHeight = 48, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
        Grid.SetRow(_fieldValue, 1);
        Grid.SetColumn(_fieldValue, 1);
        editorGrid.Children.Add(_fieldValue);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 0, 0) };
        _saveOverride = new Button { Content = "Save Override", Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(4, 0, 0, 0) };
        _resetField = new Button { Content = "Reset Field", Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(4, 0, 0, 0) };
        _resetRecord = new Button { Content = "Reset Record", Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(4, 0, 0, 0) };
        _clearOverlay = new Button { Content = "Clear BBS Overlay", Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(12, 0, 0, 0) };
        _saveOverride.Click += (_, _) => SaveOverride();
        _resetField.Click += (_, _) => ResetField();
        _resetRecord.Click += (_, _) => ResetRecord();
        _clearOverlay.Click += (_, _) => ClearOverlay();
        buttons.Children.Add(_saveOverride);
        buttons.Children.Add(_resetField);
        buttons.Children.Add(_resetRecord);
        buttons.Children.Add(_clearOverlay);
        Grid.SetRow(buttons, 2);
        Grid.SetColumnSpan(buttons, 2);
        editorGrid.Children.Add(buttons);

        editor.Child = editorGrid;
        right.Children.Add(editor);
        Grid.SetColumn(right, 2);
        body.Children.Add(right);

        root.Children.Add(body);
        Content = root;

        Loaded += (_, _) => ReloadContextAndFamily();
    }

    public ClientSessionConnection Session { get; }

    private void ReloadContextAndFamily()
    {
        try
        {
            _context = _service.ResolveContext(Session);
            _authorityText.Text = _context.Description;
            var overrideCount = _service.CountOverrides(Session);
            _overlayText.Text = _context.IsEditable
                ? $"Overlay: {overrideCount:N0} difference(s) • {_context.OverlayPath}"
                : $"World projection: {_context.BaseDataRoot}";
            var import = _service.GetLastImportMetadata(Session);
            _importStatus.Text = import is null
                ? (_context.IsEditable ? "No MDB has been compared for this BBS yet." : string.Empty)
                : $"Last MDB comparison: {import.FileName} • {import.ImportedAtUtc.ToLocalTime():g} • SHA-256 {import.Sha256[..Math.Min(12, import.Sha256.Length)]}…";
            SetEditingEnabled(_context.IsEditable);
            LoadSelectedFamily();
        }
        catch (Exception exception)
        {
            _authorityText.Text = "Game data unavailable: " + exception.Message;
            _overlayText.Text = string.Empty;
            _importStatus.Text = string.Empty;
            _records.ItemsSource = null;
            _fields.ItemsSource = null;
            SetEditingEnabled(false);
        }
    }

    private void LoadSelectedFamily()
    {
        if (_family.SelectedItem is not string family || string.IsNullOrWhiteSpace(family))
            return;

        try
        {
            _loadedRecords = _service.LoadFamily(Session, family);
            ApplySearch();
        }
        catch (Exception exception)
        {
            _loadedRecords = [];
            _records.ItemsSource = null;
            _fields.ItemsSource = null;
            _authorityText.Text = "Game data unavailable: " + exception.Message;
        }
    }

    private void ApplySearch()
    {
        var query = (_search.Text ?? string.Empty).Trim();
        var rows = string.IsNullOrWhiteSpace(query)
            ? _loadedRecords
            : _loadedRecords.Where(record =>
                record.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                record.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        _records.ItemsSource = rows;
        if (rows.Count > 0)
            _records.SelectedIndex = 0;
    }

    private void RecordSelectionChanged()
    {
        _selectedRecord = _records.SelectedItem as MudPlayGameDataRecord;
        if (_selectedRecord is null || _family.SelectedItem is not string family)
        {
            _fields.ItemsSource = null;
            return;
        }

        _fields.ItemsSource = _service.FlattenFields(Session, family, _selectedRecord);
        _fields.SelectedIndex = 0;
    }

    private void FieldSelectionChanged()
    {
        _selectedField = _fields.SelectedItem as MudPlayGameDataField;
        if (_selectedField is null)
        {
            _fieldPath.Text = string.Empty;
            _fieldValue.Text = string.Empty;
            return;
        }

        _fieldPath.Text = _selectedField.Path;
        _fieldValue.Text = _selectedField.Value;
    }

    private async Task ImportMdbAsync()
    {
        if (_context?.IsEditable != true)
            return;

        var dialog = new OpenFileDialog
        {
            Title = "Compare MajorMUD MDB with Classic",
            Filter = "MajorMUD databases (*.mdb;*.accdb)|*.mdb;*.accdb|MDB files (*.mdb)|*.mdb|ACCDB files (*.accdb)|*.accdb|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true)
            return;

        var answer = MessageBox.Show(
            this,
            "This will compare the selected database with RealmGate Classic and replace only the MDB-derived portion of this BBS overlay.\n\n" +
            "Manual overrides are preserved. The MDB itself is not copied. Classic records that are absent from the MDB are NOT automatically deleted or hidden.\n\nContinue?",
            "Import MDB as BBS Overlay",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information,
            MessageBoxResult.Yes);
        if (answer != MessageBoxResult.Yes)
            return;

        SetBusy(true);
        try
        {
            var importer = new MudPlayMdbOverlayImporter(_service);
            var progress = new Progress<MudPlayMdbImportProgress>(update =>
            {
                _importStatus.Text = update.Message;
            });
            var result = await importer.ImportAsync(Session, dialog.FileName, progress);
            if (!result.Success)
            {
                MessageBox.Show(this, result.Message, "MDB Overlay Import", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show(this, result.Message, "MDB Overlay Import", MessageBoxButton.OK, MessageBoxImage.Information);
            ReloadContextAndFamily();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "MDB Overlay Import", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _importMdb.IsEnabled = !busy && _context?.IsEditable == true;
        _saveOverride.IsEnabled = !busy && _context?.IsEditable == true;
        _resetField.IsEnabled = !busy && _context?.IsEditable == true;
        _resetRecord.IsEnabled = !busy && _context?.IsEditable == true;
        _clearOverlay.IsEnabled = !busy && _context?.IsEditable == true;
        _family.IsEnabled = !busy;
        _search.IsEnabled = !busy;
    }

    private void SaveOverride()
    {
        if (_selectedRecord is null || _selectedField is null || _family.SelectedItem is not string family)
            return;

        try
        {
            _service.SetOverride(Session, family, _selectedRecord.Id, _selectedField.Path, _fieldValue.Text);
            ReloadContextAndFamily();
            Reselect(_selectedRecord.Id, _selectedField.Path);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Game Data Overlay", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ResetField()
    {
        if (_selectedRecord is null || _selectedField is null || _family.SelectedItem is not string family)
            return;
        _service.RemoveFieldOverride(Session, family, _selectedRecord.Id, _selectedField.Path);
        ReloadContextAndFamily();
        Reselect(_selectedRecord.Id, _selectedField.Path);
    }

    private void ResetRecord()
    {
        if (_selectedRecord is null || _family.SelectedItem is not string family)
            return;
        _service.RemoveRecordOverrides(Session, family, _selectedRecord.Id);
        ReloadContextAndFamily();
        Reselect(_selectedRecord.Id, null);
    }

    private void ClearOverlay()
    {
        if (_context?.IsEditable != true)
            return;
        var answer = MessageBox.Show(
            this,
            "Remove every local game-data override for this BBS? The client will immediately fall back to Classic for every field.",
            "Clear BBS Overlay",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
            return;
        _service.ClearOverlay(Session);
        ReloadContextAndFamily();
    }

    private void Reselect(string recordId, string? fieldPath)
    {
        var record = (_records.ItemsSource as IEnumerable<MudPlayGameDataRecord>)?
            .FirstOrDefault(item => string.Equals(item.Id, recordId, StringComparison.OrdinalIgnoreCase));
        if (record is null)
            return;
        _records.SelectedItem = record;
        if (fieldPath is null)
            return;
        var field = (_fields.ItemsSource as IEnumerable<MudPlayGameDataField>)?
            .FirstOrDefault(item => string.Equals(item.Path, fieldPath, StringComparison.OrdinalIgnoreCase));
        if (field is not null)
            _fields.SelectedItem = field;
    }

    private void SetEditingEnabled(bool enabled)
    {
        _fieldValue.IsReadOnly = !enabled;
        _saveOverride.IsEnabled = enabled;
        _resetField.IsEnabled = enabled;
        _resetRecord.IsEnabled = enabled;
        _clearOverlay.IsEnabled = enabled;
        _importMdb.IsEnabled = enabled;
    }
}
