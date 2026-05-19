#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/../Build/shared.sh"

CONFIG_FILE="${CONFIG_FILE:-$SCRIPT_DIR/../../src/ElsaMina.Console/config.json}"

if [[ -f "$CONFIG_FILE" ]]; then
  CONN_STR=$(CONFIG_FILE="$CONFIG_FILE" python3 << 'PYEOF'
import json, os

def strip_jsonc(text):
    result = []
    i = 0
    in_string = False
    while i < len(text):
        c = text[i]
        if in_string:
            if c == '\\':
                result.append(c)
                i += 1
                if i < len(text):
                    result.append(text[i])
                    i += 1
            elif c == '"':
                result.append(c)
                in_string = False
                i += 1
            else:
                result.append(c)
                i += 1
        else:
            if c == '"':
                result.append(c)
                in_string = True
                i += 1
            elif text[i:i+2] == '//':
                while i < len(text) and text[i] != '\n':
                    i += 1
            else:
                result.append(c)
                i += 1
    return ''.join(result)

text = open(os.environ['CONFIG_FILE'], encoding='utf-8-sig').read()
print(json.loads(strip_jsonc(text)).get('ConnectionString', ''))
PYEOF
)
  if [[ -n "$CONN_STR" ]]; then
    # Parse key=value pairs from the connection string (e.g. Host=...;Database=...;Username=...;Password=...)
    parse_conn() { python3 -c "
import sys, re
cs = '$CONN_STR'
pairs = dict(re.split(r'=', p.strip(), 1) for p in cs.split(';') if '=' in p)
keys = {'host': ['Host','Server'], 'port': ['Port'], 'dbname': ['Database'], 'user': ['Username','User Id','User'], 'password': ['Password']}
for k, aliases in keys.items():
    for a in aliases:
        if a in pairs:
            print(f'{k}={pairs[a]}')
            break
"; }
    eval "$(parse_conn | sed 's/^/PGCONN_/')"
  fi
fi

DB_NAME="${DB_NAME:-${PGCONN_dbname:-elsamina}}"
DB_USER="${DB_USER:-${PGCONN_user:-postgres}}"
DB_HOST="${DB_HOST:-${PGCONN_host:-localhost}}"
DB_PORT="${DB_PORT:-${PGCONN_port:-5432}}"
DB_PASSWORD="${DB_PASSWORD:-${PGCONN_password:-}}"
BACKUP_DIR="${BACKUP_DIR:-$SCRIPT_DIR/../../backups}"
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
DUMP_FILE="$BACKUP_DIR/${DB_NAME}_${TIMESTAMP}.sql"
ZIP_FILE="${DUMP_FILE}.zip"

mkdir -p "$BACKUP_DIR"

echo "Dumping database '$DB_NAME'..."
PGPASSWORD="$DB_PASSWORD" pg_dump \
  -h "$DB_HOST" \
  -p "$DB_PORT" \
  -U "$DB_USER" \
  -F plain \
  --no-owner \
  --no-acl \
  "$DB_NAME" > "$DUMP_FILE"

echo "Compressing to $ZIP_FILE..."
zip -j "$ZIP_FILE" "$DUMP_FILE"
rm "$DUMP_FILE"

echo "Backup complete: $ZIP_FILE ($(du -sh "$ZIP_FILE" | cut -f1))"
