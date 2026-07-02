# MajorMUD Disassembly Evidence Excerpts — Validation0003

Source: MBBSEmu.Disassemblies-main/MajorMUD/WCCMMUD.DLL.asm.

## MajorMUD INIT schedules background routines via RTKICK/MY_RTKICK

```asm
32440:                 add     sp, 6
32441:                 mov     dx, seg dseg43
32442:                 mov     es, dx
32443:                 mov     es:word_998D2, ax
32444:                 mov     ax, seg dseg43
32445:                 mov     es, ax
32446:                 mov     es:word_998F8, 14h
32447:                 mov     ax, seg dseg43
32448:                 mov     es, ax
32449:                 mov     es:word_998FA, 3E8h
32450:                 mov     ax, seg dseg43
32451:                 mov     es, ax
32452:                 mov     es:word_998FC, 3E8h
32453:                 mov     ax, seg dseg43
32454:                 mov     es, ax
32455:                 mov     es:word_998FE, 7Dh ; '}'
32456:                 mov     ax, seg dseg43
32457:                 mov     es, ax
32458:                 mov     es:word_99900, 5
32459:                 mov     ax, seg dseg43
32460:                 mov     es, ax
32461:                 mov     es:word_99906, 64h ; 'd'
32462:                 mov     ax, seg dseg43
32463:                 mov     es, ax
32464:                 mov     es:word_99908, 64h ; 'd'
32465:                 mov     ax, seg dseg43
32466:                 mov     es, ax
32467:                 mov     es:word_9990A, 0Ah
32468:                 mov     ax, seg dseg43
32469:                 mov     es, ax
32470:                 mov     es:word_9990C, 0Ah
32471:                 mov     ax, seg dseg43
32472:                 mov     es, ax
32473:                 mov     es:word_99914, 0
32474:                 mov     es:word_99912, 85Ch
32475:                 mov     ax, seg dseg43
32476:                 mov     es, ax
32477:                 mov     es:word_99918, 0
32478:                 mov     es:word_99916, 88Dh
32479:                 mov     ax, seg dseg43
32480:                 mov     es, ax
32481:                 mov     es:word_9991C, 0
32482:                 mov     es:word_9991A, 8Eh
32483:                 mov     ax, seg dseg43
32484:                 mov     es, ax
32485:                 mov     es:word_99920, 0
32486:                 mov     es:word_9991E, 178h
32487:                 mov     ax, seg dseg43
32488:                 mov     es, ax
32489:                 mov     es:word_9992C, 0
32490:                 mov     es:word_9992A, 64h ; 'd'
32491:                 mov     ax, seg dseg43
32492:                 mov     es, ax
32493:                 mov     es:word_99930, 0
32494:                 mov     es:word_9992E, 44h ; 'D'
32495:                 mov     ax, seg dseg43
32496:                 mov     es, ax
32497:                 mov     es:word_99934, 0
32498:                 mov     es:word_99932, 0
32499:                 mov     ax, seg NTERMS
32500:                 mov     es, ax
32501:                 assume es:nothing
32502:                 mov     ax, word ptr es:NTERMS ; int nterms;
32503:                 shl     ax, 3
32504:                 push    ax
32505:                 call    ALCZER          ; char *alczer(unsigned nbytes);
32506:                 pop     cx
32507:                 mov     word ptr dword_8D46E+2, dx
32508:                 mov     word ptr dword_8D46E, ax
32509:                 mov     ax, seg NTERMS
32510:                 mov     es, ax
32511:                 mov     ax, word ptr es:NTERMS ; int nterms;
32512:                 imul    ax, 0ADh
32513:                 push    ax
32514:                 call    ALCZER          ; char *alczer(unsigned nbytes);
32515:                 pop     cx
32516:                 mov     word ptr dword_8D472+2, dx
32517:                 mov     word ptr dword_8D472, ax
32518:                 push    3E80h
32519:                 call    ALCZER          ; char *alczer(unsigned nbytes);
32520:                 pop     cx
32521:                 mov     word ptr dword_8D460+2, dx
32522:                 mov     word ptr dword_8D460, ax
32523:                 xor     dx, dx
32524:                 jmp     short loc_11715
32525: ; ---------------------------------------------------------------------------
32526: 
32527: loc_116FC:                              ; CODE XREF: _INIT__WCCMMUD+B12↓j
32528:                 mov     ax, dx
32529:                 imul    ax, 0ADh
32530:                 les     bx, dword_8D472
32531:                 assume es:nothing
32532:                 add     bx, ax
32533:                 mov     word ptr es:[bx+46h], 0
32534:                 mov     word ptr es:[bx+44h], 0
32535:                 inc     dx
32536: 
32537: loc_11715:                              ; CODE XREF: _INIT__WCCMMUD+AED↑j
32538:                 mov     ax, seg NTERMS
32539:                 mov     es, ax
32540:                 assume es:nothing
32541:                 cmp     word ptr es:NTERMS, dx ; int nterms;
32542:                 jg      short loc_116FC
32543:                 call    _ALLOCATE_BUFFERS
32544:                 call    _INIT_EMULATION
32545:                 push    seg cseg06
32546:                 push    offset _BACKGROUND_FAST
32547:                 mov     ax, seg dseg43
32548:                 mov     es, ax
32549:                 assume es:dseg43
32550:                 push    es:word_998F6
32551:                 call    RTKICK          ; rtkick(int time, void *rouptr());
32552:                 add     sp, 6
32553:                 push    seg cseg06
32554:                 push    offset _BACKGROUND_MONSTER_CREATE
32555:                 mov     ax, seg dseg43
32556:                 mov     es, ax
32557:                 push    es:word_998E8
32558:                 call    _MY_RTKICK
32559:                 add     sp, 6
32560:                 push    seg cseg06
32561:                 push    offset _BACKGROUND_OTHER
32562:                 mov     ax, seg dseg43
32563:                 mov     es, ax
32564:                 push    es:word_998EA
32565:                 call    _MY_RTKICK
32566:                 add     sp, 6
32567:                 push    seg cseg06
32568:                 push    offset _BACKGROUND_SLOW
32569:                 mov     ax, seg dseg43
32570:                 mov     es, ax
32571:                 push    es:word_998F2
32572:                 call    _MY_RTKICK
32573:                 add     sp, 6
32574:                 push    seg cseg06
32575:                 push    offset _BACKGROUND_MEDIUM
32576:                 mov     ax, seg dseg43
32577:                 mov     es, ax
32578:                 push    es:word_998F4
32579:                 call    _MY_RTKICK
32580:                 add     sp, 6
32581:                 push    seg cseg06
32582:                 push    offset _BACKGROUND_ENERGY
32583:                 mov     ax, seg dseg43
32584:                 mov     es, ax
32585:                 push    es:word_9993C
32586:                 call    _MY_RTKICK
32587:                 add     sp, 6
32588:                 push    seg cseg06
32589:                 push    offset _BACKGROUND_INACTIVITY
32590:                 mov     ax, seg dseg43
32591:                 mov     es, ax
32592:                 push    es:word_99988
32593:                 call    _MY_RTKICK
32594:                 add     sp, 6
32595:                 call    _INIT_AUTOCOMBAT
32596:                 push    word_8D47C
32597:                 push    word_8D47A
32598:                 call    SETMBK          ; FILE *setmbk(mbkptr);
32599:                 add     sp, 4
32600:                 push    0
```

