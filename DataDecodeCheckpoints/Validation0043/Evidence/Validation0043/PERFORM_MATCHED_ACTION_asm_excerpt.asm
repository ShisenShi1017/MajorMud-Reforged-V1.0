_PERFORM_MATCHED_ACTION proc far        ; CODE XREF: _PERFORM_SPECIAL_COMMAND+FD↓p
                                        ; sub_80A66+109↓p ...

var_1C8         = byte ptr -1C8h
var_38          = word ptr -38h
var_36          = word ptr -36h
var_33          = byte ptr -33h
var_32          = word ptr -32h
var_30          = byte ptr -30h
var_2E          = word ptr -2Eh
var_2C          = dword ptr -2Ch
var_28          = word ptr -28h
var_26          = word ptr -26h
var_24          = dword ptr -24h
var_20          = dword ptr -20h
var_1C          = word ptr -1Ch
var_1A          = word ptr -1Ah
var_18          = word ptr -18h
var_16          = word ptr -16h
var_14          = word ptr -14h
var_12          = word ptr -12h
var_10          = word ptr -10h
var_E           = word ptr -0Eh
var_C           = dword ptr -0Ch
var_8           = word ptr -8
var_6           = word ptr -6
var_4           = dword ptr -4
arg_0           = word ptr  6
arg_2           = word ptr  8
arg_4           = word ptr  0Ah

                enter   1C8h, 0         ; WCCMMUD_550
                push    si
                push    di
                push    ds
                mov     ax, seg dseg68
                mov     ds, ax
                assume ds:dseg68
                mov     dx, [bp+arg_4]
                mov     ax, [bp+arg_2]
                mov     word ptr [bp+var_4+2], dx
                mov     word ptr [bp+var_4], ax
                push    [bp+arg_0]
                call    _GET_PLAYER
                pop     cx
                mov     word ptr [bp+var_20+2], dx
                mov     word ptr [bp+var_20], ax
                mov     [bp+var_36], 0
                mov     ax, word ptr [bp+var_20]
                or      ax, word ptr [bp+var_20+2]
                jnz     short loc_7E86A
                mov     al, 0
                jmp     loc_807ED
; ---------------------------------------------------------------------------

loc_7E86A:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+32↑j
                mov     [bp+var_32], 0

loc_7E86F:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+5C↓j
                mov     bx, [bp+var_32]
                shl     bx, 2
                lea     ax, [bp+var_1C8]
                add     bx, ax
                mov     word ptr ss:[bx+2], 0
                mov     word ptr ss:[bx], 0
                inc     [bp+var_32]
                cmp     [bp+var_32], 64h ; 'd'
                jl      short loc_7E86F
                jmp     loc_807D6
; ---------------------------------------------------------------------------

loc_7E892:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1FB6↓j
                mov     dx, word ptr [bp+var_4+2]
                mov     ax, word ptr [bp+var_4]
                mov     [bp+var_6], dx
                mov     [bp+var_8], ax
                push    3Ah ; ':'
                push    word ptr [bp+var_4+2]
                push    word ptr [bp+var_4]
                call    STRCHR          ; char * strchr ( const char * str, int character );
                add     sp, 6
                mov     word ptr [bp+var_4+2], dx
                mov     word ptr [bp+var_4], ax
                mov     ax, word ptr [bp+var_4]
                or      ax, word ptr [bp+var_4+2]
                jz      short loc_7E8C6
                les     bx, [bp+var_4]
                mov     byte ptr es:[bx], 0
                inc     word ptr [bp+var_4]

loc_7E8C6:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+89↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aPrice   ; "price"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jz      short loc_7E95A
                push    ss
                lea     ax, [bp+var_4]
                push    ax
                push    [bp+var_6]
                push    [bp+var_8]
                push    word ptr [bp+var_20+2]
                push    word ptr [bp+var_20]
                push    [bp+arg_0]
                push    cs
                call    near ptr sub_7D70B
                add     sp, 0Eh
                mov     [bp+var_36], ax
                cmp     [bp+var_36], 2
                jz      short loc_7E903
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7E903:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+CD↑j
                mov     [bp+var_32], 0

loc_7E908:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+124↓j
                mov     bx, [bp+var_32]
                shl     bx, 2
                lea     ax, [bp+var_1C8]
                add     bx, ax
                mov     ax, ss:[bx]
                or      ax, ss:[bx+2]
                jz      short loc_7E94E
                mov     ax, seg dseg76
                mov     es, ax
                assume es:dseg76
                cmp     es:word_AC8C4, 0
                jz      short loc_7E94E
                push    0
                push    0
                push    0
                mov     bx, [bp+var_32]
                shl     bx, 2
                lea     ax, [bp+var_1C8]
                add     bx, ax
                push    word ptr ss:[bx+2]
                push    word ptr ss:[bx]
                push    [bp+arg_0]
                call    _ADD_ITEM_TO_INVENTORY
                add     sp, 0Ch

loc_7E94E:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+EA↑j
                                        ; _PERFORM_MATCHED_ACTION+F7↑j
                inc     [bp+var_32]
                cmp     [bp+var_32], 64h ; 'd'
                jl      short loc_7E908
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7E95A:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+A9↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aCast    ; "cast"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jnz     short loc_7E973
                jmp     loc_7EA15
; ---------------------------------------------------------------------------

loc_7E973:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+13D↑j
                mov     [bp+var_36], 1
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_7E9A1
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7E9A1:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+16B↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                push    [bp+var_10]
                call    _ALLOC_GET_SPELL_DATA
                pop     cx
                mov     [bp+var_26], dx
                mov     [bp+var_28], ax
                mov     ax, [bp+var_28]
                or      ax, [bp+var_26]
                jnz     short loc_7E9CF
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7E9CF:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+199↑j
                push    1
                push    [bp+arg_0]
                push    [bp+var_26]
                push    [bp+var_28]
                call    _CAST_NO_TARGET
                add     sp, 8
                or      al, al
                jnz     short loc_7EA05
                mov     ax, word ptr [bp+var_4]
                or      ax, word ptr [bp+var_4+2]
                jz      short loc_7E9F6
                les     bx, [bp+var_4]
                assume es:nothing
                mov     byte ptr es:[bx-1], 3Ah ; ':'

loc_7E9F6:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1BB↑j
                mov     word ptr [bp+var_4+2], 0
                mov     word ptr [bp+var_4], 0
                mov     [bp+var_36], 2

loc_7EA05:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1B3↑j
                push    ss
                lea     ax, [bp+var_28]
                push    ax
                call    _FREE_SPELL_DATA
                add     sp, 4
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7EA15:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+13F↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aGiveability ; "giveability"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jnz     short loc_7EA2E
                jmp     loc_7EBA0
; ---------------------------------------------------------------------------

loc_7EA2E:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1F8↑j
                mov     [bp+var_36], 1
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_7EA5C
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7EA5C:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+226↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_7EA99
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7EA99:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+263↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_12], dx
                mov     [bp+var_14], ax
                mov     [bp+var_33], 0
                mov     [bp+var_2E], 0

loc_7EAB6:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+2E5↓j
                mov     ax, [bp+var_2E]
                add     ax, ax
                les     bx, [bp+var_20]
                add     bx, ax
                mov     ax, es:[bx+722h]
                cwd
                cmp     dx, [bp+var_E]
                jnz     short loc_7EB0F
                cmp     ax, [bp+var_10]
                jnz     short loc_7EB0F
                mov     [bp+var_33], 1
                cmp     [bp+var_E], 0
                jnz     short loc_7EAE1
                cmp     [bp+var_10], 0A0h
                jz      short loc_7EB0F

loc_7EAE1:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+2A7↑j
                mov     ax, [bp+var_2E]
                add     ax, ax
                les     bx, [bp+var_20]
                add     bx, ax
                mov     ax, es:[bx+75Eh]
                cwd
                cmp     dx, [bp+var_12]
                jg      short loc_7EB0F
                jl      short loc_7EAFD
                cmp     ax, [bp+var_14]
                jnb     short loc_7EB0F

loc_7EAFD:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+2C5↑j
                mov     ax, [bp+var_2E]
                add     ax, ax
                les     bx, [bp+var_20]
                add     bx, ax
                mov     ax, [bp+var_14]
                mov     es:[bx+75Eh], ax

loc_7EB0F:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+298↑j
                                        ; _PERFORM_MATCHED_ACTION+29D↑j ...
                inc     [bp+var_2E]
                cmp     [bp+var_2E], 1Eh
                jl      short loc_7EAB6
                cmp     [bp+var_33], 0
                jnz     short loc_7EB89
                mov     [bp+var_2E], 0
                jmp     short loc_7EB7D
; ---------------------------------------------------------------------------

