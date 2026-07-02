# Import Staging

This folder is the reviewed import staging surface created by CodeUpdate-0051.

It compares current canonical Data/ files with static decode and SimulatorBuild0002 expected-model evidence. It is intentionally report-only.

## Important Boundary

These files do not promote fields, rewrite canonical data, certify runtime behavior, or replace original binary evidence. They only show what is ready to stage, what needs a diff, and what remains blocked by runtime-only behavior.

## Phone Handoff

Use Queues/PhoneDecodeImportHandoffTemplate.csv when working from Android ChatGPT. Fill only fields proven by binary-backed evidence. Leave uncertain behavior in the uncertainty column.
