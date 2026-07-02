# Validation0009 Disassembly Snippets — Monster Scheduler

## _BACKGROUND_MONSTER_CREATE (74995-75026)

### Around source line 75014

```asm
75010:                 push    offset _BACKGROUND_MONSTER_CREATE
75011:                 mov     ax, seg dseg43
75012:                 mov     es, ax
75013:                 assume es:dseg43
75014:                 push    es:word_998E8
75015:                 push    cs
75016:                 call    near ptr _MY_RTKICK
75017:                 add     sp, 6
75018:                 push    0Bh
75019:                 call    sub_1A6B0
75020:                 pop     cx
75021:                 pop     ds
```

### Around source line 75016

```asm
75012:                 mov     es, ax
75013:                 assume es:dseg43
75014:                 push    es:word_998E8
75015:                 push    cs
75016:                 call    near ptr _MY_RTKICK
75017:                 add     sp, 6
75018:                 push    0Bh
75019:                 call    sub_1A6B0
75020:                 pop     cx
75021:                 pop     ds
75022:                 assume ds:dseg81
75023:                 pop     di
```

## sub_26DF7 (78913-79608)

### Around source line 79097

```asm
79093:                 mov     ax, dx
79094:                 shl     ax, 2
79095:                 les     bx, [bp+var_10]
79096:                 add     bx, ax
79097:                 mov     ax, es:[bx+400h]
79098:                 or      ax, es:[bx+402h]
79099:                 jz      short loc_26F91
79100:                 inc     [bp+var_8]
79101: 
79102: loc_26F91:                              ; CODE XREF: sub_26DF7+195↑j
79103:                 inc     dx
79104:                 cmp     dx, 0Fh
```

### Around source line 79098

```asm
79094:                 shl     ax, 2
79095:                 les     bx, [bp+var_10]
79096:                 add     bx, ax
79097:                 mov     ax, es:[bx+400h]
79098:                 or      ax, es:[bx+402h]
79099:                 jz      short loc_26F91
79100:                 inc     [bp+var_8]
79101: 
79102: loc_26F91:                              ; CODE XREF: sub_26DF7+195↑j
79103:                 inc     dx
79104:                 cmp     dx, 0Fh
79105:                 jl      short loc_26F78
```

### Around source line 79104

```asm
79100:                 inc     [bp+var_8]
79101: 
79102: loc_26F91:                              ; CODE XREF: sub_26DF7+195↑j
79103:                 inc     dx
79104:                 cmp     dx, 0Fh
79105:                 jl      short loc_26F78
79106:                 mov     [bp+var_4], 0
79107:                 jmp     short loc_26FF6
79108: ; ---------------------------------------------------------------------------
79109: 
79110: loc_26F9E:                              ; CODE XREF: sub_26DF7+20B↓j
79111:                 push    [bp+var_4]
```

### Around source line 79176

```asm
79172:                 push    1
79173:                 call    GENRDN          ; int genrdn(int min,int max);
79174:                 add     sp, 4
79175:                 mov     dx, ax
79176:                 cmp     [bp+var_8], 0Fh
79177:                 jl      short loc_27053
79178:                 jmp     loc_2714D
79179: ; ---------------------------------------------------------------------------
79180: 
79181: loc_27053:                              ; CODE XREF: sub_26DF7+257↑j
79182:                 mov     ax, [bp+var_8]
79183:                 cmp     ax, [bp+var_A]
```

## _GENERATE_MONSTER (81612-83232)

### Around source line 81612

```asm
81612: _GENERATE_MONSTER proc far              ; CODE XREF: _USER_ALLOWED_IN_ROOM+C1↑P
81613:                                         ; _USER_ALLOWED_IN_ROOM+1CF↑P ...
81614: 
81615: var_60E         = word ptr -60Eh
81616: var_60C         = word ptr -60Ch
81617: var_5D8         = byte ptr -5D8h
81618: var_332         = byte ptr -332h
81619: var_32E         = word ptr -32Eh
```

### Around source line 81729

