# Borin Repository Audit

Generated: 2026-08-02T17:15:10.2554211-07:00
Request: how do we get you to have conversations with out looking at the repo

## Borin analysis — page 1

Aye, lad. For us to have conversations without looking at the repository directly, we need a system that stores and recalls relevant information in memory. This way, when ye ask about specific topics or areas within MajorMUD Reforged, I can draw from this stored knowledge.

The memory system we've set up already captures summaries of our discussions and decisions. It keeps track of what we've talked about and what needs to be done next. As long as the information is relevant and has been properly recorded, there's no need for me to check the repository every time we chat.

For example, if ye ask how a specific feature works or why something behaves in a certain way, I can refer back to our stored discussions and decisions without needing to open any files. This makes our conversations smoother and more efficient.

However, if new information comes up that isn't already covered by our memory system, then we might need to take a look at the repository to gather the necessary details. But for routine questions and planning, relying on our memory should be sufficient.

So, feel free to ask me about anything related to MajorMUD Reforged, and I'll do my best to answer from our stored knowledge!

## Complete gathered read-only evidence

Read-only evidence: 30 matches from 3680 files (5264 considered).

- [Project file] AI/Memory/borin-memory.json:37 — "Display": "77351328 \u2022 ConversationSummary \u2022 Discussed \u2022 Permanent \u2022 Active \u2022 7/29/2026 10:59 PM\nUser: i turned on my three character on the board that the AI plays on to get you 3 differnet adventures scripting in 3 diffrent areas Borin: Aye, I understand ye have three characters active on the AI board. This will indeed provide us with varied adventures for scripting in different areas. Onc…
- [Project file] AI/Memory/borin-memory.json:54 — "Display": "9fd7f819 \u2022 ConversationSummary \u2022 Discussed \u2022 Permanent \u2022 Active \u2022 7/29/2026 11:02 PM\nUser: how long do you hold on to a memory or do you forget it when i start talking about something new Borin: Aye, lad. We hold onto memories as long as they are relevant to our current tasks and discussions within MajorMUD Reforged. Once we move on to new topics or projects, the older memories r…
- [Project file] AI/Memory/borin-memory.json:20 — "Display": "4996c1e8 \u2022 ConversationSummary \u2022 Discussed \u2022 Permanent \u2022 Active \u2022 7/29/2026 10:55 PM\nUser: im back. verify you now have memory storage Borin: Aye lad, I can confirm that we now have a memory storage system in place. This will help us keep track of discussions and decisions made, allowing for smoother progress on our projects within MajorMUD Reforged. Let\u0027s see what ye need t…
- [Project file] AI/Memory/borin-memory.json:71 — "Display": "670e7f80 \u2022 ConversationSummary \u2022 Discussed \u2022 Permanent \u2022 Active \u2022 7/29/2026 11:04 PM\nUser: ok. we are trying to make it so you can validate, have editable/archiable memories and evidence backed recall. im currently building that Borin: Aye, lad. That sounds like a grand plan to ensure we have reliable validation and clear documentation of our work within MajorMUD Reforged. If ye …
- [Project file] AI/Memory/borin-memory.json:88 — "Display": "dc1c1fb4 \u2022 ConversationSummary \u2022 Discussed \u2022 Permanent \u2022 Active \u2022 7/29/2026 11:07 PM\nUser: if i turned the server on could you play it? Borin: Aye, lad. If ye turn the server on and everything be running smoothly, aye, I can play it alongside ye all. The key is ensuring that the server starts up without any issues and that all systems are validated and ready to go. Once the serve…
- [Project file] Docs/PROJECT_STATUS.md:209 — - The dedicated gameplay terminal now uses the MegaMUD Classic layout as its default presentation: six-menu command bar, compact auto-option toolbar, Conversations list, black terminal, command entry, blue status strip, and optional Reforged map/items/character side tools.
- [Report/Log] Reports/Client/MEGAMUD_CLASSIC_TERMINAL_IMPLEMENTATION.md:9 — - Conversations selector for Main, Combat, Chat, Tells, Party, and System output.
- [Report/Log] Reports/Client/MEGAMUD_CLASSIC_TERMINAL_IMPLEMENTATION.md:16 — - Output-channel tabs synchronized with the Conversations selector.
- [Source] Apps/Source/Reforged.App/Brains/BorinPlayerJournal.cs:493 — public List<ConversationEntry> Conversations { get; set; } = [];
- [Source] Apps/Source/Reforged.App/Brains/BorinPlayerResearch.cs:641 — ? "Borin may write private high-level reflections about his own play and conversations."
- [Source] Apps/Source/Reforged.App/Brains/BorinPlayerResearch.cs:699 — BrainContext.ForBorin(prompt, _conversationState, repositorySnapshot: null, purpose: "Private MajorMUD player reflection"),
- [Source] Apps/Source/Reforged.App/Brains/BorinPlayerResearch.cs:1322 — var speakerState = _speakerStates.GetOrAdd(speech.Speaker, _ => new SpeakerConversationState());
- [Source] Apps/Source/Reforged.App/Brains/BorinPlayerResearch.cs:1495 — var recentConversation = GetSpeakerConversationSummary(speakerState);
- [Source] Apps/Source/Reforged.App/Brains/BorinPlayerResearch.cs:2136 — private static int GetAiReplyCount(SpeakerConversationState state)
- [Source] Apps/Source/Reforged.App/Brains/BorinPlayerResearch.cs:2158 — private static string GetSpeakerConversationSummary(SpeakerConversationState state)
- [Source] Apps/Source/Reforged.App/Forge/ForgeDeveloperTools.cs:12 — Task<ForgeWorkspaceStatus> GetWorkspaceStatusAsync(ForgeConversationState state, CancellationToken token = default);
- [Source] Apps/Source/Reforged.App/Forge/ForgeDeveloperTools.cs:54 — public async Task<ForgeWorkspaceStatus> GetWorkspaceStatusAsync(ForgeConversationState state, CancellationToken token = default)
- [Source] Apps/Source/Reforged.App/Forge/ForgeServices.cs:36 — Task<string> RespondAsync(string request, ForgeConversationState state, ForgeRepositorySnapshot? snapshot, CancellationToken token = default);
- [Source] Apps/Source/Reforged.App/Forge/ForgeServices.cs:521 — public async Task<string> RespondAsync(string request, ForgeConversationState state, ForgeRepositorySnapshot? snapshot, CancellationToken token = default)
- [Source] Apps/Source/Reforged.App/Forge/ForgeServices.cs:623 — private static string OfflineConversation(string request, ForgeConversationState state, ForgeRepositorySnapshot? snapshot)
- [Source] Apps/Source/Reforged.App/Forge/ForgeServices.cs:642 — private static string Plan(string request, ForgeConversationState state, ForgeRepositorySnapshot? snapshot)
- [Source] Apps/Source/Reforged.App/ViewModels/ForgeGuardianViewModel.cs:178 — public string PlayerLastConversationSummary { get => _playerLastConversationSummary; private set => Set(ref _playerLastConversationSummary, value); }
- [Project file] AI/Memory/borin-memory.json:129 — "Text": "User: im trying to see if you can have a more fluid converstion Borin: Aye, lad. I\u0027m here to help ye with any questions or tasks ye have in mind within MajorMUD Reforged. If ye want a more fluid conversation, just let me know what ye need to discuss or accomplish. Feel free to ask about specific topics, such as crafting, validation, memory management, or anything else that comes to mind. We\u0027ll keep…
- [Project file] AI/Memory/borin-memory.json:139 — "Display": "2bc347a1 \u2022 Conversation \u2022 Discussed \u2022 Temporary \u2022 Active \u2022 7/29/2026 11:28 PM\nUser: im trying to see if you can have a more fluid converstion Borin: Aye, lad. I\u0027m here to help ye with any questions or tasks ye have in mind within MajorMUD Reforged. If ye want a more fluid conversation, just let me know what ye need to discuss or accomplish. Feel free to ask about specific to…
- [Report/Log] Reports/Client/MEGAMUD_CLASSIC_TERMINAL_IMPLEMENTATION.md:17 — - Hideable Conversations and Map/Tools panels.
- [Project file] AI/Memory/Gameplay/borin-training-01/borin-reflection-journal.json:5 — "conversations": [],
- [Source] Apps/Source/Reforged.App/Brains/BorinMemory.cs:15 — ConversationSummary,
- [Source] Apps/Source/Reforged.App/Brains/BorinMemory.cs:159 — private const int MaximumConversationSummaries = 24;
- [Source] Apps/Source/Reforged.App/Brains/BorinMemory.cs:221 — entry.Category != BorinMemoryCategory.ConversationSummary);
- [Source] Apps/Source/Reforged.App/Brains/BorinMemory.cs:251 — entry.Category != BorinMemoryCategory.ConversationSummary))