## MajorMUD cleanup routine begins and logs Entering cleanup

```asm
44590:                 mov     bx, [bp+var_4E]
44591:                 lea     ax, [bp+var_ABE]
44592:                 add     bx, ax
44593:                 mov     word ptr ss:[bx], 0
44594:                 mov     bx, [bp+var_4E]
44595:                 lea     ax, [bp+var_AC6]
44596:                 add     bx, ax
44597:                 mov     word ptr ss:[bx+2], 0
44598:                 mov     word ptr ss:[bx], 0
44599:                 inc     di
44600: 
44601: loc_18123:                              ; CODE XREF: _LJNGAME_MCUROU+44↑j
44602:                 cmp     di, 0Ah
44603:                 jle     short loc_180ED
44604:                 push    ds
44605:                 push    offset aStartingCleanu ; "Starting Cleanup"
44606:                 push    ds
44607:                 push    offset aMajormudNotice ; "MajorMUD - Notice"
44608:                 call    SHOCST          ; void shocst(char *summary, char *detail, p1, p1,...,pn);
44609:                 add     sp, 8
44610:                 push    1
44611:                 nop
44612:                 push    cs
44613:                 call    near ptr sub_1A6B0
44614:                 pop     cx
44615:                 call    sub_871A5
44616:                 push    ds
44617:                 push    offset aEnteringCleanu ; "Entering cleanup\n"
44618:                 call    _MMLOG
44619:                 add     sp, 4
44620:                 push    word ptr dword_8D48A+2
44621:                 push    word ptr dword_8D48A
44622:                 call    SETBTV          ; void setbtv(BTVFILE *bbprt);
44623:                 add     sp, 4
44624:                 push    0
44625:                 push    0Ch
44626:                 push    0
44627:                 push    0
44628:                 push    0
44629:                 push    ss
44630:                 lea     ax, [bp+var_82E]
44631:                 push    ax
44632:                 call    OBTBTVL         ; int obtbtvl (void *recptr, void *key, int keynum, int obtopt, int loktyp);
44633:                 add     sp, 0Eh
44634:                 mov     [bp+var_1E], ax
44635:                 mov     [bp+var_26], 0
```

## MajorMUD background RTKICK wrapper/routines

