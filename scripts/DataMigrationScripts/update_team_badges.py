#!/usr/bin/env python3
"""
Update the IsTeamTournament column in the Badges table based on known badge ID substrings.

Usage: python update_team_badges.py <postgres-dsn>
  e.g: python update_team_badges.py postgresql://user:pass@host/db
"""

import sys
import psycopg2

TEAM_BADGE_SUBSTRINGS = (
    'bttbr',
    'pokelandtriforce',
    'pokelandpremierleague',
    'pokelandteamtournament',
    'pokelanddoubletournament',
    'extraligue',
    'bigbangsuperleague',
    'bbpl',
    'bbrt',
    'bbsl',
    'tournoidesrgions',
    'frenchcommunityleague',
    'frenchcommunitypremierleague',
    'bigbangpremierleague',
    'bigbangregionaltournament',
    'plpliv',
    'pltbr',
    'plttv',
    'burnedtowerteambattleroyal',
    'burnedtowerteambattleroyalii',
    'burnedtowerpremierleague',
    'burnedtowerteambatteroyalspecialedition',
    'pokelandpremierleaguev',
    'burnedtowerelitechampionship',
    'pokelandteambattleroyalii',
)


def is_team_tournament(badge_id: str) -> bool:
    return any(s in badge_id for s in TEAM_BADGE_SUBSTRINGS)


def run():
    if len(sys.argv) != 2:
        print("Usage: python update_team_badges.py <postgres-dsn>")
        print("  e.g: python update_team_badges.py postgresql://user:pass@host/db")
        sys.exit(1)

    dsn = sys.argv[1]
    print(f"Connecting to: {dsn[:dsn.rfind('@') + 1]}***")

    pg = psycopg2.connect(dsn)
    pc = pg.cursor()

    pc.execute('SELECT "Id", "RoomId", "IsTeamTournament" FROM public."Badges"')
    badges = pc.fetchall()
    print(f"Found {len(badges)} badge(s)\n")

    updated = 0
    for badge_id, room_id, current_value in badges:
        team = is_team_tournament(badge_id)
        if team == current_value:
            continue
        pc.execute(
            'UPDATE public."Badges" SET "IsTeamTournament" = %s WHERE "Id" = %s AND "RoomId" = %s',
            (team, badge_id, room_id),
        )
        updated += 1
        print(f"  [{room_id}] {badge_id}: IsTeamTournament = {team}")

    pg.commit()
    pg.close()
    print(f"\n✅ Done. {updated} badge(s) updated.")


if __name__ == "__main__":
    run()
