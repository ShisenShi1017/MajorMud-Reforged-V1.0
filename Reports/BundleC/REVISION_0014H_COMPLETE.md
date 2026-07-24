# Bundle C Revision 0014H

Restores the transient `Discover(characterId, targetId)` compatibility API required by installed Bundle D consumers. The method is memory-only and does not restore persisted hidden discovery. Bundle C item SEARCH continues to call `ReplaceSearchResults`, so every search rerolls hidden items and replaces the previous item result set.