```asm
74550: 
74551: ; =============== S U B R O U T I N E =======================================
74552: 
74553: 
74554:                 public _BACKGROUND_FAST
74555: _BACKGROUND_FAST proc far               ; DATA XREF: _INIT__WCCMMUD+B21↑o
74556:                                         ; _BACKGROUND_FAST+18↓o ...
74557:                 push    si              ; WCCMMUD_134
74558:                 push    di
74559:                 push    ds
74560:                 mov     ax, seg dseg38
74561:                 mov     ds, ax
74562:                 assume ds:dseg38
74563:                 mov     ax, seg dseg36
74564:                 mov     es, ax
74565:                 assume es:dseg36
74566:                 test    byte ptr es:word_8D9AC, 1
74567:                 jnz     short loc_24960
74568:                 push    seg cseg06
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
74581:                 call    sub_1A6B0
74582:                 pop     cx
74583:                 add     word_94B10, 1
74584:                 adc     word_94B12, 0
74585:                 nop
74586:                 push    cs
74587:                 call    near ptr _PERFORM_KICKS
74588:                 nop
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
74601:                 push    0Bh
74602:                 call    sub_1A6B0
74603:                 pop     cx
74604:                 mov     ax, seg dseg36
74605:                 mov     es, ax
74606:                 assume es:dseg36
74607:                 and     es:word_8D9AC, 0FFFEh
74608: 
74609: loc_249AC:                              ; CODE XREF: _BACKGROUND_FAST+2D↑j
74610:                 pop     ds
74611:                 assume ds:dseg81
74612:                 pop     di
74613:                 pop     si
74614:                 retf
74615: _BACKGROUND_FAST endp
74616: 
74617: 
74618: ; =============== S U B R O U T I N E =======================================
74619: 
74620: ; Attributes: bp-based frame
74621: 
74622: sub_249B0       proc far                ; CODE XREF: _MY_RTKICK+D↓p
74623: 
74624: var_4           = dword ptr -4
74625: 
74626:                 enter   4, 0
74627:                 push    si
74628:                 push    di
74629:                 push    ds
74630:                 mov     ax, seg dseg38
74631:                 mov     ds, ax
74632:                 assume ds:dseg38
74633:                 mov     word ptr [bp+var_4+2], 0
74634:                 mov     word ptr [bp+var_4], 0
74635:                 mov     ax, word_94B18
74636:                 or      ax, word_94B1A
74637:                 jz      short loc_249E2
74638:                 mov     dx, word_94B1A
74639:                 mov     ax, word_94B18
74640:                 mov     word ptr [bp+var_4+2], dx
74641:                 mov     word ptr [bp+var_4], ax
74642:                 mov     es, word_94B1A
74643:                 assume es:nothing
74644:                 jmp     short loc_24A57
74645: ; ---------------------------------------------------------------------------
74646: 
74647: loc_249E2:                              ; CODE XREF: sub_249B0+1D↑j
74648:                 push    4B0h
74649:                 call    ALCZER          ; char *alczer(unsigned nbytes);
74650:                 pop     cx
74651:                 mov     word_94B1A, dx
74652:                 mov     word_94B18, ax
74653:                 mov     ax, word_94B18
74654:                 or      ax, word_94B1A
74655:                 jz      short loc_24A79
74656:                 mov     dx, word_94B1A
74657:                 mov     ax, word_94B18
74658:                 mov     word ptr [bp+var_4+2], dx
74659:                 mov     word ptr [bp+var_4], ax
74660:                 mov     cx, 1
74661:                 jmp     short loc_24A33
74662: ; ---------------------------------------------------------------------------
74663: 
74664: loc_24A0D:                              ; CODE XREF: sub_249B0+86↓j
74665:                 mov     dx, word ptr [bp+var_4+2]
74666:                 mov     ax, word ptr [bp+var_4]
74667:                 add     ax, 0Ch
74668:                 les     bx, [bp+var_4]
74669:                 mov     es:[bx+0Ah], dx
74670:                 mov     es:[bx+8], ax
74671:                 les     bx, [bp+var_4]
74672:                 mov     dx, es:[bx+0Ah]
74673:                 mov     ax, es:[bx+8]
74674:                 mov     word ptr [bp+var_4+2], dx
74675:                 mov     word ptr [bp+var_4], ax
74676:                 inc     cx
74677: 
74678: loc_24A33:                              ; CODE XREF: sub_249B0+5B↑j
74679:                 cmp     cx, 64h ; 'd'
74680:                 jl      short loc_24A0D
74681:                 les     bx, [bp+var_4]
74682:                 mov     word ptr es:[bx+0Ah], 0
74683:                 mov     word ptr es:[bx+8], 0
74684:                 mov     dx, word_94B1A
74685:                 mov     ax, word_94B18
74686:                 mov     word ptr [bp+var_4+2], dx
74687:                 mov     word ptr [bp+var_4], ax
74688:                 mov     es, word ptr [bp+var_4+2]
74689: 
74690: loc_24A57:                              ; CODE XREF: sub_249B0+30↑j
74691:                 mov     bx, word_94B18
74692:                 mov     dx, es:[bx+0Ah]
74693:                 mov     ax, es:[bx+8]
74694:                 mov     word_94B1A, dx
74695:                 mov     word_94B18, ax
74696:                 les     bx, [bp+var_4]
74697:                 mov     word ptr es:[bx+0Ah], 0
74698:                 mov     word ptr es:[bx+8], 0
74699: 
74700: loc_24A79:                              ; CODE XREF: sub_249B0+49↑j
74701:                 mov     dx, word ptr [bp+var_4+2]
74702:                 mov     ax, word ptr [bp+var_4]
74703:                 pop     ds
74704:                 assume ds:dseg81
74705:                 pop     di
74706:                 pop     si
74707:                 leave
74708:                 retf
74709: sub_249B0       endp
74710: 
74711: 
74712: ; =============== S U B R O U T I N E =======================================
74713: 
74714: ; Attributes: bp-based frame
74715: 
74716: sub_24A84       proc far                ; CODE XREF: _PERFORM_KICKS+52↓p
74717:                                         ; _MY_RTKICK+133↓p
74718: 
74719: arg_0           = dword ptr  6
74720: 
74721:                 push    bp
74722:                 mov     bp, sp
74723:                 push    si
74724:                 push    di
74725:                 push    ds
74726:                 mov     ax, seg dseg38
74727:                 mov     ds, ax
74728:                 assume ds:dseg38
74729:                 mov     ax, word ptr [bp+arg_0]
74730:                 or      ax, word ptr [bp+arg_0+2]
74731:                 jz      short loc_24AB6
74732:                 les     bx, [bp+arg_0]
74733:                 mov     dx, word_94B1A
74734:                 mov     ax, word_94B18
74735:                 mov     es:[bx+0Ah], dx
74736:                 mov     es:[bx+8], ax
74737:                 mov     dx, word ptr [bp+arg_0+2]
74738:                 mov     ax, word ptr [bp+arg_0]
74739:                 mov     word_94B1A, dx
74740:                 mov     word_94B18, ax
74741: 
74742: loc_24AB6:                              ; CODE XREF: sub_24A84+11↑j
74743:                 pop     ds
74744:                 assume ds:dseg81
74745:                 pop     di
74746:                 pop     si
74747:                 pop     bp
74748:                 retf
74749: sub_24A84       endp
74750: 
74751: ;
74752: ; External Entry #153 into the Module
74753: ; Attributes (0003): Fixed Exported Shared dataseg
74754: ;
74755: 
74756: ; =============== S U B R O U T I N E =======================================
74757: 
74758: ; Attributes: bp-based frame
74759: 
74760:                 public _PERFORM_KICKS
74761: _PERFORM_KICKS  proc far                ; CODE XREF: _BACKGROUND_FAST+43↑p
74762: 
74763: var_6           = word ptr -6
74764: var_4           = dword ptr -4
74765: 
74766:                 enter   6, 0            ; WCCMMUD_153
74767:                 push    si
74768:                 push    di
74769:                 push    ds
74770:                 mov     ax, seg dseg38
74771:                 mov     ds, ax
74772:                 assume ds:dseg38
74773:                 mov     [bp+var_6], 0
74774:                 jmp     short loc_24B13
74775: ; ---------------------------------------------------------------------------
74776: 
74777: loc_24ACE:                              ; CODE XREF: _PERFORM_KICKS+7F↓j
74778:                 inc     [bp+var_6]
74779:                 mov     dx, word ptr dword_94B14+2
74780:                 mov     ax, word ptr dword_94B14
74781:                 mov     word ptr [bp+var_4+2], dx
74782:                 mov     word ptr [bp+var_4], ax
74783:                 les     bx, dword_94B14
74784:                 mov     ax, es:[bx+4]
74785:                 or      ax, es:[bx+6]
74786:                 jz      short loc_24AF4
74787:                 les     bx, dword_94B14
74788:                 call    dword ptr es:[bx+4]
74789: 
74790: loc_24AF4:                              ; CODE XREF: _PERFORM_KICKS+2F↑j
74791:                 les     bx, [bp+var_4]
74792:                 mov     dx, es:[bx+0Ah]
74793:                 mov     ax, es:[bx+8]
74794:                 mov     word ptr dword_94B14+2, dx
74795:                 mov     word ptr dword_94B14, ax
74796:                 push    word ptr [bp+var_4+2]
74797:                 push    word ptr [bp+var_4]
74798:                 push    cs
74799:                 call    near ptr sub_24A84
74800:                 add     sp, 4
74801: 
74802: loc_24B13:                              ; CODE XREF: _PERFORM_KICKS+11↑j
74803:                 mov     ax, word ptr dword_94B14
74804:                 or      ax, word ptr dword_94B14+2
74805:                 jz      short loc_24B3C
74806:                 les     bx, dword_94B14
74807:                 mov     dx, es:[bx+2]
74808:                 mov     ax, es:[bx]
74809:                 cmp     dx, word_94B12
74810:                 ja      short loc_24B3C
74811:                 jnz     short loc_24B35
74812:                 cmp     ax, word_94B10
74813:                 ja      short loc_24B3C
74814: 
74815: loc_24B35:                              ; CODE XREF: _PERFORM_KICKS+72↑j
74816:                 cmp     [bp+var_6], 3E8h
74817:                 jl      short loc_24ACE
74818: 
74819: loc_24B3C:                              ; CODE XREF: _PERFORM_KICKS+5F↑j
74820:                                         ; _PERFORM_KICKS+70↑j ...
74821:                 pop     ds
74822:                 assume ds:dseg81
74823:                 pop     di
74824:                 pop     si
74825:                 leave
74826:                 retf
74827: _PERFORM_KICKS  endp
74828: 
74829: ;
74830: ; External Entry #152 into the Module
74831: ; Attributes (0003): Fixed Exported Shared dataseg
74832: ;
74833: 
74834: ; =============== S U B R O U T I N E =======================================
74835: 
74836: ; Attributes: bp-based frame
74837: 
74838:                 public _MY_RTKICK
74839: _MY_RTKICK      proc far                ; CODE XREF: _INIT__WCCMMUD+B46↑P
74840:                                         ; _INIT__WCCMMUD+B5E↑P ...
74841: 
74842: var_C           = dword ptr -0Ch
74843: var_8           = dword ptr -8
74844: var_4           = dword ptr -4
74845: arg_0           = word ptr  6
74846: arg_2           = word ptr  8
74847: arg_4           = word ptr  0Ah
74848: 
74849:                 enter   0Ch, 0          ; WCCMMUD_152
74850:                 push    si
74851:                 push    di
74852:                 push    ds
74853:                 mov     ax, seg dseg38
74854:                 mov     ds, ax
74855:                 assume ds:dseg38
74856:                 push    cs
74857:                 call    near ptr sub_249B0
74858:                 mov     word ptr [bp+var_4+2], dx
74859:                 mov     word ptr [bp+var_4], ax
74860:                 mov     ax, word ptr [bp+var_4]
74861:                 or      ax, word ptr [bp+var_4+2]
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
74874:                 les     bx, [bp+var_4]
74875:                 mov     dx, [bp+arg_4]
74876:                 mov     ax, [bp+arg_2]
74877:                 mov     es:[bx+6], dx
74878:                 mov     es:[bx+4], ax
74879:                 les     bx, [bp+var_4]
74880:                 mov     word ptr es:[bx+0Ah], 0
74881:                 mov     word ptr es:[bx+8], 0
74882:                 mov     ax, word ptr dword_94B14
74883:                 or      ax, word ptr dword_94B14+2
74884:                 jz      short loc_24BCE
74885:                 les     bx, dword_94B14
74886:                 mov     dx, es:[bx+2]
74887:                 mov     ax, es:[bx]
74888:                 les     bx, [bp+var_4]
74889:                 cmp     dx, es:[bx+2]
74890:                 jb      short loc_24BDE
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
74903:                 mov     dx, word ptr [bp+var_4+2]
74904:                 mov     ax, word ptr [bp+var_4]
74905:                 mov     word ptr dword_94B14+2, dx
74906:                 mov     word ptr dword_94B14, ax
74907:                 jmp     loc_24C7A
74908: ; ---------------------------------------------------------------------------
74909: 
74910: loc_24BDE:                              ; CODE XREF: _MY_RTKICK+72↑j
74911:                                         ; _MY_RTKICK+79↑j
74912:                 mov     dx, word ptr dword_94B14+2
74913:                 mov     ax, word ptr dword_94B14
74914:                 mov     word ptr [bp+var_8+2], dx
74915:                 mov     word ptr [bp+var_8], ax
74916:                 mov     dx, word ptr dword_94B14+2
74917:                 mov     ax, word ptr dword_94B14
74918:                 mov     word ptr [bp+var_C+2], dx
74919:                 mov     word ptr [bp+var_C], ax
74920:                 jmp     short loc_24C1A
74921: ; ---------------------------------------------------------------------------
74922: 
74923: loc_24BFA:                              ; CODE XREF: _MY_RTKICK+F2↓j
74924:                                         ; _MY_RTKICK+F9↓j
74925:                 mov     dx, word ptr [bp+var_8+2]
74926:                 mov     ax, word ptr [bp+var_8]
74927:                 mov     word ptr [bp+var_C+2], dx
74928:                 mov     word ptr [bp+var_C], ax
74929:                 mov     es, word ptr [bp+var_C+2]
74930:                 mov     bx, word ptr [bp+var_8]
74931:                 mov     dx, es:[bx+0Ah]
74932:                 mov     ax, es:[bx+8]
74933:                 mov     word ptr [bp+var_8+2], dx
74934:                 mov     word ptr [bp+var_8], ax
74935: 
74936: loc_24C1A:                              ; CODE XREF: _MY_RTKICK+B7↑j
74937:                 mov     ax, word ptr [bp+var_8]
74938:                 or      ax, word ptr [bp+var_8+2]
74939:                 jz      short loc_24C3C
74940:                 les     bx, [bp+var_8]
74941:                 mov     dx, es:[bx+2]
74942:                 mov     ax, es:[bx]
74943:                 les     bx, [bp+var_4]
74944:                 cmp     dx, es:[bx+2]
74945:                 jb      short loc_24BFA
74946:                 jnz     short loc_24C3C
74947:                 cmp     ax, es:[bx]
74948:                 jb      short loc_24BFA
74949: 
74950: loc_24C3C:                              ; CODE XREF: _MY_RTKICK+DF↑j
74951:                                         ; _MY_RTKICK+F4↑j
74952:                 mov     ax, word ptr [bp+var_C]
74953:                 or      ax, word ptr [bp+var_C+2]
74954:                 jz      short loc_24C6D
74955:                 les     bx, [bp+var_C]
74956:                 mov     dx, es:[bx+0Ah]
74957:                 mov     ax, es:[bx+8]
74958:                 les     bx, [bp+var_4]
74959:                 mov     es:[bx+0Ah], dx
74960:                 mov     es:[bx+8], ax
74961:                 les     bx, [bp+var_C]
74962:                 mov     dx, word ptr [bp+var_4+2]
74963:                 mov     ax, word ptr [bp+var_4]
74964:                 mov     es:[bx+0Ah], dx
74965:                 mov     es:[bx+8], ax
74966:                 jmp     short loc_24C7A
74967: ; ---------------------------------------------------------------------------
74968: 
74969: loc_24C6D:                              ; CODE XREF: _MY_RTKICK+101↑j
74970:                 push    word ptr [bp+var_4+2]
74971:                 push    word ptr [bp+var_4]
74972:                 push    cs
74973:                 call    near ptr sub_24A84
74974:                 add     sp, 4
74975: 
74976: loc_24C7A:                              ; CODE XREF: _MY_RTKICK+1E↑j
74977:                                         ; _MY_RTKICK+9A↑j ...
74978:                 pop     ds
74979:                 assume ds:dseg81
74980:                 pop     di
74981:                 pop     si
74982:                 leave
74983:                 retf
74984: _MY_RTKICK      endp
74985: 
74986: ;
74987: ; External Entry #129 into the Module
74988: ; Attributes (0003): Fixed Exported Shared dataseg
74989: ;
74990: 
74991: ; =============== S U B R O U T I N E =======================================
74992: 
74993: 
74994:                 public _BACKGROUND_MONSTER_CREATE
74995: _BACKGROUND_MONSTER_CREATE proc far     ; DATA XREF: _INIT__WCCMMUD+B39↑o
74996:                                         ; _BACKGROUND_MONSTER_CREATE+18↓o
74997:                 push    si              ; WCCMMUD_129
74998:                 push    di
74999:                 push    ds
75000:                 mov     ax, seg dseg38
```

