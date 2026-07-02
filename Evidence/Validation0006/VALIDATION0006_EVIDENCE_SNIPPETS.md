# Validation0006 Evidence Snippets

## MBBSEmu Host Cleanup: `CleanupTime` at line 95
Source: `/mnt/data/ext_validate/mbbsemu/MBBSEmu-master/MBBSEmu/HostProcess/MbbsHost.cs`

```text
92:         /// <summary>
93:         ///     Time of day that the cleanup routine will trigger
94:         /// </summary>
95:         private readonly TimeSpan _cleanupTime;
96: 
97:         /// <summary>
98:         ///     Timer that triggers when nightly cleanup should occur
```

## MBBSEmu Host Cleanup: `ProcessNightlyCleanup` at line 310
Source: `/mnt/data/ext_validate/mbbsemu/MBBSEmu-master/MBBSEmu/HostProcess/MbbsHost.cs`

```text
307:             {
308:                 WaitForNextTick();
309: 
310:                 ProcessNightlyCleanup();
311: 
312:                 //Handle Channels
313:                 ProcessIncomingSessions();
```

## MBBSEmu Host Cleanup: `DoNightlyCleanup` at line 1436
Source: `/mnt/data/ext_validate/mbbsemu/MBBSEmu-master/MBBSEmu/HostProcess/MbbsHost.cs`

```text
1433:             {
1434:                 Logger.Info($"Beginning nightly cleanup process.");
1435:                 _performCleanup = false;
1436:                 DoNightlyCleanup();
1437: 
1438:                 // signal that cleanup has completed
1439:                 _cleanupRestartEvent?.Set();
```

## MBBSEmu Host Cleanup: `mcurou` at line 1448
Source: `/mnt/data/ext_validate/mbbsemu/MBBSEmu-master/MBBSEmu/HostProcess/MbbsHost.cs`

```text
1445:         }
1446: 
1447:         /// <summary>
1448:         ///     Performs the Nightly Cleanup by invoking "MCUROU" (Nightly Cleanup) and "FINROU" (Finish-Up) routines in each module
1449:         /// </summary>
1450:         private void DoNightlyCleanup()
1451:         {
```

## MBBSEmu Host Cleanup: `finrou` at line 462
Source: `/mnt/data/ext_validate/mbbsemu/MBBSEmu-master/MBBSEmu/HostProcess/MbbsHost.cs`

```text
459:             }
460: 
461:             // let modules clean themselves up
462:             CallModuleRoutine("finrou", module => Logger.Info($"Calling shutdown routine on module {module.ModuleIdentifier}"));
463: 
464:             //clean up modules
465:             foreach (var m in _modules)
```

## MBBSEmu RTKICK/Polling: `ProcessRTKICK` at line 439
Source: `/mnt/data/ext_validate/mbbsemu/MBBSEmu-master/MBBSEmu/HostProcess/MbbsHost.cs`

```text
436:                 }
437: 
438:                 //Process Timed/Real-Time Events
439:                 ProcessRTKICK();
440:                 ProcessRTIHDLR();
441:                 ProcessSYSCYC();
442:                 ProcessTasks();
```

## MBBSEmu RTKICK/Polling: `BEGIN_POLLING` at line 410
Source: `/mnt/data/ext_validate/mbbsemu/MBBSEmu-master/MBBSEmu/HostProcess/MbbsHost.cs`

```text
407:                                     ProcessSTTROU(session);
408:                                 }
409: 
410:                                 //If the channel has been registered with BEGIN_POLLING
411:                                 if (session.PollingRoutine != null)
412:                                 {
413:                                     session.Status.Enqueue(EnumUserStatus.POLLING_STATUS);
```

## MBBSEmu RTKICK/Polling: `Methods registered using RTKICK` at line 755
Source: `/mnt/data/ext_validate/mbbsemu/MBBSEmu-master/MBBSEmu/HostProcess/MbbsHost.cs`

```text
752:         /// <summary>
753:         ///     Invokes routine registered during MAJORBBS->RTKICK()
754:         ///
755:         ///     Methods registered using RTKICK are automatically invoked after a specified delay (in seconds). Methods
756:         ///     only execute once and are then discarded.
757:         /// </summary>
758:         [MethodImpl(MethodImplOptions.AggressiveInlining)]
```

## MBBSEmu RTKICK/Polling: `only execute once` at line 756
Source: `/mnt/data/ext_validate/mbbsemu/MBBSEmu-master/MBBSEmu/HostProcess/MbbsHost.cs`

```text
753:         ///     Invokes routine registered during MAJORBBS->RTKICK()
754:         ///
755:         ///     Methods registered using RTKICK are automatically invoked after a specified delay (in seconds). Methods
756:         ///     only execute once and are then discarded.
757:         /// </summary>
758:         [MethodImpl(MethodImplOptions.AggressiveInlining)]
759:         private void ProcessRTKICK()
```

## RealTimeRoutine: `Begin Counting as soon as` at line 35
Source: `/mnt/data/ext_validate/mbbsemu/MBBSEmu-master/MBBSEmu/Module/RealTimeRoutine.cs`

```text
32:             //Only routines registered with Rtkick will have a delay > 0
33:             if (delay > 0)
34:             {
35:                 //Begin Counting as soon as the routine is created by Rtkick
36:                 Elapsed = new Stopwatch();
37:                 Elapsed.Start();
38:             }
```