```asm
81725:                 jnz     short loc_283D5
81726:                 jmp     loc_28F9D
81727: ; ---------------------------------------------------------------------------
81728: 
81729: loc_283D5:                              ; CODE XREF: _GENERATE_MONSTER+10↑j
81730:                                         ; _GENERATE_MONSTER+18↑j
81731:                 mov     ax, seg dseg42
81732:                 mov     es, ax
81733:                 assume es:dseg42
81734:                 cmp     es:byte_98D96, 0
81735:                 jz      short loc_283E5
81736:                 jmp     loc_28F9D
```

### Around source line 81730

```asm
81726:                 jmp     loc_28F9D
81727: ; ---------------------------------------------------------------------------
81728: 
81729: loc_283D5:                              ; CODE XREF: _GENERATE_MONSTER+10↑j
81730:                                         ; _GENERATE_MONSTER+18↑j
81731:                 mov     ax, seg dseg42
81732:                 mov     es, ax
81733:                 assume es:dseg42
81734:                 cmp     es:byte_98D96, 0
81735:                 jz      short loc_283E5
81736:                 jmp     loc_28F9D
81737: ; ---------------------------------------------------------------------------
```

### Around source line 81739

```asm
81735:                 jz      short loc_283E5
81736:                 jmp     loc_28F9D
81737: ; ---------------------------------------------------------------------------
81738: 
81739: loc_283E5:                              ; CODE XREF: _GENERATE_MONSTER+28↑j
81740:                 push    [bp+arg_6]
81741:                 push    [bp+arg_4]
81742:                 push    [bp+arg_2]
81743:                 push    [bp+arg_0]
81744:                 call    _GET_ROOM_DATA
81745:                 add     sp, 8
81746:                 mov     word ptr [bp+var_C+2], dx
```

## _CALC_MINUTES_DIFFERENCE (81064-81324)

### Around source line 81064

```asm
81064: _CALC_MINUTES_DIFFERENCE proc far       ; CODE XREF: _GENERATE_MONSTER+41C↓p
81065: 
81066: var_18          = word ptr -18h
81067: var_16          = word ptr -16h
81068: var_14          = word ptr -14h
81069: var_12          = word ptr -12h
81070: var_10          = word ptr -10h
81071: var_E           = word ptr -0Eh
```

### Around source line 81184

```asm
81180:                 push    word ptr es:USRNUM ; int usrnum;
81181:                 call    _TELL_USER
81182:                 pop     cx
81183: 
81184: loc_2807A:                              ; CODE XREF: _CALC_MINUTES_DIFFERENCE+87↑j
81185:                                         ; _CALC_MINUTES_DIFFERENCE+94↑j ...
81186:                 mov     ax, [bp+arg_4]
81187:                 cmp     ax, [bp+arg_0]
81188:                 ja      short loc_28092
81189:                 mov     ax, [bp+arg_4]
81190:                 cmp     ax, [bp+arg_0]
81191:                 jnz     short loc_280CF
```

### Around source line 81185

```asm
81181:                 call    _TELL_USER
81182:                 pop     cx
81183: 
81184: loc_2807A:                              ; CODE XREF: _CALC_MINUTES_DIFFERENCE+87↑j
81185:                                         ; _CALC_MINUTES_DIFFERENCE+94↑j ...
81186:                 mov     ax, [bp+arg_4]
81187:                 cmp     ax, [bp+arg_0]
81188:                 ja      short loc_28092
81189:                 mov     ax, [bp+arg_4]
81190:                 cmp     ax, [bp+arg_0]
81191:                 jnz     short loc_280CF
81192:                 mov     ax, [bp+arg_6]
```

### Around source line 81196

```asm
81192:                 mov     ax, [bp+arg_6]
81193:                 cmp     ax, [bp+arg_2]
81194:                 jbe     short loc_28099
81195: 
81196: loc_28092:                              ; CODE XREF: _CALC_MINUTES_DIFFERENCE+109↑j
81197:                 xor     dx, dx
81198:                 xor     ax, ax
81199:                 jmp     loc_281C5
81200: ; ---------------------------------------------------------------------------
81201: 
81202: loc_28099:                              ; CODE XREF: _CALC_MINUTES_DIFFERENCE+119↑j
81203:                 mov     cx, [bp+var_A]
```

## _USER_ALLOWED_IN_ROOM (6214-6586)

### Around source line 6301

