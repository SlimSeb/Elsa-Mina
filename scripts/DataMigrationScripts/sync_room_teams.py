#!/usr/bin/env python3
"""
Sync RoomTeams between "franais" and "arcade":
- For every row with RoomId = "franais", insert a mirrored row with RoomId = "arcade" (if not already present).
- For every row with RoomId = "arcade", insert a mirrored row with RoomId = "franais" (if not already present).
"""

import psycopg2
import sys

CONNECTION_STRING = input("Enter PostgreSQL connection string (e.g. postgresql://user:pass@host/db): ").strip()

ROOM_A = "franais"
ROOM_B = "arcade"

conn = psycopg2.connect(CONNECTION_STRING)
conn.autocommit = False

try:
    with conn.cursor() as cur:
        # Fetch all relevant rows
        cur.execute(
            'SELECT "TeamId", "RoomId" FROM "RoomTeams" WHERE "RoomId" IN (%s, %s)',
            (ROOM_A, ROOM_B),
        )
        rows = cur.fetchall()

    existing = {(team_id, room_id) for team_id, room_id in rows}

    to_insert = []
    for team_id, room_id in rows:
        if room_id == ROOM_A:
            mirror = (team_id, ROOM_B)
        else:
            mirror = (team_id, ROOM_A)

        if mirror not in existing and mirror not in to_insert:
            to_insert.append(mirror)

    if not to_insert:
        print("Nothing to insert.")
        conn.rollback()
        sys.exit(0)

    with conn.cursor() as cur:
        cur.executemany(
            'INSERT INTO "RoomTeams" ("TeamId", "RoomId") VALUES (%s, %s) ON CONFLICT DO NOTHING',
            to_insert,
        )

    conn.commit()
    print(f"Inserted {len(to_insert)} row(s):")
    for team_id, room_id in to_insert:
        print(f"  TeamId={team_id!r}  RoomId={room_id!r}")

except Exception as exc:
    conn.rollback()
    print(f"Error: {exc}", file=sys.stderr)
    sys.exit(1)
finally:
    conn.close()