## RealTimeRoutine: `Stopwatch` at line 17
Source: `/mnt/data/ext_validate/mbbsemu/MBBSEmu-master/MBBSEmu/Module/RealTimeRoutine.cs`

```text
14:         public readonly ushort Delay;
15: 
16:         /// <summary>
17:         ///     Stopwatch to track the time elapsed since last execution
18:         /// </summary>
19:         public Stopwatch Elapsed;
20: 
```

## RealTimeRoutine: `delay > 0` at line 32
Source: `/mnt/data/ext_validate/mbbsemu/MBBSEmu-master/MBBSEmu/Module/RealTimeRoutine.cs`

```text
29:         {
30:             Delay = delay;
31: 
32:             //Only routines registered with Rtkick will have a delay > 0
33:             if (delay > 0)
34:             {
35:                 //Begin Counting as soon as the routine is created by Rtkick
```

## RealTimeRoutine: `Executed` at line 22
Source: `/mnt/data/ext_validate/mbbsemu/MBBSEmu-master/MBBSEmu/Module/RealTimeRoutine.cs`

```text
19:         public Stopwatch Elapsed;
20: 
21:         /// <summary>
22:         ///     Denotes if this task has been executed already
23:         ///
24:         ///     RTKICK methods are only executed once
25:         /// </summary>
```

## MajorMUD Disassembly: `_CMD_SEARCH` at line 28733
Source: `/mnt/data/ext_validate/disasm/MBBSEmu.Disassemblies-main/MajorMUD/WCCMMUD.DLL.asm`

```text
28730: ; Attributes: bp-based frame
28731: 
28732:                 public _SEARCH_FOR_HIDDEN_EXITS
28733: _SEARCH_FOR_HIDDEN_EXITS proc far       ; CODE XREF: _CMD_SEARCH+194↓P
28734: 
28735: var_A           = word ptr -0Ah
28736: var_8           = dword ptr -8
```

## MajorMUD Disassembly: `_CLEAR_SEARCH_FLAGS` at line 6597
Source: `/mnt/data/ext_validate/disasm/MBBSEmu.Disassemblies-main/MajorMUD/WCCMMUD.DLL.asm`

```text
6594: 
6595: ; Attributes: bp-based frame
6596: 
6597:                 public _CLEAR_SEARCH_FLAGS
6598: _CLEAR_SEARCH_FLAGS proc far            ; CODE XREF: _MOVE_USER+32D↓p
6599:                                         ; _MOVE_USER+8F8↓p ...
6600: 
```

## MajorMUD Disassembly: `RTKICK` at line 32551
Source: `/mnt/data/ext_validate/disasm/MBBSEmu.Disassemblies-main/MajorMUD/WCCMMUD.DLL.asm`

```text
32548:                 mov     es, ax
32549:                 assume es:dseg43
32550:                 push    es:word_998F6
32551:                 call    RTKICK          ; rtkick(int time, void *rouptr());
32552:                 add     sp, 6
32553:                 push    seg cseg06
32554:                 push    offset _BACKGROUND_MONSTER_CREATE
```

## MajorMUD Disassembly: `BEGIN_POLLING` at line 34073
Source: `/mnt/data/ext_validate/disasm/MBBSEmu.Disassemblies-main/MajorMUD/WCCMMUD.DLL.asm`

```text
34070:                 mov     ax, seg USRNUM
34071:                 mov     es, ax
34072:                 push    word ptr es:USRNUM ; int usrnum;
34073:                 call    BEGIN_POLLING   ; void begin_polling(int unum,void (*rouptr)());
34074:                 add     sp, 6
34075:                 call    sub_79825
34076: 
```

## MajorMUD Disassembly: `_MCUROU` at line 19420
Source: `/mnt/data/ext_validate/disasm/MBBSEmu.Disassemblies-main/MajorMUD/WCCMMUD.DLL.asm`

```text
19417: ; Attributes: bp-based frame
19418: 
19419:                 public _REMOVE_ITEM_FROM_ROOM
19420: _REMOVE_ITEM_FROM_ROOM proc far         ; CODE XREF: _LJNGAME_MCUROU+E3B↓P
19421:                                         ; _LJNGAME_MCUROU+1189↓P ...
19422: 
19423: var_E           = word ptr -0Eh
```

## MajorMUD Disassembly: `_FINROU` at line 32902
Source: `/mnt/data/ext_validate/disasm/MBBSEmu.Disassemblies-main/MajorMUD/WCCMMUD.DLL.asm`

```text
32899: ; =============== S U B R O U T I N E =======================================
32900: 
32901: 
32902:                 public _LJNGAME_FINROU
32903: _LJNGAME_FINROU proc far                ; DATA XREF: dseg36:00C1↓o
32904:                 push    si              ; WCCMMUD_104
32905:                 push    di
```

## MajorMUD Disassembly: `_RESET_ROOM` at line 264209
Source: `/mnt/data/ext_validate/disasm/MBBSEmu.Disassemblies-main/MajorMUD/WCCMMUD.DLL.asm`

```text
264206:                 push    word ptr [bp+arg_0+2]
264207:                 push    word ptr [bp+arg_0]
264208:                 push    ds
264209:                 push    offset aSysResetRooms ; "SYS_RESET_ROOMS"
264210:                 call    SAMETO          ; int match=sameto(char *shorts, char *longs);
264211:                 add     sp, 8
264212:                 or      ax, ax
```