```asm
6297:                 push    [bp+arg_8]
6298:                 push    [bp+arg_6]
6299:                 push    [bp+arg_4]
6300:                 push    [bp+arg_2]
6301:                 call    _GENERATE_MONSTER
6302:                 add     sp, 18h
6303:                 les     bx, [bp+var_4]
6304:                 mov     dx, es:[bx+2]
6305:                 mov     ax, es:[bx]
6306:                 cmp     dx, [bp+arg_8]
6307:                 jnz     short loc_38CD
6308:                 cmp     ax, [bp+arg_6]
```

### Around source line 6410

```asm
6406:                 push    [bp+arg_8]
6407:                 push    [bp+arg_6]
6408:                 push    [bp+arg_4]
6409:                 push    [bp+arg_2]
6410:                 call    _GENERATE_MONSTER
6411:                 add     sp, 18h
6412:                 or      ax, dx
6413:                 jz      short loc_39BC
6414:                 cmp     [bp+var_E], 0Fh
6415:                 jl      short loc_3970
6416: 
6417: loc_39BC:                               ; CODE XREF: _USER_ALLOWED_IN_ROOM+18C↑j
```

### Around source line 6414

```asm
6410:                 call    _GENERATE_MONSTER
6411:                 add     sp, 18h
6412:                 or      ax, dx
6413:                 jz      short loc_39BC
6414:                 cmp     [bp+var_E], 0Fh
6415:                 jl      short loc_3970
6416: 
6417: loc_39BC:                               ; CODE XREF: _USER_ALLOWED_IN_ROOM+18C↑j
6418:                                         ; _USER_ALLOWED_IN_ROOM+1D9↑j
6419:                 les     bx, [bp+var_4]
6420:                 mov     dx, es:[bx+2]
6421:                 mov     ax, es:[bx]
```

## _SLOW_UPDATE_MONSTERS (75478-75500)

### Around source line 75478

```asm
75478: _SLOW_UPDATE_MONSTERS proc far          ; CODE XREF: _BACKGROUND_SLOW+20↑p
75479:                 push    si              ; WCCMMUD_141
75480:                 push    di
75481:                 push    ds
75482:                 mov     ax, seg dseg38
75483:                 mov     ds, ax
75484:                 assume ds:dseg38
75485:                 add     word_94B24, 1
```

### Around source line 75491

```asm
75487:                 cmp     word_94B22, 1
75488:                 jge     short loc_24F94
75489:                 inc     word_94B22
75490: 
75491: loc_24F94:                              ; CODE XREF: _SLOW_UPDATE_MONSTERS+17↑j
75492:                 nop
75493:                 push    cs
75494:                 call    near ptr _SLOW_UPDATE_NEXT_MONSTER
75495:                 pop     ds
75496:                 assume ds:dseg81
75497:                 pop     di
75498:                 pop     si
```

### Around source line 75500

```asm
75496:                 assume ds:dseg81
75497:                 pop     di
75498:                 pop     si
75499:                 retf
75500: _SLOW_UPDATE_MONSTERS endp
```

## _SLOW_UPDATE_NEXT_MONSTER (75512-75573)

### Around source line 75513

```asm
75512: _SLOW_UPDATE_NEXT_MONSTER proc far      ; CODE XREF: _LJNGAME_USER_POLLING_ROUTINE+6D↑P
75513:                                         ; _SLOW_UPDATE_MONSTERS+1F↑p
75514: 
75515: var_4           = dword ptr -4
75516: 
75517:                 enter   4, 0            ; WCCMMUD_142
75518:                 push    si
75519:                 push    di
75520:                 push    ds
```

### Around source line 75550

```asm
75546:                 push    word ptr [bp+var_4+2]
75547:                 push    word ptr [bp+var_4]
75548:                 nop
75549:                 push    cs
75550:                 call    near ptr _SLOW_UPDATE_MONSTER
75551:                 add     sp, 4
75552: 
75553: loc_24FF2:                              ; CODE XREF: _SLOW_UPDATE_NEXT_MONSTER+29↑j
75554:                                         ; _SLOW_UPDATE_NEXT_MONSTER+35↑j ...
75555:                 inc     word_94B20
75556:                 mov     ax, seg dseg43
75557:                 mov     es, ax
```

### Around source line 75559

