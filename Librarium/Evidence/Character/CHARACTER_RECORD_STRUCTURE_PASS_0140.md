# Character Record Structure Pass — CodeUpdate0140

## Scope

This pass begins Character semantic exhaustion at the record and persistence boundary. It does not restart Rooms or Trigger/Script/Quest work.

## Proven in this pass

- Three native user/save-family artifacts are present and byte-inventoried: `WCCUSERS.DAT`, default-install `wccuser2.dat`, and labeled `WBBUSER2.DAT`.
- Their exact sizes, SHA-256 hashes, 64-byte headers, block hashes, entropy, non-zero counts, and printable ASCII runs are preserved in the machine-readable report.
- The three artifacts are not byte-identical and differ in size, so they cannot be treated as interchangeable copies.
- The current Reforged runtime save model is JSON and is not evidence of the original native record layout.

## Runtime work

- Character saves now have a version-2 semantic extension that explicitly separates flags, timers, status effects, progression, derived statistics, resistances, saving throws, and runtime markers.
- Every promoted flag, timer, or status entry must carry an evidence classification.
- Save validation rejects duplicate semantic identifiers, invalid classifications, impossible HP/mana ranges, negative durations, invalid levels/experience, and unsupported save versions.
- Existing version-1 JSON saves remain readable because new semantic fields are optional and validation accepts versions 1 through 2.

## Not yet decoded

No native character field offset, bit, enumeration, checksum, record boundary, or padding byte is assigned a meaning in this pass. Those remain preserved unknown evidence. Assigning meanings requires record-boundary proof from native code, headers, labeled files, or controlled save deltas.

## Next Character pass

Build controlled cross-version record segmentation and differential evidence, then promote only proven or strongly correlated native field boundaries into the Librarium character schema.
