using System.Text;
using Reforged.App.Models;

namespace Reforged.App.Services;

public static class ClientMapLibrariumRegressionSmoke
{
    public static async Task<int> RunAsync(IApplicationSettingsService settings)
    {
        var failures = new List<string>();
        void Require(bool condition, string name) { if (!condition) failures.Add(name); }

        var maps = new ClientMapProfileService(settings);
        await maps.SelectProfileAsync("Local Host", "Classic", "Arziel");
        maps.Profile.FogOfWarEnabled = true;
        maps.Profile.ShowHiddenObjects = false;
        maps.RecordVisit("Town Gates", "1:1");
        await maps.SaveAsync();

        var reloaded = new ClientMapProfileService(settings);
        await reloaded.SelectProfileAsync("Local Host", "Classic", "Arziel");
        Require(reloaded.Profile.CharacterName == "Arziel", "character-map-profile-identity");
        Require(reloaded.Profile.Rooms.Any(room => room.RoomKey == "1:1" && room.Discovered), "fog-discovery-persists");
        Require(reloaded.Profile.FogOfWarEnabled && !reloaded.Profile.ShowHiddenObjects, "map-visibility-preferences-persist");
        await reloaded.SelectProfileAsync("Local Host", "Classic", "Different Hero");
        Require(reloaded.Profile.Rooms.Count == 0, "character-fog-isolation");

        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes("private room data"));
        var frame = $"@@REFORGED-PROTOCOL|ROOMINSPECTION|{payload}\r\n";
        var filter = new ReforgedClientProtocolFilter();
        var first = filter.Process("Visible game text\r\n" + frame[..17]);
        var second = filter.Process(frame[17..] + "More game text\r\n");
        Require((first.VisibleText + second.VisibleText).Contains("Visible game text", StringComparison.Ordinal), "visible-game-text-preserved");
        Require((first.VisibleText + second.VisibleText).Contains("More game text", StringComparison.Ordinal), "visible-tail-preserved");
        Require(!(first.VisibleText + second.VisibleText).Contains("REFORGED-PROTOCOL", StringComparison.Ordinal), "private-frame-hidden-from-terminal");
        Require(second.Messages.Count == 1 && second.Messages[0].Channel == "ROOMINSPECTION" && second.Messages[0].Payload == "private room data", "chunked-private-frame-decoded");

        filter.NoteOutboundCommand("librarium page exits 0");
        var echo = filter.Process("[HP=41]: librarium page exits 0\r\nordinary room text\r\n");
        Require(!echo.VisibleText.Contains("librarium page exits", StringComparison.OrdinalIgnoreCase), "private-command-echo-hidden");
        Require(echo.VisibleText.Contains("ordinary room text", StringComparison.Ordinal), "game-text-after-private-echo-preserved");

        // TelnetConnection echoes command input byte-by-byte, so private command
        // echoes must remain hidden even when TCP divides them across reads.
        filter.NoteOutboundCommand("clientdata stats");
        var splitEcho1 = filter.Process("[HP=35/MA=0]: cl");
        var splitEcho2 = filter.Process("ientdata ");
        var splitEcho3 = filter.Process("stats\r\nvisible after split echo\r\n");
        var splitEchoVisible = splitEcho1.VisibleText + splitEcho2.VisibleText + splitEcho3.VisibleText;
        Require(!splitEchoVisible.Contains("clientdata stats", StringComparison.OrdinalIgnoreCase), "chunked-private-command-echo-hidden");
        Require(splitEchoVisible.Contains("visible after split echo", StringComparison.Ordinal), "chunked-private-command-tail-preserved");

        // Repeated polling commands are separate private sends and every echoed
        // instance must be consumed rather than only the first one.
        filter.NoteOutboundCommand("clientdata pvp");
        filter.NoteOutboundCommand("clientdata pvp");
        var repeatedEcho = filter.Process("[HP=35/MA=0]: clientdata pvp\r\n[HP=35/MA=0]: clientdata pvp\r\n");
        Require(!repeatedEcho.VisibleText.Contains("clientdata pvp", StringComparison.OrdinalIgnoreCase), "repeated-private-command-echoes-hidden");

        Require(LibrariumFriendlyNames.Family("rooms") == "Rooms and Areas", "friendly-room-family");
        Require(LibrariumFriendlyNames.Family("npcs") == "Creatures and Bosses", "friendly-creature-family");
        Require(LibrariumFriendlyNames.Family("room_commands") == "Things You Can Do in Rooms", "friendly-room-command-family");
        return failures.Count == 0 ? 0 : 42;
    }
}