```asm
75555:                 inc     word_94B20
75556:                 mov     ax, seg dseg43
75557:                 mov     es, ax
75558:                 assume es:dseg43
75559:                 mov     ax, es:word_998C4
75560:                 cmp     ax, word_94B20
75561:                 jg      short loc_2500F
75562:                 mov     word_94B20, 0
75563:                 dec     word_94B22
75564: 
75565: loc_2500F:                              ; CODE XREF: _SLOW_UPDATE_NEXT_MONSTER+11↑j
75566:                                         ; _SLOW_UPDATE_NEXT_MONSTER+66↑j
```

## _MEDIUM_UPDATE_MONSTERS (75584-75605)

### Around source line 75584

```asm
75584: _MEDIUM_UPDATE_MONSTERS proc far        ; CODE XREF: _BACKGROUND_MEDIUM+17↑p
75585:                 push    si              ; WCCMMUD_143
75586:                 push    di
75587:                 push    ds
75588:                 mov     ax, seg dseg38
75589:                 mov     ds, ax
75590:                 assume ds:dseg38
75591:                 cmp     word_94B2C, 1
```

### Around source line 75595

```asm
75591:                 cmp     word_94B2C, 1
75592:                 jge     short loc_25027
75593:                 inc     word_94B2C
75594: 
75595: loc_25027:                              ; CODE XREF: _MEDIUM_UPDATE_MONSTERS+D↑j
75596:                 mov     word_94B28, 0
75597:                 nop
75598:                 push    cs
75599:                 call    near ptr _MEDIUM_UPDATE_NEXT_MONSTER
75600:                 pop     ds
75601:                 assume ds:dseg81
75602:                 pop     di
```

### Around source line 75605

```asm
75601:                 assume ds:dseg81
75602:                 pop     di
75603:                 pop     si
75604:                 retf
75605: _MEDIUM_UPDATE_MONSTERS endp
```

## _MEDIUM_UPDATE_NEXT_MONSTER (75617-75678)

### Around source line 75618

```asm
75617: _MEDIUM_UPDATE_NEXT_MONSTER proc far    ; CODE XREF: _LJNGAME_USER_POLLING_ROUTINE+77↑P
75618:                                         ; _MEDIUM_UPDATE_MONSTERS+1B↑p
75619: 
75620: var_4           = dword ptr -4
75621: 
75622:                 enter   4, 0            ; WCCMMUD_144
75623:                 push    si
75624:                 push    di
75625:                 push    ds
```

### Around source line 75655

```asm
75651:                 push    word ptr [bp+var_4+2]
75652:                 push    word ptr [bp+var_4]
75653:                 nop
75654:                 push    cs
75655:                 call    near ptr _MEDIUM_UPDATE_MONSTER
75656:                 add     sp, 4
75657: 
75658: loc_2508B:                              ; CODE XREF: _MEDIUM_UPDATE_NEXT_MONSTER+29↑j
75659:                                         ; _MEDIUM_UPDATE_NEXT_MONSTER+35↑j ...
75660:                 inc     word_94B2A
75661:                 mov     ax, seg dseg43
75662:                 mov     es, ax
```

### Around source line 75664

```asm
75660:                 inc     word_94B2A
75661:                 mov     ax, seg dseg43
75662:                 mov     es, ax
75663:                 assume es:dseg43
75664:                 mov     ax, es:word_998C4
75665:                 cmp     ax, word_94B2A
75666:                 jg      short loc_250A8
75667:                 mov     word_94B2A, 0
75668:                 dec     word_94B2C
75669: 
75670: loc_250A8:                              ; CODE XREF: _MEDIUM_UPDATE_NEXT_MONSTER+11↑j
75671:                                         ; _MEDIUM_UPDATE_NEXT_MONSTER+66↑j
```

## _FAST_UPDATE_MONSTERS (75689-75709)

### Around source line 75689

```asm
75689: _FAST_UPDATE_MONSTERS proc far          ; CODE XREF: _BACKGROUND_FAST+4D↑p
75690:                 push    si              ; WCCMMUD_145
75691:                 push    di
75692:                 push    ds
75693:                 mov     ax, seg dseg38
75694:                 mov     ds, ax
75695:                 assume ds:dseg38
75696:                 cmp     word_94B30, 1
```

### Around source line 75700

