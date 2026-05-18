#!/bin/bash
set -e

INPUT=$(cat)
COMMAND=$(echo "$INPUT" | jq -r '.tool_input.command // empty' 2>/dev/null || echo "")

if [ -z "$COMMAND" ]; then
  exit 0
fi

# Block destructive filesystem operations
if echo "$COMMAND" | grep -qE 'rm\s+-rf\s+[/~]'; then
  echo "BLOCKED: Destructive rm -rf on root or home directory" >&2
  exit 2
fi

# Block destructive git operations
if echo "$COMMAND" | grep -qE 'git\s+(push\s+--force|reset\s+--hard|clean\s+-fd)'; then
  echo "BLOCKED: Destructive git operation requires manual execution" >&2
  exit 2
fi

# Block database destructive operations
if echo "$COMMAND" | grep -qiE 'DROP\s+(TABLE|DATABASE|SCHEMA|INDEX)\s'; then
  echo "BLOCKED: Database destructive operation requires manual review" >&2
  exit 2
fi

# Warn on secrets in commands
if echo "$COMMAND" | grep -qiE '(api[_-]?key|secret|password|token)=\S+'; then
  echo "WARNING: Command may contain secrets — review before executing" >&2
  exit 2
fi

exit 0