loc_7EB25:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+356↓j
                mov     ax, [bp+var_2E]
                add     ax, ax
                les     bx, [bp+var_20]
                add     bx, ax
                cmp     word ptr es:[bx+722h], 0
                jnz     short loc_7EB7A
                mov     [bp+var_33], 1
                mov     ax, [bp+var_2E]
                add     ax, ax
                les     bx, [bp+var_20]
                add     bx, ax
                mov     ax, [bp+var_10]
                mov     es:[bx+722h], ax
                mov     ax, [bp+var_2E]
                add     ax, ax
                les     bx, [bp+var_20]
                add     bx, ax
                mov     ax, [bp+var_14]
                mov     es:[bx+75Eh], ax
                cmp     [bp+var_E], 0
                jnz     short loc_7EB7A
                cmp     [bp+var_10], 0A0h
                jnz     short loc_7EB7A
                push    [bp+var_14]
                push    [bp+arg_0]
                call    _ADD_SPELL_TO_SPELLBOOK
                add     sp, 4

loc_7EB7A:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+304↑j
                                        ; _PERFORM_MATCHED_ACTION+332↑j ...
                inc     [bp+var_2E]

loc_7EB7D:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+2F2↑j
                cmp     [bp+var_33], 0
                jnz     short loc_7EB89
                cmp     [bp+var_2E], 1Eh
                jl      short loc_7EB25

loc_7EB89:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+2EB↑j
                                        ; _PERFORM_MATCHED_ACTION+350↑j
                cmp     [bp+var_33], 0
                jz      short loc_7EB92
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7EB92:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+35C↑j
                mov     ax, word ptr [bp+var_4]
                or      ax, word ptr [bp+var_4+2]
                jnz     short loc_7EB9D
                jmp     loc_8028E
; ---------------------------------------------------------------------------

loc_7EB9D:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+367↑j
                jmp     loc_80286
; ---------------------------------------------------------------------------

loc_7EBA0:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1FA↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aAddability ; "addability"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jnz     short loc_7EBB9
                jmp     loc_7EC66
; ---------------------------------------------------------------------------

loc_7EBB9:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+383↑j
                mov     [bp+var_36], 1
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_7EBE7
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7EBE7:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+3B1↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_7EC24
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7EC24:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+3EE↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_12], dx
                mov     [bp+var_14], ax
                push    [bp+var_14]
                push    [bp+var_10]
                push    word ptr [bp+var_20+2]
                push    word ptr [bp+var_20]
                call    sub_7A943
                add     sp, 8
                mov     [bp+var_33], al
                cmp     [bp+var_33], 0
                jz      short loc_7EC58
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7EC58:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+422↑j
                mov     ax, word ptr [bp+var_4]
                or      ax, word ptr [bp+var_4+2]
                jnz     short loc_7EC63
                jmp     loc_8028E
; ---------------------------------------------------------------------------

loc_7EC63:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+42D↑j
                jmp     loc_80286
; ---------------------------------------------------------------------------

loc_7EC66:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+385↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aTeleport ; "teleport"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jz      short loc_7EC97
                push    ss
                lea     ax, [bp+var_4]
                push    ax
                push    [bp+var_6]
                push    [bp+var_8]
                push    word ptr [bp+var_20+2]
                push    word ptr [bp+var_20]
                push    [bp+arg_0]
                push    cs
                call    near ptr sub_7DD47
                jmp     loc_807C0
; ---------------------------------------------------------------------------

loc_7EC97:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+449↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aRemoveability ; "removeability"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jnz     short loc_7ECB0
                jmp     loc_7ED85
; ---------------------------------------------------------------------------

loc_7ECB0:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+47A↑j
                mov     [bp+var_36], 1
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_7ECDE
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7ECDE:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+4A8↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                mov     [bp+var_33], 0
                mov     [bp+var_2E], 0

loc_7ECFB:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+53B↓j
                mov     ax, [bp+var_2E]
                add     ax, ax
                les     bx, [bp+var_20]
                add     bx, ax
                mov     ax, es:[bx+722h]
                cwd
                cmp     dx, [bp+var_E]
                jnz     short loc_7ED65
                cmp     ax, [bp+var_10]
                jnz     short loc_7ED65
                mov     [bp+var_33], 1
                cmp     [bp+var_E], 0
                jnz     short loc_7ED43
                cmp     [bp+var_10], 0A0h
                jnz     short loc_7ED43
                mov     ax, [bp+var_2E]
                add     ax, ax
                les     bx, [bp+var_20]
                add     bx, ax
                push    word ptr es:[bx+75Eh]
                push    word ptr [bp+var_20+2]
                push    word ptr [bp+var_20]
                call    _PURGE_SPELL_FROM_SPELLBOOK
                add     sp, 6

loc_7ED43:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+4EC↑j
                                        ; _PERFORM_MATCHED_ACTION+4F3↑j
                mov     ax, [bp+var_2E]
                add     ax, ax
                les     bx, [bp+var_20]
                add     bx, ax
                mov     word ptr es:[bx+722h], 0
                mov     ax, [bp+var_2E]
                add     ax, ax
                les     bx, [bp+var_20]
                add     bx, ax
                mov     word ptr es:[bx+75Eh], 0

loc_7ED65:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+4DD↑j
                                        ; _PERFORM_MATCHED_ACTION+4E2↑j
                inc     [bp+var_2E]
                cmp     [bp+var_2E], 1Eh
                jl      short loc_7ECFB
                cmp     [bp+var_33], 0
                jz      short loc_7ED77
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7ED77:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+541↑j
                mov     ax, word ptr [bp+var_4]
                or      ax, word ptr [bp+var_4+2]
                jnz     short loc_7ED82
                jmp     loc_8028E
; ---------------------------------------------------------------------------

loc_7ED82:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+54C↑j
                jmp     loc_80286
; ---------------------------------------------------------------------------

loc_7ED85:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+47C↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aNomonsters ; "nomonsters"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jnz     short loc_7ED9E
                jmp     loc_7EE7E
; ---------------------------------------------------------------------------

loc_7ED9E:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+568↑j
                mov     [bp+var_36], 1
                les     bx, [bp+var_20]
                push    word ptr es:[bx+0C6h]
                push    word ptr es:[bx+0C4h]
                les     bx, [bp+var_20]
                push    word ptr es:[bx+0CAh]
                push    word ptr es:[bx+0C8h]
                call    _GET_ROOM_DATA
                add     sp, 8
                mov     word ptr [bp+var_24+2], dx
                mov     word ptr [bp+var_24], ax
                mov     ax, word ptr [bp+var_24]
                or      ax, word ptr [bp+var_24+2]
                jnz     short loc_7EDD6
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7EDD6:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+5A0↑j
                mov     [bp+var_2E], 0
                jmp     loc_7EE67
; ---------------------------------------------------------------------------

loc_7EDDE:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+647↓j
                mov     ax, [bp+var_2E]
                shl     ax, 2
                les     bx, [bp+var_24]
                add     bx, ax
                mov     ax, es:[bx+400h]
                or      ax, es:[bx+402h]
                jz      short loc_7EE64
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jz      short loc_7EE45
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                push    word ptr [bp+var_20+2]
                push    word ptr [bp+var_20]
                push    [bp+arg_0]
                push    [bp+var_E]
                push    [bp+var_10]
                push    cs
                call    near ptr sub_7D630
                add     sp, 0Ah

loc_7EE45:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+5E8↑j
                mov     ax, word ptr [bp+var_4]
                or      ax, word ptr [bp+var_4+2]
                jz      short loc_7EE55
                les     bx, [bp+var_4]
                mov     byte ptr es:[bx-1], 3Ah ; ':'

loc_7EE55:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+61A↑j
                mov     word ptr [bp+var_4+2], 0
                mov     word ptr [bp+var_4], 0
                mov     [bp+var_36], 2

loc_7EE64:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+5C2↑j
                inc     [bp+var_2E]

loc_7EE67:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+5AA↑j
                mov     ax, word ptr [bp+var_4]
                or      ax, word ptr [bp+var_4+2]
                jnz     short loc_7EE72
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7EE72:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+63C↑j
                cmp     [bp+var_2E], 0Fh
                jge     short loc_7EE7B
                jmp     loc_7EDDE
; ---------------------------------------------------------------------------

loc_7EE7B:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+645↑j
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7EE7E:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+56A↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    (offset aNomonsters+2) ; "monsters"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jnz     short loc_7EE97
                jmp     loc_7EF63
; ---------------------------------------------------------------------------

loc_7EE97:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+661↑j
                mov     [bp+var_36], 1
                les     bx, [bp+var_20]
                push    word ptr es:[bx+0C6h]
                push    word ptr es:[bx+0C4h]
                les     bx, [bp+var_20]
                push    word ptr es:[bx+0CAh]
                push    word ptr es:[bx+0C8h]
                call    _GET_ROOM_DATA
                add     sp, 8
                mov     word ptr [bp+var_24+2], dx
                mov     word ptr [bp+var_24], ax
                mov     ax, word ptr [bp+var_24]
                or      ax, word ptr [bp+var_24+2]
                jnz     short loc_7EECF
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7EECF:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+699↑j
                mov     [bp+var_33], 0
                mov     [bp+var_2E], 0

