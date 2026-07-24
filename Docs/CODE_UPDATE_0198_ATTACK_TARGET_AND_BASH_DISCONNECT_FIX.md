# Code Update 0198 — Full Attack Target and Bash Combat Fix

- Preserves `a` as an exact alias for `attack`, including multi-word targets such as `a kobold thief`.
- Routes `bash <monster>` through live combat instead of the door/object interaction engine.
- Prevents the unsupported world-object bash path from throwing and dropping the client session.
- Adds parser smoke checks for full monster names and bash combat routing.
- Corrects installer source validation so a successful build is not falsely reported as failed.
