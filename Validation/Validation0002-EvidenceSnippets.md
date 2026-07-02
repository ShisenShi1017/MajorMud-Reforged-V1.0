# Validation0002 Evidence Snippets

## MBBSEmu Host Cleanup Setup
```text
190:             _realTimeStopwatch = Stopwatch.StartNew();
191:             _incomingSessions = new Queue<SessionBase>();
192: 
193:             //Setup Cleanup Restart Event
194:             _cleanupTime = _configuration.CleanupTime;
195:             _cleanupTimer = new Timer(_ => _performCleanup = true, null, NowUntil(_cleanupTime + _cleanupGracePeriod), TimeSpan.FromDays(1));
196:             _cleanupWarningTimer = SetupCleanupWarningTimer();
197: 
198:             if (_configuration.TimerHertz > 0)
199:             {
200:                 _timerEvent = new AutoResetEvent(true);
201:                 _tickTimer = new Timer(_ => _timerEvent.Set(), this, TimeSpan.Zero, TimeSpan.FromMilliseconds(1000 / configuration.TimerHertz));
202:             }
```

## MBBSEmu Nightly Cleanup Routine
```text
1428:         ///     Method Invoked by the Nightly Cleanup time to begin the Nightly Cleanup Process
1429:         /// </summary>
1430:         private void ProcessNightlyCleanup()
1431:         {
1432:             if (_performCleanup)
1433:             {
1434:                 Logger.Info($"Beginning nightly cleanup process.");
1435:                 _performCleanup = false;
1436:                 DoNightlyCleanup();
1437: 
1438:                 // signal that cleanup has completed
1439:                 _cleanupRestartEvent?.Set();
1440:                 _cleanupRestartEvent = null;
1441: 
1442:                 _cleanupWarningTimer = SetupCleanupWarningTimer();
1443:                 Logger.Info($"Nightly cleanup complete.");
1444:             }
1445:         }
1446: 
1447:         /// <summary>
1448:         ///     Performs the Nightly Cleanup by invoking "MCUROU" (Nightly Cleanup) and "FINROU" (Finish-Up) routines in each module
1449:         /// </summary>
1450:         private void DoNightlyCleanup()
1451:         {
1452:             Logger.Info("PERFORMING NIGHTLY CLEANUP");
1453: 
1454:             // Notify Users of Nightly Cleanup
1455:             foreach (var c in _channelDictionary)
1456:                 _channelDictionary[c.Value.Channel].SendToClient($"|RESET|\r\n|B||RED|Nightly Cleanup Running -- Please log back on shortly|RESET|\r\n".EncodeToANSIArray());
1457: 
1458:             foreach (var localConsoleSession in _channelDictionary.Values.OfType<LocalConsoleSession>())
1459:                 localConsoleSession.StopHostOnStop = false;
1460: 
1461:             // removes all sessions before module cleanup
1462:             RemoveSessions(session => true);
1463: 
1464:             CallModuleRoutine("mcurou", module => Logger.Info($"Calling nightly cleanup routine on module {module.ModuleIdentifier}"));
1465:             CallModuleRoutine("finrou", module => Logger.Info($"Calling finish-up (sys-shutdown) routine on module {module.ModuleIdentifier}"));
1466: 
1467:             //Save current module config
1468:             var moduleConfigurations = (from m in _modules select m.Value.ModuleConfig).ToList();
1469: 
1470:             foreach (var m in _modules)
1471:             {
1472:                 var modulePath = m.Value.ModulePath;
1473: 
1474:                 m.Value.Dispose();
1475:                 _modules.Remove(m.Value.ModuleIdentifier);
1476:                 var exportedFunctionsToRemove = _exportedFunctions.Keys.Where(x => x.StartsWith(m.Value.ModuleIdentifier)).ToList();
1477: 
1478:                 foreach (var e in exportedFunctionsToRemove)
1479:                 {
1480:                     _exportedFunctions[e].Dispose();
1481:                     _exportedFunctions.Remove(e);
1482:                 }
1483: 
1484:                 foreach (var cleanup in m.Value.Mdf.Cleanup)
1485:                     RunProgram(modulePath, cleanup);
1486:             }
1487: 
1488:             Logger.Info("NIGHTLY CLEANUP COMPLETE -- RESTARTING HOST");
1489: 
1490:             Start(moduleConfigurations);
```

## MajorMUD background registration references
```text
line 32546: _BACKGROUND_FAST
line 32554: _BACKGROUND_MONSTER_CREATE
line 32561: _BACKGROUND_OTHER
line 32568: _BACKGROUND_SLOW
line 32575: _BACKGROUND_MEDIUM
line 32582: _BACKGROUND_ENERGY
line 32589: _BACKGROUND_INACTIVITY
line 73874: _BACKGROUND_ENERGY
line 74479: _BACKGROUND_SLOW
line 74528: _BACKGROUND_MEDIUM
line 74569: _BACKGROUND_FAST
line 74595: _BACKGROUND_FAST
line 75010: _BACKGROUND_MONSTER_CREATE
line 75052: _BACKGROUND_OTHER
line 75101: _BACKGROUND_SAVE_BUFFERS
line 75242: _BACKGROUND_INACTIVITY
line 108792: _BACKGROUND_SAVE_BUFFERS
line 180188: _BACKGROUND_UPDATE_EXITS
```

## Exported module files scanned
| File | case labels | ReadOnlySpan exports | SetState refs |
|---|---:|---:|---:|
| Doscalls.cs | 6 | 1 | 1 |
| ExportAttribute.cs | 0 | 0 | 0 |
| ExportedModuleBase.cs | 39 | 1 | 0 |
| Galgsbl.cs | 42 | 3 | 1 |
| Galme.cs | 4 | 1 | 1 |
| Galmsg.cs | 0 | 1 | 1 |
| IExportedModule.cs | 0 | 0 | 1 |
| Majorbbs.cs | 400 | 3 | 3 |
| Ordinals.cs | 0 | 0 | 0 |
| Phapi.cs | 9 | 1 | 1 |
