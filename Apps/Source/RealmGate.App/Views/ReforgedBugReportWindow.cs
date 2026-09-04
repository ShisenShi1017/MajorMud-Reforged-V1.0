using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using RealmGate.App.Models;
using RealmGate.App.Services;
using RealmGate.App.ViewModels;

namespace RealmGate.App.Views;

/// <summary>
/// User-driven Reforged problem capture. The report freezes typed session state, program
/// activity, and optional recent scrollback without collecting saved login credentials.
/// </summary>
public sealed class ReforgedBugReportWindow : Window
{
    private readonly PlayViewModel _host;
    public ClientSessionConnection Session { get; }
    private readonly ComboBox _area = new() { ItemsSource = new[] { "Client", "Terminal", "Navigation", "Combat", "Recovery / Buffs", "Party", "Inventory / Equipment", "Game Data", "Editor / Custom World", "Other" }, SelectedIndex = 0 };
    private readonly TextBox _summary = Box();
    private readonly TextBox _happened = Multi();
    private readonly TextBox _expected = Multi();
    private readonly TextBox _steps = Multi();
    private readonly TextBox _notes = Multi();
    private readonly CheckBox _scrollback = new() { Content = "Include recent terminal output (may contain in-game chat)", IsChecked = true };
    private readonly CheckBox _inventory = new() { Content = "Include captured inventory/equipment names", IsChecked = true };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };

    public ReforgedBugReportWindow(PlayViewModel host, ClientSessionConnection session)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Title = $"Report a RealmGate Problem — {DisplayCharacter(session)}";
        Width = 780;
        Height = 740;
        MinWidth = 620;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = SystemColors.ControlBrush;
        Foreground = SystemColors.ControlTextBrush;
        FontFamily = new FontFamily("Tahoma");
        FontSize = 12;

        var root = new DockPanel { Margin = new Thickness(14) };
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        header.Children.Add(new TextBlock { Text = "Capture a RealmGate problem", FontSize = 18, FontWeight = FontWeights.SemiBold });
        header.Children.Add(new TextBlock
        {
            Text = "Generate a Markdown report frozen at the time of the problem. It includes RealmGate session/automation diagnostics and can optionally include recent terminal output. Saved BBS passwords are not collected by this report.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.DimGray
        });
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);

        var footer = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition());
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(_status);
        var generate = Button("Generate on Desktop", 135);
        var saveAs = Button("Save As...", 90);
        Grid.SetColumn(generate, 1); Grid.SetColumn(saveAs, 2);
        footer.Children.Add(generate); footer.Children.Add(saveAs);
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var form = new StackPanel();
        form.Children.Add(Field("Area", _area));
        form.Children.Add(Field("Short summary", _summary));
        form.Children.Add(Field("What happened", _happened));
        form.Children.Add(Field("What you expected", _expected));
        form.Children.Add(Field("Steps to reproduce", _steps));
        form.Children.Add(Field("Additional notes", _notes));
        _scrollback.Margin = new Thickness(0, 8, 0, 3); _inventory.Margin = new Thickness(0, 3, 0, 3);
        form.Children.Add(_scrollback); form.Children.Add(_inventory);
        form.Children.Add(new TextBlock { Text = "Tip: capture the report while the bad state is still visible. Recent program activity is always included because it is usually more diagnostic than a screenshot.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 8, 0, 0) });
        scroll.Content = form; root.Children.Add(scroll);
        Content = root;

        generate.Click += async (_, _) => await GenerateAsync(null);
        saveAs.Click += async (_, _) =>
        {
            var dlg = new SaveFileDialog { Title = "Save RealmGate problem report", Filter = "Markdown (*.md)|*.md|All files (*.*)|*.*", FileName = $"realmgate-problem-{DateTime.Now:yyyyMMdd-HHmmss}.md" };
            if (dlg.ShowDialog(this) == true) await GenerateAsync(dlg.FileName);
        };
    }

    private async Task GenerateAsync(string? destination)
    {
        if (string.IsNullOrWhiteSpace(_summary.Text))
        {
            _status.Text = "Enter a short summary before generating the report.";
            return;
        }
        try
        {
            _status.Text = "Capturing current RealmGate state...";
            var request = new ReforgedBugReportRequest
            {
                Area = _area.SelectedItem?.ToString() ?? "Client",
                Summary = _summary.Text,
                WhatHappened = _happened.Text,
                Expected = _expected.Text,
                Steps = _steps.Text,
                Notes = _notes.Text,
                IncludeRecentScrollback = _scrollback.IsChecked == true,
                IncludeInventoryNames = _inventory.IsChecked == true
            };
            var path = await ReforgedParityCompletionService.WriteBugReportAsync(Session, _host.AutomationOptions, request, destination);
            _status.Text = "Report created: " + path;
        }
        catch (Exception ex)
        {
            _status.Text = "Report generation failed: " + ex.Message;
        }
    }

    private static FrameworkElement Field(string label, Control control)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        panel.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 3) });
        panel.Children.Add(control);
        return panel;
    }
    private static TextBox Box() => new() { MinHeight = 26 };
    private static TextBox Multi() => new() { MinHeight = 82, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private static Button Button(string text, double width) => new() { Content = text, Width = width, Margin = new Thickness(4, 0, 0, 0), Padding = new Thickness(6, 4, 6, 4) };
    private static string DisplayCharacter(ClientSessionConnection session) => string.IsNullOrWhiteSpace(session.CharacterName) ? session.DisplayName : session.CharacterName;
}