## MajorMUD CMD_SEARCH and hidden exit search path

```asm
190900: ; ---------------------------------------------------------------------------
190901: 
190902: loc_5FB4D:                              ; CODE XREF: _CMD_SEARCH+17↑j
190903:                 les     bx, [bp+arg_0]
190904:                 assume es:nothing
190905:                 and     word ptr es:[bx+6E2h], 0FBFFh
190906:                 les     bx, [bp+arg_0]
190907:                 and     word ptr es:[bx+7AEh], 0FEFFh
190908:                 les     bx, [bp+arg_0]
190909:                 and     word ptr es:[bx+6E2h], 0FFFBh
190910:                 les     bx, [bp+arg_0]
190911:                 mov     byte ptr es:[bx+5ECh], 0
190912:                 push    ss
190913:                 lea     ax, [bp+var_C]
190914:                 push    ax
190915:                 push    ss
190916:                 lea     ax, [bp+var_8]
190917:                 push    ax
190918:                 push    word ptr [bp+arg_0+2]
190919:                 push    word ptr [bp+arg_0]
190920:                 mov     ax, seg USRNUM
190921:                 mov     es, ax
190922:                 assume es:nothing
190923:                 push    word ptr es:USRNUM ; int usrnum;
190924:                 call    _CAN_SEE
190925:                 add     sp, 0Eh
190926:                 or      al, al
190927:                 jz      short loc_5FBC9
190928:                 mov     ax, seg USRNUM
190929:                 mov     es, ax
190930:                 push    word ptr es:USRNUM ; int usrnum;
190931:                 call    _IS_INSIDE_AUTOCOMBAT
190932:                 pop     cx
190933:                 or      al, al
190934:                 jz      short loc_5FBE0
190935:                 push    ds
190936:                 push    offset aYouMayNotSearc ; "You may not search while attacking!\r"
190937:                 call    PRF             ; prf(string);
190938:                 add     sp, 4
190939:                 mov     ax, seg USRNUM
190940:                 mov     es, ax
190941:                 push    word ptr es:USRNUM ; int usrnum;
190942:                 call    _TELL_USER
190943:                 pop     cx
190944: 
190945: loc_5FBC9:                              ; CODE XREF: _CMD_SEARCH+68↑j
190946:                 push    1
190947:                 mov     ax, seg USRNUM
190948:                 mov     es, ax
190949:                 push    word ptr es:USRNUM ; int usrnum;
190950:                 call    _ADD_DELAY
190951:                 add     sp, 4
190952:                 jmp     loc_5FCF1
190953: ; ---------------------------------------------------------------------------
190954: 
190955: loc_5FBE0:                              ; CODE XREF: _CMD_SEARCH+7C↑j
190956:                 mov     ax, seg MARGC
190957:                 mov     es, ax
190958:                 cmp     word ptr es:MARGC, 1 ; int margc;
190959:                 jz      short loc_5FBF0
190960:                 jmp     loc_5FC7E
190961: ; ---------------------------------------------------------------------------
190962: 
190963: loc_5FBF0:                              ; CODE XREF: _CMD_SEARCH+BC↑j
190964:                 push    1
190965:                 push    1
190966:                 push    0
190967:                 push    0
190968:                 mov     ax, seg USRNUM
190969:                 mov     es, ax
190970:                 push    word ptr es:USRNUM ; int usrnum;
190971:                 call    _DISPLAY_ITEMS_IN_ROOM
190972:                 add     sp, 0Ah
190973:                 push    ds
190974:                 push    offset a79dK036m_4 ; "\x1B[[\x1B[79D\x1B[K| \b \b \b \b]\x1B["...
190975:                 call    PRF             ; prf(string);
190976:                 add     sp, 4
190977:                 mov     ax, word ptr [bp+arg_0]
190978:                 add     ax, 1Eh
190979:                 push    word ptr [bp+arg_0+2]
190980:                 push    ax
190981:                 push    ds
190982:                 push    offset aSIsSearchingTh ; "%s is searching the area.\r"
190983:                 call    PRF             ; prf(string);
190984:                 add     sp, 8
190985:                 push    0FFh
190986:                 push    1
190987:                 push    1
190988:                 push    0
190989:                 push    0FFFFh
190990:                 mov     ax, seg USRNUM
190991:                 mov     es, ax
190992:                 push    word ptr es:USRNUM ; int usrnum;
190993:                 les     bx, [bp+arg_0]
190994:                 assume es:nothing
190995:                 push    word ptr es:[bx+0CAh]
190996:                 push    word ptr es:[bx+0C8h]
190997:                 les     bx, [bp+arg_0]
190998:                 push    word ptr es:[bx+0C6h]
190999:                 push    word ptr es:[bx+0C4h]
191000:                 call    _TELL_ROOM
191001:                 add     sp, 14h
191002: 
191003: loc_5FC63:                              ; CODE XREF: _CMD_SEARCH+19C↓j
191004:                                         ; _CMD_SEARCH+1BA↓j
191005:                 push    1
191006:                 mov     ax, seg USRNUM
191007:                 mov     es, ax
191008:                 assume es:nothing
191009:                 push    word ptr es:USRNUM ; int usrnum;
191010:                 call    _ADD_DELAY
191011:                 add     sp, 4
191012:                 mov     [bp+var_2], 2
191013:                 jmp     short loc_5FCF1
191014: ; ---------------------------------------------------------------------------
191015: 
191016: loc_5FC7E:                              ; CODE XREF: _CMD_SEARCH+BE↑j
191017:                 mov     ax, seg MARGC
191018:                 mov     es, ax
191019:                 cmp     word ptr es:MARGC, 2 ; int margc;
191020:                 jnz     short loc_5FCEC
191021:                 mov     ax, seg MARGV
191022:                 mov     es, ax
191023:                 push    word ptr es:MARGV+6 ; char *margv[];
191024:                 push    word ptr es:MARGV+4 ; char *margv[];
191025:                 push    ss
191026:                 lea     ax, [bp+var_6]
191027:                 push    ax
191028:                 call    _PARSE_COMMAND
191029:                 add     sp, 8
191030:                 mov     [bp+var_4], ax
191031:                 cmp     [bp+var_4], 0
191032:                 jz      short loc_5FCCD
191033:                 cmp     [bp+var_6], 0Ah
191034:                 jge     short loc_5FCCD
191035:                 push    [bp+var_6]
191036:                 mov     ax, seg USRNUM
191037:                 mov     es, ax
191038:                 push    word ptr es:USRNUM ; int usrnum;
191039:                 call    _SEARCH_FOR_HIDDEN_EXITS
191040:                 add     sp, 4
191041:                 jmp     short loc_5FC63
191042: ; ---------------------------------------------------------------------------
191043: 
191044: loc_5FCCD:                              ; CODE XREF: _CMD_SEARCH+17F↑j
191045:                                         ; _CMD_SEARCH+185↑j
191046:                 push    ds
191047:                 push    offset aWhyWouldYouWan_4 ; "Why would you want to search that?\r"
191048:                 call    PRF             ; prf(string);
191049:                 add     sp, 4
191050:                 mov     ax, seg USRNUM
191051:                 mov     es, ax
191052:                 push    word ptr es:USRNUM ; int usrnum;
191053:                 call    _TELL_USER
191054:                 pop     cx
191055:                 jmp     loc_5FC63
191056: ; ---------------------------------------------------------------------------
191057: 
191058: loc_5FCEC:                              ; CODE XREF: _CMD_SEARCH+15A↑j
191059:                 mov     [bp+var_2], 0
191060: 
191061: loc_5FCF1:                              ; CODE XREF: _CMD_SEARCH+AE↑j
191062:                                         ; _CMD_SEARCH+14D↑j
191063:                 mov     ax, [bp+var_2]
191064: 
191065: loc_5FCF4:                              ; CODE XREF: _CMD_SEARCH+1B↑j
191066:                 pop     ds
191067:                 assume ds:dseg81
191068:                 pop     di
191069:                 pop     si
191070:                 leave
191071:                 retf
191072: _CMD_SEARCH     endp
191073: 
191074: ;
191075: ; External Entry #451 into the Module
191076: ; Attributes (0003): Fixed Exported Shared dataseg
191077: ;
191078: 
191079: ; =============== S U B R O U T I N E =======================================
191080: 
```

