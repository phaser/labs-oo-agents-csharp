#!/usr/bin/env bash
# SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
# SPDX-License-Identifier: Apache-2.0
#
# Clone upstream labs-OO-Agents into reference/ (git-ignored) at the pinned
# commit. This script is the single source of the pin; docs/port-plan.md
# records why it is pinned there.
set -euo pipefail

UPSTREAM_URL="https://github.com/NVIDIA-NeMo/labs-OO-Agents"
UPSTREAM_SHA="1454b78c8f0a85c97eab1c6a91cc8cfc95b34de5"

cd "$(git rev-parse --show-toplevel)"

if [[ ! -d reference/.git ]]; then
    git clone "$UPSTREAM_URL" reference
fi
git -C reference fetch --quiet origin
git -C reference checkout --quiet "$UPSTREAM_SHA"
echo "reference/ is at $UPSTREAM_SHA"
