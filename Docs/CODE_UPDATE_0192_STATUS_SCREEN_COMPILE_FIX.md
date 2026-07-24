# Code Update 0192 — Status Screen Compile Fix

Fixes compiler error CS0103 in `RuntimeCharacterFoundation.cs` by relocating `TryReadInt` into `RuntimeCharacterCatalog`, the class that calls it from `ReadIntDictionary`.

This update is cumulative through Code Update 0191.
