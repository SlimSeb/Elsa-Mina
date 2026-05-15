#!/bin/bash
set -euo pipefail

CONFIG_FILE="$(dirname "$0")/../src/ElsaMina.Console/config.json"

if [ ! -f "$CONFIG_FILE" ]; then
    echo "config.json not found at $CONFIG_FILE"
    exit 1
fi

read_config() {
    python3 -c "import json,sys; print(json.load(open('$CONFIG_FILE')).get('$1',''))"
}

BUCKET=$(read_config S3BucketName)
ENDPOINT=$(read_config S3EndpointUrl)
ACCESS_KEY=$(read_config S3AccessKey)
SECRET_KEY=$(read_config S3SecretKey)

if [ -z "$BUCKET" ] || [ -z "$ENDPOINT" ] || [ -z "$ACCESS_KEY" ] || [ -z "$SECRET_KEY" ]; then
    echo "Missing S3 configuration in config.json"
    exit 1
fi

echo "Clearing bucket: $BUCKET"
echo "Endpoint: $ENDPOINT"
read -rp "Are you sure? This will delete all objects. [y/N] " confirm
if [[ "$confirm" != "y" && "$confirm" != "Y" ]]; then
    echo "Aborted."
    exit 0
fi

AWS_ACCESS_KEY_ID="$ACCESS_KEY" \
AWS_SECRET_ACCESS_KEY="$SECRET_KEY" \
aws s3 rm "s3://$BUCKET" \
    --recursive \
    --endpoint-url "$ENDPOINT"

echo "Bucket cleared."
