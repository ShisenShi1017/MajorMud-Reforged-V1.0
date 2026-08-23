# Classic Command Runtime Matrix

This matrix is generated from the same parser registrations, active Classic contextual catalog, and runtime-route catalog used by the server. Exact aliases override normal prefix abbreviation rules.

## Inventory

- 95 built-in canonical commands.
- 44 exact aliases.
- 17 explicit runtime route families.
- 25 evidence-backed contextual command records.
- 18 additional contextual verbs, for 113 unique active commands.

## Built-in commands

| Command | Exact aliases | Lowest accepted input | Route | Live consumer |
|---|---|---:|---|---|
| `look` | `l` | `l` | RoomPresentation | RuntimeLiveGameplayFacade room/search renderers |
| `brief` | `br` | `br` | RoomPresentation | RuntimeLiveGameplayFacade room/search renderers |
| `verbose` | `verb` | `ver` | RoomPresentation | RuntimeLiveGameplayFacade room/search renderers |
| `search` | `sea` | `sea` | RoomPresentation | RuntimeLiveGameplayFacade room/search renderers |
| `examine` | `x` | `x` | RoomPresentation | RuntimeLiveGameplayFacade room/search renderers |
| `score` | `sc`, `stat`, `stats`, `status` | `sc` | CharacterState | RuntimeLiveGameplayFacade character/item-state renderers |
| `character` | `char`, `sheet` | `cha` | CharacterState | RuntimeLiveGameplayFacade character/item-state renderers |
| `wealth` | `money` | `weal` | CharacterState | RuntimeLiveGameplayFacade character/item-state renderers |
| `experience` | `exp` | `exp` | CharacterState | RuntimeLiveGameplayFacade character/item-state renderers |
| `inventory` | `i`, `inv` | `i` | CharacterState | RuntimeLiveGameplayFacade character/item-state renderers |
| `equipment` | `eq`, `wearing` | `eq` | CharacterState | RuntimeLiveGameplayFacade character/item-state renderers |
| `get` | `g` | `g` | ItemRuntime | RuntimeLiveGameplayFacade.ExecuteItemCommand |
| `drop` | — | `dro` | ItemRuntime | RuntimeLiveGameplayFacade.ExecuteItemCommand |
| `put` | — | `put` | ItemRuntime | RuntimeLiveGameplayFacade.ExecuteItemCommand |
| `give` | — | `giv` | ItemRuntime | RuntimeLiveGameplayFacade.ExecuteItemCommand |
| `equip` | — | `equip` | ItemRuntime | RuntimeLiveGameplayFacade.ExecuteItemCommand |
| `wear` | — | `wear` | ItemRuntime | RuntimeLiveGameplayFacade.ExecuteItemCommand |
| `wield` | — | `wie` | ItemRuntime | RuntimeLiveGameplayFacade.ExecuteItemCommand |
| `remove` | — | `rem` | ItemRuntime | RuntimeLiveGameplayFacade.ExecuteItemCommand |
| `attack` | `a`, `att` | `a` | CombatRuntime | RuntimeLiveGameplayFacade combat/stealth runtime |
| `kill` | `k` | `k` | CombatRuntime | RuntimeLiveGameplayFacade combat/stealth runtime |
| `flee` | — | `fle` | CombatRuntime | RuntimeLiveGameplayFacade combat/stealth runtime |
| `combat` | — | `comb` | CombatRuntime | RuntimeLiveGameplayFacade combat/stealth runtime |
| `round` | — | `rou` | CombatRuntime | RuntimeLiveGameplayFacade combat/stealth runtime |
| `assist` | — | `ass` | CombatRuntime | RuntimeLiveGameplayFacade combat/stealth runtime |
| `cast` | — | `cas` | PowerRuntime | RuntimeLiveGameplayFacade.ExecuteLivePowerCommand |
| `prepare` | — | `prep` | PowerRuntime | RuntimeLiveGameplayFacade.ExecuteLivePowerCommand |
| `use` | — | `use` | PowerRuntime | RuntimeLiveGameplayFacade.ExecuteLivePowerCommand |
| `dispel` | — | `dis` | PowerRuntime | RuntimeLiveGameplayFacade.ExecuteLivePowerCommand |
| `abilities` | — | `abi` | PowerRuntime | RuntimeLiveGameplayFacade.ExecuteLivePowerCommand |
| `skills` | — | `ski` | PowerRuntime | RuntimeLiveGameplayFacade.ExecuteLivePowerCommand |
| `talk` | — | `tal` | NarrativeRuntime | RuntimeLiveGameplayFacade.ExecuteWorldNarrativeCommand |
| `buy` | — | `buy` | ServiceRuntime | RuntimeLiveGameplayFacade.ExecuteLiveServicesBundleCommand |
| `sell` | — | `sell` | ServiceRuntime | RuntimeLiveGameplayFacade.ExecuteLiveServicesBundleCommand |
| `repair` | — | `rep` | ServiceRuntime | RuntimeLiveGameplayFacade.ExecuteLiveServicesBundleCommand |
| `train` | — | `trai` | ServiceRuntime | RuntimeLiveGameplayFacade.ExecuteLiveServicesBundleCommand |
| `tat` | — | `tat` | ServiceRuntime | RuntimeLiveGameplayFacade.ExecuteLiveServicesBundleCommand |
| `bash` | `ba` | `ba` | CombatRuntime | RuntimeLiveGameplayFacade combat/stealth runtime |
| `smash` | — | `sm` | CombatRuntime | RuntimeLiveGameplayFacade combat/stealth runtime |
| `backstab` | `bs` | `bs` | CombatRuntime | RuntimeLiveGameplayFacade combat/stealth runtime |
| `hide` | `hid` | `hi` | CombatRuntime | RuntimeLiveGameplayFacade combat/stealth runtime |
| `sneak` | `sn` | `sn` | CombatRuntime | RuntimeLiveGameplayFacade combat/stealth runtime |
| `open` | — | `op` | LivingWorldRuntime | RuntimeLiveGameplayFacade.ExecuteLivingWorldCommand |
| `close` | — | `clo` | LivingWorldRuntime | RuntimeLiveGameplayFacade.ExecuteLivingWorldCommand |
| `lock` | — | `loc` | LivingWorldRuntime | RuntimeLiveGameplayFacade.ExecuteLivingWorldCommand |
| `unlock` | — | `unl` | LivingWorldRuntime | RuntimeLiveGameplayFacade.ExecuteLivingWorldCommand |
| `pull` | — | `pul` | LivingWorldRuntime | RuntimeLiveGameplayFacade.ExecuteLivingWorldCommand |
| `push` | — | `pus` | LivingWorldRuntime | RuntimeLiveGameplayFacade.ExecuteLivingWorldCommand |
| `activate` | — | `act` | LivingWorldRuntime | RuntimeLiveGameplayFacade.ExecuteLivingWorldCommand |
| `worldstate` | — | `worldst` | RuntimeDiagnostics | Runtime host/live-world diagnostic renderers |
| `services` | — | `serv` | ServiceRuntime | RuntimeLiveGameplayFacade.ExecuteLiveServicesBundleCommand |
| `list` | — | `lis` | ServiceRuntime | RuntimeLiveGameplayFacade.ExecuteLiveServicesBundleCommand |
| `bank` | — | `ban` | BankRuntime | RuntimeLiveGameplayFacade bank runtime |
| `deposit` | — | `dep` | BankRuntime | RuntimeLiveGameplayFacade bank runtime |
| `withdraw` | — | `wit` | BankRuntime | RuntimeLiveGameplayFacade bank runtime |
| `trade` | — | `trad` | ServiceRuntime | RuntimeLiveGameplayFacade.ExecuteLiveServicesBundleCommand |
| `quest` | — | `que` | NarrativeRuntime | RuntimeLiveGameplayFacade.ExecuteWorldNarrativeCommand |
| `accept` | — | `acc` | NarrativeRuntime | RuntimeLiveGameplayFacade.ExecuteWorldNarrativeCommand |
| `abandon` | — | `aba` | NarrativeRuntime | RuntimeLiveGameplayFacade.ExecuteWorldNarrativeCommand |
| `complete` | — | `comp` | NarrativeRuntime | RuntimeLiveGameplayFacade.ExecuteWorldNarrativeCommand |
| `journal` | `qlog` | `jou` | NarrativeRuntime | RuntimeLiveGameplayFacade.ExecuteWorldNarrativeCommand |
| `say` | — | `say` | CommunicationRuntime | RuntimeLiveGameplayFacade communication/session runtime |
| `tell` | — | `tel` | CommunicationRuntime | RuntimeLiveGameplayFacade communication/session runtime |
| `messages` | — | `mes` | CommunicationRuntime | RuntimeLiveGameplayFacade communication/session runtime |
| `mudplus` | `mmplus` | `mudp` | MajorMudPlusRuntime | RuntimeLiveGameplayFacade.ExecuteMajorMudPlusCommand |
| `description` | `desc` | `desc` | MajorMudPlusRuntime | RuntimeLiveGameplayFacade.ExecuteMajorMudPlusCommand |
| `mudreg` | `/mudreg` | `mudr` | MajorMudPlusRuntime | RuntimeLiveGameplayFacade.ExecuteMajorMudPlusCommand |
| `buylife` | — | `buyl` | MajorMudPlusRuntime | RuntimeLiveGameplayFacade.ExecuteMajorMudPlusCommand |
| `plusadmin` | — | `plusa` | MajorMudPlusRuntime | RuntimeLiveGameplayFacade.ExecuteMajorMudPlusCommand |
| `sysop` | `sys` | `sys` | SysopRuntime | RuntimeLiveGameplayFacade.ExecuteWccSysopCommand |
| `respawn` | — | `res` | RecoveryRuntime | RuntimeLiveGameplayFacade character recovery runtime |
| `recover` | — | `rec` | RecoveryRuntime | RuntimeLiveGameplayFacade character recovery runtime |
| `ready` | — | `ready` | RuntimeDiagnostics | Runtime host/live-world diagnostic renderers |
| `audit` | — | `aud` | RuntimeDiagnostics | Runtime host/live-world diagnostic renderers |
| `save` | — | `save` | CharacterState | RuntimeLiveGameplayFacade character/item-state renderers |
| `who` | — | `who` | CommunicationRuntime | RuntimeLiveGameplayFacade communication/session runtime |
| `where` | — | `whe` | RuntimeDiagnostics | Runtime host/live-world diagnostic renderers |
| `world` | — | `world` | RuntimeDiagnostics | Runtime host/live-world diagnostic renderers |
| `worlds` | — | `worlds` | RuntimeDiagnostics | Runtime host/live-world diagnostic renderers |
| `sessions` | — | `sess` | RuntimeDiagnostics | Runtime host/live-world diagnostic renderers |
| `state` | — | `state` | RuntimeDiagnostics | Runtime host/live-world diagnostic renderers |
| `events` | — | `eve` | RuntimeDiagnostics | Runtime host/live-world diagnostic renderers |
| `help` | `?` | `?` | Help | Runtime command help catalog |
| `commands` | `cmds` | `cmds` | Help | Runtime command help catalog |
| `quit` | `logout`, `exit` | `qui` | SessionExit | RuntimeCommandDispatcher.RequestsSessionExit |
| `north` | `n` | `n` | Movement | RuntimeSession.TryMove |
| `south` | `s` | `s` | Movement | RuntimeSession.TryMove |
| `east` | `e` | `e` | Movement | RuntimeSession.TryMove |
| `west` | `w` | `w` | Movement | RuntimeSession.TryMove |
| `northeast` | `ne` | `ne` | Movement | RuntimeSession.TryMove |
| `northwest` | `nw` | `nw` | Movement | RuntimeSession.TryMove |
| `southeast` | `se` | `se` | Movement | RuntimeSession.TryMove |
| `southwest` | `sw` | `sw` | Movement | RuntimeSession.TryMove |
| `up` | `u` | `u` | Movement | RuntimeSession.TryMove |
| `down` | `d` | `d` | Movement | RuntimeSession.TryMove |

## Additional evidence-backed contextual verbs

These commands are not generic built-ins. They resolve only through the generated Classic world catalog and execute through `TryExecuteDecodedWorldCommand`; the exact room phrase and effect remain data-bound.

`ascend`, `board`, `climb`, `cross`, `descend`, `drink`, `enter`, `go`, `jump`, `move`, `press`, `read`, `ring`, `run`, `swim`, `touch`, `turn`, `walk`.

## Required compatibility behavior

- Exact `g` resolves to `get`.
- Exact `go` remains a separate contextual room verb.
- `quit`, `qui`, `logout`, and `exit` all resolve to the canonical session-exit command.
- Exact `sys` resolves to `sysop`; `eq` resolves to `equipment`; `ba` resolves to `bash`.
- Unknown or unbound contextual commands fail closed and do not mutate runtime state.
