Combat Resolution & Defense Order
Source of truth: wccmmud.dll calculate_attack @0x0042b800 (the single function that resolves one swing) and its caller attack_user_monster @0x0042cfab. Our port: sql/phase4_combat-BBS.sql → calculate_attack(...) — a step-for-step replica, DLL-verified. Every defensive check for every attack (normal, backstab, bash, smash, kick) goes through this one function, so the order below is the whole story.

TL;DR order per swing: 1) attack-type modifiers → 2) HIT CHANCE (vs Armor Class) → 3) miss check → 4) crit → 5) damage roll → 6) DR (flat) → 7) DODGE → 8) bash/smash multiplier → 9) glance/finalize.

Hit-chance (AC) is checked first. Dodge is checked last — after damage and DR are already computed — as a separate roll that negates the whole hit. DR sits between them.

The exact sequence (one swing)
#	Step	DLL	What it does
0	Attack-type setup	switch(DAT_004877e4)	Sets accuracy modifier + pre-DR damage % + crit on/off based on which attack was used (see table below).
1	Pre-DR damage scaling	min=((mult+100)*min)/100	Applies the type's damage % to the weapon's min/max before anything else.
2	Hit roll	iVar3 = genrdn(1,100)	The d100 that decides hit/miss.
3	HIT CHANCE (Armor Class)	see formula	Computes the to-hit % from attacker accuracy vs the defender's AC + DV.
4	Miss check	if (roll < hit_chance)	Roll ≥ hit_chance → MISS, done. Nothing else is evaluated.
5	Critical	genrdn(0,100) < crit%	Crit doubles min→2×max, max→4×max. Disabled for bash/smash/backstab.
6	Damage roll	genrdn(0,(max-min)+1)+min	Rolls raw damage in [min, max].
7	DR (damage reduction)	damage -= DR/10	Flat subtraction (see DR section).
8	DODGE	if (dodge_bn>0 && genrdn(0,100) < dodge%)	Separate roll; on success damage = 0, outcome = DODGED, done.
9	bash/smash multiplier	dmg *= 3 / *= 5	Applied after DR (before/after dodge is moot — dodge already zeroed it).
10	Glance / finalize	if (dmg<=0 && was hit) → glance	Landed hit reduced to 0 by DR = a "glance" (hit, no damage), not a miss.
Key takeaway for the ordering question: there is no separate "armor check" and "dodge check" happening in parallel or in some other order. Armor Class is baked into the hit-chance (step 3, first). Dodge is a completely separate second defense rolled at the very end (step 8), and it can save a defender even on a swing that already hit and rolled full damage.

1. Hit chance (Armor Class) — checked FIRST
Normal attacks (types 1,2,3,5,6,7,8):
    hit_rating = (AV + acc_mod)^2 / 140
    hit_chance = 100 - (DV + AC)^2 / hit_rating          (clamped to 10..99)

Backstab / "quick" (type 4):
    hit_chance = AV - DV                                  (clamped to 10..99)
AV = attacker's Attack Value (fighter-struct [0]).
DV = defender's Defense/dodge-derived value (+0x04).
AC = defender's armor modifier (+0x08, ability/armor based).
acc_mod = per-attack-type accuracy modifier (step 0) plus the defender's imposed accuracy field (+0x248).
Roll is genrdn(1,100); hit if roll < hit_chance, else miss.
Note the backstab path ignores AC entirely and uses a flat AV − DV, which is why a successful stealth backstab lands so reliably.
2. DR — the "two contradicting things", resolved
DR is a FLAT subtraction, applied AFTER the damage roll and BEFORE dodge. It is not a percentage and it is not applied to the min/max before rolling.

DLL:  DAT_00495fd4 = (roll + min) - (defender[0xc] / 10)
The fighter-struct stores DR as damage_resistance × 10 (+0x0c). The formula divides by 10, so the net effect is: subtract the defender's DR stat, flat, once per hit.
Monster with damage_resistance = 5 → struct holds 50 → damage -= 50/10 = 5.
It happens at step 7 — after the damage roll (step 6), before the dodge roll (step 8).
If DR reduces damage to ≤ 0 on a hit that landed, the swing becomes a glance (hit_type 1: "you hit but do no damage"), not a miss and not a dodge.
So of your two sources: the correct one is "flat DR, subtract the DR value, after the damage roll." Any source claiming DR is a percentage, or that it scales the weapon's damage range before rolling, or that it's rolled together with dodge, is wrong for this engine.

