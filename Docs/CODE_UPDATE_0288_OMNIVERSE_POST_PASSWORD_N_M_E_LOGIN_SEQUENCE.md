# Code Update 0288 — Omniverse Post-Password N → M → E Login Sequence

## Live finding

The Omniverse BBS login path has an additional native step after the password is submitted. The verified sequence is:

1. Submit Borin's password and press Enter.
2. Type `N` and press Enter.
3. Type `M` and press Enter.
4. Type `E` and press Enter.
5. Wait for the MajorMUD HP prompt before allowing gameplay or private client refreshes.

The previous controller implemented password → M → E and therefore skipped the required `N` step.

## Runtime change

`BorinPlayerResearch` now models a separate Omniverse-only post-password state. After the sensitive password send succeeds, it waits for new server output, sends the configured `N` selection through `ClientCommandOrigin.SystemLogin`, then continues through the existing prompt-driven `M` and `E` stages.

The extra `N` stage is selected only when the configured/connected target resolves to `OmniverseBbs`. Reforged-server and other login targets do not inherit this Omniverse-specific menu action.

The setting `omniversePostPasswordSelection` defaults to `N` and is normalized to one uppercase character, consistent with the existing `mainMenuSelection=M` and `majorMudMenuSelection=E` settings.

## Login isolation

Code Update 0287 remains authoritative: TCP Connected is not treated as in-game. Before the HP/game prompt is verified, only Human and SystemLogin traffic may pass. Omniverse never receives Reforged-only `clientdata` or `roomdata` probes.

## Regression coverage

The Borin character harness now includes `Omniverse post-password login sequence is N then M then E`, which asserts the configured sequence remains N → M → E and that lower-case configuration values normalize safely.
