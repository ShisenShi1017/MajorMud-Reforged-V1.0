namespace Reforged.Server;

public sealed partial class RuntimeLiveGameplayFacade
{
    public string RenderClientPanelData(RuntimeSession session, RuntimeCommand command)
    {
        var panel = command.Arguments.FirstOrDefault()?.Trim().ToLowerInvariant() ?? string.Empty;
        var payload = panel switch
        {
            "inventory" => RenderInventory(session),
            "equipment" => RenderEquipment(session),
            "party" or "par" => RenderClassicParty(session),
            "who" => RenderWho(),
            "stats" or "score" => RenderScore(session),
            "spells" => ExecutePolicyCommand(session, RuntimeCommand.Parse("spells", session.World.CommandCatalog)),
            _ => "Unknown private client panel request."
        };
        return ReforgedClientProtocol.Encode("PANELDATA", payload);
    }
}
