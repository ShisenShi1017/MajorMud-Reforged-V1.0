# Code Update 0255 — Borin Canonical Terminal Prompt and Sensitive Trace Repair

## Scope

This cumulative repair includes Code Updates 0237 through 0255. It preserves the direct
LLamaSharp/llama.cpp brain, the GGUF model, AI configuration, knowledge corpus, credentials,
and all existing memory files.

## Proven terminal-state failure

The Play terminal and Borin use the same configured session (`borin-training-01`), but two
conditions allowed the screen and Borin's login state to disagree:

1. Omniverse may present `MAJORMUD:` as a partial prompt without CR/LF. The previous pattern
   accepted `[MAJORMUD]:` and the `[E] . Enter the Realm` menu line, but not the real plain
   `MAJORMUD:` spelling.
2. When the Play workspace connected the canonical session before Borin's player service was
   configured, the player service entered a waiting state but did not start its 150 ms partial
   screen monitor. Completed lines were still observed, while a newline-free prompt could remain
   visible indefinitely without being evaluated.

The uploaded telnet trace also confirmed that the canonical Borin session reached the MajorMUD
menu and that the terminal transport preserved partial ANSI output. No trace or credentials are
included in this update.

## Repair

- Recognize plain `MAJORMUD:`, ANSI-colored `MAJORMUD:`, the spaced `M A J O R  M U D` banner,
  and `[E] . Enter the Realm`.
- Accept both `HP=` and `HP:`/`H=` MajorMUD statline forms as verified in-game prompts.
- Start one idempotent login monitor whenever the canonical Play session is already connected
  during configuration.
- Start the same monitor when the canonical session reports `Connected`, even if the connection
  was created from the Play workspace rather than Borin's Connect & Login button.
- Stop the monitor on disconnect or fault.
- Continue reading `ClassicScreen`, `LastReceivedLine`, and `LivePromptText` from the exact
  configured session, so Forge and Play cannot maintain separate Borin login truth.

## Sensitive telnet trace repair

`SendSensitiveCommandAsync` already hid credentials from terminal history and command events, but
raw telnet traffic capture could still log the outbound bytes. Sensitive application writes now
produce only:

`TX-DATA [REDACTED] (<byte count> bytes)`

The bytes sent to the Host are unchanged. Protocol negotiation traffic remains available for
diagnostics.

Any older telnet trace created before this update should be treated as sensitive and deleted or
stored securely.

## Automated validation

The deterministic Borin harness now contains sixteen no-model scenarios. Two new scenarios verify:

- Real Omniverse partial/menu prompt recognition and flexible MajorMUD statline recognition.
- Sensitive telnet trace redaction without changing transmitted Host bytes.

The installer restores and builds the actual WPF project, runs all deterministic tests, publishes
to a clean staging directory, and mirrors the result to `Release\MajorMudReforgedApp` only after
the build and tests succeed.

## Pass 0018B repair

- Corrected ANSI normalization so escape sequences require an actual ESC byte. The earlier optional-ESC expression could strip the opening `[H` from ordinary MajorMUD statlines such as `[HP: 18/20 MA: 4/7]:`, preventing in-realm detection.
- Kept the colon-delimited and equals-delimited statline regression tests authoritative.
- Moved the trace-redaction depth decrement into the owning stream instance, eliminating compiler warning CS0197 without weakening trace redaction.