```asm
75696:                 cmp     word_94B30, 1
75697:                 jge     short loc_250C0
75698:                 inc     word_94B30
75699: 
75700: loc_250C0:                              ; CODE XREF: _FAST_UPDATE_MONSTERS+D↑j
75701:                 nop
75702:                 push    cs
75703:                 call    near ptr _FAST_UPDATE_NEXT_MONSTER
75704:                 pop     ds
75705:                 assume ds:dseg81
75706:                 pop     di
75707:                 pop     si
```

### Around source line 75709

```asm
75705:                 assume ds:dseg81
75706:                 pop     di
75707:                 pop     si
75708:                 retf
75709: _FAST_UPDATE_MONSTERS endp
```

## _FAST_UPDATE_NEXT_MONSTER (75721-75785)

### Around source line 75722

```asm
75721: _FAST_UPDATE_NEXT_MONSTER proc far      ; CODE XREF: _LJNGAME_USER_POLLING_ROUTINE+72↑P
75722:                                         ; _FAST_UPDATE_MONSTERS+15↑p
75723: 
75724: var_4           = dword ptr -4
75725: 
75726:                 enter   4, 0            ; WCCMMUD_146
75727:                 push    si
75728:                 push    di
75729:                 push    ds
```

### Around source line 75762

```asm
75758:                 push    word ptr [bp+var_4+2]
75759:                 push    word ptr [bp+var_4]
75760:                 nop
75761:                 push    cs
75762:                 call    near ptr _FAST_UPDATE_MONSTER
75763:                 add     sp, 4
75764: 
75765: loc_25128:                              ; CODE XREF: _FAST_UPDATE_NEXT_MONSTER+29↑j
75766:                                         ; _FAST_UPDATE_NEXT_MONSTER+35↑j ...
75767:                 inc     word_94B2E
75768:                 mov     ax, seg dseg43
75769:                 mov     es, ax
```

### Around source line 75771

```asm
75767:                 inc     word_94B2E
75768:                 mov     ax, seg dseg43
75769:                 mov     es, ax
75770:                 assume es:dseg43
75771:                 mov     ax, es:word_998C4
75772:                 cmp     ax, word_94B2E
75773:                 jg      short loc_25145
75774:                 mov     word_94B2E, 0
75775:                 dec     word_94B30
75776: 
75777: loc_25145:                              ; CODE XREF: _FAST_UPDATE_NEXT_MONSTER+11↑j
75778:                                         ; _FAST_UPDATE_NEXT_MONSTER+70↑j
```

## _BACKGROUND_SLOW (74357-74497)

### Around source line 74380

```asm
74376:                 push    cs
74377:                 call    near ptr _SLOW_UPDATE_CHARACTERS
74378:                 nop
74379:                 push    cs
74380:                 call    near ptr _SLOW_UPDATE_MONSTERS
74381:                 inc     word_94B1E
74382:                 cmp     word_94B1E, 14h
74383:                 jle     short loc_247E9
74384:                 mov     word_94B1E, 0
74385:                 call    _RESTOCK_ITEMS
74386: 
74387: loc_247E9:                              ; CODE XREF: _BACKGROUND_SLOW+2C↑j
```

### Around source line 74483

```asm
74479:                 push    offset _BACKGROUND_SLOW
74480:                 mov     ax, seg dseg43
74481:                 mov     es, ax
74482:                 assume es:dseg43
74483:                 push    es:word_998F2
74484:                 nop
74485:                 push    cs
74486:                 call    near ptr _MY_RTKICK
74487:                 add     sp, 6
74488:                 push    0Bh
74489:                 call    sub_1A6B0
74490:                 pop     cx
```

### Around source line 74486

```asm
74482:                 assume es:dseg43
74483:                 push    es:word_998F2
74484:                 nop
74485:                 push    cs
74486:                 call    near ptr _MY_RTKICK
74487:                 add     sp, 6
74488:                 push    0Bh
74489:                 call    sub_1A6B0
74490:                 pop     cx
74491:                 pop     ds
74492:                 assume ds:dseg81
74493:                 pop     di
```

## _BACKGROUND_MEDIUM (74508-74544)

### Around source line 74524

```asm
74520:                 push    cs
74521:                 call    near ptr _MEDIUM_UPDATE_CHARACTERS
74522:                 nop
74523:                 push    cs
74524:                 call    near ptr _MEDIUM_UPDATE_MONSTERS
74525:                 push    cs
74526:                 call    near ptr sub_23FC9
74527:                 push    seg cseg06
74528:                 push    offset _BACKGROUND_MEDIUM
74529:                 mov     ax, seg dseg43
74530:                 mov     es, ax
74531:                 push    es:word_998F4
```