## Structured Classic trace evidence

This turn did not request a structured Classic trace. Use /trace <question> when needed.

This turn did not request a structured Classic trace. Use /trace <question> when needed.

## Ranked persistent knowledge evidence

20 indexed knowledge chunks retrieved for this turn.

Indexed knowledge 1:
[Validation/report evidence] Simulator/DataSimVer/0005/RoomObjectClues.csv:815-823
wcc_map12_room1799,12,1799,Deep Dark Pit,,tunnel|wall,"Deep Dark Pit This huge, circular pit is at least twenty feet deep, with steep, rough walls of stone. The ground is littered with cracked bones of all shapes and sizes, along with the odd ruined piece of metal. A horrible stench fills the pit, as if some mammoth, smelly beast lived here. A small tunnel at the back of t",none
wcc_map12_room2249,12,2249,Small Stone Chamber,,statue,"Small Stone Chamber This circular carved chamber appears to once have been an ancient temple of some sort. Stone tiles set in the floor are inscribed with swirling symbols. Standing in the centre of the room, upon a raised dais, is a tall statue of a spear-bearing warrior.",none
wcc_map16_room311,16,311,Temple Stairs,key_or_lock_text|level_text|special_text,door|stairs|altar,Temple Stairs You are standing on the second level of steps leading to an altar. You notice the outline of a door leading to the east. From here you have a good view of the twin pyramids that domainte the city. A second story walkway connects them together. Light shines off the golden symbols decorating the buildings ,none
wcc_map16_room312,16,312,Temple Stairs,level_text|special_text,stairs|altar,Temple Stairs You are standing on the first level of steps leading to an altar. From here you have a good view of the twin pyramids that dominate the city. A second story walkway connects them together. Light shines off the golden symbols decorating the buildings below.,none
wcc_map16_room313,16,313,Temple Stairs,special_text,stairs|altar,Temple Stairs You are standing at the foot of several sets of steps leading to an altar. From here you have a good view of the twin pyramids that dominate the city. A second story walkway connects them together. Light shines off the golden symbols decorating the buildings.,none
wcc_map16_room314,16,314,Temple Stairs,special_text,stairs|altar,Temple Stairs You are standing at the foot of several sets of steps leading to an altar. From here you have a good view of the twin pyramids that dominate the city. A second story walkway connects them together. Light shines off the golden symbols decorating the buildings.,none
wcc_map16_room317,16,317,"Armour Shop, Front Room",,well|wall,"Armour Shop, Front Room Gleaming pieces of armour line the walls on hooks or are displayed in glass cases. These pieces have been well cared for and look very expensive. A slave waits behind the counter to serve you, while two others work at polishing a breastplate.",none
wcc_map16_room320,16,320,Bank,,curtain,"Bank You drool with greed at the sights in the bank. Heaping piles of gold and jewels are behind the counter, and guarded by several large and heavily armoured amazon warriors. There also appears to be a priestess chanting from behind a curtai
...[indexed chunk shortened for prompt safety]

Indexed knowledge 2:
[Validation/report evidence] Simulator/DataSimVer/0007/RoomStateClues.csv:408-417
5,12,Dusty Village,lock_or_key_clue,"Dusty Village Although well-shielded from the sun's glaring eye and the withering winds of the desert, this small village is nonetheless plagued by clouds of dust which catch in your throat. The buildings here seem to be semi-permanent, constructed of heavy cl"
50,12,Storage Room,command_clue,"Storage Room The back of this large tent is packed with boxes and bags, each containing an ample supply of the wares this shop has for sale. Each stack of boxes and pile of bags is arranged in a particular order, as if the occupant were extremely concerned wit"
51,12,Cloakroom,command_clue|lock_or_key_clue,"Cloakroom The entrance to the inn is dusty, but well-maintained. A wooden rack high on the wall provides a place to hang cloaks and other clothing. Brass lamps affixed to the walls give off a din, warm light that softens the hard angles of the stone. The inn's"
54,12,Kitchen,command_clue,"Kitchen This strictly functional kitchen is laid out to make the best use of the space. In the centre of the room is firepit, over which an iron grill has been suspended to cook meats. The sharp smell of spices is strong here."
1799,12,Deep Dark Pit,command_clue|special_travel_clue,"Deep Dark Pit This huge, circular pit is at least twenty feet deep, with steep, rough walls of stone. The ground is littered with cracked bones of all shapes and sizes, along with the odd ruined piece of metal. A horrible stench fills the pit, as if some mammo"
2381,12,Platform of Stars,command_clue,"Platform of Stars Luminous stars surround you on all sides, you look down and see what looks like a gigantic blue marble, filled with green and yellow, and swirling clouds of white. A bright spectacle of star showers and comets can be seen far away."
311,16,Temple Stairs,lock_or_key_clue|level_clue|special_travel_clue,Temple Stairs You are standing on the second level of steps leading to an altar. You notice the outline of a door leading to the east. From here you have a good view of the twin pyramids that domainte the city.  A second story walkway connects them together. L
312,16,Temple Stairs,level_clue|special_travel_clue,Temple Stairs You are standing on the first level of steps leading to an altar. From here you have a good view of the twin pyramids that dominate the city.  A second story walkway connects them together. Light shines off the golden symbols decorating the build
313,16,Temple Stairs,special_travel_clue,Temple Stairs You are standing at the foot of several sets of steps leading to an altar.  From here you have a good view of the twin pyramids that dominate the city.  A second story walkway connects them together. Light shines off the golden symbols decorating
314,16,Temple Stairs,special_travel_clue,Temple Stairs You are standing at the foot of 
...[indexed chunk shortened for prompt safety]

Indexed knowledge 3:
[Validation/report evidence] Simulator/DataSimVer/0004/RoomTextGateClues.csv:307-315
wcc_map1_room2144,1,2144,"Newhaven, Spell Shop",key_lock_clue,"Newhaven, Spell Shop This dimly lit room is one of the strangest that you have seen. The walls are decorated with bizarre ornaments ranging from elaborate tapestries to stuffed orc heads. A wizened old man cloaked in strange garb sits behind a small counter, apparently murmuring to himself. You can ",none
wcc_map1_room2145,1,2145,"Newhaven, General Store",text_command_clue|key_lock_clue,"Newhaven, General Store This large wooden shack carries all sorts of useful goods. A big, long-haired man sits behind a counter whistling to himself, and smiles at you as you enter. The general store is a necessary shop for adventurers, as it sells light sources used to explore murky caverns. You ca",none
wcc_map1_room2147,1,2147,"Newhaven, Adventurer's Guild",text_command_clue,"Newhaven, Adventurer's Guild This nicely crafted room is well furnished and decorated with things that you have never seen before, from strange exotic weapons, to swirling vials of liquid. A well toned man, stands in front of a practice mat with his arms crossed. He stands up lightly at your approac",none
wcc_map1_room2149,1,2149,"Newhaven, Docks",text_command_clue,"Newhaven, Docks You stand at the southernmost edge of Newhaven. The view from here is amazing. As you stare out across the Silver river you behold such sights as the notorious Port Blackwater to the west, and the fabled town of Silvermere southwards. A large half-shaven man sits off in the corner, w",none
wcc_map1_room2153,1,2153,"Skali's Fine Armour, Back Room",text_command_clue,"Skali's Fine Armour, Back Room This small, square room is littered with shield of all sizes and types. Skali watches from the southern end of the showroom, observing your every move. You continue into the centre of the shop to the south.",none
wcc_map1_room2154,1,2154,"Sentara's Clothing, Basement",level_gate_clue,"Sentara's Clothing, Basement This is a long, wooden-walled room set beneath ground level. The floor is well-swept and is in good condition. Shelves and racks of leather boots and helms fill its length. A young boy sitting in a chair in the corner waits to take your order.",none
wcc_map1_room2190,1,2190,"Newhaven, Healer",text_command_clue|key_lock_clue,"Newhaven, Healer This small, stark stone chamber is bereft of even the simple ornamentation, and its bare walls gleam in the bright light of the torches lining its walls. A short, stocky man with a black beard sits behind a large stone table at the far end of the room. There is an iron washbasin and",none
wcc_map1_room2226,1,2226,Marble Chamber,key_lock_clue,"Marble Chamber This small, round marble chamber is illuminated by a single glowing crystal hanging from the ceiling. Its curving wall is broken by four identical passages, each
...[indexed chunk shortened for prompt safety]

