#!/usr/bin/env python3
"""
Migrate shop data from legacy JSON format to PostgreSQL ShopItems table.

Usage: python3 scripts/migrate_shop.py <postgres-dsn>

  e.g: python3 scripts/migrate_shop.py postgresql://user:pass@host/db

Requires: psycopg2 (pip install psycopg2-binary)
"""

import sys
import psycopg2

SHOP_DATA = {
    "1": [
        {"id": 1, "article": "Prize Winner de 3 jours", "price": "4"},
        {"id": 2, "article": "Prize Winner d'une semaine et <b>accès au deuxième palier</b>", "price": "6"},
        {"id": 3, "article": "Voice de 3 jours", "price": "6"},
        {"id": 4, "article": "Voice de 5 jours et <b>accès au deuxième palier</b>", "price": "8"},
    ],
    "2": [
        {"id": 5, "article": "Prize Winner d'une semaine", "price": "6"},
        {"id": 6, "article": "Voice de 5 jours", "price": "8"},
        {"id": 7, "article": "Prize Winner de 2 semaines et <b>accès au troisième palier</b>", "price": "10"},
        {"id": 8, "article": "Rôle customisé à intégrer dans nos parties de Loups-Garous", "price": "10"},
        {"id": 9, "article": "Voice d'une semaine et <b>accès au troisième palier</b>", "price": "12"},
    ],
    "3": [
        {"id": 10, "article": "Avatar & Bio sur Elsa-Mina", "price": "10"},
        {"id": 11, "article": "Prize Winner d'un mois", "price": "12"},
        {"id": 12, "article": "Prize Winner de 2 semaines sur Franais et <b>accès au quatrième palier</b>", "price": "14"},
        {"id": 13, "article": "Pokémon customisé dans le SSB Arcade", "price": "14"},
        {"id": 14, "article": "Voice de 2 semaines et <b>accès au quatrième palier</b>", "price": "16"},
    ],
    "4": [
        {"id": 15, "article": "Voice de 2 semaines", "price": "12"},
        {"id": 16, "article": "Badge de l'acheteur compulsif", "price": "14"},
        {"id": 17, "article": "Join Phrase customisée sur Arcade", "price": "16"},
        {"id": 18, "article": "Rôle spécial sur le Discord d'OFCS", "price": "16"},
        {"id": 19, "article": "Voice d'un mois", "price": "18"},
        {"id": 20, "article": "Prize Winner permanent", "price": "20"},
    ],
}

if len(sys.argv) != 2:
    print("Usage: python3 scripts/migrate_shop.py <postgres-dsn>")
    print("  e.g: python3 scripts/migrate_shop.py postgresql://user:pass@host/db")
    raise SystemExit(1)

conn = psycopg2.connect(sys.argv[1])
cur = conn.cursor()

rows = [
    (item["id"], tier, item["article"], item["price"])
    for tier, items in SHOP_DATA.items()
    for item in items
]

cur.executemany(
    'INSERT INTO "ShopItems" ("Id", "Tier", "Article", "Price") VALUES (%s, %s, %s, %s)',
    rows,
)

# Advance the sequence past the highest inserted id so new inserts don't conflict
max_id = max(item["id"] for items in SHOP_DATA.values() for item in items)
cur.execute(f'SELECT setval(pg_get_serial_sequence(\'"ShopItems"\', \'Id\'), {max_id})')

conn.commit()
cur.close()
conn.close()

print(f"Inserted {len(rows)} shop items.")
