using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Reforged.App.Services;
using Reforged.App.ViewModels;

namespace Reforged.App.Views;

/// <summary>
/// MudPlay client-integration entry point. The workshop and client game-data overlay
/// remain Reforged-owned modeless tools bound to the active terminal session.
/// </summary>
internal static class MudPlayClientMenuBootstrap
{
    private static readonly ConditionalWeakTable<ClientTerminalWindow, object> Installed = new();
    private static readonly object Marker = new();

    [ModuleInitializer]
    internal static void Initialize()
    {
        EventManager.RegisterClassHandler(
            typeof(ClientTerminalWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnTerminalLoaded));
    }

    private static void OnTerminalLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ClientTerminalWindow terminal)
            return;

        if (Installed.TryGetValue(terminal, out _))
            return;

        if (terminal.FindName("ClassicMenuBar") is not Menu menu)
            return;

        var viewMenu = menu.Items
            .OfType<MenuItem>()
            .FirstOrDefault(item => NormalizeHeader(item.Header) == "View");

        if (viewMenu is null)
            return;

        if (!viewMenu.Items.OfType<MenuItem>().Any(item =>
                string.Equals(NormalizeHeader(item.Header), "Character Workshop...", StringComparison.OrdinalIgnoreCase)))
        {
            viewMenu.Items.Add(new Separator());
            var workshop = new MenuItem
            {
                Header = "Character Workshop...",
                ToolTip = "Open the MudPlay-inspired Reforged Character Workshop: live state, equipment comparison, Item Finder, and progress planning."
            };
            workshop.Click += (_, _) => OpenWorkshop(terminal);
            viewMenu.Items.Add(workshop);
        }

        var optionsMenu = menu.Items
            .OfType<MenuItem>()
            .FirstOrDefault(item => NormalizeHeader(item.Header) == "Options");

        if (optionsMenu is not null &&
            !optionsMenu.Items.OfType<MenuItem>().Any(item =>
                string.Equals(NormalizeHeader(item.Header), "Client Game Data...", StringComparison.OrdinalIgnoreCase)))
        {
            optionsMenu.Items.Add(new Separator());
            var gameData = new MenuItem
            {
                Header = "Client Game Data...",
                ToolTip = "Browse the active Reforged world data, or edit the current external BBS overlay over Classic."
            };
            gameData.Click += (_, _) => OpenGameData(terminal);
            optionsMenu.Items.Add(gameData);
        }

        if (optionsMenu is not null &&
            !optionsMenu.Items.OfType<MenuItem>().Any(item =>
                string.Equals(NormalizeHeader(item.Header), "Aliases, Macros, Triggers & Events...", StringComparison.OrdinalIgnoreCase)))
        {
            var automationRules = new MenuItem
            {
                Header = "Aliases, Macros, Triggers & Events...",
                ToolTip = "Manage parameterized aliases, multi-step macros, bounded text/regex triggers, and interval events through Reforged's existing automation safety layer."
            };
            automationRules.Click += (_, _) => OpenAutomationRules(terminal);
            optionsMenu.Items.Add(automationRules);
        }

        if (optionsMenu is not null &&
            !optionsMenu.Items.OfType<MenuItem>().Any(item =>
                string.Equals(NormalizeHeader(item.Header), "Inventory & Equipment Automation...", StringComparison.OrdinalIgnoreCase)))
        {
            var inventoryAutomation = new MenuItem
            {
                Header = "Inventory & Equipment Automation...",
                ToolTip = "Manage per-character equipment sets, buy/sell/stash/discard rules, banking thresholds, and safe recovery workflows."
            };
            inventoryAutomation.Click += (_, _) => OpenInventoryAutomation(terminal);
            optionsMenu.Items.Add(inventoryAutomation);
        }

        if (optionsMenu is not null &&
            !optionsMenu.Items.OfType<MenuItem>().Any(item =>
                string.Equals(NormalizeHeader(item.Header), "Recovery & Buff Automation...", StringComparison.OrdinalIgnoreCase)))
        {
            var recoveryAutomation = new MenuItem
            {
                Header = "Recovery & Buff Automation...",
                ToolTip = "Configure Reforged-native self healing, cures, rest/meditation, maintained buffs, equipment hooks, and party-healing thresholds."
            };
            recoveryAutomation.Click += (_, _) => OpenRecoveryAutomation(terminal);
            optionsMenu.Items.Add(recoveryAutomation);
        }

        if (optionsMenu is not null &&
            !optionsMenu.Items.OfType<MenuItem>().Any(item =>
                string.Equals(NormalizeHeader(item.Header), "Combat Decision Engine...", StringComparison.OrdinalIgnoreCase)))
        {
            var combatAutomation = new MenuItem
            {
                Header = "Combat Decision Engine...",
                ToolTip = "Configure Reforged-native target selection, equipment strategy, opening/round actions, spell safety, defensive actions, retargeting, and escape policy."
            };
            combatAutomation.Click += (_, _) => OpenCombatAutomation(terminal);
            optionsMenu.Items.Add(combatAutomation);
        }

        if (optionsMenu is not null &&
            !optionsMenu.Items.OfType<MenuItem>().Any(item =>
                string.Equals(NormalizeHeader(item.Header), "Party Coordinator...", StringComparison.OrdinalIgnoreCase)))
        {
            var partyAutomation = new MenuItem
            {
                Header = "Party Coordinator...",
                ToolTip = "Configure Reforged-native leader/follower coordination, rescue/healing, rank maintenance, exact-name invites, reconnect recovery, and party safety."
            };
            partyAutomation.Click += (_, _) => OpenPartyAutomation(terminal);
            optionsMenu.Items.Add(partyAutomation);
        }

        if (optionsMenu is not null &&
            !optionsMenu.Items.OfType<MenuItem>().Any(item =>
                string.Equals(NormalizeHeader(item.Header), "Diagnostics & Customization...", StringComparison.OrdinalIgnoreCase)))
        {
            optionsMenu.Items.Add(new Separator());
            var diagnostics = new MenuItem
            {
                Header = "Diagnostics & Customization...",
                ToolTip = "Open Reforged scrollback search, program log, session statistics, Telnet wire inspector, editable quick toolbar, command hotkeys, and right-click commands."
            };
            diagnostics.Click += (_, _) => OpenDiagnostics(terminal);
            optionsMenu.Items.Add(diagnostics);
        }

        var actionMenu = menu.Items
            .OfType<MenuItem>()
            .FirstOrDefault(item => NormalizeHeader(item.Header) == "Action");
        if (actionMenu is not null &&
            !actionMenu.Items.OfType<MenuItem>().Any(item =>
                string.Equals(NormalizeHeader(item.Header), "Navigation Assistant...", StringComparison.OrdinalIgnoreCase)))
        {
            actionMenu.Items.Add(new Separator());
            var navigation = new MenuItem
            {
                Header = "Navigation Assistant...",
                ToolTip = "Plan hazard-aware routes, saved/stash destinations, safe loops, lair routes, and route recovery from the effective Reforged/BBS game data."
            };
            navigation.Click += (_, _) => OpenNavigation(terminal);
            actionMenu.Items.Add(navigation);
        }

        terminal.PreviewKeyDown += OnCustomKeyDown;
        ApplyCustomizations(terminal);
        Installed.Add(terminal, Marker);
    }

    private static void OpenWorkshop(ClientTerminalWindow terminal)
    {
        if (terminal.DataContext is not ClientTerminalWindowContext context)
            return;

        var existing = Application.Current.Windows
            .OfType<MudPlayCharacterWorkshopWindow>()
            .FirstOrDefault(window => ReferenceEquals(window.Session, context.Session));

        if (existing is not null)
        {
            if (existing.WindowState == WindowState.Minimized)
                existing.WindowState = WindowState.Normal;

            existing.Show();
            existing.Activate();
            return;
        }

        var window = new MudPlayCharacterWorkshopWindow(context.Session)
        {
            Owner = terminal
        };
        window.Show();
        window.Activate();
    }

    private static void OpenGameData(ClientTerminalWindow terminal)
    {
        if (terminal.DataContext is not ClientTerminalWindowContext context)
            return;

        var existing = Application.Current.Windows
            .OfType<MudPlayGameDataOverlayWindow>()
            .FirstOrDefault(window => ReferenceEquals(window.Session, context.Session));

        if (existing is not null)
        {
            if (existing.WindowState == WindowState.Minimized)
                existing.WindowState = WindowState.Normal;
            existing.Show();
            existing.Activate();
            return;
        }

        var window = new MudPlayGameDataOverlayWindow(context.Session)
        {
            Owner = terminal
        };
        window.Show();
        window.Activate();
    }



    private static void OpenNavigation(ClientTerminalWindow terminal)
    {
        if (terminal.DataContext is not ClientTerminalWindowContext context)
            return;

        var existing = Application.Current.Windows
            .OfType<MudPlayNavigationAssistantWindow>()
            .FirstOrDefault(window => ReferenceEquals(window.Session, context.Session));
        if (existing is not null)
        {
            if (existing.WindowState == WindowState.Minimized)
                existing.WindowState = WindowState.Normal;
            existing.Show();
            existing.Activate();
            return;
        }

        var window = new MudPlayNavigationAssistantWindow(context.Host, context.Session)
        {
            Owner = terminal
        };
        window.Show();
        window.Activate();
    }

    private static void OpenInventoryAutomation(ClientTerminalWindow terminal)
    {
        if (terminal.DataContext is not ClientTerminalWindowContext context)
            return;

        var existing = Application.Current.Windows
            .OfType<MudPlayInventoryAutomationWindow>()
            .FirstOrDefault(window => ReferenceEquals(window.Session, context.Session));
        if (existing is not null)
        {
            if (existing.WindowState == WindowState.Minimized)
                existing.WindowState = WindowState.Normal;
            existing.Show();
            existing.Activate();
            return;
        }

        var window = new MudPlayInventoryAutomationWindow(context.Host, context.Session)
        {
            Owner = terminal
        };
        window.Show();
        window.Activate();
    }


    private static void OpenCombatAutomation(ClientTerminalWindow terminal)
    {
        if (terminal.DataContext is not ClientTerminalWindowContext context)
            return;

        var existing = Application.Current.Windows
            .OfType<ReforgedCombatAutomationWindow>()
            .FirstOrDefault(window => ReferenceEquals(window.Session, context.Session));
        if (existing is not null)
        {
            if (existing.WindowState == WindowState.Minimized)
                existing.WindowState = WindowState.Normal;
            existing.Show();
            existing.Activate();
            return;
        }

        var window = new ReforgedCombatAutomationWindow(context.Host, context.Session)
        {
            Owner = terminal
        };
        window.Show();
        window.Activate();
    }

    private static void OpenPartyAutomation(ClientTerminalWindow terminal)
    {
        if (terminal.DataContext is not ClientTerminalWindowContext context)
            return;

        var existing = Application.Current.Windows
            .OfType<ReforgedPartyAutomationWindow>()
            .FirstOrDefault(window => ReferenceEquals(window.Session, context.Session));
        if (existing is not null)
        {
            if (existing.WindowState == WindowState.Minimized)
                existing.WindowState = WindowState.Normal;
            existing.Show();
            existing.Activate();
            return;
        }

        var window = new ReforgedPartyAutomationWindow(context.Host, context.Session)
        {
            Owner = terminal
        };
        window.Show();
        window.Activate();
    }

    private static void OpenRecoveryAutomation(ClientTerminalWindow terminal)
    {
        if (terminal.DataContext is not ClientTerminalWindowContext context)
            return;

        var existing = Application.Current.Windows
            .OfType<ReforgedRecoveryAutomationWindow>()
            .FirstOrDefault(window => ReferenceEquals(window.Session, context.Session));
        if (existing is not null)
        {
            if (existing.WindowState == WindowState.Minimized)
                existing.WindowState = WindowState.Normal;
            existing.Show();
            existing.Activate();
            return;
        }

        var window = new ReforgedRecoveryAutomationWindow(context.Host, context.Session)
        {
            Owner = terminal
        };
        window.Show();
        window.Activate();
    }

    private static void OpenAutomationRules(ClientTerminalWindow terminal)
    {
        if (terminal.DataContext is not ClientTerminalWindowContext context)
            return;

        var existing = Application.Current.Windows
            .OfType<MudPlayAutomationRulesWindow>()
            .FirstOrDefault(window => ReferenceEquals(window.Session, context.Session));
        if (existing is not null)
        {
            if (existing.WindowState == WindowState.Minimized)
                existing.WindowState = WindowState.Normal;
            existing.Show();
            existing.Activate();
            return;
        }

        var window = new MudPlayAutomationRulesWindow(context.Host, context.Session)
        {
            Owner = terminal
        };
        window.Show();
        window.Activate();
    }

    private static void OpenDiagnostics(ClientTerminalWindow terminal)
    {
        if (terminal.DataContext is not ClientTerminalWindowContext context)
            return;

        var existing = Application.Current.Windows
            .OfType<ReforgedClientDiagnosticsWindow>()
            .FirstOrDefault(window => ReferenceEquals(window.Session, context.Session));
        if (existing is not null)
        {
            if (existing.WindowState == WindowState.Minimized)
                existing.WindowState = WindowState.Normal;
            existing.Show();
            existing.Activate();
            return;
        }

        var window = new ReforgedClientDiagnosticsWindow(terminal, context.Host, context.Session)
        {
            Owner = terminal
        };
        window.Show();
        window.Activate();
    }

    private static void OpenCommunications(ClientTerminalWindow terminal)
    {
        if (terminal.DataContext is not ClientTerminalWindowContext context) return;
        var existing = Application.Current.Windows.OfType<ReforgedConversationWindow>()
            .FirstOrDefault(window => ReferenceEquals(window.Session, context.Session));
        if (existing is not null) { if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal; existing.Show(); existing.Activate(); return; }
        new ReforgedConversationWindow(context.Host, context.Session) { Owner = terminal }.Show();
    }

    private static void OpenCreatureIntel(ClientTerminalWindow terminal)
    {
        if (terminal.DataContext is not ClientTerminalWindowContext context) return;
        var existing = Application.Current.Windows.OfType<ReforgedCreatureIntelWindow>()
            .FirstOrDefault(window => ReferenceEquals(window.Session, context.Session));
        if (existing is not null) { if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal; existing.Show(); existing.Activate(); return; }
        new ReforgedCreatureIntelWindow(context.Session) { Owner = terminal }.Show();
    }

    private static void OpenBugReport(ClientTerminalWindow terminal)
    {
        if (terminal.DataContext is not ClientTerminalWindowContext context) return;
        var existing = Application.Current.Windows.OfType<ReforgedBugReportWindow>()
            .FirstOrDefault(window => ReferenceEquals(window.Session, context.Session));
        if (existing is not null) { if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal; existing.Show(); existing.Activate(); return; }
        new ReforgedBugReportWindow(context.Host, context.Session) { Owner = terminal }.Show();
    }

    internal static void ApplyCustomizations(ClientTerminalWindow terminal)
    {
        if (terminal.DataContext is not ClientTerminalWindowContext context)
            return;

        var presentation = context.Host.AutomationOptions.Presentation;
        if (terminal.FindName("ClassicToolBarTray") is ToolBarTray tray)
        {
            tray.Visibility = presentation.ShowToolbar ? Visibility.Visible : Visibility.Collapsed;
            var old = tray.ToolBars.FirstOrDefault(toolbar => string.Equals(toolbar.Tag?.ToString(), "ReforgedCustomQuickToolbar", StringComparison.Ordinal));
            if (old is not null) tray.ToolBars.Remove(old);

            var visible = context.Host.AutomationOptions.Toolbar
                .Where(item => item.Visible)
                .OrderBy(item => item.Order)
                .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (visible.Length > 0)
            {
                var custom = new ToolBar { Tag = "ReforgedCustomQuickToolbar", Background = System.Windows.Media.Brushes.Transparent };
                foreach (var option in visible)
                {
                    var button = new Button
                    {
                        Content = string.IsNullOrWhiteSpace(option.Label) ? option.AutomationArea : option.Label,
                        Padding = new Thickness(7, 2, 7, 2),
                        Margin = new Thickness(1, 0, 1, 0),
                        ToolTip = $"Reforged quick tool: {option.AutomationArea}"
                    };
                    button.Click += (_, _) => OpenToolbarTarget(terminal, option.AutomationArea);
                    custom.Items.Add(button);
                }
                tray.ToolBars.Add(custom);
            }
        }

        if (terminal.FindName("ClassicStatusBar") is StatusBar statusBar)
            statusBar.Visibility = presentation.ShowStatusBar ? Visibility.Visible : Visibility.Collapsed;

        if (terminal.FindName("ClassicTerminalSurface") is ClassicMegaMudTerminal surface)
        {
            if (!presentation.EnableCustomContextMenu)
            {
                surface.ContextMenu = null;
            }
            else
            {
                var menu = new ContextMenu();
                foreach (var option in context.Host.AutomationOptions.ContextCommands
                    .Where(item => item.Visible && ReforgedClientDiagnosticsService.IsSafeUserCommand(item.Command))
                    .OrderBy(item => item.Order)
                    .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase))
                {
                    var commandItem = new MenuItem { Header = string.IsNullOrWhiteSpace(option.Label) ? option.Command : option.Label };
                    commandItem.Click += async (_, _) => await SendSafeUserCommandAsync(context, option.Command);
                    menu.Items.Add(commandItem);
                }
                if (menu.Items.Count > 0) menu.Items.Add(new Separator());
                var communications = new MenuItem { Header = "Communications Desk..." };
                communications.Click += (_, _) => OpenCommunications(terminal);
                menu.Items.Add(communications);
                var intel = new MenuItem { Header = "Creature Intel..." };
                intel.Click += (_, _) => OpenCreatureIntel(terminal);
                menu.Items.Add(intel);
                var workshop = new MenuItem { Header = "Character Workshop..." };
                workshop.Click += (_, _) => OpenWorkshop(terminal);
                menu.Items.Add(workshop);
                var diagnostics = new MenuItem { Header = "Diagnostics & Customization..." };
                diagnostics.Click += (_, _) => OpenDiagnostics(terminal);
                menu.Items.Add(diagnostics);
                var problem = new MenuItem { Header = "Report a Problem..." };
                problem.Click += (_, _) => OpenBugReport(terminal);
                menu.Items.Add(problem);
                surface.ContextMenu = menu;
            }
        }
    }

    private static void OpenToolbarTarget(ClientTerminalWindow terminal, string? area)
    {
        switch ((area ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal).Replace("&", "And", StringComparison.Ordinal).ToLowerInvariant())
        {
            case "health": OpenRecoveryAutomation(terminal); break;
            case "spells": OpenWorkshop(terminal); break;
            case "combat": OpenCombatAutomation(terminal); break;
            case "party": OpenPartyAutomation(terminal); break;
            case "roaming": OpenNavigation(terminal); break;
            case "lootandcash": OpenInventoryAutomation(terminal); break;
            case "events": OpenAutomationRules(terminal); break;
            default: OpenDiagnostics(terminal); break;
        }
    }

    private static async void OnCustomKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not ClientTerminalWindow terminal || terminal.DataContext is not ClientTerminalWindowContext context)
            return;
        if (!context.Host.AutomationOptions.Presentation.EnableCommandHotkeys)
            return;
        if (!ClientCommandIsolationPolicy.HasVerifiedGameplay(context.Session))
            return;
        if (Keyboard.FocusedElement is TextBoxBase or PasswordBox or ComboBox or MenuItem)
            return;

        foreach (var binding in context.Host.AutomationOptions.KeyBindings
            .Where(item => item.Enabled)
            .OrderBy(item => item.Order))
        {
            if (!ReforgedClientDiagnosticsService.IsSafeUserCommand(binding.Command) || string.IsNullOrWhiteSpace(binding.Gesture))
                continue;
            try
            {
                if (new KeyGestureConverter().ConvertFromString(binding.Gesture) is not KeyGesture gesture || !gesture.Matches(terminal, e))
                    continue;
                e.Handled = true;
                await SendSafeUserCommandAsync(context, binding.Command);
                return;
            }
            catch
            {
                // Invalid user-entered gestures are inert; the customization window remains editable.
            }
        }
    }

    private static async Task SendSafeUserCommandAsync(ClientTerminalWindowContext context, string? command)
    {
        if (!ClientCommandIsolationPolicy.HasVerifiedGameplay(context.Session) ||
            !ReforgedClientDiagnosticsService.IsSafeUserCommand(command))
            return;
        await context.Host.SendTerminalCommandAsync(context.Session, command);
    }

    private static string NormalizeHeader(object? header) =>
        (header?.ToString() ?? string.Empty).Replace("_", string.Empty, StringComparison.Ordinal).Trim();
}