### Around source line 74531

```asm
74527:                 push    seg cseg06
74528:                 push    offset _BACKGROUND_MEDIUM
74529:                 mov     ax, seg dseg43
74530:                 mov     es, ax
74531:                 push    es:word_998F4
74532:                 nop
74533:                 push    cs
74534:                 call    near ptr _MY_RTKICK
74535:                 add     sp, 6
74536:                 push    0Bh
74537:                 call    sub_1A6B0
74538:                 pop     cx
```

### Around source line 74534

```asm
74530:                 mov     es, ax
74531:                 push    es:word_998F4
74532:                 nop
74533:                 push    cs
74534:                 call    near ptr _MY_RTKICK
74535:                 add     sp, 6
74536:                 push    0Bh
74537:                 call    sub_1A6B0
74538:                 pop     cx
74539:                 pop     ds
74540:                 assume ds:dseg81
74541:                 pop     di
```

## _BACKGROUND_FAST (74555-74615)

### Around source line 74573

```asm
74569:                 push    offset _BACKGROUND_FAST
74570:                 mov     ax, seg dseg43
74571:                 mov     es, ax
74572:                 assume es:dseg43
74573:                 push    es:word_998F6
74574:                 call    RTKICK          ; rtkick(int time, void *rouptr());
74575:                 add     sp, 6
74576:                 jmp     short loc_249AC
74577: ; ---------------------------------------------------------------------------
74578: 
74579: loc_24960:                              ; CODE XREF: _BACKGROUND_FAST+13↑j
74580:                 push    7
```

### Around source line 74574

```asm
74570:                 mov     ax, seg dseg43
74571:                 mov     es, ax
74572:                 assume es:dseg43
74573:                 push    es:word_998F6
74574:                 call    RTKICK          ; rtkick(int time, void *rouptr());
74575:                 add     sp, 6
74576:                 jmp     short loc_249AC
74577: ; ---------------------------------------------------------------------------
74578: 
74579: loc_24960:                              ; CODE XREF: _BACKGROUND_FAST+13↑j
74580:                 push    7
74581:                 call    sub_1A6B0
```

### Around source line 74593

```asm
74589:                 push    cs
74590:                 call    near ptr _FAST_UPDATE_CHARACTERS
74591:                 nop
74592:                 push    cs
74593:                 call    near ptr _FAST_UPDATE_MONSTERS
74594:                 push    seg cseg06
74595:                 push    offset _BACKGROUND_FAST
74596:                 mov     ax, seg dseg43
74597:                 mov     es, ax
74598:                 push    es:word_998F6
74599:                 call    RTKICK          ; rtkick(int time, void *rouptr());
74600:                 add     sp, 6
```

### Around source line 74598

```asm
74594:                 push    seg cseg06
74595:                 push    offset _BACKGROUND_FAST
74596:                 mov     ax, seg dseg43
74597:                 mov     es, ax
74598:                 push    es:word_998F6
74599:                 call    RTKICK          ; rtkick(int time, void *rouptr());
74600:                 add     sp, 6
74601:                 push    0Bh
74602:                 call    sub_1A6B0
74603:                 pop     cx
74604:                 mov     ax, seg dseg36
74605:                 mov     es, ax
```

## _MY_RTKICK (74839-74984)

### Around source line 74839

```asm
74839: _MY_RTKICK      proc far                ; CODE XREF: _INIT__WCCMMUD+B46↑P
74840:                                         ; _INIT__WCCMMUD+B5E↑P ...
74841: 
74842: var_C           = dword ptr -0Ch
74843: var_8           = dword ptr -8
74844: var_4           = dword ptr -4
74845: arg_0           = word ptr  6
74846: arg_2           = word ptr  8
```

### Around source line 74866

```asm
74862:                 jnz     short loc_24B62
74863:                 jmp     loc_24C7A
74864: ; ---------------------------------------------------------------------------
74865: 
74866: loc_24B62:                              ; CODE XREF: _MY_RTKICK+1C↑j
74867:                 mov     ax, [bp+arg_0]
74868:                 cwd
74869:                 add     ax, word_94B10
74870:                 adc     dx, word_94B12
74871:                 les     bx, [bp+var_4]
74872:                 mov     es:[bx+2], dx
74873:                 mov     es:[bx], ax
```

