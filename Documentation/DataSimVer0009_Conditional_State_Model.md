# DataSimVer0009 Conditional State Model

MajorMUD should be treated as a Worldgroup-era data interpreter. Because `/Data/quests.json` currently contains no quest records, this pass does not invent quest data. Instead it catalogs condition clues from rooms, items, monsters/NPCs, and spells.

Classifications are expected-model evidence only:
- quest_or_flag_clue
- key_lock_door_clue
- level_rank_clue
- identity_membership_clue
- phrase_command_clue
- transport_clue
- typed_exit_present
- command_verb_clue

Use these files as a comparison map once original module/MBBSEmu reverse-engineering identifies the actual parser, flag storage, and evaluation order.