## Room/area reset causes monsters to start generating appropriately

```asm
239400:                 mov     ax, es:[bx+5B9h]
239401:                 or      ax, es:[bx+5BBh]
239402:                 jz      short loc_79C3C
239403:                 push    [bp+arg_6]
239404:                 push    [bp+arg_4]
239405:                 les     bx, [bp+var_4]
239406:                 push    word ptr es:[bx+5BBh]
239407:                 push    word ptr es:[bx+5B9h]
239408:                 call    _GET_ROOM_DATA
239409:                 add     sp, 8
239410:                 mov     word ptr [bp+var_4+2], dx
239411:                 mov     word ptr [bp+var_4], ax
239412:                 mov     ax, word ptr [bp+var_4]
239413:                 or      ax, word ptr [bp+var_4+2]
239414:                 jz      short loc_79C3C
239415:                 les     bx, [bp+var_4]
239416:                 mov     word ptr es:[bx+5B7h], 0
239417:                 les     bx, [bp+var_4]
239418:                 mov     byte ptr es:[bx+5EDh], 1
239419:                 push    ds
239420:                 push    offset aAreaResetMonst ; "Area Reset - monsters will start genera"...
239421:                 jmp     short loc_79C34
239422: ; ---------------------------------------------------------------------------
239423: 
239424: loc_79C1E:                              ; CODE XREF: sub_79B96+35↑j
239425:                 les     bx, [bp+var_4]
239426:                 mov     byte ptr es:[bx+5F7h], 0
239427:                 les     bx, [bp+var_4]
239428:                 mov     byte ptr es:[bx+5EDh], 1
239429:                 push    ds
239430:                 push    offset aRoomResetMonst ; "Room reset - monsters will start genera"...
239431: 
239432: loc_79C34:                              ; CODE XREF: sub_79B96+86↑j
239433:                 call    PRF             ; prf(string);
239434:                 add     sp, 4
239435: 
239436: loc_79C3C:                              ; CODE XREF: sub_79B96+44↑j
239437:                                         ; sub_79B96+6D↑j
239438:                 push    ds
239439:                 push    offset aRecoveryWillBe ; "Recovery will be done during next shutd"...
239440:                 call    PRF             ; prf(string);
239441:                 add     sp, 4
239442:                 mov     ax, seg dseg42
239443:                 mov     es, ax
239444:                 assume es:dseg42
239445:                 mov     es:byte_98D97, 1
239446:                 mov     ax, seg USRNUM
239447:                 mov     es, ax
239448:                 assume es:nothing
239449:                 push    word ptr es:USRNUM ; int usrnum;
239450:                 call    _TELL_USER
```

