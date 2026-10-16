// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/config/test_truncation_config.py

using Nooa.Config;
using Nooa.Errors;

namespace Nooa.Tests.Config;

public class MediaCaptureConfigTests
{
    [Fact]
    public void Defaults_NoFieldsSet_HasUpstreamValue()
    {
        Assert.Equal(5, new MediaCaptureConfig().MaxAttachmentsPerExecution);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Validate_NotPositive_Throws(int value)
    {
        var ex = Assert.Throws<ConfigValidationException>(() => new MediaCaptureConfig { MaxAttachmentsPerExecution = value }.Validate());

        Assert.Equal(
            $"Invalid MediaCaptureConfig:\n  - media_capture.MaxAttachmentsPerExecution must be > 0, got {value}",
            ex.Message);
    }

    [Fact]
    public void Validate_Positive_Passes()
    {
        new MediaCaptureConfig { MaxAttachmentsPerExecution = 20 }.Validate();
    }
}