### Around source line 74895

```asm
74891:                 ja      short loc_24BBC
74892:                 cmp     ax, es:[bx]
74893:                 jbe     short loc_24BDE
74894: 
74895: loc_24BBC:                              ; CODE XREF: _MY_RTKICK+74↑j
74896:                 les     bx, [bp+var_4]
74897:                 mov     dx, word ptr dword_94B14+2
74898:                 mov     ax, word ptr dword_94B14
74899:                 mov     es:[bx+0Ah], dx
74900:                 mov     es:[bx+8], ax
74901: 
74902: loc_24BCE:                              ; CODE XREF: _MY_RTKICK+5E↑j
```

### Around source line 74902

```asm
74898:                 mov     ax, word ptr dword_94B14
74899:                 mov     es:[bx+0Ah], dx
74900:                 mov     es:[bx+8], ax
74901: 
74902: loc_24BCE:                              ; CODE XREF: _MY_RTKICK+5E↑j
74903:                 mov     dx, word ptr [bp+var_4+2]
74904:                 mov     ax, word ptr [bp+var_4]
74905:                 mov     word ptr dword_94B14+2, dx
74906:                 mov     word ptr dword_94B14, ax
74907:                 jmp     loc_24C7A
74908: ; ---------------------------------------------------------------------------
74909: 
```

## _PERFORM_KICKS (74761-74827)

## _ADD_MONSTER_TO_ROOM (10687-10761)

### Around source line 10687

```asm
10687: _ADD_MONSTER_TO_ROOM proc far           ; CODE XREF: _GENERATE_MONSTER+B2B↓P
10688:                                         ; _MOVE_MONSTER+56E↓P ...
10689: 
10690: arg_0           = dword ptr  6
10691: arg_4           = word ptr  0Ah
10692: arg_6           = word ptr  0Ch
10693: arg_8           = word ptr  0Eh
10694: arg_A           = word ptr  10h
```

### Around source line 10718

```asm
10714:                 add     sp, 8
10715:                 mov     word ptr [bp+arg_0+2], dx
10716:                 mov     word ptr [bp+arg_0], ax
10717: 
10718: loc_5FED:                               ; CODE XREF: _ADD_MONSTER_TO_ROOM+11↑j
10719:                 mov     ax, word ptr [bp+arg_0]
10720:                 or      ax, word ptr [bp+arg_0+2]
10721:                 jz      short loc_603A
10722:                 xor     cx, cx
10723: 
10724: loc_5FF7:                               ; CODE XREF: _ADD_MONSTER_TO_ROOM+78↓j
10725:                 mov     ax, cx
```

### Around source line 10724

```asm
10720:                 or      ax, word ptr [bp+arg_0+2]
10721:                 jz      short loc_603A
10722:                 xor     cx, cx
10723: 
10724: loc_5FF7:                               ; CODE XREF: _ADD_MONSTER_TO_ROOM+78↓j
10725:                 mov     ax, cx
10726:                 shl     ax, 2
10727:                 les     bx, [bp+arg_0]
10728:                 add     bx, ax
10729:                 mov     ax, es:[bx+400h]
10730:                 or      ax, es:[bx+402h]
10731:                 jnz     short loc_6034
```

### Around source line 10729

```asm
10725:                 mov     ax, cx
10726:                 shl     ax, 2
10727:                 les     bx, [bp+arg_0]
10728:                 add     bx, ax
10729:                 mov     ax, es:[bx+400h]
10730:                 or      ax, es:[bx+402h]
10731:                 jnz     short loc_6034
10732:                 les     bx, [bp+arg_0]
10733:                 mov     byte ptr es:[bx+5EDh], 1
10734:                 mov     ax, cx
10735:                 shl     ax, 2
10736:                 les     bx, [bp+arg_0]
```

## _TAKE_MONSTER_FROM_ROOM (10773-10848)

### Around source line 10814

