using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeWccSysopSmokeCheck(string Name, bool Passed, string Detail);

public sealed record RuntimeWccSysopSmokeResult(
    bool Passed,
    int TotalChecks,
    int PassedChecks,
    IReadOnlyList<RuntimeWccSysopSmokeCheck> Checks)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeWccSysopSmokeReport
{
    public static RuntimeWccSysopSmokeResult Run(string projectRoot)
    {
        var checks = new List<RuntimeWccSysopSmokeCheck>();
        void Check(string name, bool passed, string detail) => checks.Add(new(name, passed, detail));

        try
        {
            var catalog = RuntimeWccSysopCatalog.Load(projectRoot, "Classic");
            Check("catalog-loads-generated-data", true, $"commands={catalog.Commands.Count}");
            Check("all-decoded-command-roots-retained", catalog.Commands.Count == 20, $"commands={catalog.Commands.Count}; expected=20");
            Check("legacy-setting-retained", catalog.LegacySetting == "SYSOPKEY", catalog.LegacySetting);
            Check("user-confirmed-unlock-retained", catalog.ConfiguredValue == "code_of_twinks_78", catalog.ConfiguredValue);
            Check("plaintext-key-command-denied", !catalog.PlaintextKeyCommandAllowed, catalog.PlaintextKeyCommandAllowed.ToString());
            Check("authenticated-authority-declared",
                catalog.RuntimeAuthority == "authenticated_account_role_and_explicit_capabilities", catalog.RuntimeAuthority);

            var executable = new[] { "buffers", "status", "map", "report", "reset", "speak", "arena" };
            Check("safe-native-roots-executable",
                executable.All(name => catalog.Commands.TryGetValue(name, out var command) &&
                                       command.RuntimeDisposition == "executable"),
                string.Join(',', executable));
            Check("list-users-evil-gang-and-limited-presence-index-runtime-enabled",
                catalog.Commands["list"].RuntimeDisposition == "executable_authoritative_users_evil_gang_limited_presence_index" &&
                catalog.Commands["list"].Forms.SequenceEqual(new[] { "USERS", "EVIL", "GANG <gangname>", "LIMITED" }),
                catalog.Commands["list"].RuntimeDisposition);
            Check("report-users-mask-and-notopten-enabled",
                catalog.Commands["report"].RuntimeDisposition == "executable" &&
                catalog.Commands["report"].Forms.SequenceEqual(new[] { "USERS [mask]", "NOTOPTEN" }),
                catalog.Commands["report"].RuntimeDisposition);
            Check("god-authoritative-character-state-enabled-legacy-effects-fail-closed",
                catalog.Commands["god"].RuntimeDisposition == "partially_executable_authoritative_character_state_legacy_only_effects_fail_closed" &&
                catalog.Commands["god"].Forms.Contains("INVENTORY") &&
                catalog.Commands["god"].Forms.Contains("SPELLS") &&
                catalog.Commands["god"].Forms.Contains("PROFILE") &&
                catalog.Commands["god"].Forms.Contains("POWERS") &&
                catalog.Commands["god"].Forms.Contains("ABILITIES") &&
                catalog.Commands["god"].Forms.Contains("ADD EVIL <amount>") &&
                catalog.Commands["god"].Forms.Contains("CHANGE SEX") &&
                catalog.Commands["god"].Forms.Contains("STATUS") &&
                catalog.Commands["god"].Forms.Contains("SPEAK <message>") &&
                catalog.Commands["god"].Forms.Contains("DISABLE TOPTEN") &&
                catalog.Commands["god"].Forms.Contains("ENABLE TOPTEN"),
                catalog.Commands["god"].RuntimeDisposition);
            Check("topten-character-flag-promoted",
                RuntimeCharacterFlagIds.ToptenDisabled == "sysop.topten.disabled",
                RuntimeCharacterFlagIds.ToptenDisabled);
            Check("report-users-mask-account-and-name-semantics",
                RuntimeWccSysopService.MatchesReportUserMask("board-user-42", "Ardent Vale", "USER-42") &&
                RuntimeWccSysopService.MatchesReportUserMask("board-user-42", "Ardent Vale", "dent va") &&
                RuntimeWccSysopService.MatchesReportUserMask("board-user-42", "Ardent Vale", string.Empty) &&
                !RuntimeWccSysopService.MatchesReportUserMask("board-user-42", "Ardent Vale", "missing"),
                "case-insensitive substring filter covers local account id and full character name");
            Check("gameop-scope-retained",
                catalog.Commands.Values.Count(command => command.GameopAllowed) == 10,
                $"gameop={catalog.Commands.Values.Count(command => command.GameopAllowed)}");
            Check("all-sysop-roots-finally-classified",
                catalog.Commands.Values.All(command => !command.RuntimeDisposition.Contains("implementation_required", StringComparison.OrdinalIgnoreCase)) &&
                catalog.Commands["initialize"].RuntimeDisposition == "executable_standalone_spawn_reinitialization" &&
                catalog.Commands["configure"].RuntimeDisposition == "partially_executable_show_maxusers_not_applicable_standalone_app_owns_realm_capacity" &&
                catalog.Commands["lightning"].RuntimeDisposition == "executable_native_fiend_lightning_contract" &&
                catalog.Commands["reload"].RuntimeDisposition == "not_applicable_no_wccmud_ini_in_standalone_reforged",
                "20/20 roots have executable, not-applicable, or exact-blocker dispositions");
            var lightningRecord = RuntimeChunkedClassicCatalog.LoadRecords(
                    Path.Combine(projectRoot, "Data", "Classic", "sysop_commands.json"))
                .Single(record => JsonValueReader.ReadString(record, "command", string.Empty).Equals("lightning", StringComparison.OrdinalIgnoreCase));
            Check("sysop-lightning-has-no-alignment-mutation",
                JsonValueReader.ReadString(lightningRecord, "alignment_effect", string.Empty).Equals("none", StringComparison.OrdinalIgnoreCase),
                "LIGHTEVY is target eligibility only; SYSOP LIGHTNING never changes good/evil points");
            Check("sysop-lightning-native-damage-and-presentation-contract",
                catalog.Commands["lightning"].AllowAllTargets
                && catalog.Commands["lightning"].DamageMinimumMaximumHitPointPercent == 25
                && catalog.Commands["lightning"].DamageMaximumMaximumHitPointPercent == 50
                && catalog.Commands["lightning"].NativeMessageId == 2842
                && Enumerable.Range(0, 100).Select(_ => RuntimeWccSysopService.RollNativeLightningDamage(100, Random.Shared)).All(value => value is >= 25 and <= 50)
                && RuntimeWccSysopService.FormatNativeLightning("A bolt strikes %s for %d damage!", "Target", 37) == "A bolt strikes Target for 37 damage!",
                "native DLL contract is inclusive 25%-50% maximum HP with decoded LIGHTEVY=YES and WCCMSG2 message 2842");
            Check("persistent-gang-administration-connected",
                new[] { "disband", "disable", "enable", "gangsize" }.All(name =>
                    catalog.Commands[name].RuntimeDisposition == "executable_persistent_gang_state") &&
                catalog.Commands["unwrap"].RuntimeDisposition == "not_applicable_current_reforged_gang_state_requires_no_legacy_unwrap",
                "LIST GANG plus four persistent administration commands are live; legacy UNWRAP is not required by current Reforged gang state.");
            Check("gang-state-schema-owned",
                typeof(RuntimeClassicGangRecord).GetProperty(nameof(RuntimeClassicGangRecord.Members)) is not null &&
                typeof(RuntimeClassicGangRecord).GetProperty(nameof(RuntimeClassicGangRecord.GangHouseId)) is not null &&
                typeof(RuntimeClassicGangStateService).GetMethod(nameof(RuntimeClassicGangStateService.Disband)) is not null,
                "identity, leader, roster, size, experience, TOPTEN, disband notifications, and gang-house link are persistent");
            Check("player-gang-lifecycle-command-routes",
                new[] { "create", "invite", "uninvite", "join", "leave", "broadgang", "disband", "promote", "demote", "topten" }.All(RuntimeCommand.SupportedCommands.Contains) &&
                RuntimeCommand.Parse("bg hello").Name == "broadgang" &&
                RuntimeCommand.Parse("top 5 gangs").Name == "topten" &&
                RuntimeCommand.Parse("create gang Night Watch").ArgumentText == "gang Night Watch",
                "CREATE/INVITE/UNINVITE/JOIN/LEAVE/BG/DISBAND/PROMOTE/DEMOTE/TOPTEN routes are registered");
            Check("gang-role-and-login-notification-lifecycle-owned",
                typeof(RuntimeClassicGangStateService).GetMethod(nameof(RuntimeClassicGangStateService.SetMemberRole)) is not null &&
                typeof(RuntimeClassicGangStateService).GetMethod(nameof(RuntimeClassicGangStateService.ConsumeDisbandNotifications)) is not null,
                "leader-only lieutenant changes and one-time disband login delivery are persistent");
            Check("gang-experience-and-ranking-owned",
                typeof(RuntimeClassicGangStateService).GetMethod(nameof(RuntimeClassicGangStateService.AwardMemberExperience)) is not null &&
                typeof(RuntimeClassicGangStateService).GetMethod(nameof(RuntimeClassicGangStateService.Top)) is not null,
                "NPC combat, decoded ASK, and generated TALK earned-XP deltas persist to gang totals; TOPTEN excludes disabled/disbanded gangs and orders by experience");
            Check("goto-targets-active",
                catalog.Commands["goto"].RuntimeDisposition == "active_runtime",
                catalog.Commands["goto"].RuntimeDisposition);
            var gotoRecord = RuntimeChunkedClassicCatalog.LoadRecords(
                    Path.Combine(projectRoot, "Data", "Classic", "sysop_commands.json"))
                .Single(record => JsonValueReader.ReadString(record, "command", string.Empty).Equals("goto", StringComparison.OrdinalIgnoreCase));
            var targets = gotoRecord.GetProperty("targets");
            var expectedGotoTargets = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
            {
                ["newhaven"]="room.map1.2150",
                ["silvermere"]="room.map1.2189",
                ["rhudaur"]="room.map2.2519",
                ["khazarad"]="room.map6.1249",
                ["lost_city"]="room.map16.426",
                ["support"]="room.map1.164"
            };
            Check("all-named-goto-targets-exact",
                expectedGotoTargets.All(pair=>targets.TryGetProperty(pair.Key,out var value)&&value.GetString()==pair.Value) &&
                expectedGotoTargets.All(pair=>catalog.Commands["goto"].Targets.TryGetValue(RuntimeWccSysopCatalog.NormalizeDestination(pair.Key),out var value)&&value==pair.Value),
                string.Join(',',expectedGotoTargets.Select(pair=>$"{pair.Key}={pair.Value}")));
            Check("invasion-native-scripts-recovered",
                catalog.Commands["invasion"].RuntimeDisposition ==
                "active_room_scoped_decoded_trigger" &&
                catalog.Commands["invasion"].Forms.Count == 6,
                catalog.Commands["invasion"].RuntimeDisposition);
            Check("sysop-command-registered",
                RuntimeCommand.Parse("sysop status").Name == "sysop",
                RuntimeCommand.Parse("sysop status").Name);
            Check("sys-goto-alias-registered",
                RuntimeCommand.Parse("sys goto newhaven").Name == "sysop" &&
                RuntimeCommand.Parse("sys goto newhaven").ArgumentText == "goto newhaven",
                $"name={RuntimeCommand.Parse("sys goto newhaven").Name}; args={RuntimeCommand.Parse("sys goto newhaven").ArgumentText}");
            Check("unlock-value-not-command",
                !RuntimeCommand.SupportedCommands.Contains("code_of_twinks_78"),
                "configured unlock is not in player command catalog");
            var interactions = RuntimeChunkedClassicCatalog.LoadRecords(
                Path.Combine(projectRoot, "Data", "Classic", "room_interactions.json"))
                .Where(record => JsonValueReader.ReadString(record, "interaction_kind", string.Empty)
                    .Equals("native_support_invasion", StringComparison.OrdinalIgnoreCase)).ToArray();
            Check("six-support-room-invasion-commands-projected",
                interactions.Length == 6 && interactions.All(record =>
                    JsonValueReader.ReadString(record, "runtime_disposition", string.Empty)
                        .Equals("active_runtime", StringComparison.OrdinalIgnoreCase)),
                $"projected={interactions.Length}");
            var requiredRandomIds = new[] { 4369,4370,4371,4372,4647,4648,4649,4650,4651,4652,4653,4654,4655,
                4659,4660,4661,4662,4663,4664,4665,4666,4669,4670,4671,4672 };
            var generatedScripts=RuntimeChunkedClassicCatalog.LoadRecords(Path.Combine(projectRoot,"Data","Classic","invasion_scripts.json"));
            Check("wcc-update-invasion-tables-recovered",
                generatedScripts.Count==31&&requiredRandomIds.All(id=>generatedScripts.Any(record=>JsonValueReader.ReadInt(record,"textblock_id",0)==id)),
                $"generated={generatedScripts.Count}; placementChildren={requiredRandomIds.Count(id=>generatedScripts.Any(record=>JsonValueReader.ReadInt(record,"textblock_id",0)==id))}/{requiredRandomIds.Length}");
            var invasionCatalog=RuntimeWccInvasionCatalog.Load(projectRoot,"Classic");
            var invasionCommands=new[]{"orc invasion","cult invasion","red invasion","green invasion","duergar invasion","saracen invasion"};
            Check("invasion-definitions-consumed-from-generated-data",
                invasionCommands.All(command=>invasionCatalog.RequireDefinition(command) is { SupportRoomId:"room.map1.164", GateItemId.Length:>0, PlacementTextblockIds.Length:>0, SuccessMessageId:>0 }),
                string.Join(',',invasionCommands));
        }
        catch (Exception ex)
        {
            Check("catalog-loads-generated-data", false, $"{ex.GetType().Name}: {ex.Message}");
        }

        return new RuntimeWccSysopSmokeResult(
            checks.All(check => check.Passed),
            checks.Count,
            checks.Count(check => check.Passed),
            checks);
    }
}
