# Classic Evil-Point Reputation and Redemption — Pass 29 / Update 0300

## Scope

This pass closes the numeric Evil-Point-to-reputation transition points and the default EP-forgiveness configuration values. It does **not** claim exact guard spawn cadence, bounty-hunter/divine actor ordering, FORGIVE transaction history, Lawful creation sequencing, or the wall-clock schedule that invokes daily cleanup.

## Recovered MajorMUD reference points

The archived MajorMUD legal-system documentation gives these reference ranges:

- Saint: `-220 <= EP < -200`
- Good: `-200 <= EP < -50`
- Neutral: `-50 <= EP < 30`
- Seedy: `30 <= EP < 40`
- Outlaw: `40 <= EP < 80`
- Criminal: `80 <= EP < 120`
- Villain: `120 <= EP < 210`
- Fiend: `210 <= EP < 300`
- At the 300-EP lockout boundary, the character remains Fiend and cannot initiate another attack until EP falls below 300.

The same documentation states that Evil Points are forgiven at cleanup, so decreasing EP moves the character back toward Good/Saint.

Public archival copy: `https://www.mudinfo.net/viewtopic.php?t=2509` (described there as pulled via Archive.org from Mudcentral.com).

## Native setup defaults

Retained WCCMMUD setup/message evidence independently preserves:

- `MAXEPDAY`: `Max EPs to forgive per day? 18`
- `EPCYCLE`: `Minutes per Evil Point Cycle? 60`
- `EPAMOUNT`: `EPs to forgive per cycle? 1`

The archived legal-system documentation gives the cleanup arithmetic already carried by Reforged:

- if an evil action occurred that day: reduce EP by 2;
- if no evil action occurred: `3 + floor(minutes_played / EPCYCLE) * EPAMOUNT`;
- cap the total forgiveness at `MAXEPDAY`;
- when current EP is negative, remove the documented base 2-point portion before progressing farther toward Good/Saint.

Pass 29 fixes the generated runtime value for `MAXEPDAY` from a descriptive string to executable integer `18`; the prior parser therefore no longer silently fell back to an unlimited daily cap.

## OmniMUD Classic corroboration supplied by the project owner

Observed display at `EP: 237`:

- current reputation: `FIEND`
- `Next: 28 to Villain`
- rate display: `1 EP per 60 mins online (max 18/day)`
- displayed bands include Seedy 30-39, Outlaw 40-79, Criminal 80-119, Villain 120-209, Fiend >=210.

This is **corroboration**, not native MajorMUD proof. It matches the recovered MajorMUD evil-side transition points and the WCCMMUD default cycle/cap values. Its `~28h` presentation is not used to replace the separately recovered MajorMUD cleanup formula or to invent a cleanup schedule.

## Enforcement source conflict retained

Native player help (`HL1B11`) says Villain is Criminal plus possible intervention by the Gods. The archived legal-system operator documentation instead describes Villain as adding bounty-hunter/monster pursuit and Fiend as adding divine lightning. Pass 29 records this conflict and does not activate a guessed additional enforcement actor. Numeric reputation transitions are independent of that unresolved actor/cadence question.