loc_7EED8:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+6C9↓j
                mov     ax, [bp+var_2E]
                shl     ax, 2
                les     bx, [bp+var_24]
                add     bx, ax
                mov     ax, es:[bx+400h]
                or      ax, es:[bx+402h]
                jz      short loc_7EEF3
                mov     [bp+var_33], 1

loc_7EEF3:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+6BC↑j
                inc     [bp+var_2E]
                cmp     [bp+var_2E], 0Fh
                jl      short loc_7EED8
                cmp     [bp+var_33], 0
                jz      short loc_7EF05
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7EF05:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+6CF↑j
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jz      short loc_7EF55
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                push    word ptr [bp+var_20+2]
                push    word ptr [bp+var_20]
                push    [bp+arg_0]
                push    [bp+var_E]
                push    [bp+var_10]
                push    cs
                call    near ptr sub_7D630
                add     sp, 0Ah

loc_7EF55:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+6F8↑j
                mov     ax, word ptr [bp+var_4]
                or      ax, word ptr [bp+var_4+2]
                jnz     short loc_7EF60
                jmp     loc_8028E
; ---------------------------------------------------------------------------

loc_7EF60:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+72A↑j
                jmp     loc_80286
; ---------------------------------------------------------------------------

loc_7EF63:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+663↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aNeedmonster ; "needmonster"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jz      short loc_7EF94
                push    ss
                lea     ax, [bp+var_4]
                push    ax
                push    [bp+var_6]
                push    [bp+var_8]
                push    word ptr [bp+var_20+2]
                push    word ptr [bp+var_20]
                push    [bp+arg_0]
                push    cs
                call    near ptr sub_7E6A6
                jmp     loc_807C0
; ---------------------------------------------------------------------------

loc_7EF94:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+746↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aSummon_0 ; "summon"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jnz     short loc_7EFAD
                jmp     loc_7F039
; ---------------------------------------------------------------------------

loc_7EFAD:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+777↑j
                mov     [bp+var_36], 1
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_7EFDB
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7EFDB:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+7A5↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                push    1
                push    1
                push    0FFFFh
                push    7FFFh
                push    0
                push    [bp+var_E]
                push    [bp+var_10]
                push    0
                les     bx, [bp+var_20]
                push    word ptr es:[bx+0C6h]
                push    word ptr es:[bx+0C4h]
                les     bx, [bp+var_20]
                push    word ptr es:[bx+0CAh]
                push    word ptr es:[bx+0C8h]
                call    _GENERATE_MONSTER
                add     sp, 18h
                or      ax, dx
                jz      short loc_7F02B
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7F02B:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+7F5↑j
                mov     ax, word ptr [bp+var_4]
                or      ax, word ptr [bp+var_4+2]
                jnz     short loc_7F036
                jmp     loc_8028E
; ---------------------------------------------------------------------------

loc_7F036:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+800↑j
                jmp     loc_80286
; ---------------------------------------------------------------------------

loc_7F039:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+779↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aMessage ; "message"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jz      short loc_7F0AA
                mov     [bp+var_36], 1
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_7F07D
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7F07D:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+847↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                push    word ptr [bp+var_20+2]
                push    word ptr [bp+var_20]
                push    [bp+arg_0]
                push    [bp+var_E]
                push    [bp+var_10]
                push    cs
                call    near ptr sub_7D630
                add     sp, 0Ah
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7F0AA:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+81C↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aTakeitem ; "takeitem"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jnz     short loc_7F0C3
                jmp     loc_7F219
; ---------------------------------------------------------------------------

loc_7F0C3:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+88D↑j
                mov     [bp+var_36], 1
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_7F0F1
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7F0F1:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+8BB↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                push    0
                push    0
                push    ss
                lea     ax, [bp+var_30]
                push    ax
                push    [bp+var_E]
                push    [bp+var_10]
                push    [bp+arg_0]
                call    _REMOVE_ITEM_FROM_INVENTORY
                add     sp, 0Eh
                or      al, al
                jz      short loc_7F126
                jmp     loc_7F1D8
; ---------------------------------------------------------------------------

loc_7F126:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+8F0↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     [bp+var_32], 0

loc_7F149:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+965↓j
                mov     bx, [bp+var_32]
                shl     bx, 2
                lea     ax, [bp+var_1C8]
                add     bx, ax
                mov     ax, ss:[bx]
                or      ax, ss:[bx+2]
                jz      short loc_7F18F
                mov     ax, seg dseg76
                mov     es, ax
                assume es:dseg76
                cmp     es:word_AC8C4, 0
                jz      short loc_7F18F
                push    0
                push    0
                push    0
                mov     bx, [bp+var_32]
                shl     bx, 2
                lea     ax, [bp+var_1C8]
                add     bx, ax
                push    word ptr ss:[bx+2]
                push    word ptr ss:[bx]
                push    [bp+arg_0]
                call    _ADD_ITEM_TO_INVENTORY
                add     sp, 0Ch

loc_7F18F:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+92B↑j
                                        ; _PERFORM_MATCHED_ACTION+938↑j
                inc     [bp+var_32]
                cmp     [bp+var_32], 64h ; 'd'
                jl      short loc_7F149
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jz      short loc_7F1CA
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                push    word ptr [bp+var_20+2]
                push    word ptr [bp+var_20]
                push    [bp+arg_0]
                push    [bp+var_E]
                push    [bp+var_10]
                push    cs
                call    near ptr sub_7D630
                add     sp, 0Ah

loc_7F1CA:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+96D↑j
                mov     ax, word ptr [bp+var_4]
                or      ax, word ptr [bp+var_4+2]
                jnz     short loc_7F1D5
                jmp     loc_8028E
; ---------------------------------------------------------------------------

loc_7F1D5:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+99F↑j
                jmp     loc_80286
; ---------------------------------------------------------------------------

loc_7F1D8:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+8F2↑j
                mov     [bp+var_32], 0
                jmp     short loc_7F1E2
; ---------------------------------------------------------------------------

loc_7F1DF:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+9CA↓j
                inc     [bp+var_32]

loc_7F1E2:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+9AC↑j
                cmp     [bp+var_32], 64h ; 'd'
                jge     short loc_7F1FD
                mov     bx, [bp+var_32]
                shl     bx, 2
                lea     ax, [bp+var_1C8]
                add     bx, ax
                mov     ax, ss:[bx]
                or      ax, ss:[bx+2]
                jnz     short loc_7F1DF

loc_7F1FD:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+9B5↑j
                mov     bx, [bp+var_32]
                shl     bx, 2
                lea     ax, [bp+var_1C8]
                add     bx, ax
                mov     dx, [bp+var_E]
                mov     ax, [bp+var_10]
                mov     ss:[bx+2], dx
                mov     ss:[bx], ax
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7F219:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+88F↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aAddexp  ; "addexp"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jz      short loc_7F284
                mov     [bp+var_36], 1
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_7F25D
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7F25D:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+A27↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                push    0
                push    [bp+var_E]
                push    [bp+var_10]
                push    [bp+arg_0]
                call    _ADD_QUEST_EXP
                jmp     loc_7FC0D
; ---------------------------------------------------------------------------

loc_7F284:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+9FC↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aGiveitem ; "giveitem"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jnz     short loc_7F29D
                jmp     loc_7F35E
; ---------------------------------------------------------------------------

loc_7F29D:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+A67↑j
                mov     [bp+var_36], 1
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_7F2CB
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7F2CB:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+A95↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                push    0
                push    0
                push    0FFFEh
                push    [bp+var_E]
                push    [bp+var_10]
                push    [bp+arg_0]
                call    _ADD_ITEM_TO_INVENTORY
                add     sp, 0Ch
                or      al, al
                jz      short loc_7F2FD
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7F2FD:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+AC7↑j
                call    CLRPRF          ; clrprf()
                push    [bp+var_E]
                push    [bp+var_10]
                call    _GET_ITEM_DATA
                add     sp, 4
                mov     word ptr [bp+var_2C+2], dx
                mov     word ptr [bp+var_2C], ax
                mov     ax, word ptr [bp+var_2C]
                or      ax, word ptr [bp+var_2C+2]
                jz      short loc_7F350
                les     bx, [bp+var_2C]
                assume es:nothing
                push    word ptr es:[bx+31Eh]
                push    0
                push    [bp+var_E]
                push    [bp+var_10]
                les     bx, [bp+var_20]
                push    word ptr es:[bx+0C6h]
                push    word ptr es:[bx+0C4h]
                les     bx, [bp+var_20]
                push    word ptr es:[bx+0CAh]
                push    word ptr es:[bx+0C8h]
                call    _ADD_ITEM_TO_ROOM
                add     sp, 10h

loc_7F350:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+AEB↑j
                mov     ax, word ptr [bp+var_4]
                or      ax, word ptr [bp+var_4+2]
                jnz     short loc_7F35B
                jmp     loc_80725
; ---------------------------------------------------------------------------

loc_7F35B:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+B25↑j
                jmp     loc_8071D
