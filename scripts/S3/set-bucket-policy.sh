#!/bin/bash
set -euo pipefail

# OVH S3 does not support bucket policies (PutBucketPolicy returns NotImplemented).
# Setting the bucket ACL to "private" achieves the same goal: anonymous listing is denied
# while per-object PublicRead ACLs still allow individual files to be fetched by URL.

CONFIG_FILE="$(dirname "$0")/../../src/ElsaMina.Console/config.json"

if [ ! -f "$CONFIG_FILE" ]; then
    echo "config.json not found at $CONFIG_FILE"
    exit 1
fi

read_config() {
    tail -c +4 "$CONFIG_FILE" | grep -v '^\s*//' | jq -r ".$1 // empty"
}

BUCKET=$(read_config S3BucketName)
ENDPOINT=$(read_config S3EndpointUrl)
ACCESS_KEY=$(read_config S3AccessKey)
SECRET_KEY=$(read_config S3SecretKey)

if [ -z "$BUCKET" ] || [ -z "$ENDPOINT" ] || [ -z "$ACCESS_KEY" ] || [ -z "$SECRET_KEY" ]; then
    echo "Missing S3 configuration in config.json"
    exit 1
fi

echo "Setting bucket ACL to private for: $BUCKET"

AWS_ACCESS_KEY_ID="$ACCESS_KEY" \
AWS_SECRET_ACCESS_KEY="$SECRET_KEY" \
aws s3api put-bucket-acl \
    --bucket "$BUCKET" \
    --acl private \
    --endpoint-url "$ENDPOINT"

echo "Done. Bucket listing is now private; individual objects remain publicly readable by URL."
