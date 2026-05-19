#!/usr/bin/env python3
"""
Migrate arcade levels data from legacy JSON format to PostgreSQL ArcadeLevels table.

Usage: python3 scripts/migrate_arcade_levels.py <postgres-dsn>

  e.g: python3 scripts/migrate_arcade_levels.py postgresql://user:pass@host/db

Requires: psycopg2 (pip install psycopg2-binary)
"""

import sys
import psycopg2

ARCADE_LEVELS_DATA = {
    "2": [
        "Nultiprise",
        "Rethanonapkmn",
        "Zoyotte",
        "Le Dieunnotor",
        "Super Florizarre",
        "xKSr",
        "Frankl1",
        "tytyui",
        "spargowen",
        "kaideos",
        "Tuthur",
        "RudeLiees",
        "Azeriz",
        "l'0ursMart1",
        "Kindyy",
        "Leafwater claws",
        "Canard",
        "maxfrgiratina",
        "akotellete",
    ],
    "3": [
        "mimilimi",
        "CaoJie",
        "Uber45",
        "JojoOui",
        "Hats",
        "jaimelacrep",
        "Nowelle",
        "Zoyotte",
        "Kiki23",
        "Guysmash",
        "Despotia",
        "Mindnight",
        "blueflea.n",
        "BalooDanceur",
        "RoaringMoonlight2",
    ],
    "4": [
        "fraises des boas",
        "palapapop",
        "Les2BG",
        "Turtlek",
        "Lyna 氷",
        "Maybca",
        "RL",
        "Dragonillis",
        "Thorys",
        "Akeras",
        "Allan2004",
        "Sulfura152",
        "lemonstre1",
        "Giga㋛Chandelure",
        "tf",
        "MrPioupiou",
    ],
}

if len(sys.argv) != 2:
    print("Usage: python3 scripts/migrate_arcade_levels.py <postgres-dsn>")
    print("  e.g: python3 scripts/migrate_arcade_levels.py postgresql://user:pass@host/db")
    raise SystemExit(1)

conn = psycopg2.connect(sys.argv[1])
cur = conn.cursor()

rows = [
    (username, int(level))
    for level, usernames in ARCADE_LEVELS_DATA.items()
    for username in usernames
]

cur.executemany(
    'INSERT INTO "ArcadeLevels" ("Id", "Level") VALUES (%s, %s) ON CONFLICT ("Id") DO UPDATE SET "Level" = EXCLUDED."Level"',
    rows,
)

conn.commit()
cur.close()
conn.close()

print(f"Upserted {len(rows)} arcade level entries.")