Indexed knowledge 4:
[Validation/report evidence] Simulator/DataSimVer/0004/RoomTextGateClues.csv:704-712
wcc_map16_room311,16,311,Temple Stairs,key_lock_clue|level_gate_clue,Temple Stairs You are standing on the second level of steps leading to an altar. You notice the outline of a door leading to the east. From here you have a good view of the twin pyramids that domainte the city.  A second story walkway connects them together. Light shines off the golden symbols decor,none
wcc_map16_room312,16,312,Temple Stairs,level_gate_clue,Temple Stairs You are standing on the first level of steps leading to an altar. From here you have a good view of the twin pyramids that dominate the city.  A second story walkway connects them together. Light shines off the golden symbols decorating the buildings below.,none
wcc_map16_room317,16,317,"Armour Shop, Front Room",text_command_clue,"Armour Shop, Front Room Gleaming pieces of armour line the walls on hooks or are displayed in glass cases. These pieces have been well cared for and look very expensive.  A slave waits behind the counter to serve you, while two others work at polishing a breastplate.",none
wcc_map16_room318,16,318,"Armour Shop, Back Room",text_command_clue|key_lock_clue,"Armour Shop, Back Room The back room of the armour shop has items that are of a lesser value to the amazon people. Many look as if they are the spoils of war. They are items you would never see the women wearing, or items with dents and poor craftsmanship. A slave waits at the counter to serve you.",none
wcc_map16_room321,16,321,Market Street,text_command_clue|key_lock_clue,Market Street Amazon warriors filter out of a large door to the north.  It appears to be the city barracks.  Further to the west Market Street turns south into an alley.,none
wcc_map16_room323,16,323,Well Lit Alley,text_command_clue|key_lock_clue,"Well Lit Alley You are in a well-lit alley with exits to the north and south.  A door off to the south looks as if it opens up into another shop.  You notice strange etchings along the walls here, symbols of war.",none
wcc_map16_room324,16,324,Well Lit Alley,text_command_clue|key_lock_clue,"Well Lit Alley You are in a well-lit alley with exits to the north and south.  A doorway to the east opens up into a weapons shop.  You notice strange etchings along the walls here, symbols of war.",none
wcc_map16_room327,16,327,Well Lit Alley,text_command_clue|key_lock_clue,"Well Lit Alley You are in a well-lit alley with exits to the east and west.  The traffic is much less active in this area of town.  To your north is a door leading to a hides shop. The stench from tanning agents nauseates you slightly.  You notice strange etchings along the walls here, symbols of wa",none
wcc_map16_room329,16,329,Hides Shop,text_command_clue,Hides Shop The reek of leather and tanning agents is nauseatingly strong when you enter this store.  A pair of 
...[indexed chunk shortened for prompt safety]

Indexed knowledge 5:
[Validation/report evidence] Simulator/DataSimVer/0004/RoomTextGateClues.csv:733-742
wcc_map16_room458,16,458,Central Street,key_lock_clue,Central Street The tall pyramids block out most direct sunlight on this path. To either side of you is a large pyramid covered in holy symbols and depictions of war. Intricate designs are inlaid with precious metals and gems.,none
wcc_map16_room459,16,459,Central Street,key_lock_clue,Central Street The tall pyramids block out most direct sunlight on this path. To either side of you is a large pyramid covered in holy symbols and depictions of war. Intricate designs are inlaid with precious metals and gems.,none
wcc_map16_room460,16,460,Central Street,key_lock_clue,Central Street The tall pyramids block out most direct sunlight on this path. To either side of you is a large pyramid covered in holy symbols and depictions of war. Intricate designs are inlaid with precious metals and gems.,none
wcc_map16_room461,16,461,Central Street,key_lock_clue,Central Street The tall pyramids block out most direct sunlight on this path. To either side of you is a large pyramid covered in holy symbols and depictions of war. Intricate designs are inlaid with precious metals and gems.,none
wcc_map16_room462,16,462,Central Street,key_lock_clue,Central Street The tall pyramids block out most direct sunlight on this path. To either side of you is a large pyramid covered in holy symbols and depictions of war. Intricate designs are inlaid with precious metals and gems.,none
wcc_map16_room463,16,463,Temple Stairs,level_gate_clue,Temple Stairs You are standing on the first level of steps leading to an altar. From here you have a good view of the twin pyramids that dominate the city.  A second story walkway connects them together. Light shines off the golden symbols decorating the buildings below.,none
wcc_map16_room464,16,464,Temple Stairs,level_gate_clue,Temple Stairs You are standing on the second level of steps leading to an altar. From here you have a good view of the twin pyramids that dominate the city. A second story walkway connects them together. Light shines off the golden symbols decorating the buildings below.,none
wcc_map16_room465,16,465,Temple Stairs,level_gate_clue,Temple Stairs You are standing on the second level of steps leading to an altar. From here you have a good view of the twin pyramids that dominate the city. A second story walkway connects them together. Light shines off the golden symbols decorating the buildings below.,none
wcc_map16_room466,16,466,Temple Stairs,level_gate_clue,Temple Stairs You are standing on the first level of steps leading to an altar. From here you have a good view of the twin pyramids that dominate the city. A second story walkway connects them together. Light shines off the golden symbols decorating the buildings below.,none
wcc_map16_room467,16,467,Temple Stairs,level_gat
...[indexed chunk shortened for prompt safety]

Indexed knowledge 6:
[Validation/report evidence] Simulator/DataSimVer/0004/RoomTextGateClues.csv:808-816
wcc_map17_room2436,17,2436,Jandar's Missile Weapons,text_command_clue|key_lock_clue,"Jandar's Missile Weapons Unstrung bows hang about the shop for you to look at and handle.  Some are of an obviously better quality than others, and some are barely passable as useable.  The local guild will tell you that bows, like the many other forms of commerce, have come into a shortage the past",none
wcc_map17_room2437,17,2437,Abandoned Shop,text_command_clue,"Abandoned Shop This looks as if it may have once been a tanner's shop, but it is now boarded up and abandoned.  It looks as if it were deliberately destroyed by a mob of people.  You notice some guild markings they scrawled in dye across ruined hides.",none
wcc_map17_room2438,17,2438,Lud's Smithy,key_lock_clue|level_gate_clue,"Lud's Smithy Arguably the warmest building in the city, the forge keeps things quite toasty in here.  The red glow of the forge casts an odd light about the room.  You see a few items hanging from the walls, and a few in a pile behind the master smith.  He greets you with a quick nod of his head whi",none
wcc_map17_room2439,17,2439,Hap's General Store,text_command_clue|key_lock_clue,"Hap's General Store Most of the items in this store appear to be junk.  There are a few usable items left, but most of it is not worth wasting your coin on. One look at Hap tells you he's a little off kilter these days.  He has a crooked smile and smells like he's been hitting the bottle a little to",none
wcc_map17_room2440,17,2440,The Goode Seamstress,text_command_clue,"The Goode Seamstress The inside of this shop is stocked with delicate threads and embroidery.  Each garment has been carefully sewn and is displayed just so.  Robes are neatly folded in the polished glass case, or hung on the wall to catch your eye when you first enter the shop.  The seamstress is i",none
wcc_map17_room2441,17,2441,Tavern,text_command_clue,"Tavern The city tavern is quite lively for such a dreary town.  The townsfolk may be mired in depression, but they seem to forget their troubles here with a drink in hand and a merry song played on a mandolin by one of the local bards.",none
wcc_map17_room2442,17,2442,Tavern,text_command_clue,"Tavern The city tavern is quite lively for such a dreary town.  The townsfolk may be mired in depression, but they seem to forget their troubles here with a drink in hand and a merry song played on a mandolin by one of the local bards.",none
wcc_map17_room2443,17,2443,City Park,text_command_clue,"City Park This park was once beautiful, with flowers of every sort cultivated in small circular patches throughout the great lawns.  In the center of the park is a small pond that held ducks and swans.  The grass is now frozen, the plants are a sickly shade of green under the snow, and the pond is s",no
...[indexed chunk shortened for prompt safety]

