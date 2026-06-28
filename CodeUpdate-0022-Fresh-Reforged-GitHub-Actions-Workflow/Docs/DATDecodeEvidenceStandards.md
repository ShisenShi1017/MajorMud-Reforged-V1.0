# DAT Decode Evidence Standards

1. Do not name numeric fields until verified by cross-reference.
2. Preserve unknown bytes/fields in generated output.
3. Prefer field names like `unknown_u16_offset_0042` until the value is proven.
4. A field is considered proven only when it matches repeated records and known CSV/source data.
5. Full decoding is not complete until every byte in every record is either named or preserved.
