namespace RealmGate.Server;

/// <summary>
/// Executes a real generated-Classic-world native generation regression in an
/// isolated temporary save. No user saves or original game files are touched.
/// Requires rebuilding Data/Classic with the current Librarium compiler first.
/// </summary>
public static class RuntimeClassicNativeGenerationSmokeReport
{
    public static IReadOnlyList<string> Run(string projectRoot)
    {
        var failures = new List<string>();
        var smokeRoot = Path.Combine(Path.GetTempPath(),
            "reforged-classic-native-spawn-" + Guid.NewGuid().ToString("N"));
        try
        {
            var options = ServerRuntimeOptions.Load(projectRoot);
            var definition = options.Worlds.FirstOrDefault(world =>
                world.Id.Equals("Classic", StringComparison.OrdinalIgnoreCase));
            if (definition is null)
                return new[] { "Classic world definition is unavailable" };
            var world = new RuntimeWorldLoader().Load(projectRoot, definition);
            var room = world.FindRoom(1, 2342);
            if (room is null)
                return new[] { "Original Map 1 Room 2342 is not loaded" };
            if (!world.SpawnsByRoom.TryGetValue(room.Id, out var spawns))
                return new[] { "No generated Silver River spawn records; rebuild Classic data" };
            var native = spawns.FirstOrDefault(spawn =>
                spawn.MonsterGroupId == 21 && spawn.HasQualifiedMonsterIndexRange
                && spawn.NativeRoomGenerationCap == 10
                && spawn.NativeAreaControllerResolved && spawn.NativeAreaControllerCap == 15
                && spawn.MinimumMonsterIndex == 4 && spawn.MaximumMonsterIndex == 7);
            if (native is null)
                return new[] { "Silver River native cap/index projection missing; rebuild Classic data" };
            if (!native.GroupDefinitions.Select(npc => npc.LegacyId)
                    .OrderBy(id => id).SequenceEqual(new[] { 266, 267, 268 }))
                failures.Add("native-river-monster-candidates");

            var living = new RuntimeLivingWorldService(projectRoot, world, smokeRoot);
            var before = living.Capture();
            if (before.Npcs.Any(npc => npc.SpawnId.Equals(native.Id, StringComparison.OrdinalIgnoreCase)))
                failures.Add("ordinary-classic-npcs-must-not-pre-materialize");

            var now = DateTimeOffset.UtcNow;
            var initial = living.AdmitDueNpcRespawnsForRoom(room.Id, now);
            var generated = initial.Transitions.Where(transition =>
                transition.EventCode.StartsWith("npc-native-generated:", StringComparison.Ordinal)).ToArray();
            if (generated.Length != 1)
                failures.Add("room-entry-must-create-one-native-identity");

            var current = living.Capture();
            var first = current.Npcs.FirstOrDefault(npc =>
                npc.SpawnId.Equals(native.Id, StringComparison.OrdinalIgnoreCase)
                && npc.Alive && npc.Metadata.ContainsKey("native_classic_generated_monster"));
            if (first is null || !new[] { "npc.266", "npc.267", "npc.268" }
                    .Contains(first.NpcId, StringComparer.OrdinalIgnoreCase))
                failures.Add("generated-identity-must-be-eligible-river-monster");

            // Repeated native admissions cannot exceed the original per-room
            // ten-generation limit or shared controller cap fifteen.
            for (var i = 0; i < 25; i++)
                living.AdmitDueNpcRespawnsForRoom(room.Id, now.AddSeconds(i + 1));
            current = living.Capture();
            var roomPopulation = current.NativeRoomGeneratedCounts?.GetValueOrDefault(room.Id) ?? -1;
            if (roomPopulation > 10 || roomPopulation < 1)
                failures.Add("native-per-room-generated-count-10");
            if (current.NativeAreaPopulationCounts?.GetValueOrDefault(native.PopulationAreaKey) is not int areaPopulation
                || areaPopulation > 15 || areaPopulation < 1)
                failures.Add("native-controller-generated-count-15");
            if (living.NpcsInRoom(room.Id).Count > 15)
                failures.Add("native-physical-room-15-monster-slots");

            var aliveIds = living.Capture().Npcs.Where(npc => npc.Alive)
                .Select(npc => npc.InstanceId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var resumed = new RuntimeLivingWorldService(projectRoot, world, smokeRoot);
            var resumedSnapshot = resumed.Capture();
            if (!aliveIds.SetEquals(resumedSnapshot.Npcs.Where(npc => npc.Alive)
                    .Select(npc => npc.InstanceId)))
                failures.Add("native-save-restart-must-retain-unique-survivor-ids");
            if (resumedSnapshot.NativeRoomGeneratedCounts?.GetValueOrDefault(room.Id) != roomPopulation)
                failures.Add("native-room-counter-survives-restart");

            if (first is not null && resumed.ApplyNpcDamage(first.InstanceId, first.MaximumHitPoints, now.AddMinutes(1)))
            {
                var afterDeath = resumed.Capture();
                if (afterDeath.Npcs.Any(npc => npc.InstanceId.Equals(first.InstanceId,
                        StringComparison.OrdinalIgnoreCase) && npc.Alive))
                    failures.Add("native-dead-instance-must-remain-dead");
                if (afterDeath.NativeRoomGeneratedCounts?.GetValueOrDefault(room.Id) != roomPopulation - 1)
                    failures.Add("native-death-decrements-room-generation-count");
                if (afterDeath.NativeRoomLastKilledUtc is null
                    || !afterDeath.NativeRoomLastKilledUtc.ContainsKey(room.Id))
                    failures.Add("native-death-updates-last-killed");
                resumed.AdmitDueNpcRespawnsForRoom(room.Id, now.AddMinutes(1));
                if (resumed.Capture().Npcs.Any(npc => npc.InstanceId.Equals(first.InstanceId,
                        StringComparison.OrdinalIgnoreCase) && npc.Alive))
                    failures.Add("native-death-must-not-resurrect-same-identity");
            }
            else
                failures.Add("native-death-damage-transition");

            var livingBeforeReset = resumed.Capture().Npcs.Count(npc => npc.Alive);
            if (!resumed.ResetNativeClassicRoomPopulation(room.Id, now.AddMinutes(2))
                || resumed.Capture().NativeRoomGeneratedCounts?.GetValueOrDefault(room.Id) != 0
                || resumed.Capture().Npcs.Count(npc => npc.Alive) != livingBeforeReset)
                failures.Add("sysop-reset-room-counter-without-killing-monsters");
            if (!resumed.ResetNativeClassicAreaPopulation(room.Id, now.AddMinutes(2))
                || resumed.Capture().NativeAreaPopulationCounts?
                    .GetValueOrDefault(native.PopulationAreaKey) != 0
                || resumed.Capture().Npcs.Count(npc => npc.Alive) != livingBeforeReset)
                failures.Add("sysop-reset-area-counter-without-killing-monsters");
        }
        catch (Exception exception)
        {
            failures.Add($"{exception.GetType().Name}:{exception.Message}");
        }
        finally
        {
            try { if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return failures;
    }
}
