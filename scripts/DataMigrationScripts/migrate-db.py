#!/usr/bin/env python3
"""
Migration script: SQLite (old schema) → PostgreSQL (new schema)

Usage: python migrate.py <path/to/room.sqlite3> <postgres-dsn>

The room ID is derived from the SQLite filename (without extension),
e.g. franais.sqlite3 → RoomId "franais".
"""

import sys
import os
import sqlite3
import psycopg2
from datetime import datetime, timezone

LOG_INTERVAL = 500  # print progress every N rows
MAX_PLAYTIME_SECONDS = 10 * 3_600  # 10 hours — only users above this are inserted into RoomUsers

def log_progress(i, total, label="rows"):
    if i % LOG_INTERVAL == 0 or i == total:
        print(f"  ... {i}/{total} {label} processed")

def run():
    if len(sys.argv) != 3:
        print("Usage: python migrate.py <path/to/db.sqlite3> <postgres-dsn>")
        print("  e.g: python migrate.py franais.sqlite3 postgresql://user:pass@host/db")
        sys.exit(1)

    SQLITE_PATH     = sys.argv[1]
    POSTGRES_DSN    = sys.argv[2]
    DEFAULT_ROOM_ID = os.path.splitext(os.path.basename(SQLITE_PATH))[0]

    print(f"SQLite path  : {SQLITE_PATH}")
    print(f"Room ID      : {DEFAULT_ROOM_ID}")
    print(f"Postgres DSN : {POSTGRES_DSN[:POSTGRES_DSN.rfind('@') + 1]}***")
    print()

    sqlite = sqlite3.connect(SQLITE_PATH)
    sqlite.row_factory = sqlite3.Row
    pg = psycopg2.connect(POSTGRES_DSN)

    sc = sqlite.cursor()
    pc = pg.cursor()

    now = datetime.now(timezone.utc)

    # ── 1. Ensure the default Room exists ─────────────────────────────────────
    print("Ensuring default room exists...")
    pc.execute(
        'INSERT INTO public."Rooms" ("Id", "Title") VALUES (%s, %s) ON CONFLICT DO NOTHING',
        (DEFAULT_ROOM_ID, DEFAULT_ROOM_ID)
    )

    # ── 2. Users  (userdata → Users) ──────────────────────────────────────────
    print("Migrating Users...")
    sc.execute("SELECT userid, name FROM userdata")
    rows = sc.fetchall()
    total = len(rows)
    print(f"  {total} userdata rows to insert")
    for i, row in enumerate(rows, 1):
        pc.execute(
            '''INSERT INTO public."Users" ("UserId", "UserName", "RegisterDate", "LastOnline", "LastSeenRoomId", "LastSeenAction")
               VALUES (%s, %s, NULL, NULL, NULL, 0)
               ON CONFLICT ("UserId") DO NOTHING''',
            (row["userid"], row["name"])
        )
        log_progress(i, total)

    # Patch RegisterDate from regdatecache
    print("  Patching RegisterDate from regdatecache...")
    sc.execute("SELECT userid, regdate FROM regdatecache")
    rows = sc.fetchall()
    total = len(rows)
    print(f"  {total} regdatecache rows to patch")
    for i, row in enumerate(rows, 1):
        if row["regdate"]:
            reg = datetime.fromtimestamp(int(row["regdate"]), tz=timezone.utc)
            pc.execute(
                'UPDATE public."Users" SET "RegisterDate" = %s WHERE "UserId" = %s',
                (reg, row["userid"])
            )
        log_progress(i, total)

    # Patch LastOnline / LastSeenAction from seens
    print("  Patching LastOnline/LastSeenAction from seens...")
    sc.execute("SELECT userid, action, instant FROM seens")
    rows = sc.fetchall()
    total = len(rows)
    print(f"  {total} seens rows to patch")
    for i, row in enumerate(rows, 1):
        action_int = 0
        try:
            action_int = int(row["action"]) if row["action"] is not None else 0
        except (ValueError, TypeError):
            pass
        last_seen = None
        try:
            if row["instant"]:
                last_seen = datetime.fromisoformat(str(row["instant"]))
                if last_seen.tzinfo is None:
                    last_seen = last_seen.replace(tzinfo=timezone.utc)
        except Exception:
            pass
        # Ensure user row exists (seens may reference users not in userdata)
        pc.execute(
            '''INSERT INTO public."Users" ("UserId", "LastSeenAction", "LastOnline", "LastSeenRoomId")
               VALUES (%s, %s, %s, NULL)
               ON CONFLICT ("UserId") DO UPDATE
                   SET "LastSeenAction" = EXCLUDED."LastSeenAction",
                       "LastOnline"     = EXCLUDED."LastOnline"''',
            (row["userid"], action_int, last_seen)
        )
        log_progress(i, total)

    # ── 3. RoomUsers  (userdata + titles + avatars + ontime + joinphrases) ────
    print("Migrating RoomUsers...")

    # Only users above the playtime threshold get a RoomUsers row — PLUS any
    # user who holds a badge, since BadgeHoldings has FK → RoomUsers(Id, RoomId)
    # and that FK cannot be satisfied by a Users-only row.
    sc.execute(f"SELECT userid FROM ontime WHERE CAST(ontime AS INTEGER) > {MAX_PLAYTIME_SECONDS}")
    active_user_ids: set[str] = {r[0] for r in sc.fetchall() if r[0]}
    print(f"  {len(active_user_ids)} users above {MAX_PLAYTIME_SECONDS}s playtime threshold")

    sc.execute("SELECT DISTINCT userid FROM badges")
    badge_holders: set[str] = {r[0] for r in sc.fetchall() if r[0]}
    extra = badge_holders - active_user_ids
    active_user_ids.update(extra)
    if extra:
        print(f"  Added {len(extra)} badge holder(s) below playtime threshold to RoomUsers (required by FK)")

    # Collect every user ID referenced across all old tables so the Users
    # table is fully populated and no downstream FK can break.
    all_user_ids: set[str] = set(active_user_ids)
    for table, col in [
        ("userdata",     "userid"),
        ("titles",       "userid"),
        ("avatars",      "userid"),
        ("joinphrases",  "userid"),
        ("badges",       "userid"),  # BadgeHoldings FK → RoomUsers
        ("seens",        "userid"),
        ("regdatecache", "userid"),
    ]:
        sc.execute(f"SELECT {col} FROM {table}")
        before = len(all_user_ids)
        all_user_ids.update(r[0] for r in sc.fetchall() if r[0])
        print(f"  Collected {len(all_user_ids) - before} new user IDs from '{table}' (total: {len(all_user_ids)})")

    # Ensure every known user exists in Users (parent of RoomUsers)
    print(f"  Upserting {len(all_user_ids)} unique users into Users table...")
    for i, uid in enumerate(all_user_ids, 1):
        pc.execute(
            '''INSERT INTO public."Users" ("UserId", "LastSeenAction")
               VALUES (%s, 0)
               ON CONFLICT ("UserId") DO NOTHING''',
            (uid,)
        )
        log_progress(i, len(all_user_ids), "users")

    # Supplemental data
    titles = {}
    sc.execute("SELECT userid, title FROM titles")
    for r in sc.fetchall():
        titles[r["userid"]] = r["title"]

    avatars = {}
    sc.execute("SELECT userid, avatar FROM avatars")
    for r in sc.fetchall():
        avatars[r["userid"]] = r["avatar"]

    ontimes = {}
    sc.execute(f"SELECT userid, ontime FROM ontime WHERE CAST(ontime AS INTEGER) > {MAX_PLAYTIME_SECONDS}")
    for r in sc.fetchall():
        try:
            ontimes[r["userid"]] = int(r["ontime"]) if r["ontime"] else 0
        except (ValueError, TypeError):
            ontimes[r["userid"]] = 0

    joinphrases = {}
    sc.execute("SELECT userid, joinphrase FROM joinphrases")
    for r in sc.fetchall():
        joinphrases[r["userid"]] = r["joinphrase"]

    # Insert RoomUsers only for active users (above playtime threshold)
    total = len(active_user_ids)
    print(f"  Inserting {total} RoomUsers rows (active users only)...")
    for i, uid in enumerate(active_user_ids, 1):
        seconds = ontimes.get(uid, 0)
        play_time = f"{seconds} seconds"
        pc.execute(
            '''INSERT INTO public."RoomUsers" ("Id", "RoomId", "Avatar", "Title", "JoinPhrase", "PlayTime")
               VALUES (%s, %s, %s, %s, %s, %s::interval)
               ON CONFLICT ("Id", "RoomId") DO UPDATE
                   SET "Avatar"     = EXCLUDED."Avatar",
                       "Title"      = EXCLUDED."Title",
                       "JoinPhrase" = EXCLUDED."JoinPhrase",
                       "PlayTime"   = EXCLUDED."PlayTime"''',
            (
                uid,
                DEFAULT_ROOM_ID,
                avatars.get(uid),
                titles.get(uid),
                joinphrases.get(uid),
                play_time,
            )
        )
        log_progress(i, total, "RoomUsers")

    # ── 4. Badges  (badgedata → Badges) ───────────────────────────────────────
    print("Migrating Badges...")
    sc.execute("SELECT badgeid, name, img, istrophy FROM badgedata")
    rows = sc.fetchall()
    total = len(rows)
    print(f"  {total} badge rows to insert")

    # Build known_badge_ids in the same pass to avoid a second query in step 5
    known_badge_ids: set[str] = set()
    for i, row in enumerate(rows, 1):
        pc.execute(
            '''INSERT INTO public."Badges" ("Id", "RoomId", "Name", "Image", "IsTrophy", "IsTeamTournament")
               VALUES (%s, %s, %s, %s, %s, false)
               ON CONFLICT ("Id", "RoomId") DO NOTHING''',
            (
                row["badgeid"],
                DEFAULT_ROOM_ID,
                row["name"],
                row["img"],
                bool(row["istrophy"]),
            )
        )
        known_badge_ids.add(row["badgeid"])
        log_progress(i, total)

    # ── 5. BadgeHoldings  (badges → BadgeHoldings) ────────────────────────────
    # Safety net: every badge holder must have a RoomUsers row regardless of
    # playtime. A user may hold a valid badge yet be absent from ontime entirely,
    # so we cannot rely on active_user_ids being complete. We upsert a minimal
    # RoomUsers row (0 playtime) here so the FK can never fail.
    print("  Ensuring all badge holders have a RoomUsers row...")
    sc.execute("SELECT DISTINCT userid FROM badges")
    for r in sc.fetchall():
        uid = r[0]
        if not uid:
            continue
        pc.execute(
            '''INSERT INTO public."Users" ("UserId", "LastSeenAction")
               VALUES (%s, 0)
               ON CONFLICT ("UserId") DO NOTHING''',
            (uid,)
        )
        pc.execute(
            '''INSERT INTO public."RoomUsers" ("Id", "RoomId", "Avatar", "Title", "JoinPhrase", "PlayTime")
               VALUES (%s, %s, NULL, NULL, NULL, '0 seconds'::interval)
               ON CONFLICT ("Id", "RoomId") DO NOTHING''',
            (uid, DEFAULT_ROOM_ID)
        )

    print("Migrating BadgeHoldings...")
    sc.execute("SELECT userid, badgeid FROM badges")
    rows = sc.fetchall()
    total = len(rows)
    print(f"  {total} badge holding rows to insert")
    skipped = 0
    for i, row in enumerate(rows, 1):
        # Only skip holdings whose badge never existed in badgedata.
        # SQLite had no FK enforcement so orphaned badge IDs could accumulate.
        if row["badgeid"] not in known_badge_ids:
            skipped += 1
            log_progress(i, total)
            continue
        pc.execute(
            '''INSERT INTO public."BadgeHoldings" ("BadgeId", "RoomId", "UserId")
               VALUES (%s, %s, %s)
               ON CONFLICT DO NOTHING''',
            (row["badgeid"], DEFAULT_ROOM_ID, row["userid"])
        )
        log_progress(i, total)
    if skipped:
        print(f"  Warning: skipped {skipped} BadgeHoldings row(s) with no matching badge in badgedata")

    # ── 6. AddedCommands  (addedcommands → AddedCommands) ─────────────────────
    print("Migrating AddedCommands...")
    sc.execute("SELECT id, content, by, created FROM addedcommands")
    rows = sc.fetchall()
    total = len(rows)
    print(f"  {total} added command rows to insert")
    for i, row in enumerate(rows, 1):
        created = None
        try:
            if row["created"]:
                created = datetime.fromisoformat(str(row["created"]))
                if created.tzinfo is None:
                    created = created.replace(tzinfo=timezone.utc)
        except Exception:
            pass
        created = created or datetime(1970, 1, 1, tzinfo=timezone.utc)
        pc.execute(
            '''INSERT INTO public."AddedCommands" ("Id", "RoomId", "Content", "Author", "CreationDate")
               VALUES (%s, %s, %s, %s, %s)
               ON CONFLICT ("Id", "RoomId") DO NOTHING''',
            (row["id"], DEFAULT_ROOM_ID, row["content"], row["by"], created)
        )
        log_progress(i, total)

    # ── 7. SavedPolls  (poll → SavedPolls) ────────────────────────────────────
    print("Migrating SavedPolls...")
    sc.execute("SELECT id, content FROM poll")
    rows = sc.fetchall()
    total = len(rows)
    print(f"  {total} poll rows to insert")
    for i, row in enumerate(rows, 1):
        pc.execute(
            '''INSERT INTO public."SavedPolls" ("RoomId", "Content", "EndedAt", "SavedRoomId")
               VALUES (%s, %s, %s, %s)
               ON CONFLICT DO NOTHING''',
            (DEFAULT_ROOM_ID, row["content"], now, DEFAULT_ROOM_ID)
        )
        log_progress(i, total)

    # ── Done ──────────────────────────────────────────────────────────────────
    pg.commit()
    sqlite.close()
    pg.close()
    print("✅ Migration complete!")


if __name__ == "__main__":
    run()