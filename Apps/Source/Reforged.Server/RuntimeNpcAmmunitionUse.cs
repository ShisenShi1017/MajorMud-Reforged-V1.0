using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeNpcAmmunitionRequest(
    string InstanceId,
    string WeaponItemInstanceId,
    string AmmunitionItemInstanceId,
    int Quantity,
    bool CompatibilityConfirmed,
    string Authority);

public sealed record RuntimeNpcAmmunitionResult(
    bool Succeeded,
    string InstanceId,
    string WeaponItemInstanceId,
    string AmmunitionItemInstanceId,
    int Quantity,
    string Reason,
    RuntimeItemInstance? Ammunition);

/// <summary>Exact host-authorized NPC ammunition-consumption boundary. It validates an already equipped weapon and already carried ammunition, but does not infer weapon/ammunition compatibility, selection, rate, timing, or attack behavior.</summary>
public sealed class RuntimeNpcAmmunitionService
{
    private readonly RuntimeLivingWorldService _living;
    private readonly RuntimeItemEngine _items;

    public RuntimeNpcAmmunitionService(RuntimeLivingWorldService living, RuntimeItemEngine items)
    {
        _living = living;
        _items = items;
    }

    public RuntimeNpcAmmunitionResult Consume(RuntimeNpcAmmunitionRequest request)
    {
        var npc = _living.InspectNpc(request.InstanceId);
        if (npc is null) return Reject(request, "instance-not-found");
        if (!npc.Alive) return Reject(request, "npc-is-not-alive");
        if (string.IsNullOrWhiteSpace(request.Authority)) return Reject(request, "authority-required");
        if (!request.CompatibilityConfirmed) return Reject(request, "compatibility-confirmation-required");
        if (request.Quantity <= 0) return Reject(request, "positive-quantity-required");

        try
        {
            var weapon = _items.RequireInstance(request.WeaponItemInstanceId);
            if (!string.Equals(weapon.OwnerId, npc.InstanceId, StringComparison.OrdinalIgnoreCase) ||
                !weapon.LocationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(weapon.EquippedSlot))
                return Reject(request, "weapon-is-not-equipped-by-npc");

            var ammo = _items.RequireInstance(request.AmmunitionItemInstanceId);
            if (!string.Equals(ammo.OwnerId, npc.InstanceId, StringComparison.OrdinalIgnoreCase) ||
                !ammo.LocationKind.Equals(RuntimeItemLocations.Npc, StringComparison.OrdinalIgnoreCase))
                return Reject(request, "ammunition-is-not-carried-by-npc");

            var consumed = _items.ConsumeNpcQuantity(ammo.InstanceId, npc.InstanceId, request.Quantity);
            return new(true, npc.InstanceId, weapon.InstanceId, ammo.InstanceId, request.Quantity, "consumed", consumed);
        }
        catch (InvalidOperationException ex)
        {
            return Reject(request, Normalize(ex.Message));
        }
    }

    private static RuntimeNpcAmmunitionResult Reject(RuntimeNpcAmmunitionRequest request, string reason) =>
        new(false, request.InstanceId ?? string.Empty, request.WeaponItemInstanceId ?? string.Empty,
            request.AmmunitionItemInstanceId ?? string.Empty, request.Quantity, reason, null);

    private static string Normalize(string value) => string.Join('-', value.Trim().TrimEnd('.').ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}

public sealed class RuntimeNpcAmmunitionSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = [];
    public List<string> Failures { get; } = [];

    public static RuntimeNpcAmmunitionSmokeReport Run(string projectRoot)
    {
        var report = new RuntimeNpcAmmunitionSmokeReport();
        try
        {
            var options = ServerRuntimeOptions.Load(projectRoot);
            var host = new ServerRuntimeHost(projectRoot, options);
            host.Bootstrap();
            var world = host.Worlds.TryGetValue(options.DefaultWorldId, out var value) ? value : host.Worlds.Values.First();
            var living = new RuntimeLivingWorldService(projectRoot, world, Path.Combine(Path.GetTempPath(), "reforged-npc-ammunition-" + Guid.NewGuid().ToString("N")));
            var npc = living.InspectNpcs(new(IncludeDead: false)).First();
            var catalog = RuntimeItemCatalog.Load(projectRoot, world.Definition.Id);
            var items = new RuntimeItemEngine(catalog);
            var equipment = catalog.EquipmentDefinitions.First();
            var weapon = items.Create(equipment.ItemId, RuntimeItemLocations.Npc, npc.InstanceId, 1, npc.InstanceId);
            items.EquipNpc(weapon.InstanceId, npc.InstanceId, equipment.Slot);
            var ammoDef = catalog.Items.First();
            var ammoQuantity = ammoDef.Stackable ? 3 : 1;
            var ammo = items.Create(ammoDef.Id, RuntimeItemLocations.Npc, npc.InstanceId, ammoQuantity, npc.InstanceId);
            var service = new RuntimeNpcAmmunitionService(living, items);

            Require(!service.Consume(new(npc.InstanceId, weapon.InstanceId, ammo.InstanceId, 1, false, "smoke-host")).Succeeded, "compatibility-confirmation-enforced");
            Require(!service.Consume(new(npc.InstanceId, weapon.InstanceId, ammo.InstanceId, 1, true, string.Empty)).Succeeded, "authority-enforced");
            var consumeQuantity = ammoDef.Stackable ? 2 : 1;
            var accepted = service.Consume(new(npc.InstanceId, weapon.InstanceId, ammo.InstanceId, consumeQuantity, true, "smoke-host"));
            Require(accepted.Succeeded, "exact-ammunition-consumed");
            if (ammoDef.Stackable) Require(items.RequireInstance(ammo.InstanceId).StackQuantity == 1, "exact-quantity-remains");
            else Require(items.Find(ammo.InstanceId) is null, "exact-ammunition-instance-removed");
            report.Checks.AddRange(["compatibility-confirmation-enforced", "authority-enforced", "exact-ammunition-consumed", ammoDef.Stackable ? "exact-quantity-remains" : "exact-ammunition-instance-removed"]);
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
