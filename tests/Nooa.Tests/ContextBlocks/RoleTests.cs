// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/context_blocks/roles.py

using Nooa.ContextBlocks;

namespace Nooa.Tests.ContextBlocks;

public class RoleTests
{
    [Fact]
    public void Role_DefinesTheSixUpstreamRoles()
    {
        Assert.Equal(
            ["System", "User", "Assistant", "Tool", "RuntimeEvent", "Metadata"],
            Enum.GetNames<Role>());
    }
}
