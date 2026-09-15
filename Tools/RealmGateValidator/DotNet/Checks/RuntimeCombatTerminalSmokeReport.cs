using System.Text;
using System.Text.Json;

namespace RealmGate.Server;

public sealed class RuntimeCombatTerminalSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();

    public static RuntimeCombatTerminalSmokeReport Run()
    {
        var report = new RuntimeCombatTerminalSmokeReport();
        void Require(bool condition, string name)
        {
            if (condition) report.Checks.Add(name);
            else report.Failures.Add(name);
        }

        var prompt = "[HP=45/50]:";
        var messages = new[]
        {
            "You hit the goblin for 8 damage.\nThe goblin dies.\nYou gain 120 experience.\n*Combat Off*"
        };

        var output = RuntimeTerminalOutputFormatter.ComposeAsynchronousOutput(
            messages,
            prompt,
            promptAlreadyVisible: true);

        Require(output.StartsWith(RuntimeTerminalOutputFormatter.NetworkNewLine, StringComparison.Ordinal),
            "async-combat-output-finishes-existing-prompt-line");
        Require(RuntimeTerminalOutputFormatter.ContainsOnlyCrLfLineEndings(output),
            "combat-output-crlf-only");
        Require(output.EndsWith(prompt, StringComparison.Ordinal),
            "combat-completion-restores-prompt");
        Require(RuntimeTerminalOutputFormatter.CountOccurrences(output, prompt) == 1,
            "combat-completion-no-duplicate-prompt");
        Require(!output.Contains("\r\n\r\n", StringComparison.Ordinal),
            "combat-completion-no-extra-blank-line");

        var deathIndex = output.IndexOf("The goblin dies.", StringComparison.Ordinal);
        var xpIndex = output.IndexOf("You gain 120 experience.", StringComparison.Ordinal);
        var combatOffIndex = output.IndexOf("*Combat Off*", StringComparison.Ordinal);
        var promptIndex = output.IndexOf(prompt, StringComparison.Ordinal);
        Require(deathIndex >= 0 && xpIndex > deathIndex && combatOffIndex > xpIndex && promptIndex > combatOffIndex,
            "combat-message-order-before-prompt");

        var bytes = Encoding.ASCII.GetBytes(output);
        Require(bytes.Length >= prompt.Length + 2
            && bytes[0] == 13
            && bytes[1] == 10,
            "combat-raw-bytes-start-crlf");
        Require(!bytes.Where((value, index) => value == 10 && (index == 0 || bytes[index - 1] != 13)).Any(),
            "combat-raw-bytes-no-bare-lf");

        var noPrompt = RuntimeTerminalOutputFormatter.ComposeAsynchronousOutput(
            new[] { "The player dies.\n*Combat Off*" },
            prompt: null,
            promptAlreadyVisible: true);
        Require(!noPrompt.Contains(prompt, StringComparison.Ordinal),
            "modal-or-death-state-can-suppress-prompt");

        foreach (var scenario in new[]
                 {
                     "normal-melee-kill",
                     "spell-kill",
                     "damage-over-time-kill",
                     "npc-flees",
                     "player-flees",
                     "target-despawns",
                     "combat-cancelled",
                     "player-moves-out-of-combat",
                     "player-death",
                     "grouped-combat",
                     "multiple-enemies-final-enemy",
                     "disconnect-reconnect",
                     "script-triggered-combat-termination"
                 })
        {
            var scenarioOutput = RuntimeTerminalOutputFormatter.ComposeAsynchronousOutput(
                new[] { $"{scenario}\n*Combat Off*" },
                prompt,
                promptAlreadyVisible: true);
            Require(scenarioOutput.EndsWith(prompt, StringComparison.Ordinal)
                    && RuntimeTerminalOutputFormatter.CountOccurrences(scenarioOutput, prompt) == 1
                    && RuntimeTerminalOutputFormatter.ContainsOnlyCrLfLineEndings(scenarioOutput),
                "prompt-restoration-" + scenario);
        }

        return report;
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}
