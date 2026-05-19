#!/bin/bash
set -euo pipefail

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

LIFECYCLE=$(cat << 'EOF'
{
  "Rules": [
    {
      "ID": "expire-tagged-objects",
      "Status": "Enabled",
      "Filter": {
        "Tag": {
          "Key": "expiry",
          "Value": "7d"
        }
      },
      "Expiration": {
        "Days": 7
      }
    }
  ]
}
EOF
)

echo "Applying lifecycle rule to bucket: $BUCKET"

AWS_ACCESS_KEY_ID="$ACCESS_KEY" \
AWS_SECRET_ACCESS_KEY="$SECRET_KEY" \
aws s3api put-bucket-lifecycle-configuration \
    --bucket "$BUCKET" \
    --lifecycle-configuration "$LIFECYCLE" \
    --endpoint-url "$ENDPOINT"

echo "Lifecycle rule applied. Objects tagged expiry=7d will be deleted after 7 days."