; ---------------------------------------------------------------------------

loc_7F35E:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+A69↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aHideitem ; "hideitem"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jnz     short loc_7F377
                jmp     loc_7F415
; ---------------------------------------------------------------------------

loc_7F377:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+B41↑j
                mov     [bp+var_36], 1
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_7F3A5
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7F3A5:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+B6F↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                push    [bp+var_E]
                push    [bp+var_10]
                call    _GET_ITEM_DATA
                add     sp, 4
                mov     word ptr [bp+var_2C+2], dx
                mov     word ptr [bp+var_2C], ax
                mov     ax, word ptr [bp+var_2C]
                or      ax, word ptr [bp+var_2C+2]
                jz      short loc_7F407
                les     bx, [bp+var_2C]
                push    word ptr es:[bx+31Eh]
                push    1
                push    [bp+var_E]
                push    [bp+var_10]
                les     bx, [bp+var_20]
                push    word ptr es:[bx+0C6h]
                push    word ptr es:[bx+0C4h]
                les     bx, [bp+var_20]
                push    word ptr es:[bx+0CAh]
                push    word ptr es:[bx+0C8h]
                call    _ADD_ITEM_TO_ROOM
                add     sp, 10h

loc_7F407:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+BA2↑j
                mov     ax, word ptr [bp+var_4]
                or      ax, word ptr [bp+var_4+2]
                jnz     short loc_7F412
                jmp     loc_80725
; ---------------------------------------------------------------------------

loc_7F412:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+BDC↑j
                jmp     loc_8071D
; ---------------------------------------------------------------------------

loc_7F415:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+B43↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aCheckability ; "checkability"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jnz     short loc_7F42E
                jmp     loc_7F50C
; ---------------------------------------------------------------------------

loc_7F42E:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+BF8↑j
                mov     [bp+var_36], 1
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_7F45C
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7F45C:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+C26↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                cwd
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                push    0
                push    0
                push    [bp+arg_0]
                push    [bp+var_10]
                call    _USER_HAS_ABILITY
                add     sp, 8
                or      al, al
                jnz     short loc_7F495
                mov     ax, word ptr [bp+var_4]
                or      ax, word ptr [bp+var_4+2]
                jnz     short loc_7F492
                jmp     loc_8028E
; ---------------------------------------------------------------------------

loc_7F492:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+C5C↑j
                jmp     loc_80286
; ---------------------------------------------------------------------------

loc_7F495:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+C54↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_7F4BE
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7F4BE:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+C88↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                cwd
                mov     [bp+var_12], dx
                mov     [bp+var_14], ax
                push    0
                push    0
                push    0
                push    0
                push    0
                push    [bp+arg_0]
                push    [bp+var_10]
                call    _GET_USER_ABILITY_VALUE
                add     sp, 0Eh
                cwd
                cmp     dx, [bp+var_12]
                jle     short loc_7F4F4
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7F4F4:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+CBE↑j
                jl      short loc_7F4FE
                cmp     ax, [bp+var_14]
                jb      short loc_7F4FE
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7F4FE:                              ; CODE XREF: _PERFORM_MATCHED_ACTION:loc_7F4F4↑j
                                        ; _PERFORM_MATCHED_ACTION+CC8↑j
                mov     ax, word ptr [bp+var_4]
                or      ax, word ptr [bp+var_4+2]
                jnz     short loc_7F509
                jmp     loc_8028E
; ---------------------------------------------------------------------------

loc_7F509:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+CD3↑j
                jmp     loc_80286
; ---------------------------------------------------------------------------

loc_7F50C:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+BFA↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aClass_2 ; "class"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jz      short loc_7F582
                mov     [bp+var_36], 1
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_7F550
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7F550:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+D1A↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                les     bx, [bp+var_20]
                mov     ax, es:[bx+92h]
                cmp     ax, [bp+var_10]
                jnz     short loc_7F574
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7F574:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+D3E↑j
                mov     ax, word ptr [bp+var_4]
                or      ax, word ptr [bp+var_4+2]
                jnz     short loc_7F57F
                jmp     loc_8028E
; ---------------------------------------------------------------------------

loc_7F57F:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+D49↑j
                jmp     loc_80286
; ---------------------------------------------------------------------------

loc_7F582:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+CEF↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aRace_1  ; "race"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jz      short loc_7F5F8
                mov     [bp+var_36], 1
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_7F5C6
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7F5C6:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+D90↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                les     bx, [bp+var_20]
                mov     ax, es:[bx+90h]
                cmp     ax, [bp+var_10]
                jnz     short loc_7F5EA
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7F5EA:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+DB4↑j
                mov     ax, word ptr [bp+var_4]
                or      ax, word ptr [bp+var_4+2]
                jnz     short loc_7F5F5
                jmp     loc_8028E
; ---------------------------------------------------------------------------

loc_7F5F5:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+DBF↑j
                jmp     loc_80286
; ---------------------------------------------------------------------------

loc_7F5F8:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+D65↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aGoodaligned ; "goodaligned"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jnz     short loc_7F611
                jmp     loc_7F6CC
; ---------------------------------------------------------------------------

loc_7F611:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+DDB↑j
                mov     [bp+var_36], 1
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_7F63F
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7F63F:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+E09↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                les     bx, [bp+var_20]
                mov     ax, es:[bx+53Ch]
                cwd
                cmp     dx, [bp+var_E]
                jge     short loc_7F664
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7F664:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+E2E↑j
                jg      short loc_7F66E
                cmp     ax, [bp+var_10]
                ja      short loc_7F66E
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7F66E:                              ; CODE XREF: _PERFORM_MATCHED_ACTION:loc_7F664↑j
                                        ; _PERFORM_MATCHED_ACTION+E38↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jz      short loc_7F6BE
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                push    word ptr [bp+var_20+2]
                push    word ptr [bp+var_20]
                push    [bp+arg_0]
                push    [bp+var_E]
                push    [bp+var_10]
                push    cs
                call    near ptr sub_7D630
                add     sp, 0Ah

loc_7F6BE:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+E61↑j
                mov     ax, word ptr [bp+var_4]
                or      ax, word ptr [bp+var_4+2]
                jnz     short loc_7F6C9
                jmp     loc_8028E
; ---------------------------------------------------------------------------

loc_7F6C9:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+E93↑j
                jmp     loc_80286
; ---------------------------------------------------------------------------

loc_7F6CC:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+DDD↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aEvilaligned ; "evilaligned"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jnz     short loc_7F6E5
                jmp     loc_7F7A0
; ---------------------------------------------------------------------------

loc_7F6E5:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+EAF↑j
                mov     [bp+var_36], 1
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_7F713
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7F713:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+EDD↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                les     bx, [bp+var_20]
                mov     ax, es:[bx+53Ch]
                cwd
                cmp     dx, [bp+var_E]
                jle     short loc_7F738
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7F738:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+F02↑j
                jl      short loc_7F742
                cmp     ax, [bp+var_10]
                jb      short loc_7F742
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7F742:                              ; CODE XREF: _PERFORM_MATCHED_ACTION:loc_7F738↑j
                                        ; _PERFORM_MATCHED_ACTION+F0C↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jz      short loc_7F792
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                push    word ptr [bp+var_20+2]
                push    word ptr [bp+var_20]
                push    [bp+arg_0]
                push    [bp+var_E]
                push    [bp+var_10]
                push    cs
                call    near ptr sub_7D630
                add     sp, 0Ah

loc_7F792:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+F35↑j
                mov     ax, word ptr [bp+var_4]
                or      ax, word ptr [bp+var_4+2]
                jnz     short loc_7F79D
                jmp     loc_8028E
; ---------------------------------------------------------------------------

loc_7F79D:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+F67↑j
                jmp     loc_80286
; ---------------------------------------------------------------------------

loc_7F7A0:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+EB1↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aMinlevel ; "minlevel"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jnz     short loc_7F7B9
                jmp     loc_7F875
; ---------------------------------------------------------------------------

loc_7F7B9:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+F83↑j
                mov     [bp+var_36], 1
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_7F7E7
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7F7E7:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+FB1↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                les     bx, [bp+var_20]
                mov     ax, es:[bx+94h]
                xor     dx, dx
                cmp     dx, [bp+var_E]
                jle     short loc_7F80D
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7F80D:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+FD7↑j
                jl      short loc_7F817
                cmp     ax, [bp+var_10]
                jb      short loc_7F817
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7F817:                              ; CODE XREF: _PERFORM_MATCHED_ACTION:loc_7F80D↑j
                                        ; _PERFORM_MATCHED_ACTION+FE1↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jz      short loc_7F867
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                push    word ptr [bp+var_20+2]
                push    word ptr [bp+var_20]
                push    [bp+arg_0]
                push    [bp+var_E]
                push    [bp+var_10]
                push    cs
                call    near ptr sub_7D630
                add     sp, 0Ah

loc_7F867:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+100A↑j
                mov     ax, word ptr [bp+var_4]
                or      ax, word ptr [bp+var_4+2]
                jnz     short loc_7F872
                jmp     loc_8028E
