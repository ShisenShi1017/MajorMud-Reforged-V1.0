# Database Version 0006

Name: DAT Decode Correction and Continuation

Purpose:
Continue native `.DAT` analysis while tightening terminology so future work does not confuse skill IDs, ability IDs, flags, offsets, references, or lookup values with record counts.

Status:
Investigative decoder foundation. Not a complete binary importer yet.

Rule reinforced:
A value is only a record count when proven by header structure, file layout, or cross-check verification. Otherwise it must be labeled as a reference, flag, offset, field value, or unknown.
