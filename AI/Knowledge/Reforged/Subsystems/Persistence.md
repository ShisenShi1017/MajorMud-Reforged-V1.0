# Persistence, Reset, and Cleanup

- Every persistent behavior needs an explicit owner, serialization identity, load path, reset/cleanup interaction, and validation after restart or reconnect.
- Gang-house persistence applies only when the deed and upkeep requirements are satisfied.
- Hidden/discovered state, inventories, shops, ground contents, quests, and character state must not be blended into one generic persistence rule.
- A successful runtime action is not proof of persistence until the appropriate save/restart/reset test passes.