Indexed knowledge 7:
[Other approved project text] Tools/Simulator/MajorMUD-Reforged-SimulatorPrototype/Evidence/RuntimePrep0006-NPCDialogueScriptTriggerQuestInteractionExtraction/Payload/RuntimePrep0006/WBBMessageBlockMatrix_RuntimePrep0006.csv:230-270
wbbMMHLP.MSG,HL1T8,302,97,general_text_message_candidate,Movement travel north south east west northeast northwest southeast southwest up down ne nw se sw
wbbMMHLP.MSG,HL1B8,305,775,dialogue_or_user_message_candidate;script_trigger_or_prompt_candidate,"[0m[2J[1;33mNorth      East     South    West    Northwest   Northeast   Southwest
Southeast  Up       Down     Sneak   Exits       Picklock    Go <Location>

[37mN, E, S W, NE, SE, [0m- General movement commands (North, East, South, West,
[1mSW, NW, Up, Down     [0metc...) To get to special locations, you may have to
                     type a corresponding action command. (Ie... Go Hole,
                     or Climb Tree). You should experiment with various
                     commands.
[1mHide               [0m- If your class permits it, you will be able to hide
                     amongst the shadows.
[1mSneak              [0m- If class permits, you will be able to effectively
                     walk from room to room without being seen moving."
wbbMMHLP.MSG,HL1T9,321,68,general_text_message_candidate,Party frontrank backrank midrank invite uninvite rank parties partys
wbbMMHLP.MSG,HL1B9,326,1265,dialogue_or_user_message_candidate,"[0m[2J[1;33mInvite     Uninvite    Party     Join    Backrank   Frontrank

[37mInvite <USERNAME>   [0m- Will invite someone into your adventuring group.
[1mUninvite <USERNAME> [0m- Will kick someone out of your adventuring group.
[1mJoin <USERNAME>     [0m- Allows you to join another user, but ONLY if he/she
                      has already invited you.
[1mLeave               [0m- Pretty straightforward.
[1mParty               [0m- Will display who is in your adventuring group, and what
                      ranks they are in.
[1mBackrank            [0m- If you are in someone's adventuring group, this will
                      put you into the back rank. You will be harder to hit
                      in combat, but will also have a harder time hitting
                      opponents with close combat attacks. Great for magic
                      users casting ranged spells, or players with missile
                      weapons.
[1mFrontrank           [0m- Will place you into the frontrank, if a spot is currently
                      available. At least the same amount of users must be in the front
                      rank as the back. You will have an easier time hitting opponents,
		      but will get hit easier as well."
wbbMMHLP.MSG,HL1T10,348,25,general_text_message_candidate,Training room advancement
wbbMMHLP.MSG,HL1B10,351,288,quest_or_progression_interaction_candidate;shop_or_economy_interaction_candidate;script_trigger_or_prompt_candidate,"[0m
To train to the next level, you must find the appropriate training room
for you
...[indexed chunk shortened for prompt safety]

Indexed knowledge 8:
[Other approved project text] Tools/Simulator/MajorMUD-Reforged-SimulatorPrototype/Evidence/RuntimePrep0006-NPCDialogueScriptTriggerQuestInteractionExtraction/Payload/RuntimePrep0006/WBBMessageBlockMatrix_RuntimePrep0006.csv:487-534
often, such as people sneaking around, robbing them, etc. Intellect is a main
component in many skills, and is useful to a thief, but it is vital to a mage!
Mages use Intellect as the main stat for their spells. A Mage with low
Intellect will be at a severe disadvantage with spells when compared to a mage
with higher Intellect. Druids use both Intellect and Wisdom for their spells,
so they need to have good levels in both stats. Warrior-types can usually get
along without much Intellect, and historically many have done so -- but
Intellect is also a component of Critical Hits, so a canny fighter will do
heavy blows more often than a dull-witted opponent.

Intellect directly affects the following skills and abilities: [1mStealth[0m,
[1mThievery[0m, [1mTraps[0m, [1mPicklocks[0m, [1mTracking[0m, [1mPerception[0m, [1mMagic Resistance[0m,
[1mSpellcasting[0m, and [1mCritical Hit Chance[0m."
wbbMMHLP.MSG,HL1T18E,653,16,general_text_message_candidate,Willpower wisdom
wbbMMHLP.MSG,HL1B18E,656,805,dialogue_or_user_message_candidate;quest_or_progression_interaction_candidate,"[0m
[1;33mWILLPOWER (WIL)

[0mThis stat is a measure of the character's cool-headedness, determination,
mental focus and strength of faith. It is a must for any Priestly character,
as this is the stat they use for their spell-casting. Druids use both
Willpower and Intellect, so they need to have good levels in both stats.
Willpower is the prime component in Magic Resistance, so anyone needing to
resist spells had better have a good value in this stat. A Witchunter with
high WIL would have a formidable MagicRes! As for Intellect, a Warrior-type
could probably get along with a low Wisdom, but would then be much more
vulnerable to magic spells.

Willpower directly affects the following skills and abilities: [1mSpellcasting[0m,
[1mTracking[0m, [1mPerception [0mand [1mMagic Resistance[0m."
wbbMMHLP.MSG,HL1T18F,673,19,general_text_message_candidate,Health constitution
wbbMMHLP.MSG,HL1B18F,676,1286,quest_or_progression_interaction_candidate;script_trigger_or_prompt_candidate,"[0m
[0;1;33mHEALTH (HEA)

[0mHealth is a character's vitality, toughness, and general physical well-being.
Health governs how many Hit Points a character starts with, as well as how
many are gained each time a new Level is reached. Poison is less effective on
a healthier person, and Health is a component in Magic Resistance. Where
Willpower is the toughness of the mind, Health is the toughness of the body.
Health is a prime stat for any fighting classes, but is very useful for all
characters, as everyone will sustain damage at some point in their perilous
careers.

Having a high Health increases the rate at which you naturally heal, and the
rate at which you gain HP through resting. It may also
...[indexed chunk shortened for prompt safety]

Indexed knowledge 9:
[Validation/report evidence] Simulator/DataSimVer/0005/RoomObjectClues.csv:76-84
wcc_map1_room184,1,184,Intersection of Bridge St. & Crown St.,key_or_lock_text,passage|bridge|well,"Intersection of Bridge St. & Crown St. This is a cobblestoned street. It is dimly lit by guttering lanterns hung from tall posts. It is relatively clean, and is quiet and fairly unpopulated. The buildings here are plain-looking and unornamented, but are well-made. Crown street crosses the east-west passage of the large",none
wcc_map1_room186,1,186,Bridge Street,,bridge|well,"Bridge Street This is a cobblestoned street. It is dimly lit by guttering lanterns hung from tall posts. It is eerily quiet here, and though the street is clean and well-kept, you get the impression that is is rarely traveled. It continues to the north and south.",none
wcc_map1_room187,1,187,Bridge Street,,bridge|well,"Bridge Street This is a cobblestoned street. It is dimly lit by guttering lanterns hung from tall posts. It is eerily quiet here, and though the street is clean and well-kept, you get the impression that is is rarely traveled. It continues to the north and south.",none
wcc_map1_room188,1,188,Bridge Street,,bridge|well,"Bridge Street This is a cobblestoned street. It is dimly lit by guttering lanterns hung from tall posts. It is eerily quiet here, and though the street is clean and well-kept, you get the impression that is is rarely traveled. It continues to the north and south. There is a wide alley to the east here.",none
wcc_map1_room189,1,189,Bridge Street,,bridge|well,"Bridge Street This is a cobblestoned street. It is dimly lit by guttering lanterns hung from tall posts. It is eerily quiet here, and though the street is clean and well-kept, you get the impression that is is rarely traveled. It continues to the north and south.",none
wcc_map1_room190,1,190,Bridge Street,,path|bridge|well,"Bridge Street This is a cobblestoned street. It is dimly lit by guttering lanterns hung from tall posts. It is eerily quiet here, and though the street is clean and well-kept, you get the impression that is is rarely traveled. It continues to the north and south. There is a wide street crossing Bridge street's path off",none
wcc_map1_room197,1,197,Alley,,path|well|wall,"Alley This alleyway is dark and seems to be well-traveled, due to the fact that the debris is pushed up against the walls to clear a path. It continues to the east, and to the west, it opens up onto a street.",none
wcc_map1_room198,1,198,Alley,key_or_lock_text,door|path|well|wall|window,"Alley This alleyway is dark and seems to be well-traveled, due to the fact that the debris is pushed up against the walls to clear a path. There is a large, steel-banded door in the wall of a nondescript house to the north. Its windows are carefully shuttered, and you can hear faint movement noises from within. The all",none
wcc_
...[indexed chunk shortened for prompt safety]

Indexed knowledge 10:
[Validation/report evidence] Simulator/DataSimVer/0007/RoomStateClues.csv:156-165
2142,1,"Newhaven, Armour Shop",command_clue,"Newhaven, Armour Shop This makeshift store carries a small assortment of clothes and armour. Behind a small wooden counter sits a large, bearded man, with a cheerful look on his face. Other than the assortment of items here, there is little of interest. You ca"
2144,1,"Newhaven, Spell Shop",special_travel_clue,"Newhaven, Spell Shop This dimly lit room is one of the strangest that you have seen. The walls are decorated with bizarre ornaments ranging from elaborate tapestries to stuffed orc heads. A wizened old man cloaked in strange garb sits behind a small counter, a"
2145,1,"Newhaven, General Store",command_clue,"Newhaven, General Store This large wooden shack carries all sorts of useful goods. A big, long-haired man sits behind a counter whistling to himself, and smiles at you as you enter. The general store is a necessary shop for adventurers, as it sells light sourc"
2149,1,"Newhaven, Docks",command_clue,"Newhaven, Docks You stand at the southernmost edge of Newhaven. The view from here is amazing. As you stare out across the Silver river you behold such sights as the notorious Port Blackwater to the west, and the fabled town of Silvermere southwards. A large h"
2153,1,"Skali's Fine Armour, Back Room",command_clue,"Skali's Fine Armour, Back Room This small, square room is littered with shield of all sizes and types. Skali watches from the southern end of the showroom, observing your every move. You continue into the centre of the shop to the south."
2154,1,"Sentara's Clothing, Basement",level_clue,"Sentara's Clothing, Basement This is a long, wooden-walled room set beneath ground level. The floor is well-swept and is in good condition. Shelves and racks of leather boots and helms fill its length. A young boy sitting in a chair in the corner waits to take"
2227,1,Marble Passage,special_travel_clue,"Marble Passage This narrow passageway is made of smooth white marble, polished to a mirrorlike shine. Your every sound echoes down its length like a thunderclap, it is so silent here. The passage slopes downwards at a steady angle, heading into the bowels of t"
2228,1,Marble Passage,special_travel_clue,"Marble Passage This narrow passageway is made of smooth white marble, polished to a mirrorlike shine. Your every sound echoes down its length like a thunderclap, it is so silent here. The passage slopes downwards at a steady angle, heading into the bowels of t"
2229,1,Marble Passage,special_travel_clue,"Marble Passage This narrow passageway is made of smooth white marble, polished to a mirrorlike shine. Your every sound echoes down its length like a thunderclap, it is so silent here. The passage slopes downwards at a steady angle, heading into the bowels of t"
2230,1,Marble Passage,special_travel_clue,"Marble 
...[indexed chunk shortened for prompt safety]

Indexed knowledge 11:
[Validation/report evidence] Simulator/DataSimVer/0007/RoomStateClues.csv:479-488
2384,17,Seaside Way,command_clue,"Seaside Way The western wall of Arlysia is much shorter than the other walls.  It was built this way, so that in more prosperous times, the wealthy ship owners could look out their top windows and see the boats in dock, and look out over Crystal Lake.  With th"
2433,17,Aeriana's Apothecary,command_clue,"Aeriana's Apothecary An assundry of vials and beakers full of liquids of all colors and smells line the shelves here.  Some bottles are stoppered, some are left open precariously.  A stove unlike any you've seen before sits in the middle of the room with a mer"
2436,17,Jandar's Missile Weapons,command_clue,"Jandar's Missile Weapons Unstrung bows hang about the shop for you to look at and handle.  Some are of an obviously better quality than others, and some are barely passable as useable.  The local guild will tell you that bows, like the many other forms of comm"
2438,17,Lud's Smithy,level_clue,"Lud's Smithy Arguably the warmest building in the city, the forge keeps things quite toasty in here.  The red glow of the forge casts an odd light about the room.  You see a few items hanging from the walls, and a few in a pile behind the master smith.  He gre"
2439,17,Hap's General Store,command_clue,"Hap's General Store Most of the items in this store appear to be junk.  There are a few usable items left, but most of it is not worth wasting your coin on. One look at Hap tells you he's a little off kilter these days.  He has a crooked smile and smells like "
2440,17,The Goode Seamstress,command_clue,"The Goode Seamstress The inside of this shop is stocked with delicate threads and embroidery.  Each garment has been carefully sewn and is displayed just so.  Robes are neatly folded in the polished glass case, or hung on the wall to catch your eye when you fi"
2472,17,Dirt Street,command_clue,"Dirt Street Until recently, this was an undeveloped field, intended for use by the guild.  However, necessity and the larege influx of permanent residents have created a sort of housing development made up of tents and shacks and other thrown together protecti"
2473,17,Dirt Street,command_clue,"Dirt Street Until recently, this was an undeveloped field, intended for use by the guild.  However, necessity and the larege influx of permanent residents have created a sort of housing development made up of tents and shacks and other thrown together protecti"
2474,17,Dirt Street,command_clue,"Dirt Street Until recently, this was an undeveloped field, intended for use by the guild.  However, necessity and the larege influx of permanent residents have created a sort of housing development made up of tents and shacks and other thrown together protecti"
2503,17,"Town Hall, Stairwell",special_travel_clue,"Town Hall, Stairwell The seat of the Arlysian 
...[indexed chunk shortened for prompt safety]

Indexed knowledge 12:
[Other approved project text] Tools/Simulator/MajorMUD-Reforged-SimulatorPrototype/Evidence/RuntimePrep0006-NPCDialogueScriptTriggerQuestInteractionExtraction/Payload/RuntimePrep0006/DialogueScriptQuestCandidateMatrix_RuntimePrep0006.csv:221-262
current room. This will display all the necessary information on
[s               purchasing an item, including it's going price and how many items[u
[0;1m               [0mare available.
[1mBuy <Item>   [0m- Will attempt to purchase an item at the best price possible
               (based on your charm).
[1mSell <Item>  [0m- Will attempt to sell an item at the best price possible (Again
               this is based on your charm).
[1mAppraise     [0m- Informs you on the selling price you will receive for
                the item that you are appraising."
wbbMMHLP.MSG,HL1T8,302,wbbMMHLP.h,24,26,yes,general_text_message_candidate,97,Movement travel north south east west northeast northwest southeast southwest up down ne nw se sw
wbbMMHLP.MSG,HL1B8,305,wbbMMHLP.h,25,27,yes,dialogue_or_user_message_candidate;script_trigger_or_prompt_candidate,775,"[0m[2J[1;33mNorth      East     South    West    Northwest   Northeast   Southwest
Southeast  Up       Down     Sneak   Exits       Picklock    Go <Location>

[37mN, E, S W, NE, SE, [0m- General movement commands (North, East, South, West,
[1mSW, NW, Up, Down     [0metc...) To get to special locations, you may have to
                     type a corresponding action command. (Ie... Go Hole,
                     or Climb Tree). You should experiment with various
                     commands.
[1mHide               [0m- If your class permits it, you will be able to hide
                     amongst the shadows.
[1mSneak              [0m- If class permits, you will be able to effectively
                     walk from room to room without being seen moving."
wbbMMHLP.MSG,HL1T9,321,wbbMMHLP.h,26,28,yes,general_text_message_candidate,68,Party frontrank backrank midrank invite uninvite rank parties partys
wbbMMHLP.MSG,HL1B9,326,wbbMMHLP.h,27,29,yes,dialogue_or_user_message_candidate,1265,"[0m[2J[1;33mInvite     Uninvite    Party     Join    Backrank   Frontrank

[37mInvite <USERNAME>   [0m- Will invite someone into your adventuring group.
[1mUninvite <USERNAME> [0m- Will kick someone out of your adventuring group.
[1mJoin <USERNAME>     [0m- Allows you to join another user, but ONLY if he/she
                      has already invited you.
[1mLeave               [0m- Pretty straightforward.
[1mParty               [0m- Will display who is in your adventuring group, and what
                      ranks they are in.
[1mBackrank            [0m- If you are in someone's adventuring group, this will
                      put you into the back rank. You will be harder to hit
                      in combat, but will also have a harder time hitting
                      opponents with close combat attacks. Great for magic
                      users casting ranged spells, or players with mi
...[indexed chunk shortened for prompt safety]

Indexed knowledge 13:
[Other approved project text] Tools/Simulator/MajorMUD-Reforged-SimulatorPrototype/Evidence/RuntimePrep0006-NPCDialogueScriptTriggerQuestInteractionExtraction/Payload/RuntimePrep0006/WBBMessageBlockMatrix_RuntimePrep0006.csv:271-313
wbbMMHLP.MSG,HL1T11,362,114,quest_or_progression_interaction_candidate,chaos good evil reputation alignment criminals gods sins laws saints fiends outlaws villains seedy neutrality jail
wbbMMHLP.MSG,HL1B11,365,1515,dialogue_or_user_message_candidate;quest_or_progression_interaction_candidate;script_trigger_or_prompt_candidate,"[0m[2J[sLike any respectful city, Silvermere has its laws. Some follow them, some
don't.[u
[0mIf you are in the latter category, your stay in the realm may be cut short, in
more ways than one. There are two main criminal acts in the Realm. Attacking
""innocents"" (The town doesnt care if you slay a hated person), or stealing from
others. Although the latter is more tolerated of course. People will be
classified in the following titles, so watch who you trust.

[1;37mLawful   [0;32m-  Attacking a lawful player will give thrice the evil points.
[1;37mSaint    [0;32m-  A true saint, in society and in heart. A rarity in these times.
[1;37mGood     [0;32m-  Someone with a noteworthy reputation of lawfulness.
[36mNeutral  [32m-  An average citizen, you will be treated fairly within the realm.
[37mSeedy    [32m-  The dregs of society. Bad deeds done to you will be ignored.
[35mOutlaw   [32m-  Guards will attack you on sight, but will spare your life.
[1;35mCriminal [0;32m-  Same as Outlaw, except that Guards will SLAY you on sight.
[31mVillain  [32m-  Same as Criminal, except that the ""Gods"" may sometimes intervene.
[1;31mFiend    [0;32m-  The utmost in evil. Don't count on living too long in this state.

[37mCurrently, the only way to become a more ""respected"" citizen is to wait it off.
[sIn times like these, your deeds will eventually be forgotten, and it is commonly[u
[0mknown for an infamous villain to become a well respected townsman after a
[scertain amount of time."
wbbMMHLP.MSG,HL1T12,390,4,general_text_message_candidate,Tips
wbbMMHLP.MSG,HL1B12,393,1131,quest_or_progression_interaction_candidate;shop_or_economy_interaction_candidate;script_trigger_or_prompt_candidate,"[0m[2JHere are some helpful ""hints"" or advice to get you started...

[1;33m1. [0mRead all of the help files, or at least glance through them. This will save
   you a lot of time and effort overall.

[1;33m2. [0mPick a class that you feel comfortable with. We have tried to make ALL of
   the classes equal in efficiency, but each one has its own unique features.

[1;33m2. [0mOnce you have your character made, check out all of the stores in Newhaven
   and equip yourself with weaponry, armour, and spells (if you are a
   spellcaster). Don't go down into the arena until you are armed and ready.

[1;33m3. [0mTo familiarize yourself with the combat system, go to the Newhaven Arena.
   Here you will be able to earn experience
...[indexed chunk shortened for prompt safety]

Indexed knowledge 14:
[Validation/report evidence] Simulator/DataSimVer/0004/RoomTextGateClues.csv:51-59
wcc_map1_room186,1,186,Bridge Street,text_command_clue,"Bridge Street This is a cobblestoned street. It is dimly lit by guttering lanterns hung from tall posts. It is eerily quiet here, and though the street is clean and well-kept, you get the impression that is is rarely traveled. It continues to the north and south.",none
wcc_map1_room187,1,187,Bridge Street,text_command_clue,"Bridge Street This is a cobblestoned street. It is dimly lit by guttering lanterns hung from tall posts. It is eerily quiet here, and though the street is clean and well-kept, you get the impression that is is rarely traveled. It continues to the north and south.",none
wcc_map1_room188,1,188,Bridge Street,text_command_clue,"Bridge Street This is a cobblestoned street. It is dimly lit by guttering lanterns hung from tall posts. It is eerily quiet here, and though the street is clean and well-kept, you get the impression that is is rarely traveled. It continues to the north and south. There is a wide alley to the east he",none
wcc_map1_room189,1,189,Bridge Street,text_command_clue,"Bridge Street This is a cobblestoned street. It is dimly lit by guttering lanterns hung from tall posts. It is eerily quiet here, and though the street is clean and well-kept, you get the impression that is is rarely traveled. It continues to the north and south.",none
wcc_map1_room190,1,190,Bridge Street,text_command_clue,"Bridge Street This is a cobblestoned street. It is dimly lit by guttering lanterns hung from tall posts. It is eerily quiet here, and though the street is clean and well-kept, you get the impression that is is rarely traveled. It continues to the north and south. There is a wide street crossing Brid",none
wcc_map1_room197,1,197,Alley,text_command_clue,"Alley This alleyway is dark and seems to be well-traveled, due to the fact that the debris is pushed up against the walls to clear a path. It continues to the east, and to the west, it opens up onto a street.",none
wcc_map1_room198,1,198,Alley,text_command_clue|key_lock_clue,"Alley This alleyway is dark and seems to be well-traveled, due to the fact that the debris is pushed up against the walls to clear a path. There is a large, steel-banded door in the wall of a nondescript house to the north. Its windows are carefully shuttered, and you can hear faint movement noises ",none
wcc_map1_room199,1,199,Thieves' Guild,text_command_clue|key_lock_clue,"Thieves' Guild This large, smoky room is filled with a half-dozen tables. Seated at these are scruffy-looking men playing various games of chance. As you approach, the room quiets down as everyone begins whispering, and you receive a few hard stares. You can exit through doorways to the north and we",none
wcc_map1_room200,1,200,Ninja Training Room,text_command_clue,"Ninja Training Room A we
...[indexed chunk shortened for prompt safety]

Indexed knowledge 15:
[Validation/report evidence] Simulator/DataSimVer/0005/RoomObjectClues.csv:85-92
wcc_map1_room200,1,200,Ninja Training Room,,hole|well|curtain|wall,"Ninja Training Room A well toned man is here, dressed in padded black garb. He is circling a cloth dummy hung from the ceiling. At your entry, he lunges suddenly, running the dummy through with his long sword. Abruptly, he seems to take notice of your presence, turns toward you, and casually gestures with his blade to ",none
wcc_map1_room201,1,201,Thief Training Room,key_or_lock_text,well,"Thief Training Room A short, weaselly-looking man sits at a table here, nimbly twirling a dagger in his hand. As you enter, he swiftly stabs it down into the table's surface, and looks you up and down appraisingly. After a short moment, he stands up and says ""Well? What do you want?"" You can exit through a doorway to t",none
wcc_map1_room202,1,202,Thieves' Guild,key_or_lock_text|special_text,trapdoor,"Thieves' Guild This large, smoky room is filled with a half-dozen tables. Seated at these are scruffy-looking men playing various games of chance. As you approach, the room quiets down as everyone begins whispering, and you receive a few hard stares. You can exit through doorways to the south and west. There is a trapd",none
wcc_map1_room203,1,203,Arena Entrance,key_or_lock_text,door,"Arena Entrance This is the entrance to the city's arena, where combatants come to hone their deadly skills, and where townsfolk come to be thus entertained. There are quite a few people here, and you have to push your way through the crowd at some points. You can continue into the heart of the arena to the north, or yo",none
wcc_map1_room204,1,204,Arena Stands,level_text,,"Arena Stands This is the upper level of the arena, where the townsfolk can observe the combatants. A small stairwell leads down to the fighting below. To your west lies a concession stand, where food and even some weaponry can be purchased for those who get inspired by viewing the battling warriors. The main exit is to",none
wcc_map1_room206,1,206,Arena Practice Room,special_text,,"Arena Practice Room This room is magicked with a powerful spell, enabling people to battle without receiving grievous injury. It is used by all to practice their skills, and to test their preference for weapons and the like. It is also used for those who wish to settle a grudge, but are not willing to kill for it. The ",none
wcc_map1_room212,1,212,Intersection of Crown St. & Brass St.,key_or_lock_text,passage|bridge|well,"Intersection of Crown St. & Brass St. This is a cobblestoned street. It is dimly lit by guttering lanterns hung from tall posts. It is relatively clean, and is quiet and fairly unpopulated. The buildings here are plain-looking and unornamented, but are well-made. Bridge street crosses the east-west passage of Brass str",none
wcc_map1_room214,1,214,Crown S
...[indexed chunk shortened for prompt safety]

Indexed knowledge 16:
[Validation/report evidence] Simulator/DataSimVer/0005/RoomObjectClues.csv:749-757
wcc_map8_room485,8,485,"City, Main Street",,river|well|window,"City, Main Street This large, well-travelled dirt road snakes through town like a great dark earthen river. It is clean and well-kept, and there doesn't seem to be many people about for whatever reason. A feeling of unsettling watchfulness pervades the area, making your hair stand on end. A massive pile of rubble stand",none
wcc_map8_room487,8,487,Narrow Alley,,crack,"Narrow Alley This alley is nothing more than a crack between two buildings, barely four wide. Small furry animals that ressemble rats scurry back and forth at your feet.",none
wcc_map8_room488,8,488,Narrow Alley,,crack,"Narrow Alley This alley is nothing more than a crack between two buildings, barely four wide. Small furry animals that ressemble rats scurry back and forth at your feet.",none
wcc_map8_room491,8,491,Cobblestone Path,,path,"Cobblestone Path This short pathway appears to be made of cobblestone, small rocks fitted together to form a relatively smooth roadway. But to your horror, looking closely you discover you are standing on the tops of hundreds of skulls! Some are human, some are demi-human, but all have been cleaned and polished with gr",none
wcc_map8_room492,8,492,Royal Prison,key_or_lock_text,door,"Royal Prison This stone chamber is grim and nearly featureless. A heavy wooden desk sits in the centre of the room, facing the door, and a staircase in the southwestern corner leads down into darkness.",none
wcc_map8_room559,8,559,"Dark-Elf City, Moatside",key_or_lock_text,gate|passage|crack,"Dark-Elf City, Moatside A wide, rocky chasm splits the ground here. Ten feet down, the crack is filled with a moat of black, noxious water. Shadowy forms swim beneath the surface but you can only guess what they are. Attached by chains as thick around as an ogre's arm is a lowered drawbridge. On the other side stands a",none
wcc_map8_room560,8,560,"Dark-Elf Castle, Gatehouse",key_or_lock_text,gate|hole|wall,"Dark-Elf Castle, Gatehouse This large stone chamber is quite bare, save for guard alcoves cut into the walls and a wooden table and chairs sitting in the centre of the room. A massive iron wheel, set with long bars with which to turn it, is set in the north wall. A great chain snaking out from beneath it disappears int",none
wcc_map10_room268,10,268,"Rocky Trail, Bottleneck Pass",key_or_lock_text,path|wall,"Rocky Trail, Bottleneck Pass This rough trail leads upwards in a fairly steep climb, with many large rocks making it a troublesome journey. The air at this elevation is quite crisp and cool, and gets colder the higher you go up. The jagged stone walls rise up at the sides of the trail here, and narrow considerably, mak",none
wcc_map10_room269,10,269,"Rocky Trail, Bottleneck Pass",key_or_lock_text,path|wall,"Rocky Tra
...[indexed chunk shortened for prompt safety]

Indexed knowledge 17:
[Validation/report evidence] Simulator/DataSimVer/0005/RoomObjectClues.csv:845-855
wcc_map16_room384,16,384,"Elders' Council Chambers, Meeting Hall",level_text,,"Elders' Council Chambers, Meeting Hall The amazons gather here weekly to present or listen to petitions from other citizens. A large chair is here, ornately decorated with symbols of war and inlaid with gold and gems. This is where the master battle trainer for the amazons meet with new recruits to test their mettle.",none
wcc_map16_room412,16,412,Barracks,,wall,Barracks Neatly made beds line the walls here. Slaves scurry around the room picking up clutter and repairing weapons or armour. Amazon warriors filter about bragging and showing off their latest spoils of war.,none
wcc_map16_room413,16,413,Barracks,,wall,Barracks Neatly made beds line the walls here. Slaves scurry around the room picking up clutter and repairing weapons or armour. Amazon warriors filter about bragging and showing off their latest spoils of war.,none
wcc_map16_room414,16,414,Barracks,,wall,Barracks Neatly made beds line the walls here. Slaves scurry around the room picking up clutter and repairing weapons or armour. Amazon warriors filter about bragging and showing off their latest spoils of war. You feel a soft breeze from the western wall.,none
wcc_map16_room415,16,415,Barracks,,wall,Barracks Neatly made beds line the walls here. Slaves scurry around the room picking up clutter and repairing weapons or armour. Amazon warriors filter about bragging and showing off their latest spoils of war.,none
wcc_map16_room416,16,416,Barracks,key_or_lock_text,door|wall,Barracks To your south is a large door leading to Market Street. North takes you further into the barracks. Neatly made beds line the walls here. Slaves scurry around the room picking up clutter and repairing weapons or armour.,none
wcc_map16_room417,16,417,Market Street,key_or_lock_text,door,"Market Street Amazon warriors filter out of a large door to the north, and press past you busy with various tasks. To the south is Market Street and the shops.",none
wcc_map16_room419,16,419,Corner of Market Street and Alley,,well|wall,"Corner of Market Street and Alley To the west is Market Street and several shops. A well lit alley runs south of you, possibly leading to more shops. You notice strange etchings along the walls here, symbols of war.",none
wcc_map16_room420,16,420,Well Lit Alley,,well|wall,"Well Lit Alley You are in a well-lit alley with exits to the north and south. You notice strange etchings along the walls here, symbols of war.",none
wcc_map16_room421,16,421,Well Lit Alley,,well|wall,"Well Lit Alley You are in a well-lit alley with exits to the north and south. You notice strange etchings along the walls here, symbols of war.",none
wcc_map16_room422,16,422,"Well Lit Alley, Corner",,well|wall,"Well Lit Alley, Corner You are in a well-lit 
...[indexed chunk shortened for prompt safety]

Indexed knowledge 18:
[Validation/report evidence] Simulator/DataSimVer/0007/RoomStateClues.csv:429-438
384,16,"Elders' Council Chambers, Meeting Hall",level_clue,"Elders' Council Chambers, Meeting Hall The amazons gather here weekly to present or listen to petitions from other citizens. A large chair is here, ornately decorated with symbols of war and inlaid with gold and gems. This is where the master battle trainer fo"
416,16,Barracks,lock_or_key_clue,Barracks To your south is a large door leading to Market Street.  North takes you further into the barracks.  Neatly made beds line the walls here. Slaves scurry around the room picking up clutter and repairing weapons or armour.
417,16,Market Street,command_clue|lock_or_key_clue,"Market Street Amazon warriors filter out of a large door to the north, and press past you busy with various tasks.  To the south is Market Street and the shops."
430,16,Temple Stairs,special_travel_clue,Temple Stairs You are standing at the foot of several sets of steps leading to an altar.  From here you have a good view of the twin pyramids that dominate the city.  A second story walkway connects them together. Light shines off the golden symbols decorating
431,16,Temple Stairs,command_clue|lock_or_key_clue|special_travel_clue,Temple Stairs You are standing at the foot of several sets of steps leading to an altar.  To the east is an open door.  From here you have a good view of the twin pyramids that dominate the city.  A second story walkway connects them together.  Light shines of
432,16,"Magic Shop, Front Room",command_clue|special_travel_clue,"Magic Shop, Front Room Various magical items are displayed in cases along the wall. Some look to be crafted here in the city, while others look like the spoils of war. A slave waits here to serve you. You notice a large sign on the counter here."
433,16,"Magic Shop, Back Room",special_travel_clue,"Magic Shop, Back Room A dark curtain separates this room from the front.  Here you will find rarer and more expensive magical artifacts.  A priestess sits at a table, considering you closely as you walk in."
444,16,Temple Stairs,special_travel_clue,Temple Stairs You are standing at the foot of several sets of steps leading to an altar.  From here you have a good view of the twin pyramids that dominate the city.  A second story walkway connects them together. Light shines off the golden symbols decorating
445,16,Temple Stairs,special_travel_clue,Temple Stairs You are standing at the foot of several sets of steps leading to an altar. From here you have a good view of the twin pyramids that dominate the city. A second story walkway connects them together. Light shines off the golden symbols decorating t
463,16,Temple Stairs,level_clue|special_travel_clue,Temple Stairs You are standing on the first level of steps leading to an altar. From here you have a good view of the twin pyramids that d
...[indexed chunk shortened for prompt safety]

Indexed knowledge 19:
[Other approved project text] Tools/Simulator/MajorMUD-Reforged-SimulatorPrototype/Evidence/RuntimePrep0006-NPCDialogueScriptTriggerQuestInteractionExtraction/Payload/RuntimePrep0006/DialogueScriptQuestCandidateMatrix_RuntimePrep0006.csv:1237-1278
2) Type DRAG USER <direction>.  You will drag the user through the exit
   to <direction>.  You will also go into drag mode after which all moves
   will drag the user along with you.

Note that you may move slower than normal while dragging someone."
wbbMMHLP.MSG,HL1T54,1603,wbbMMHLP.h,132,134,yes,general_text_message_candidate,11,Backstab bs
wbbMMHLP.MSG,HL1B54,1606,wbbMMHLP.h,133,135,yes,general_text_message_candidate,138,"If you have the ability, BACKSTAB may be used while hiding or sneaking to
get a chance at a hit which does a lot more damage than normal."
wbbMMHLP.MSG,HL1T55,1612,wbbMMHLP.h,134,136,yes,general_text_message_candidate,24,Practice Arena colliseum
wbbMMHLP.MSG,HL1B55,1615,wbbMMHLP.h,135,137,yes,dialogue_or_user_message_candidate;quest_or_progression_interaction_candidate;script_trigger_or_prompt_candidate,248,"If you are in the practice arena, you may fight with other players with
no fear of evil points or loss of life.  If you are killed in the arena
you will revive in the Halls Of The Dead with as many HP as you had the
last time you entered the arena."
wbbMMHLP.MSG,HL1T56,1623,wbbMMHLP.h,136,138,yes,general_text_message_candidate,7,Actions
wbbMMHLP.MSG,HL1B56,1626,wbbMMHLP.h,137,139,yes,script_trigger_or_prompt_candidate,253,"Type [37mACTION LIST[33m to list the actions available.

Type [37mACTION ON[33m to turn on the use of actions.

Type [37mACTION OFF[33m to turn off the use of actions.

If actions are turned off, you will not be able to do use any action
commands."
wbbMMHLP.MSG,HL1T57,1638,wbbMMHLP.h,138,140,yes,script_trigger_or_prompt_candidate,60,Weapons armour class armor class AC DR glance glancing blows
wbbMMHLP.MSG,HL1B57,1641,wbbMMHLP.h,139,141,yes,dialogue_or_user_message_candidate;quest_or_progression_interaction_candidate;script_trigger_or_prompt_candidate,1235,"[0;1;33mWEAPONS AND ARMOUR

[0mThe realm is a dangerous place, and no character will survive long without
protection. Most character will need a good weapon-- usually the more
expensive, the better, but try out many different ones until you find one that
seems right for you. One factor you must keep in mind is that all weapons have
a minimum strength, and anyone with a lower strength will suffer from slower
attacks. When you ready the weapon, if it says ""This weapon feels heavy in your
hands."", then you need more strength to wield it effectively.

Armour is important for everyone. The best armours are the heavier ones, but
often force your [0;1mencumbrance[0m up to uncomfortable levels. You should never
neglect to wear armour, and make sure you are wearing a piece of armour on
every location you can! Your AC/DR stat is a measure of how protected you are:
AC stands for Armour Class, and DR stands for Damage Reduction. AC makes you
harder to hi
...[indexed chunk shortened for prompt safety]

Indexed knowledge 20:
[Validation/report evidence] Simulator/DataSimVer/0004/RoomTextGateClues.csv:60-67
wcc_map1_room201,1,201,Thief Training Room,text_command_clue|key_lock_clue,"Thief Training Room A short, weaselly-looking man sits at a table here, nimbly twirling a dagger in his hand. As you enter, he swiftly stabs it down into the table's surface, and looks you up and down appraisingly. After a short moment, he stands up and says ""Well? What do you want?"" You can exit th",none
wcc_map1_room202,1,202,Thieves' Guild,text_command_clue|key_lock_clue|special_exit_clue,"Thieves' Guild This large, smoky room is filled with a half-dozen tables. Seated at these are scruffy-looking men playing various games of chance. As you approach, the room quiets down as everyone begins whispering, and you receive a few hard stares. You can exit through doorways to the south and we",none
wcc_map1_room203,1,203,Arena Entrance,text_command_clue|key_lock_clue,"Arena Entrance This is the entrance to the city's arena, where combatants come to hone their deadly skills, and where townsfolk come to be thus entertained. There are quite a few people here, and you have to push your way through the crowd at some points. You can continue into the heart of the arena",none
wcc_map1_room204,1,204,Arena Stands,level_gate_clue,"Arena Stands This is the upper level of the arena, where the townsfolk can observe the combatants. A small stairwell leads down to the fighting below. To your west lies a concession stand, where food and even some weaponry can be purchased for those who get inspired by viewing the battling warriors.",none
wcc_map1_room206,1,206,Arena Practice Room,text_command_clue|special_exit_clue,"Arena Practice Room This room is magicked with a powerful spell, enabling people to battle without receiving grievous injury. It is used by all to practice their skills, and to test their preference for weapons and the like. It is also used for those who wish to settle a grudge, but are not willing ",none
wcc_map1_room212,1,212,Intersection of Crown St. & Brass St.,text_command_clue|key_lock_clue,"Intersection of Crown St. & Brass St. This is a cobblestoned street. It is dimly lit by guttering lanterns hung from tall posts. It is relatively clean, and is quiet and fairly unpopulated. The buildings here are plain-looking and unornamented, but are well-made. Bridge street crosses the east-west ",none
wcc_map1_room214,1,214,Crown Street,text_command_clue,"Crown Street This is a cobblestoned street. It is dimly lit by guttering lanterns hung from tall posts. It is relatively clean, and is quiet and fairly unpopulated. The buildings here are plain-looking and unornamented, but are well-made. The street continues to the east and west.",none
wcc_map1_room215,1,215,Crown Street,text_command_clue,"Crown Street This is a cobblestoned street. It is dimly lit by guttering lanterns hung from tal
...[indexed chunk shortened for prompt safety]

## Compact Reforged bootstrap orientation

2 compact cards for General • Stale baseline

IMPORTANT: the repository changed after the bootstrap baseline. Use these cards only for architecture and routing. Verify all current implementation/status claims through fresh read-only evidence, current reports, and exact file/line citations.

Knowledge card `AI/Knowledge/Reforged/ProjectIdentity.md` (General):
# Reforged Project Identity

- MajorMUD Reforged is a standalone, data-driven Reforged Realms project with client, host/runtime, editor, Librarium, Guardian Hall, and AI-player capabilities.
- Classic parity is the protected first priority. Custom and AI worlds must not weaken or rewrite Classic behavior.
- The runtime remains authoritative for live behavior; Borin is a conversational, research, and approval-gated engineering layer.
- The Librarium preserves decoded provenance and identities. Generated `Data/Classic` feeds runtime catalogs and live consumers.
- Borin must distinguish discussion, decision, installation, successful build, and successful validation. One state never proves a later state.
- A project summary or memory is orientation only. Current implementation claims require fresh files, reports, logs, and exact evidence.

Knowledge card `AI/Knowledge/Reforged/AuthorityRules.md` (General):
# Authority Rules

1. Decoded WCC and required Worldgroup gameplay data are executable gameplay authority when traced into the Librarium, generated Classic data, runtime consumers, persistence/reset/cleanup, and parity validation.
2. Imported databases are reference-only. They may help identify records but never override decoded/runtime evidence.
3. Do not guess native or unresolved behavior. Mark it unknown, inferred, or suspected until evidence proves it.
4. Classic has no factions or reputation. `Guarded` is a specific protection behavior, not generic NPC alliance or assist logic.
5. Real server/gameplay observations are evidence, but strategy, player preference, character-specific behavior, and possible defects remain separately classified.
6. Current reports and successful validators outrank stale memory or old index results.
7. Do not push to GitHub before the project's stated Classic-parity and cleanup gate is satisfied.

---
This report preserves gathered evidence. Matching text is not proof that every runtime trace stage is complete.
