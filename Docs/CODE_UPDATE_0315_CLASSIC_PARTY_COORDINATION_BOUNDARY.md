# Code Update 0315 — Pass 44 Classic Party Coordination Boundary

## Parity movement

- `native.party-coordination`: **UNKNOWN → STRONG**
- Score: **97 PASS / 85 STRONG / 18 UNKNOWN = 200**
- Strict parity: **48.5%**
- Evidence-backed parity: **91.0%**

## Exact native evidence connected

Pass 44 consolidates the retained WCCMMHLP payloads that already define the core Classic party shell:

- message 27: INVITE, UNINVITE, JOIN, LEAVE, PARTY, BACKRANK and FRONTRANK
- message 147: invited FOLLOW behavior, follower moves where the leader moves, and LEAVE/UNINVITE/explicit exit stop-following behavior
- message 153: SET FOLLOW BLIND/NORMAL room-description behavior
- message 217: LEAVE party/broadcast/gang command scope
- message 227: explicit SHARE PARTY/ROOM distribution, sender exclusion, hidden/RECEIVE-off exclusion, integer division and sender-retained remainder

Retained Pass 23/24 evidence also supports the six-character party cap and the reward boundary: NPC XP eligibility is tied to active combat with the exact NPC rather than party membership, and monster currency is not automatically divided by party membership.

## User gameplay observations retained as hypotheses

The current operator report says a leader leaving disbands the party; quests remain actor-command-local; death and disconnect do not create a MajorMUD party wait timer; ordinary room/teleport commands are actor-local unless a specific effect is party-wide; and PvP room attacks exclude party members. These observations are useful test targets, but Pass 44 does **not** label them exact native proof without an independent retained/native owner.

In particular, MegaMUD wait behavior is explicitly excluded from the MajorMUD server parity boundary.

## Runtime boundary

No speculative leader succession or wait timer is added. The existing live party path remains evidence-gated:

- six-member STRONG policy retained
- no invented invitation timeout
- directional follower movement remains active from exact native FOLLOW help
- ordinary follower explicit exit stops following
- multi-member leader LEAVE still fails closed with `party-leader-leave-native-unknown` pending exact proof
- unexpected session disappearance does not silently decide native disconnect membership cleanup
- party/key movement composes through resulting door state rather than inventing a party-wide key-transfer mechanic

## Still unresolved

- exact multi-member leader LEAVE/disband transition, cleanup ordering and native messages
- exact leader/default rank behavior and Frontrank spot formula
- unexpected linkdead/disconnect membership cleanup
- death membership cleanup ordering
- special scripted/spell teleport and resurrection propagation
- PvP room-spell party exclusion and exact admission ordering
- exact remaining party failure/rendering text

## Protected boundaries

- no `WCCMMUD.INI`
- Borin Trainer unchanged
- Custom1 Simple Editor unchanged
- WPF/UI fixes unchanged
- Pass 40 Shadow LOOK implementation preserved
- Pass 43 event boundary preserved