; ---------------------------------------------------------------------------

loc_7F872:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+103C↑j
                jmp     loc_80286
; ---------------------------------------------------------------------------

loc_7F875:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+F85↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aMaxlevel ; "maxlevel"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jnz     short loc_7F88E
                jmp     loc_7F94A
; ---------------------------------------------------------------------------

loc_7F88E:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1058↑j
                mov     [bp+var_36], 1
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_7F8BC
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7F8BC:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1086↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                les     bx, [bp+var_20]
                mov     ax, es:[bx+94h]
                xor     dx, dx
                cmp     dx, [bp+var_E]
                jge     short loc_7F8E2
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7F8E2:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+10AC↑j
                jg      short loc_7F8EC
                cmp     ax, [bp+var_10]
                ja      short loc_7F8EC
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7F8EC:                              ; CODE XREF: _PERFORM_MATCHED_ACTION:loc_7F8E2↑j
                                        ; _PERFORM_MATCHED_ACTION+10B6↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jz      short loc_7F93C
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                push    word ptr [bp+var_20+2]
                push    word ptr [bp+var_20]
                push    [bp+arg_0]
                push    [bp+var_E]
                push    [bp+var_10]
                push    cs
                call    near ptr sub_7D630
                add     sp, 0Ah

loc_7F93C:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+10DF↑j
                mov     ax, word ptr [bp+var_4]
                or      ax, word ptr [bp+var_4+2]
                jnz     short loc_7F947
                jmp     loc_8028E
; ---------------------------------------------------------------------------

loc_7F947:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1111↑j
                jmp     loc_80286
; ---------------------------------------------------------------------------

loc_7F94A:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+105A↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aCheckitem ; "checkitem"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jnz     short loc_7F963
                jmp     loc_7FA78
; ---------------------------------------------------------------------------

loc_7F963:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+112D↑j
                mov     [bp+var_36], 1
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_7F991
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7F991:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+115B↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                mov     [bp+var_33], 0
                mov     [bp+var_2E], 0

loc_7F9AE:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+11A7↓j
                mov     ax, [bp+var_2E]
                shl     ax, 2
                les     bx, [bp+var_20]
                add     bx, ax
                mov     dx, es:[bx+0D8h]
                mov     ax, es:[bx+0D6h]
                cmp     dx, [bp+var_E]
                jnz     short loc_7F9D1
                cmp     ax, [bp+var_10]
                jnz     short loc_7F9D1
                mov     [bp+var_33], 1

loc_7F9D1:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1195↑j
                                        ; _PERFORM_MATCHED_ACTION+119A↑j
                inc     [bp+var_2E]
                cmp     [bp+var_2E], 64h ; 'd'
                jl      short loc_7F9AE
                cmp     [bp+var_33], 0
                jnz     short loc_7FA11
                mov     [bp+var_2E], 0

loc_7F9E5:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+11DE↓j
                mov     ax, [bp+var_2E]
                shl     ax, 2
                les     bx, [bp+var_20]
                add     bx, ax
                mov     dx, es:[bx+331h]
                mov     ax, es:[bx+32Fh]
                cmp     dx, [bp+var_E]
                jnz     short loc_7FA08
                cmp     ax, [bp+var_10]
                jnz     short loc_7FA08
                mov     [bp+var_33], 1

loc_7FA08:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+11CC↑j
                                        ; _PERFORM_MATCHED_ACTION+11D1↑j
                inc     [bp+var_2E]
                cmp     [bp+var_2E], 32h ; '2'
                jl      short loc_7F9E5

loc_7FA11:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+11AD↑j
                cmp     [bp+var_33], 0
                jz      short loc_7FA1A
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7FA1A:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+11E4↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jz      short loc_7FA6A
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                push    word ptr [bp+var_20+2]
                push    word ptr [bp+var_20]
                push    [bp+arg_0]
                push    [bp+var_E]
                push    [bp+var_10]
                push    cs
                call    near ptr sub_7D630
                add     sp, 0Ah

loc_7FA6A:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+120D↑j
                mov     ax, word ptr [bp+var_4]
                or      ax, word ptr [bp+var_4+2]
                jnz     short loc_7FA75
                jmp     loc_8028E
; ---------------------------------------------------------------------------

loc_7FA75:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+123F↑j
                jmp     loc_80286
; ---------------------------------------------------------------------------

loc_7FA78:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+112F↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aFailitem ; "failitem"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jnz     short loc_7FA91
                jmp     loc_7FBA6
; ---------------------------------------------------------------------------

loc_7FA91:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+125B↑j
                mov     [bp+var_36], 1
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_7FABF
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7FABF:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1289↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                mov     [bp+var_33], 0
                mov     [bp+var_2E], 0

loc_7FADC:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+12D5↓j
                mov     ax, [bp+var_2E]
                shl     ax, 2
                les     bx, [bp+var_20]
                add     bx, ax
                mov     dx, es:[bx+0D8h]
                mov     ax, es:[bx+0D6h]
                cmp     dx, [bp+var_E]
                jnz     short loc_7FAFF
                cmp     ax, [bp+var_10]
                jnz     short loc_7FAFF
                mov     [bp+var_33], 1

loc_7FAFF:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+12C3↑j
                                        ; _PERFORM_MATCHED_ACTION+12C8↑j
                inc     [bp+var_2E]
                cmp     [bp+var_2E], 64h ; 'd'
                jl      short loc_7FADC
                cmp     [bp+var_33], 0
                jnz     short loc_7FB3F
                mov     [bp+var_2E], 0

loc_7FB13:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+130C↓j
                mov     ax, [bp+var_2E]
                shl     ax, 2
                les     bx, [bp+var_20]
                add     bx, ax
                mov     dx, es:[bx+331h]
                mov     ax, es:[bx+32Fh]
                cmp     dx, [bp+var_E]
                jnz     short loc_7FB36
                cmp     ax, [bp+var_10]
                jnz     short loc_7FB36
                mov     [bp+var_33], 1

loc_7FB36:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+12FA↑j
                                        ; _PERFORM_MATCHED_ACTION+12FF↑j
                inc     [bp+var_2E]
                cmp     [bp+var_2E], 32h ; '2'
                jl      short loc_7FB13

loc_7FB3F:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+12DB↑j
                cmp     [bp+var_33], 0
                jnz     short loc_7FB48
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7FB48:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1312↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jz      short loc_7FB98
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                push    word ptr [bp+var_20+2]
                push    word ptr [bp+var_20]
                push    [bp+arg_0]
                push    [bp+var_E]
                push    [bp+var_10]
                push    cs
                call    near ptr sub_7D630
                add     sp, 0Ah

loc_7FB98:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+133B↑j
                mov     ax, word ptr [bp+var_4]
                or      ax, word ptr [bp+var_4+2]
                jnz     short loc_7FBA3
                jmp     loc_8028E
; ---------------------------------------------------------------------------

loc_7FBA3:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+136D↑j
                jmp     loc_80286
; ---------------------------------------------------------------------------

loc_7FBA6:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+125D↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aText_0  ; "text"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jz      short loc_7FC13
                mov     [bp+var_36], 1
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_7FBEA
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7FBEA:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+13B4↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                push    0
                push    0
                push    [bp+var_E]
                push    [bp+var_10]
                call    _DISPLAY_LONG_TEXT

loc_7FC0D:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+A50↑j
                add     sp, 8
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7FC13:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1389↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aRoomtext ; "roomtext"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jz      short loc_7FC9A
                mov     [bp+var_36], 1
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_7FC57
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7FC57:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1421↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                push    0
                push    0
                push    [bp+var_E]
                push    [bp+var_10]
                les     bx, [bp+var_20]
                push    word ptr es:[bx+0C6h]
                push    word ptr es:[bx+0C4h]
                les     bx, [bp+var_20]
                push    word ptr es:[bx+0CAh]
                push    word ptr es:[bx+0C8h]
                call    _DISPLAY_LONG_TEXT_TO_ROOM
                add     sp, 10h
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7FC9A:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+13F6↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aRoomitem ; "roomitem"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jnz     short loc_7FCB3
                jmp     loc_7FDF8
; ---------------------------------------------------------------------------

loc_7FCB3:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+147D↑j
                mov     [bp+var_36], 1
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_7FCE1
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7FCE1:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+14AB↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                mov     [bp+var_33], 0
                les     bx, [bp+var_20]
                push    word ptr es:[bx+0C6h]
                push    word ptr es:[bx+0C4h]
                les     bx, [bp+var_20]
                push    word ptr es:[bx+0CAh]
                push    word ptr es:[bx+0C8h]
                call    _GET_ROOM_DATA
                add     sp, 8
                mov     word ptr [bp+var_24+2], dx
                mov     word ptr [bp+var_24], ax
                mov     ax, word ptr [bp+var_24]
                or      ax, word ptr [bp+var_24+2]
                jz      short loc_7FD91
                mov     [bp+var_2E], 0