3. Dodge — resolved LAST
if (AV < 9)            dodge_chance = 0            (attacker too weak → target can't "dodge")
else if (dodge_bn > 0) dodge_chance = (dodge_bn * 10) / (AV >> 3)      (cap 95)
if (attack_type == 4)  dodge_chance /= 5           (backstab is 5x harder to dodge)

dodge happens only if dodge_bn > 0:
    if (genrdn(0,100) < dodge_chance)  →  DODGED (damage = 0, outcome 3)
dodge_bn = defender's dodge bonus (+0x20). Only positive values give a dodge chance; a negative dodge_bn (blind/debuff) instead feeds a forced-hit path back in step 2.
Dodge scales inversely with attacker AV (AV >> 3 = AV ÷ 8): a higher-level/accuracy attacker is harder to dodge.
Dodge is rolled after hit-chance succeeded and after damage + DR were computed. It is the defender's "second chance" and fully negates the hit.
Against a backstab (type 4) the dodge chance is divided by 5 — backstabs are far harder to dodge than to be missed.
Attack types (DAT_004877e4) and how they change the defense math
Set by the command: cmd_punch=1, cmd_kick=2, cmd_jumpkick=3, cmd_backstab=4 (when it qualifies), cmd_attack=5, cmd_bash=6, cmd_smash=7, (8 = +125% special).

Type	Attack	Accuracy mod	Pre-DR dmg	Crit?	Special defense behavior
1	Punch	0	+0%	yes	Normal hit-chance + normal dodge.
2	Kick (lightning feet)	0	+33%	yes	Normal.
3	Jumpkick (flying feet)	0	+66%	yes	Normal.
4	Backstab	0	+0% here¹	no	Hit = AV − DV (ignores AC); dodge ÷ 5.
5	Normal weapon	0	+0%	yes	Normal.
6	Bash	−15	+10%	no	Normal hit-chance/dodge; ×3 damage after DR.
7	Smash	−25	+20%	no	Normal hit-chance/dodge; ×5 damage after DR.
8	Special (+125%)	−75	+125%	no	Normal hit-chance/dodge.
¹ Backstab's large damage comes from stealth-tier multipliers applied in the attack orchestrator, not from the type's pre-DR %. The defense math for a backstab is what's shown here.

"Order of defenses against a special attack": it is the same two-gate order as any attack — (1) hit-chance/AC first, then (2) dodge last — the attack type only changes the numbers fed into those two gates (accuracy penalty, and for backstab the AV−DV hit formula + dodge÷5). There is no extra, separate defense layer inserted for specials.

Backstab pre-conditions (before it's even a type-4)
cmd_backstab @0x0045156f decides whether the swing is a real backstab before combat math:

If the player is not hidden / already engaged (+0x5f6 == 0 && !(0x6f4 & 4)) → downgraded to a normal type-5 attack (no backstab).
Barehanded, or a weapon that has the backstab ability (item_has_ability(0x74)) → type-4 backstab.
A weapon without the backstab ability → "You cannot backstab with this weapon." → type-5.
Outcome codes (hit_type, returned by calculate_attack)
0 = miss · 1 = glance (hit, 0 damage — e.g. eaten by DR) · 2 = normal hit · 3 = dodged · 4 = critical.

Fighter-struct offset reference
Attacker (param_1[]): [0]=AV · [5]=damage descriptor · [6]/[7]=min/max damage · [0x91]=crit chance. Defender (param_2+): +0x04=DV · +0x08=AC modifier · +0x0c=DR ×10 · +0x20=dodge_bn · +0x248=imposed accuracy modifier.

Not part of damage
roll_hit_location @0x0042bb43 (Head/Torso/Arm/…) is message flavor only — the DLL sets a location + percentage but never reads it in the damage path. Hit location does not modify damage.