```asm
10810:                 mov     ax, cx
10811:                 shl     ax, 2
10812:                 les     bx, [bp+arg_0]
10813:                 add     bx, ax
10814:                 mov     dx, es:[bx+402h]
10815:                 mov     ax, es:[bx+400h]
10816:                 cmp     dx, [bp+arg_E]
10817:                 jnz     short loc_60BC
10818:                 cmp     ax, [bp+arg_C]
10819:                 jnz     short loc_60BC
10820:                 les     bx, [bp+arg_0]
10821:                 mov     byte ptr es:[bx+5EDh], 1
```

### Around source line 10815

```asm
10811:                 shl     ax, 2
10812:                 les     bx, [bp+arg_0]
10813:                 add     bx, ax
10814:                 mov     dx, es:[bx+402h]
10815:                 mov     ax, es:[bx+400h]
10816:                 cmp     dx, [bp+arg_E]
10817:                 jnz     short loc_60BC
10818:                 cmp     ax, [bp+arg_C]
10819:                 jnz     short loc_60BC
10820:                 les     bx, [bp+arg_0]
10821:                 mov     byte ptr es:[bx+5EDh], 1
10822:                 mov     ax, cx
```

### Around source line 10826

```asm
10822:                 mov     ax, cx
10823:                 shl     ax, 2
10824:                 les     bx, [bp+arg_0]
10825:                 add     bx, ax
10826:                 mov     word ptr es:[bx+402h], 0
10827:                 mov     word ptr es:[bx+400h], 0
10828:                 mov     ax, cx
10829:                 jmp     short loc_60C5
10830: ; ---------------------------------------------------------------------------
10831: 
10832: loc_60BC:                               ; CODE XREF: _TAKE_MONSTER_FROM_ROOM+4E↑j
10833:                                         ; _TAKE_MONSTER_FROM_ROOM+53↑j
```

### Around source line 10827

```asm
10823:                 shl     ax, 2
10824:                 les     bx, [bp+arg_0]
10825:                 add     bx, ax
10826:                 mov     word ptr es:[bx+402h], 0
10827:                 mov     word ptr es:[bx+400h], 0
10828:                 mov     ax, cx
10829:                 jmp     short loc_60C5
10830: ; ---------------------------------------------------------------------------
10831: 
10832: loc_60BC:                               ; CODE XREF: _TAKE_MONSTER_FROM_ROOM+4E↑j
10833:                                         ; _TAKE_MONSTER_FROM_ROOM+53↑j
10834:                 inc     cx
```

## _CONFIRM_MONSTER_IN_ROOM (10860-10921)

### Around source line 10895

```asm
10891:                 mov     ax, cx
10892:                 shl     ax, 2
10893:                 les     bx, [bp+var_4]
10894:                 add     bx, ax
10895:                 mov     dx, es:[bx+402h]
10896:                 mov     ax, es:[bx+400h]
10897:                 cmp     dx, [bp+arg_A]
10898:                 jnz     short loc_611C
10899:                 cmp     ax, [bp+arg_8]
10900:                 jnz     short loc_611C
10901:                 mov     ax, cx
10902:                 jmp     short loc_6125
```

### Around source line 10896

```asm
10892:                 shl     ax, 2
10893:                 les     bx, [bp+var_4]
10894:                 add     bx, ax
10895:                 mov     dx, es:[bx+402h]
10896:                 mov     ax, es:[bx+400h]
10897:                 cmp     dx, [bp+arg_A]
10898:                 jnz     short loc_611C
10899:                 cmp     ax, [bp+arg_8]
10900:                 jnz     short loc_611C
10901:                 mov     ax, cx
10902:                 jmp     short loc_6125
10903: ; ---------------------------------------------------------------------------
```

### Around source line 10908

```asm
10904: 
10905: loc_611C:                               ; CODE XREF: _CONFIRM_MONSTER_IN_ROOM+47↑j
10906:                                         ; _CONFIRM_MONSTER_IN_ROOM+4C↑j
10907:                 inc     cx
10908:                 cmp     cx, 0Fh
10909:                 jl      short loc_60FA
10910: 
10911: loc_6122:                               ; CODE XREF: _CONFIRM_MONSTER_IN_ROOM+2C↑j
10912:                 mov     ax, 0FFFFh
10913: 
10914: loc_6125:                               ; CODE XREF: _CONFIRM_MONSTER_IN_ROOM+50↑j
10915:                 pop     ds
```