loc_7FD2E:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1527↓j
                mov     ax, [bp+var_2E]
                shl     ax, 2
                les     bx, [bp+var_24]
                add     bx, ax
                mov     dx, es:[bx+46Eh]
                mov     ax, es:[bx+46Ch]
                cmp     dx, [bp+var_E]
                jnz     short loc_7FD51
                cmp     ax, [bp+var_10]
                jnz     short loc_7FD51
                mov     [bp+var_33], 1

loc_7FD51:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1515↑j
                                        ; _PERFORM_MATCHED_ACTION+151A↑j
                inc     [bp+var_2E]
                cmp     [bp+var_2E], 11h
                jl      short loc_7FD2E
                cmp     [bp+var_33], 0
                jnz     short loc_7FD91
                mov     [bp+var_2E], 0

loc_7FD65:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+155E↓j
                mov     ax, [bp+var_2E]
                shl     ax, 2
                les     bx, [bp+var_24]
                add     bx, ax
                mov     dx, es:[bx+4D4h]
                mov     ax, es:[bx+4D2h]
                cmp     dx, [bp+var_E]
                jnz     short loc_7FD88
                cmp     ax, [bp+var_10]
                jnz     short loc_7FD88
                mov     [bp+var_33], 1

loc_7FD88:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+154C↑j
                                        ; _PERFORM_MATCHED_ACTION+1551↑j
                inc     [bp+var_2E]
                cmp     [bp+var_2E], 0Fh
                jl      short loc_7FD65

loc_7FD91:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+14F6↑j
                                        ; _PERFORM_MATCHED_ACTION+152D↑j
                cmp     [bp+var_33], 0
                jz      short loc_7FD9A
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7FD9A:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1564↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jz      short loc_7FDEA
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                push    word ptr [bp+var_20+2]
                push    word ptr [bp+var_20]
                push    [bp+arg_0]
                push    [bp+var_E]
                push    [bp+var_10]
                push    cs
                call    near ptr sub_7D630
                add     sp, 0Ah

loc_7FDEA:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+158D↑j
                mov     ax, word ptr [bp+var_4]
                or      ax, word ptr [bp+var_4+2]
                jnz     short loc_7FDF5
                jmp     loc_8028E
; ---------------------------------------------------------------------------

loc_7FDF5:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+15BF↑j
                jmp     loc_80286
; ---------------------------------------------------------------------------

loc_7FDF8:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+147F↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aFailroomitem ; "failroomitem"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jnz     short loc_7FE11
                jmp     loc_7FF56
; ---------------------------------------------------------------------------

loc_7FE11:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+15DB↑j
                mov     [bp+var_36], 1
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_7FE3F
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7FE3F:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1609↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                mov     [bp+var_33], 0
                les     bx, [bp+var_20]
                push    word ptr es:[bx+0C6h]
                push    word ptr es:[bx+0C4h]
                les     bx, [bp+var_20]
                push    word ptr es:[bx+0CAh]
                push    word ptr es:[bx+0C8h]
                call    _GET_ROOM_DATA
                add     sp, 8
                mov     word ptr [bp+var_24+2], dx
                mov     word ptr [bp+var_24], ax
                mov     ax, word ptr [bp+var_24]
                or      ax, word ptr [bp+var_24+2]
                jz      short loc_7FEEF
                mov     [bp+var_2E], 0

loc_7FE8C:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1685↓j
                mov     ax, [bp+var_2E]
                shl     ax, 2
                les     bx, [bp+var_24]
                add     bx, ax
                mov     dx, es:[bx+46Eh]
                mov     ax, es:[bx+46Ch]
                cmp     dx, [bp+var_E]
                jnz     short loc_7FEAF
                cmp     ax, [bp+var_10]
                jnz     short loc_7FEAF
                mov     [bp+var_33], 1

loc_7FEAF:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1673↑j
                                        ; _PERFORM_MATCHED_ACTION+1678↑j
                inc     [bp+var_2E]
                cmp     [bp+var_2E], 11h
                jl      short loc_7FE8C
                cmp     [bp+var_33], 0
                jnz     short loc_7FEEF
                mov     [bp+var_2E], 0

loc_7FEC3:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+16BC↓j
                mov     ax, [bp+var_2E]
                shl     ax, 2
                les     bx, [bp+var_24]
                add     bx, ax
                mov     dx, es:[bx+4D4h]
                mov     ax, es:[bx+4D2h]
                cmp     dx, [bp+var_E]
                jnz     short loc_7FEE6
                cmp     ax, [bp+var_10]
                jnz     short loc_7FEE6
                mov     [bp+var_33], 1

loc_7FEE6:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+16AA↑j
                                        ; _PERFORM_MATCHED_ACTION+16AF↑j
                inc     [bp+var_2E]
                cmp     [bp+var_2E], 0Fh
                jl      short loc_7FEC3

loc_7FEEF:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1654↑j
                                        ; _PERFORM_MATCHED_ACTION+168B↑j
                cmp     [bp+var_33], 0
                jnz     short loc_7FEF8
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7FEF8:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+16C2↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jz      short loc_7FF48
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                push    word ptr [bp+var_20+2]
                push    word ptr [bp+var_20]
                push    [bp+arg_0]
                push    [bp+var_E]
                push    [bp+var_10]
                push    cs
                call    near ptr sub_7D630
                add     sp, 0Ah

loc_7FF48:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+16EB↑j
                mov     ax, word ptr [bp+var_4]
                or      ax, word ptr [bp+var_4+2]
                jnz     short loc_7FF53
                jmp     loc_8028E
; ---------------------------------------------------------------------------

loc_7FF53:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+171D↑j
                jmp     loc_80286
; ---------------------------------------------------------------------------

loc_7FF56:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+15DD↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aClearitem ; "clearitem"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jnz     short loc_7FF6F
                jmp     loc_80043
; ---------------------------------------------------------------------------

loc_7FF6F:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1739↑j
                mov     [bp+var_36], 1
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_7FF9D
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7FF9D:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1767↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                push    [bp+var_E]
                push    [bp+var_10]
                les     bx, [bp+var_20]
                push    word ptr es:[bx+0C6h]
                push    word ptr es:[bx+0C4h]
                les     bx, [bp+var_20]
                push    word ptr es:[bx+0CAh]
                push    word ptr es:[bx+0C8h]
                call    sub_7A544
                add     sp, 0Ch
                mov     [bp+var_33], al
                cmp     [bp+var_33], 0
                jz      short loc_7FFE5
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_7FFE5:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+17AF↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jz      short loc_80035
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                push    word ptr [bp+var_20+2]
                push    word ptr [bp+var_20]
                push    [bp+arg_0]
                push    [bp+var_E]
                push    [bp+var_10]
                push    cs
                call    near ptr sub_7D630
                add     sp, 0Ah

loc_80035:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+17D8↑j
                mov     ax, word ptr [bp+var_4]
                or      ax, word ptr [bp+var_4+2]
                jnz     short loc_80040
                jmp     loc_8028E
; ---------------------------------------------------------------------------

loc_80040:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+180A↑j
                jmp     loc_80286
; ---------------------------------------------------------------------------

loc_80043:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+173B↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aAddevil ; "addevil"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jz      short loc_800AA
                mov     [bp+var_36], 1
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_80087
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_80087:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1851↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                cwd
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                les     bx, [bp+var_20]
                mov     ax, [bp+var_10]
                add     es:[bx+53Ch], ax
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_800AA:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1826↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aTestability ; "testability"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jnz     short loc_800C3
                jmp     loc_801B5
; ---------------------------------------------------------------------------

loc_800C3:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+188D↑j
                mov     [bp+var_36], 1
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_800F1
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_800F1:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+18BB↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                cwd
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_8012F
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_8012F:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+18F9↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                cwd
                mov     [bp+var_12], dx
                mov     [bp+var_14], ax
                push    0
                push    0
                push    [bp+arg_0]
                push    [bp+var_10]
                call    _USER_HAS_ABILITY
                add     sp, 8
                or      al, al
                jnz     short loc_8017C
                cmp     [bp+var_12], 0
                jge     short loc_80163
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_80163:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+192D↑j
                jnz     short loc_8016E
                cmp     [bp+var_14], 0
                jnb     short loc_8016E
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_8016E:                              ; CODE XREF: _PERFORM_MATCHED_ACTION:loc_80163↑j
                                        ; _PERFORM_MATCHED_ACTION+1938↑j
                mov     ax, word ptr [bp+var_4]
                or      ax, word ptr [bp+var_4+2]
                jnz     short loc_80179
                jmp     loc_8028E
; ---------------------------------------------------------------------------

loc_80179:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1943↑j
                jmp     loc_80286
; ---------------------------------------------------------------------------

