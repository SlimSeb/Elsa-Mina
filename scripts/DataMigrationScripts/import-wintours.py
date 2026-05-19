#!/usr/bin/env python3
"""
Import legacy win-tournament JSON files into TournamentRecords.

Each file must be named  wintours_<roomid>.json  and contain a flat
JSON object mapping userId → winCount.

Since only win counts are available, TournamentsEnteredCount is set
equal to WinsCount (minimum lower bound). All other stats default to 0.
Existing rows are always overwritten.

Usage:
    python import-wintours.py <wintours_*.json ...> <postgres-dsn>
    python import-wintours.py --dir <folder> <postgres-dsn>

Examples:
    python import-wintours.py wintours_franais.json postgresql://u:p@host/db
    python import-wintours.py --dir ./legacy_data postgresql://u:p@host/db
"""

import sys
import os
import json
import glob
import argparse
import psycopg2

LOG_INTERVAL = 100


def log_progress(i, total, label="rows"):
    if i % LOG_INTERVAL == 0 or i == total:
        print(f"  ... {i}/{total} {label} processed")


def room_id_from_filename(path: str) -> str:
    """Extract room ID from a filename like wintours_<roomid>.json."""
    base = os.path.splitext(os.path.basename(path))[0]
    prefix = "wintours_"
    if not base.startswith(prefix):
        raise ValueError(
            f"File '{path}' does not match the expected pattern 'wintours_<roomid>.json'"
        )
    return base[len(prefix):]


def import_file(pc, path: str) -> int:
    room_id = room_id_from_filename(path)

    with open(path, "r", encoding="utf-8") as fh:
        data: dict = json.load(fh)

    if not isinstance(data, dict):
        raise ValueError(f"Expected a JSON object in '{path}', got {type(data).__name__}")

    print(f"\n── Room: {room_id}  ({len(data)} users)  [{path}]")

    # 1. Ensure the Room row exists
    pc.execute(
        'INSERT INTO public."Rooms" ("Id", "Title") VALUES (%s, %s) ON CONFLICT DO NOTHING',
        (room_id, room_id),
    )

    entries = list(data.items())
    total = len(entries)
    for i, (user_id, wins) in enumerate(entries, 1):
        wins = int(wins)

        # 2. Ensure Users row exists
        pc.execute(
            '''INSERT INTO public."Users" ("UserId", "LastSeenAction")
               VALUES (%s, 0)
               ON CONFLICT ("UserId") DO NOTHING''',
            (user_id,),
        )

        # 3. Ensure RoomUsers row exists (required by FK on TournamentRecords)
        pc.execute(
            '''INSERT INTO public."RoomUsers" ("Id", "RoomId", "Avatar", "Title", "JoinPhrase", "PlayTime")
               VALUES (%s, %s, NULL, NULL, NULL, '0 seconds'::interval)
               ON CONFLICT ("Id", "RoomId") DO NOTHING''',
            (user_id, room_id),
        )

        # 4. Upsert TournamentRecord
        pc.execute(
            '''INSERT INTO public."TournamentRecords"
                   ("UserId", "RoomId", "WinsCount", "TournamentsEnteredCount",
                    "RunnerUpCount", "ThirdPlaceCount", "PlayedGames", "WonGames")
               VALUES (%s, %s, %s, %s, 0, 0, 0, 0)
               ON CONFLICT ("UserId", "RoomId") DO UPDATE
                   SET "WinsCount"               = EXCLUDED."WinsCount",
                       "TournamentsEnteredCount" = GREATEST(
                           public."TournamentRecords"."TournamentsEnteredCount",
                           EXCLUDED."WinsCount"
                       )''',
            (user_id, room_id, wins, wins),
        )

        log_progress(i, total, "users")

    print(f"  ✔ {total} records upserted")

    return total


def run():
    parser = argparse.ArgumentParser(
        description="Import legacy wintours_<roomid>.json files into TournamentRecords."
    )
    parser.add_argument(
        "files",
        nargs="*",
        metavar="wintours_<roomid>.json",
        help="One or more JSON files to import.",
    )
    parser.add_argument(
        "--dir",
        metavar="FOLDER",
        help="Import all wintours_*.json files found in this folder.",
    )
    parser.add_argument(
        "postgres_dsn",
        metavar="postgres-dsn",
        help="PostgreSQL connection string, e.g. postgresql://user:pass@host/db",
    )
    args = parser.parse_args()

    # Collect all target files
    target_files: list[str] = list(args.files)
    if args.dir:
        found = sorted(glob.glob(os.path.join(args.dir, "wintours_*.json")))
        if not found:
            print(f"No wintours_*.json files found in '{args.dir}'", file=sys.stderr)
            sys.exit(1)
        target_files.extend(found)

    if not target_files:
        parser.print_help()
        sys.exit(1)

    # Mask password in log output
    dsn = args.postgres_dsn
    safe_dsn = dsn[: dsn.rfind("@") + 1] + "***" if "@" in dsn else dsn

    print(f"Postgres DSN : {safe_dsn}")
    print(f"Files        : {len(target_files)}")

    pg = psycopg2.connect(dsn)
    pc = pg.cursor()

    total_users = 0
    for path in target_files:
        try:
            total_users += import_file(pc, path)
        except Exception as exc:
            pg.rollback()
            print(f"\n❌ Error processing '{path}': {exc}", file=sys.stderr)
            pg.close()
            sys.exit(1)

    pg.commit()
    pg.close()
    print(f"\n✅ Done — {total_users} user entries processed across {len(target_files)} file(s).")


if __name__ == "__main__":
    run()
