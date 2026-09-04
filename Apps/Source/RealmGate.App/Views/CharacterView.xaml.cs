using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using RealmGate.App.Models;
using RealmGate.App.Services;

namespace RealmGate.App.Views;

public partial class CharacterView : UserControl
{
    private readonly IClientSessionService? _sessions;
    private readonly DispatcherTimer _refreshTimer;
    private ClientSessionConnection? _selected;
    private RealmGateWorkshopDataCatalog? _catalog;
    private bool _catalogLoading;
    private readonly List<RealmGateWorkshopItem> _plannedItems = new();

    public CharacterView() : this(null) { }

    public CharacterView(IClientSessionService? sessions)
    {
        InitializeComponent();
        _sessions = sessions;
        if (_sessions is not null)
        {
            SessionList.ItemsSource = _sessions.Sessions;
            if (_sessions.Sessions.Count > 0)
                SessionList.SelectedIndex = 0;
        }

        _refreshTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(750), DispatcherPriority.Background, (_, _) => RefreshSelected(), Dispatcher);
        Loaded += (_, _) => _refreshTimer.Start();
        Unloaded += (_, _) => _refreshTimer.Stop();
    }

    private async void SessionList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_selected is not null)
            _selected.PropertyChanged -= Selected_OnPropertyChanged;

        _selected = SessionList.SelectedItem as ClientSessionConnection;
        _catalog = null;
        if (_selected is not null)
            _selected.PropertyChanged += Selected_OnPropertyChanged;

        RefreshSelected();
        await LoadCatalogAsync();
    }

    private void Selected_OnPropertyChanged(object? sender, PropertyChangedEventArgs e) => Dispatcher.InvokeAsync(RefreshSelected);

    private void RefreshSelected()
    {
        var session = _selected;
        if (session is null)
        {
            CharacterTitle.Text = "No character selected";
            CharacterSubtitle.Text = "Open or configure a Play session to populate this Character area.";
            AuthorityStatus.Text = "Character planning is read-only.";
            IdentityText.Text = VitalsText.Text = ProgressText.Text = StatusText.Text = StatisticsText.Text = BonusBreakdownText.Text = ProgressionDetailsText.Text = string.Empty;
            CurrentEquipmentList.ItemsSource = KnownSpellsList.ItemsSource = ActiveEffectsList.ItemsSource = EquipmentSpellEffectsList.ItemsSource = null;
            PlannedEquipmentList.ItemsSource = null;
            ItemFinderSummary.Text = PlannedItemText.Text = LoadoutComparisonText.Text = CpPlanSummary.Text = string.Empty;
            return;
        }

        var name = string.IsNullOrWhiteSpace(session.CharacterName) ? session.DisplayName : session.CharacterName;
        CharacterTitle.Text = name;
        CharacterSubtitle.Text = $"{session.ServerProfileName} / {session.WorldName} • {session.ConnectionState} • {session.CurrentRoomName}";
        AuthorityStatus.Text = _catalog is null
            ? "Effective game-data authority: loading…"
            : $"{_catalog.AuthorityDescription} • {_catalog.Items.Count:N0} effective item records";

        IdentityText.Text =
            $"Name:       {name}\n" +
            $"World:      {session.WorldName}\n" +
            $"Level:      {session.CharacterLevel}\n" +
            $"Location:   {Dash(session.CurrentRoomName)}\n" +
            $"Party:      {(session.PartyMembers.Count == 0 ? "—" : $"{session.PartyMembers.Count} member(s)")}";

        VitalsText.Text =
            $"HP:         {session.HealthCurrent:N0}/{session.HealthMaximum:N0} ({session.HealthPercent}%)\n" +
            $"Mana/Kai:   {session.ManaCurrent:N0}/{session.ManaMaximum:N0} ({session.ManaPercent}%)\n" +
            $"Carry:      {session.CarriedWeight:N0}/{session.CarryingCapacity:N0}\n" +
            $"Encumbered: {session.EncumbrancePercent}% {session.EncumbranceBand}";

        ProgressText.Text =
            $"XP:         {session.ExperienceCurrent:N0}\n" +
            $"XP needed:  {session.ExperienceNeeded:N0}\n" +
            $"XP remain:  {session.ExperienceRemaining:N0}\n" +
            $"Session XP: {session.SessionExperienceGained:N0}\n" +
            $"CP:         {session.CharacterPoints:N0}\n" +
            $"Lives:      {session.Lives:N0}";

        StatusText.Text =
            $"Combat:     {(session.InCombat ? "Active" : "No")}\n" +
            $"Resting:    {(session.IsResting ? "Yes" : "No")}\n" +
            $"Moving:     {(session.IsMoving ? "Yes" : "No")}\n" +
            $"PvP:        {session.PvpRealmSummary}";

        CurrentEquipmentList.ItemsSource = session.EquippedItems;
        KnownSpellsList.ItemsSource = session.LearnedSpellDetails.Count > 0
            ? session.LearnedSpellDetails.Select(x => $"{x.Name} • mana {x.ManaCost:N0} • energy {x.EnergyCost:N0}").ToArray()
            : session.LearnedSpells.ToArray();
        ActiveEffectsList.ItemsSource = session.ActiveEffectDetails.Count > 0
            ? session.ActiveEffectDetails.Select(FormatActiveEffect).ToArray()
            : session.ActiveEffects.ToArray();

        var matched = _catalog is null ? Array.Empty<RealmGateWorkshopItem>() : session.EquippedItems.Select(x => _catalog.FindByDisplayName(x)).Where(x => x is not null).Cast<RealmGateWorkshopItem>().ToArray();
        EquipmentSpellEffectsList.ItemsSource = _catalog is null ? Array.Empty<string>() : BuildEquipmentSpellEffects(matched, _catalog).ToArray();
        EquipmentSummary.Text = _catalog is null
            ? $"Current captured equipment: {session.EquippedItems.Count:N0}. Loading effective item records…"
            : $"Matched {matched.Length:N0}/{session.EquippedItems.Count:N0} equipped entries • AC bonus sum {matched.Sum(x => x.ArmorClassBonus):+#;-#;0} • DR bonus sum {matched.Sum(x => x.DamageResistanceBonus):+#;-#;0} • catalog weight {matched.Sum(x => x.Weight):N0}.";

        StatisticsText.Text =
            $"HP current/max        {session.HealthCurrent:N0} / {session.HealthMaximum:N0}\n" +
            $"Mana current/max      {session.ManaCurrent:N0} / {session.ManaMaximum:N0}\n" +
            $"Encumbrance           {session.EncumbrancePercent}%\n" +
            $"Combat events         {session.SessionCombatEventCount:N0}\n" +
            $"Rooms observed        {session.SessionRoomsObserved:N0}\n" +
            $"Equipment AC bonus    {matched.Sum(x => x.ArmorClassBonus):+#;-#;0}\n" +
            $"Equipment DR bonus    {matched.Sum(x => x.DamageResistanceBonus):+#;-#;0}\n" +
            $"Equipment accuracy    {matched.Sum(x => x.Accuracy):+#;-#;0}\n" +
            $"Equipment weight      {matched.Sum(x => x.Weight):N0}\n" +
            $"Decoded item abilities {matched.Sum(x => x.Abilities.Count):N0}\n" +
            $"Equipment spell refs   {matched.Count(x => x.CastSpellLegacyId > 0):N0}";

        BonusBreakdownText.Text = BuildBonusBreakdown(matched);
        ProgressionDetailsText.Text = BuildProgression(session);
        RefreshItemFinder();
        RefreshPlannedLoadout();
        RefreshCpPlan();
    }

    private async Task LoadCatalogAsync()
    {
        if (_catalogLoading || _selected is null) return;
        _catalogLoading = true;
        var session = _selected;
        try
        {
            _catalog = await Task.Run(() => RealmGateWorkshopDataCatalog.Load(session));
        }
        catch (Exception exception)
        {
            _catalog = null;
            AuthorityStatus.Text = "Catalog unavailable: " + exception.Message;
        }
        finally
        {
            _catalogLoading = false;
            RefreshSelected();
        }
    }

    private void ItemSearchBox_OnTextChanged(object sender, TextChangedEventArgs e) => RefreshItemFinder();

    private void RefreshItemFinder()
    {
        if (_catalog is null)
        {
            ItemFinderGrid.ItemsSource = null;
            return;
        }

        var query = ItemSearchBox.Text.Trim();
        IEnumerable<RealmGateWorkshopItem> items = _catalog.Items;
        if (query.Length > 0)
            items = items.Where(item => item.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || item.Slot.Contains(query, StringComparison.OrdinalIgnoreCase));
        if (_selected?.CharacterLevel > 0)
            items = items.Where(item => item.MinimumLevel <= 0 || item.MinimumLevel <= _selected.CharacterLevel);
        var rows = items.Take(1000).ToArray();
        ItemFinderGrid.ItemsSource = rows;
        ItemFinderSummary.Text = $"{rows.Length:N0} item(s) shown" + (rows.Length == 1000 ? " (display capped at 1,000)" : string.Empty) + " • level filtering uses captured level only; class/race eligibility is not guessed.";
    }

    private void ItemFinderGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ItemFinderGrid.SelectedItem is not RealmGateWorkshopItem item)
        {
            PlannedItemText.Text = "Select an item to inspect it as a hypothetical planning candidate.";
            return;
        }

        PlannedItemText.Text = $"Candidate — {item.Name} • ID {item.Id} • type {Dash(item.ItemType)} • slot {Dash(item.Slot)} • damage {item.Damage} • AC {item.ArmorClassBonus:+#;-#;0} • DR {item.DamageResistanceBonus:+#;-#;0} • accuracy {item.Accuracy:+#;-#;0} • speed {item.Speed:+#;-#;0} • weight {item.Weight:N0} • cost {item.Cost:N0} • restrictions {item.Restrictions}. Planning only; no equip command is sent.";
    }

    private void AddPlannedItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (ItemFinderGrid.SelectedItem is not RealmGateWorkshopItem item) return;
        if (!string.IsNullOrWhiteSpace(item.Slot))
            _plannedItems.RemoveAll(existing => string.Equals(existing.Slot, item.Slot, StringComparison.OrdinalIgnoreCase));
        _plannedItems.Add(item);
        RefreshPlannedLoadout();
    }

    private void RemovePlannedItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (PlannedEquipmentList.SelectedItem is RealmGateWorkshopItem item)
            _plannedItems.Remove(item);
        RefreshPlannedLoadout();
    }

    private void ClearPlannedItems_OnClick(object sender, RoutedEventArgs e)
    {
        _plannedItems.Clear();
        RefreshPlannedLoadout();
    }

    private void RefreshPlannedLoadout()
    {
        PlannedEquipmentList.ItemsSource = null;
        PlannedEquipmentList.ItemsSource = _plannedItems.ToArray();
        if (_selected is null || _catalog is null)
        {
            LoadoutComparisonText.Text = "Load the effective game-data projection to compare a planned loadout.";
            return;
        }

        var current = _selected.EquippedItems
            .Select(text => _catalog.FindByDisplayName(text))
            .Where(item => item is not null)
            .Cast<RealmGateWorkshopItem>()
            .ToArray();
        var planned = _plannedItems.ToArray();

        static long Weight(IEnumerable<RealmGateWorkshopItem> rows) => rows.Sum(x => Math.Max(0, x.Weight));
        static int Ac(IEnumerable<RealmGateWorkshopItem> rows) => rows.Sum(x => x.ArmorClassBonus);
        static int Dr(IEnumerable<RealmGateWorkshopItem> rows) => rows.Sum(x => x.DamageResistanceBonus);
        static int Accuracy(IEnumerable<RealmGateWorkshopItem> rows) => rows.Sum(x => x.Accuracy);
        static int MinDamage(IEnumerable<RealmGateWorkshopItem> rows) => rows.Sum(x => Math.Max(0, x.MinimumDamage));
        static int MaxDamage(IEnumerable<RealmGateWorkshopItem> rows) => rows.Sum(x => Math.Max(0, x.MaximumDamage));

        string Row(string name, long a, long b) => $"{name,-16} {a,10:N0} {b,10:N0} {b-a,10:+#;-#;0}";
        var aboveLevel = planned.Where(x => x.MinimumLevel > 0 && _selected.CharacterLevel > 0 && x.MinimumLevel > _selected.CharacterLevel).ToArray();
        var warnings = aboveLevel.Length == 0 ? string.Empty : $"\nAbove captured level: {string.Join(", ", aboveLevel.Select(x => $"{x.Name} (L{x.MinimumLevel})"))}";

        LoadoutComparisonText.Text =
            "Decoded loadout comparison (current vs planned)\n" +
            $"{"Metric",-16} {"Current",10} {"Planned",10} {"Delta",10}\n" +
            Row("Item weight", Weight(current), Weight(planned)) + "\n" +
            Row("AC bonus", Ac(current), Ac(planned)) + "\n" +
            Row("DR bonus", Dr(current), Dr(planned)) + "\n" +
            Row("Accuracy", Accuracy(current), Accuracy(planned)) + "\n" +
            Row("Min damage", MinDamage(current), MinDamage(planned)) + "\n" +
            Row("Max damage", MaxDamage(current), MaxDamage(planned)) + warnings +
            "\nPlanning compares decoded item fields only; it does not infer proc effects, class/race eligibility, or unproven formulas.";
    }

    private void CpPlan_OnTextChanged(object sender, TextChangedEventArgs e) => RefreshCpPlan();

    private void RefreshCpPlan()
    {
        if (CpPlanSummary is null) return;
        var available = _selected?.CharacterPoints ?? 0;
        static int Read(TextBox? box) => box is not null && int.TryParse(box.Text.Trim(), out var value) && value > 0 ? value : 0;
        var planned = Read(CpCombatBox) + Read(CpPhysicalBox) + Read(CpMagicBox) + Read(CpOtherBox);
        var remaining = available - planned;
        CpPlanSummary.Text = remaining >= 0
            ? $"Available CP: {available:N0} • planned reserve: {planned:N0} • unassigned: {remaining:N0}"
            : $"Available CP: {available:N0} • planned reserve: {planned:N0} • over budget by {-remaining:N0}";
    }

    private static string BuildBonusBreakdown(IEnumerable<RealmGateWorkshopItem> items)
    {
        var rows = items.ToArray();
        if (rows.Length == 0) return "No authoritative/effective equipped-item matches are available yet.";
        var lines = new List<string>();
        Append("AC", rows, x => x.ArmorClassBonus, lines);
        Append("DR", rows, x => x.DamageResistanceBonus, lines);
        Append("Accuracy", rows, x => x.Accuracy, lines);
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "MinLevel", "CastsSp", "%Spell" };
        foreach (var group in rows.SelectMany(item => item.Abilities.Select(a => (item, ability: a)))
                     .Where(x => !excluded.Contains(x.ability.Name) && x.ability.NumericValue != 0)
                     .GroupBy(x => x.ability.Name, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add($"{group.Key} {group.Sum(x => x.ability.NumericValue):+#;-#;0}");
            foreach (var entry in group) lines.Add($"  {entry.item.Name} {entry.ability.NumericValue:+#;-#;0}");
        }
        return lines.Count == 0 ? "Matched equipment currently exposes no non-zero decoded bonuses in these fields." : string.Join("\n", lines);
    }

    private static void Append(string label, IEnumerable<RealmGateWorkshopItem> items, Func<RealmGateWorkshopItem, int> value, ICollection<string> lines)
    {
        var contributing = items.Select(item => (item, bonus: value(item))).Where(x => x.bonus != 0).ToArray();
        if (contributing.Length == 0) return;
        lines.Add($"{label} {contributing.Sum(x => x.bonus):+#;-#;0}");
        foreach (var entry in contributing) lines.Add($"  {entry.item.Name} {entry.bonus:+#;-#;0}");
    }

    private static IEnumerable<string> BuildEquipmentSpellEffects(IEnumerable<RealmGateWorkshopItem> items, RealmGateWorkshopDataCatalog catalog)
    {
        foreach (var item in items.Where(x => x.CastSpellLegacyId > 0))
        {
            var spell = catalog.FindSpellByLegacyId(item.CastSpellLegacyId);
            var spellName = spell?.Name ?? $"spell #{item.CastSpellLegacyId}";
            var chance = item.SpellChancePercent > 0 ? $" • chance {item.SpellChancePercent}%" : string.Empty;
            yield return $"{item.Name} → {spellName}{chance} • decoded item spell reference";
        }
    }

    private static string FormatActiveEffect(ClientActiveEffectState effect)
    {
        var expiry = effect.ExpiresAtUtc is null ? "no captured expiry" : $"expires {effect.ExpiresAtUtc.Value.LocalDateTime:g}";
        return $"{effect.Name} • {expiry}";
    }

    private static string BuildProgression(ClientSessionConnection session)
    {
        var elapsed = session.ConnectedAt is null ? TimeSpan.Zero : DateTimeOffset.UtcNow - session.ConnectedAt.Value;
        var hours = elapsed.TotalHours;
        var xpPerHour = hours > 0.01 ? session.SessionExperienceGained / hours : 0;
        var percent = session.ExperienceNeeded > 0 ? 100.0 * Math.Clamp((double)session.ExperienceCurrent / session.ExperienceNeeded, 0, 1) : 0;
        var eta = xpPerHour > 0 && session.ExperienceRemaining > 0 ? TimeSpan.FromHours(session.ExperienceRemaining / xpPerHour) : (TimeSpan?)null;
        var samples = session.SessionExperienceSamples.Count;
        var sampleSpan = samples >= 2 ? session.SessionExperienceSamples[^1].Timestamp - session.SessionExperienceSamples[0].Timestamp : TimeSpan.Zero;
        return
            $"Total/current XP      {session.ExperienceCurrent:N0}\n" +
            $"XP to next            {session.ExperienceRemaining:N0}\n" +
            $"Level progress        {percent:N1}% (derived from captured XP values)\n" +
            $"Session XP            {session.SessionExperienceGained:N0}\n" +
            $"Session duration      {elapsed:dd\\.hh\\:mm\\:ss}\n" +
            $"XP/hour               {xpPerHour:N0} (derived)\n" +
            $"XP history samples     {samples:N0} over {sampleSpan:dd\\.hh\\:mm\\:ss}\n" +
            $"ETA at current rate   {(eta is null ? "—" : eta.Value.ToString("dd\\.hh\\:mm\\:ss"))}";
    }

    private static string Dash(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;
}
