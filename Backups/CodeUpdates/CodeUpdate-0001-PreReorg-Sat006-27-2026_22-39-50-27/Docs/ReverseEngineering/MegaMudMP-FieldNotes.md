# MegaMud MP Field Notes

## Observed structure

Typical file begins with a metadata line:

```text
[][Winterhawk]
```

Room/header lines look like:

```text
[AADE:Rhudaur:Abandoned Asylum, Dead End--9 1180]
```

Currently parsed as:

- code: `AADE`
- area: `Rhudaur`
- label: `Abandoned Asylum, Dead End`
- mapNumber: `9`
- roomNumber: `1180`

Route lines are colon-separated. Example:

```text
54300004:8DA00042:3:-1:0:::
54300004:0000:s
```

The first two fields often appear hexadecimal-like. Later fields may contain directions or commands. Numeric fields are intentionally preserved but not named yet.

## Evidence usage

MegaMud MP data helps verify:

- room names
- area names
- map number / room number references
- route commands
- possible navigation graph relationships

It does not replace native `.DAT` decoding.
