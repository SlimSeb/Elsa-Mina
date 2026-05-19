#!/usr/bin/env python3
"""
Import teams from a teams.json file into the PostgreSQL database.

Usage: python import_teams.py <path/to/teams.json> <postgres-dsn>

  e.g: python import_teams.py teams.json postgresql://user:pass@host/db

The script is idempotent: re-running it will upsert Teams and skip
existing RoomTeams rows without error.
"""

import json
import sys
from datetime import datetime, timezone

import psycopg2

LOG_INTERVAL = 100


def log_progress(i, total, label="teams"):
    if i % LOG_INTERVAL == 0 or i == total:
        print(f"  ... {i}/{total} {label} processed")


def parse_date(date_str):
    """Parse a date string in DD/MM/YY format; returns a UTC datetime or None."""
    if not date_str:
        return None
    for fmt in ("%d/%m/%y", "%d/%m/%Y"):
        try:
            return datetime.strptime(date_str, fmt).replace(tzinfo=timezone.utc)
        except ValueError:
            continue
    print(f"  Warning: could not parse date '{date_str}', defaulting to epoch")
    return datetime(1970, 1, 1, tzinfo=timezone.utc)


def run():
    if len(sys.argv) != 3:
        print("Usage: python import_teams.py <path/to/teams.json> <postgres-dsn>")
        print("  e.g: python import_teams.py teams.json postgresql://user:pass@host/db")
        sys.exit(1)

    json_path = sys.argv[1]
    postgres_dsn = sys.argv[2]

    print(f"Teams JSON   : {json_path}")
    print(f"Postgres DSN : {postgres_dsn[:postgres_dsn.rfind('@') + 1]}***")
    print()

    with open(json_path, encoding="utf-8") as f:
        teams_data = json.load(f)

    total = len(teams_data)
    print(f"Loaded {total} teams from {json_path}")
    print()

    pg = psycopg2.connect(postgres_dsn)
    pc = pg.cursor()

    # ── 1. Collect all room IDs referenced by any team ────────────────────────
    all_room_ids = {room for entry in teams_data.values() for room in (entry.get("rooms") or [])}
    print(f"Ensuring {len(all_room_ids)} room(s) exist: {sorted(all_room_ids)}")
    for room_id in all_room_ids:
        pc.execute(
            'INSERT INTO public."Rooms" ("Id", "Title") VALUES (%s, %s) ON CONFLICT DO NOTHING',
            (room_id, room_id),
        )

    # ── 2. Insert Teams ────────────────────────────────────────────────────────
    print(f"\nInserting {total} Teams rows...")
    for i, (team_id, entry) in enumerate(teams_data.items(), 1):
        creation_date = parse_date(entry.get("date")) or datetime(1970, 1, 1, tzinfo=timezone.utc)
        team_json = json.dumps(entry.get("team") or [], ensure_ascii=False)

        pc.execute(
            '''INSERT INTO public."Teams" ("Id", "Name", "Author", "Link", "Format", "CreationDate", "TeamJson")
               VALUES (%s, %s, %s, %s, %s, %s, %s)
               ON CONFLICT ("Id") DO UPDATE
                   SET "Name"         = EXCLUDED."Name",
                       "Author"       = EXCLUDED."Author",
                       "Link"         = EXCLUDED."Link",
                       "Format"       = EXCLUDED."Format",
                       "CreationDate" = EXCLUDED."CreationDate",
                       "TeamJson"     = EXCLUDED."TeamJson"''',
            (
                entry.get("id", team_id),
                entry.get("name"),
                entry.get("author"),
                entry.get("link"),
                entry.get("tier"),
                creation_date,
                team_json,
            ),
        )
        log_progress(i, total)

    # ── 3. Insert RoomTeams ────────────────────────────────────────────────────
    room_team_pairs = [
        (entry.get("id", team_id), room)
        for team_id, entry in teams_data.items()
        for room in (entry.get("rooms") or [])
    ]
    total_rt = len(room_team_pairs)
    print(f"\nInserting {total_rt} RoomTeams rows...")
    for i, (team_id, room_id) in enumerate(room_team_pairs, 1):
        pc.execute(
            '''INSERT INTO public."RoomTeams" ("TeamId", "RoomId")
               VALUES (%s, %s)
               ON CONFLICT DO NOTHING''',
            (team_id, room_id),
        )
        log_progress(i, total_rt, "RoomTeams")

    # ── Done ──────────────────────────────────────────────────────────────────
    pg.commit()
    pg.close()
    print("\n✅ Import complete!")


if __name__ == "__main__":
    run()