## Item attribute strings include cleanup/delete/recharge/visibility/quest flags

```asm
300120:                 dd aNotNeutralAlig      ; "Not Neutral Aligned"
300121:                 dd aAutoUseAbiliti      ; "Auto Use Abilities"
300122:                 dd aDescriptiveMes      ; "Descriptive Message"
300123:                 dd aAlterBackstabA      ; "Alter Backstab Attack Value"
300124:                 dd aAlterBackstabM      ; "Alter Backstab Min. Damage"
300125:                 dd aAlterBackstabM_0    ; "Alter Backstab Max. Damage"
300126:                 dd aDeleteAtCleanu      ; "Delete at cleanup"
300127:                 dd aStartingMessag      ; "Starting Message"
300128:                 dd aRechargeAtClea      ; "Recharge at cleanup"
300129:                 dd aRemoveSpell         ; "Remove spell"
300130:                 dd aAlterHealingRa      ; "Alter healing rate"
300131:                 dd aNegateAbility       ; "Negate ability"
300132:                 dd aQuestTypeOne        ; "Quest type one"
300133:                 dd aQuestTypeTwo        ; "Quest type two"
300134:                 dd aQuestTypeThree      ; "Quest type three"
300135:                 dd aQuestTypeFour       ; "Quest type four"
300136:                 dd aQuestTypeFive       ; "Quest type five"
300137:                 dd aQuestTypeSix        ; "Quest type six"
300138:                 dd aQuestTypeSeven      ; "Quest type seven"
300139:                 dd aQuestTypeEight      ; "Quest type eight"
300140:                 dd aQuestTypeNine       ; "Quest type nine"
300141:                 dd aQuestTypeTen        ; "Quest type ten"
300142:                 dd aMinimumLevelAl      ; "Minimum level allowed"
300143:                 dd aMaximumLevelAl      ; "Maximum level allowed"
300144:                 dd aShockshieldMes      ; "Shockshield Message"
300145:                 dd aVisiblePlacedI      ; "Visible Placed Item"
300146:                 dd aSpellImmunity       ; "Spell Immunity"
300147:                 dd aTeleportRoom        ; "Teleport Room"
300148:                 dd aTeleportMap         ; "Teleport Map"
300149:                 dd aHitMagical          ; "Hit Magical"
300150:                 dd aClearItem           ; "Clear Item"
300151:                 dd aNonMagicalSpel      ; "Non Magical Spell"
300152:                 dd aAlterManaRegen      ; "Alter Mana Regeneration"
300153:                 dd aGuardedByMonst      ; "Guarded by Monster"
300154:                 dd aResistWater         ; "Resist Water"
300155:                 dd aTriggerTextBlo      ; "Trigger Text Block"
300156:                 dd aDeleteFromInve      ; "Delete from inventory at cleanup"
300157:                 dd aHealMana            ; "Heal Mana"
300158:                 dd aCastOnEnding        ; "Cast on ending"
300159:                 dd aRune                ; "Rune"
300160:                 dd aTerminateSpell      ; "Terminate Spell"
300161:                 dd aRemainVisibleA      ; "Remain Visible at cleanup"
300162:                 dd aDeathTextBlock      ; "Death text block"
300163:                 dd aQuestItem           ; "Quest item"
300164:                 dd aScatterItems        ; "Scatter items"
300165:                 dd aRequiredToHit       ; "Required to hit"
```