loc_8017C:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1927↑j
                push    0
                push    0
                push    0
                push    0
                push    0
                push    [bp+arg_0]
                push    [bp+var_10]
                call    _GET_USER_ABILITY_VALUE
                add     sp, 0Eh
                cwd
                cmp     dx, [bp+var_12]
                jge     short loc_8019D
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_8019D:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1967↑j
                jg      short loc_801A7
                cmp     ax, [bp+var_14]
                ja      short loc_801A7
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_801A7:                              ; CODE XREF: _PERFORM_MATCHED_ACTION:loc_8019D↑j
                                        ; _PERFORM_MATCHED_ACTION+1971↑j
                mov     ax, word ptr [bp+var_4]
                or      ax, word ptr [bp+var_4+2]
                jnz     short loc_801B2
                jmp     loc_8028E
; ---------------------------------------------------------------------------

loc_801B2:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+197C↑j
                jmp     loc_80286
; ---------------------------------------------------------------------------

loc_801B5:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+188F↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aFailability ; "failability"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jnz     short loc_801CE
                jmp     loc_802A0
; ---------------------------------------------------------------------------

loc_801CE:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1998↑j
                mov     [bp+var_36], 1
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_801FC
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_801FC:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+19C6↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                cwd
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_8023A
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_8023A:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1A04↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                cwd
                mov     [bp+var_12], dx
                mov     [bp+var_14], ax
                push    0
                push    0
                push    [bp+arg_0]
                push    [bp+var_10]
                call    _USER_HAS_ABILITY
                add     sp, 8
                or      al, al
                jnz     short loc_80268
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_80268:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1A32↑j
                push    word ptr [bp+var_20+2]
                push    word ptr [bp+var_20]
                push    [bp+arg_0]
                push    [bp+var_12]
                push    [bp+var_14]
                push    cs
                call    near ptr sub_7D630
                add     sp, 0Ah
                mov     ax, word ptr [bp+var_4]
                or      ax, word ptr [bp+var_4+2]
                jz      short loc_8028E

loc_80286:                              ; CODE XREF: _PERFORM_MATCHED_ACTION:loc_7EB9D↑j
                                        ; _PERFORM_MATCHED_ACTION:loc_7EC63↑j ...
                les     bx, [bp+var_4]
                mov     byte ptr es:[bx-1], 3Ah ; ':'

loc_8028E:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+369↑j
                                        ; _PERFORM_MATCHED_ACTION+42F↑j ...
                mov     word ptr [bp+var_4+2], 0
                mov     word ptr [bp+var_4], 0
                mov     [bp+var_36], 2
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_802A0:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+199A↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aRemoteaction ; "remoteaction"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jnz     short loc_802B9
                jmp     loc_80424
; ---------------------------------------------------------------------------

loc_802B9:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1A83↑j
                mov     [bp+var_36], 1
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_802E7
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_802E7:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1AB1↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_80324
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_80324:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1AEE↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_12], dx
                mov     [bp+var_14], ax
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_80361
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_80361:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1B2B↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_16], dx
                mov     [bp+var_18], ax
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_8039E
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_8039E:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1B68↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                cwd
                mov     [bp+var_1A], dx
                mov     [bp+var_1C], ax
                cmp     [bp+var_1A], 0
                jge     short loc_803BC
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_803BC:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1B86↑j
                jnz     short loc_803C7
                cmp     [bp+var_1C], 0
                jnb     short loc_803C7
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_803C7:                              ; CODE XREF: _PERFORM_MATCHED_ACTION:loc_803BC↑j
                                        ; _PERFORM_MATCHED_ACTION+1B91↑j
                cmp     [bp+var_1A], 0
                jle     short loc_803D0
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_803D0:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1B9A↑j
                jnz     short loc_803DB
                cmp     [bp+var_1C], 0Ah
                jbe     short loc_803DB
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_803DB:                              ; CODE XREF: _PERFORM_MATCHED_ACTION:loc_803D0↑j
                                        ; _PERFORM_MATCHED_ACTION+1BA5↑j
                les     bx, [bp+var_20]
                push    word ptr es:[bx+0C6h]
                push    word ptr es:[bx+0C4h]
                push    [bp+var_E]
                push    [bp+var_10]
                call    _GET_ROOM_DATA
                add     sp, 8
                mov     word ptr [bp+var_24+2], dx
                mov     word ptr [bp+var_24], ax
                mov     ax, word ptr [bp+var_24]
                or      ax, word ptr [bp+var_24+2]
                jnz     short loc_80407
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_80407:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1BD1↑j
                push    [bp+var_1C]
                push    [bp+var_18]
                push    [bp+var_12]
                push    [bp+var_14]
                push    word ptr [bp+var_24+2]
                push    word ptr [bp+var_24]
                call    sub_7A9EB
                add     sp, 0Ch
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_80424:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1A85↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aRandom  ; "random"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jz      short loc_804A9
                mov     [bp+var_36], 1
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_80468
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_80468:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1C32↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                push    [bp+var_E]
                push    [bp+var_10]
                push    [bp+arg_0]
                nop
                push    cs
                call    near ptr sub_80A66
                add     sp, 6
                mov     ah, 0
                mov     [bp+var_36], ax
                cmp     [bp+var_36], 2
                jz      short loc_8049B
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_8049B:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1C65↑j
                mov     ax, word ptr [bp+var_4]
                or      ax, word ptr [bp+var_4+2]
                jnz     short loc_804A6
                jmp     loc_80725
; ---------------------------------------------------------------------------

loc_804A6:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1C70↑j
                jmp     loc_8071D
; ---------------------------------------------------------------------------

loc_804A9:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1C07↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aAdddelay ; "adddelay"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jz      short loc_8050E
                mov     [bp+var_36], 1
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_804ED
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_804ED:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1CB7↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                les     bx, [bp+var_20]
                add     al, es:[bx+6ACh]
                les     bx, [bp+var_20]
                mov     es:[bx+6ACh], al
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_8050E:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1C8C↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aTestskill ; "testskill"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jz      short loc_80528
                push    0
                jmp     short loc_80540
; ---------------------------------------------------------------------------

loc_80528:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1CF1↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aCheckskill ; "checkskill"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jz      short loc_8055E
                push    1

loc_80540:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1CF5↑j
                push    ss
                lea     ax, [bp+var_4]
                push    ax
                push    [bp+var_6]
                push    [bp+var_8]
                push    word ptr [bp+var_20+2]
                push    word ptr [bp+var_20]
                push    [bp+arg_0]
                push    cs
                call    near ptr sub_7D949
                add     sp, 10h
                jmp     loc_807C3
; ---------------------------------------------------------------------------

loc_8055E:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1D0B↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aGivecoins ; "givecoins"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jnz     short loc_80577
                jmp     loc_806DF
; ---------------------------------------------------------------------------

loc_80577:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1D41↑j
                mov     [bp+var_36], 1
                push    [bp+var_6]
                push    [bp+var_8]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jnz     short loc_805A5
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_805A5:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1D6F↑j
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    ATOL            ; long int atol(const char *str);
                add     sp, 4
                mov     [bp+var_E], dx
                mov     [bp+var_10], ax
                push    word ptr [bp+var_C+2]
                push    word ptr [bp+var_C]
                call    SKPWRD          ; char *skpwrd(char *cp);
                add     sp, 4
                push    dx
                push    ax
                call    SKPWHT          ; char *skpwht(char *string);
                add     sp, 4
                mov     word ptr [bp+var_C+2], dx
                mov     word ptr [bp+var_C], ax
                mov     [bp+var_12], 0
                mov     [bp+var_14], 4
                mov     ax, word ptr [bp+var_C]
                or      ax, word ptr [bp+var_C+2]
                jz      short loc_8064E
                les     bx, [bp+var_C]
                mov     al, es:[bx]
                mov     ah, 0
                push    ax
                call    TOUPPER         ; int toupper (int c);
                pop     cx
                mov     [bp+var_38], ax
                mov     cx, 5           ; switch 5 cases
                mov     bx, offset word_80810

loc_80601:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1DDB↓j
                mov     ax, cs:[bx]
                cmp     ax, [bp+var_38]
                jz      short loc_80610
                add     bx, 2
                loop    loc_80601
                jmp     short def_80610 ; jumptable 00080610 default case, case 67
; ---------------------------------------------------------------------------

loc_80610:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1DD6↑j
                jmp     word ptr cs:[bx+0Ah] ; switch jump
; ---------------------------------------------------------------------------

loc_80614:                              ; CODE XREF: _PERFORM_MATCHED_ACTION:loc_80610↑j
                                        ; DATA XREF: cseg27:jpt_80610↓o
                mov     [bp+var_12], 0  ; jumptable 00080610 case 82
                mov     [bp+var_14], 0
                jmp     short loc_8064E
; ---------------------------------------------------------------------------

loc_80620:                              ; CODE XREF: _PERFORM_MATCHED_ACTION:loc_80610↑j
                                        ; DATA XREF: cseg27:jpt_80610↓o
                mov     [bp+var_12], 0  ; jumptable 00080610 case 80
                mov     [bp+var_14], 1
                jmp     short loc_8064E
; ---------------------------------------------------------------------------

