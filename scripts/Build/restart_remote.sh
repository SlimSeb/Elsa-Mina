#!/usr/bin/env bash
set -eu

cd ./ElsaV2/output

echo "Writing config file"
printf '%s' "$CONFIG_FILE" > config.json

# Match on the comm name (15-char kernel truncation of "ElsaMina.Console" = "ElsaMina.Consol").
# This only matches the actual binary, never the bash shell running this script.
PROC_PATTERN="ElsaMina.Consol"

echo "Checking existing instances of ElsaMina.Console"
if pids="$(pgrep "$PROC_PATTERN")"; then
  echo "Found PIDs: $pids"
  echo "Process details:"
  ps -p "$pids" -o pid,ppid,pgid,stat,args || true

  echo "Killing existing instances of ElsaMina.Console: $pids"
  kill "$pids" || true
  echo "Kill signal sent"

  kill_start=$SECONDS
  i=0
  while [[ "$i" -lt 20 ]]; do
    if ! pgrep "$PROC_PATTERN" > /dev/null; then
      echo "All instances stopped after $((SECONDS - kill_start))s"
      break
    fi
    sleep 0.5
    i=$((i + 1))
  done

  if pgrep "$PROC_PATTERN" > /dev/null; then
    echo "Force killing remaining instances"
    pkill -KILL "$PROC_PATTERN" || true
  fi
else
  echo "No existing instances found"
fi

echo "Setting permissions"
chmod u+x ElsaMina.Console

echo "Starting ElsaMina.Console"
setsid nohup ./ElsaMina.Console >> elsa.log 2>&1 < /dev/null &
NEW_PID=$!
echo "Launched with PID $NEW_PID"

sleep 5
echo "Checking if process is still running after 5s..."
if pgrep "$PROC_PATTERN" > /dev/null; then
  echo "ElsaMina.Console started successfully"
  pgrep "$PROC_PATTERN" | xargs -I{} ps -p {} -o pid,ppid,pgid,stat,args || true
else
  echo "Failed to start ElsaMina.Console"
  echo "Last 20 lines of elsa.log:"
  tail -20 elsa.log || true
  exit 1
fi
