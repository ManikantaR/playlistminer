#!/bin/bash
set -e

INPUT=$(cat)
FILE=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty' 2>/dev/null || echo "")

if [ -z "$FILE" ] || [ ! -f "$FILE" ]; then
  exit 0
fi

# Format C# files with dotnet format
if [[ "$FILE" == *.cs ]]; then
  if command -v dotnet &>/dev/null; then
    # Find the nearest .csproj or .sln
    DIR=$(dirname "$FILE")
    while [ "$DIR" != "/" ]; do
      if ls "$DIR"/*.csproj 1>/dev/null 2>&1 || ls "$DIR"/*.sln 1>/dev/null 2>&1; then
        dotnet format "$DIR" --include "$FILE" --verbosity quiet 2>/dev/null || true
        break
      fi
      DIR=$(dirname "$DIR")
    done
  fi
fi

# Format TypeScript/JavaScript with Prettier
if [[ "$FILE" =~ \.(ts|tsx|js|jsx|json)$ ]]; then
  WEB_DIR=$(echo "$FILE" | grep -o '.*/web/' || echo "")
  if [ -n "$WEB_DIR" ] && [ -f "${WEB_DIR}node_modules/.bin/prettier" ]; then
    "${WEB_DIR}node_modules/.bin/prettier" --write "$FILE" 2>/dev/null || true
  fi
fi

exit 0