loc_8062C:                              ; CODE XREF: _PERFORM_MATCHED_ACTION:loc_80610↑j
                                        ; DATA XREF: cseg27:jpt_80610↓o
                mov     [bp+var_12], 0  ; jumptable 00080610 case 71
                mov     [bp+var_14], 2
                jmp     short loc_8064E
; ---------------------------------------------------------------------------

loc_80638:                              ; CODE XREF: _PERFORM_MATCHED_ACTION:loc_80610↑j
                                        ; DATA XREF: cseg27:jpt_80610↓o
                mov     [bp+var_12], 0  ; jumptable 00080610 case 83
                mov     [bp+var_14], 3
                jmp     short loc_8064E
; ---------------------------------------------------------------------------

def_80610:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1DDD↑j
                                        ; _PERFORM_MATCHED_ACTION:loc_80610↑j
                                        ; DATA XREF: ...
                mov     [bp+var_12], 0  ; jumptable 00080610 default case, case 67
                mov     [bp+var_14], 4

loc_8064E:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1DB6↑j
                                        ; _PERFORM_MATCHED_ACTION+1DED↑j ...
                mov     cx, 5           ; switch 5 cases
                mov     bx, offset word_807F2

loc_80654:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1E37↓j
                mov     ax, cs:[bx]
                cmp     ax, [bp+var_14]
                jnz     short loc_80665
                mov     ax, cs:[bx+0Ah]
                cmp     ax, [bp+var_12]
                jz      short loc_8066D

loc_80665:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1E29↑j
                add     bx, 2
                loop    loc_80654
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_8066D:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1E32↑j
                jmp     word ptr cs:[bx+14h] ; switch jump
; ---------------------------------------------------------------------------

loc_80671:                              ; CODE XREF: _PERFORM_MATCHED_ACTION:loc_8066D↑j
                                        ; DATA XREF: cseg27:jpt_8066D↓o
                les     bx, [bp+var_20] ; jumptable 0008066D case 0
                mov     dx, [bp+var_E]
                mov     ax, [bp+var_10]
                add     es:[bx+603h], ax
                adc     es:[bx+605h], dx
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_80687:                              ; CODE XREF: _PERFORM_MATCHED_ACTION:loc_8066D↑j
                                        ; DATA XREF: cseg27:jpt_8066D↓o
                les     bx, [bp+var_20] ; jumptable 0008066D case 1
                mov     dx, [bp+var_E]
                mov     ax, [bp+var_10]
                add     es:[bx+607h], ax
                adc     es:[bx+609h], dx
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_8069D:                              ; CODE XREF: _PERFORM_MATCHED_ACTION:loc_8066D↑j
                                        ; DATA XREF: cseg27:jpt_8066D↓o
                les     bx, [bp+var_20] ; jumptable 0008066D case 2
                mov     dx, [bp+var_E]
                mov     ax, [bp+var_10]
                add     es:[bx+60Bh], ax
                adc     es:[bx+60Dh], dx
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_806B3:                              ; CODE XREF: _PERFORM_MATCHED_ACTION:loc_8066D↑j
                                        ; DATA XREF: cseg27:jpt_8066D↓o
                les     bx, [bp+var_20] ; jumptable 0008066D case 3
                mov     dx, [bp+var_E]
                mov     ax, [bp+var_10]
                add     es:[bx+60Fh], ax
                adc     es:[bx+611h], dx
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_806C9:                              ; CODE XREF: _PERFORM_MATCHED_ACTION:loc_8066D↑j
                                        ; DATA XREF: cseg27:jpt_8066D↓o
                les     bx, [bp+var_20] ; jumptable 0008066D case 4
                mov     dx, [bp+var_E]
                mov     ax, [bp+var_10]
                add     es:[bx+613h], ax
                adc     es:[bx+615h], dx
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_806DF:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1D43↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aTestTournament ; "test_tournament"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jz      short loc_80732
                mov     [bp+var_36], 2
                mov     ax, seg dseg76
                mov     es, ax
                assume es:dseg76
                cmp     es:byte_AC8B9, 2
                jnz     short loc_8070C
                mov     [bp+var_36], 1

loc_8070C:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1ED4↑j
                cmp     [bp+var_36], 2
                jz      short loc_80715
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_80715:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1EDF↑j
                mov     ax, word ptr [bp+var_4]
                or      ax, word ptr [bp+var_4+2]
                jz      short loc_80725

loc_8071D:                              ; CODE XREF: _PERFORM_MATCHED_ACTION:loc_7F35B↑j
                                        ; _PERFORM_MATCHED_ACTION:loc_7F412↑j ...
                les     bx, [bp+var_4]
                assume es:nothing
                mov     byte ptr es:[bx-1], 3Ah ; ':'

loc_80725:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+B27↑j
                                        ; _PERFORM_MATCHED_ACTION+BDE↑j ...
                mov     word ptr [bp+var_4+2], 0
                mov     word ptr [bp+var_4], 0
                jmp     def_8066D       ; jumptable 0008066D default case
; ---------------------------------------------------------------------------

loc_80732:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1EC2↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aCheckspell ; "checkspell"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jz      short loc_80762
                push    ss
                lea     ax, [bp+var_4]
                push    ax
                push    [bp+var_6]
                push    [bp+var_8]
                push    word ptr [bp+var_20+2]
                push    word ptr [bp+var_20]
                push    [bp+arg_0]
                push    cs
                call    near ptr sub_7D83A
                jmp     short loc_807C0
; ---------------------------------------------------------------------------

loc_80762:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1F15↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aFlag    ; "flag"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jz      short loc_80792
                push    ss
                lea     ax, [bp+var_4]
                push    ax
                push    [bp+var_6]
                push    [bp+var_8]
                push    word ptr [bp+var_20+2]
                push    word ptr [bp+var_20]
                push    [bp+arg_0]
                push    cs
                call    near ptr sub_7E22F
                jmp     short loc_807C0
; ---------------------------------------------------------------------------

loc_80792:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1F45↑j
                push    [bp+var_6]
                push    [bp+var_8]
                push    ds
                push    offset aLearnspell ; "learnspell"
                call    SAMETO          ; int match=sameto(char *shorts, char *longs);
                add     sp, 8
                or      ax, ax
                jz      short def_8066D ; jumptable 0008066D default case
                push    ss
                lea     ax, [bp+var_4]
                push    ax
                push    [bp+var_6]
                push    [bp+var_8]
                push    word ptr [bp+var_20+2]
                push    word ptr [bp+var_20]
                push    [bp+arg_0]
                push    cs
                call    near ptr sub_7E528

loc_807C0:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+463↑j
                                        ; _PERFORM_MATCHED_ACTION+760↑j ...
                add     sp, 0Eh

loc_807C3:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1D2A↑j
                mov     [bp+var_36], ax

def_8066D:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+CF↑j
                                        ; _PERFORM_MATCHED_ACTION+126↑j ...
                mov     ax, word ptr [bp+var_4] ; jumptable 0008066D default case
                or      ax, word ptr [bp+var_4+2]
                jz      short loc_807D6
                les     bx, [bp+var_4]
                mov     byte ptr es:[bx-1], 3Ah ; ':'

loc_807D6:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+5E↑j
                                        ; _PERFORM_MATCHED_ACTION+1F9B↑j
                mov     ax, word ptr [bp+var_4]
                or      ax, word ptr [bp+var_4+2]
                jz      short loc_807EA
                les     bx, [bp+var_4]
                cmp     byte ptr es:[bx], 0
                jz      short loc_807EA
                jmp     loc_7E892
; ---------------------------------------------------------------------------

loc_807EA:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+1FAB↑j
                                        ; _PERFORM_MATCHED_ACTION+1FB4↑j
                mov     al, byte ptr [bp+var_36]

loc_807ED:                              ; CODE XREF: _PERFORM_MATCHED_ACTION+36↑j
                pop     ds
                assume ds:dseg81
                pop     di
                pop     si
                leave
                retf
_PERFORM_MATCHED_ACTION endp

; ---------------------------------------------------------------------------
word_807F2      dw      0,     1,     2,     3
                                        ; DATA XREF: _PERFORM_MATCHED_ACTION+1E20↑o
                dw      4               ; low halves of switch values
                dw      0,     0,     0,     0 ; high halves of switch values
                dw      0
jpt_8066D       dw offset loc_80671     ; jump table for switch statement
                dw offset loc_80687
                dw offset loc_8069D
                dw offset loc_806B3
                dw offset loc_806C9
word_80810      dw    43h,   47h,   50h,   52h
                                        ; DATA XREF: _PERFORM_MATCHED_ACTION+1DCD↑o
                dw    53h               ; value table for switch statement
jpt_80610       dw offset def_80610     ; jump table for switch statement
                dw offset loc_8062C
                dw offset loc_80620
                dw offset loc_80614
                dw offset loc_80638
;
; External Entry #551 into the Module
; Attributes (0003): Fixed Exported Shared dataseg
;

; =============== S U B R O U T I N E =======================================

; Attributes: bp-based frame

                public _PERFORM_SPECIAL_COMMAND
