#!/usr/bin/env python3
"""
Normalize the Id field in the ArcadeLevels table by applying ToLowerAlphaNum:
lowercase the string and strip all non-alphanumeric characters.

Usage: python3 scripts/normalize_arcade_level_ids.py <postgres-dsn>

  e.g: python3 scripts/normalize_arcade_level_ids.py postgresql://user:pass@host/db

Requires: psycopg2 (pip install psycopg2-binary)
"""

import re
import sys
import psycopg2


def to_lower_alpha_num(text: str) -> str:
    return re.sub(r"[^a-z0-9]", "", text.lower())


if len(sys.argv) != 2:
    print("Usage: python3 scripts/normalize_arcade_level_ids.py <postgres-dsn>")
    print("  e.g: python3 scripts/normalize_arcade_level_ids.py postgresql://user:pass@host/db")
    raise SystemExit(1)

conn = psycopg2.connect(sys.argv[1])
cur = conn.cursor()

cur.execute('SELECT "Id" FROM "ArcadeLevels"')
rows = cur.fetchall()

updated = 0
for (original_id,) in rows:
    normalized_id = to_lower_alpha_num(original_id)
    if normalized_id != original_id:
        cur.execute(
            'UPDATE "ArcadeLevels" SET "Id" = %s WHERE "Id" = %s',
            (normalized_id, original_id),
        )
        updated += 1

conn.commit()
cur.close()
conn.close()

print(f"Updated {updated}/{len(rows)} arcade level IDs.")