#!/usr/bin/env bash
# SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
# SPDX-License-Identifier: Apache-2.0
#
# Fail if a C# source file has no SPDX license header in its first 5 lines.
# Checks tracked and untracked, non-ignored files (same rule as upstream
# scripts/check_license_headers.py).
set -euo pipefail

cd "$(git rev-parse --show-toplevel)"

missing=0
while IFS= read -r -d '' file; do
    if ! head -n 5 "$file" | grep -q 'SPDX-License-Identifier'; then
        echo "missing SPDX header: $file"
        missing=1
    fi
done < <(git ls-files --cached --others --exclude-standard -z -- '*.cs' '*.sh')

exit "$missing"
