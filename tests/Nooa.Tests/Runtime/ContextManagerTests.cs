// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/runtime/test_context_api.py (store and protection rules)

using Nooa.Errors;
using Nooa.Runtime;

namespace Nooa.Tests.Runtime;

public class ContextManagerTests
{
    private static ContextManager WithProtected()
    {
        var manager = new ContextManager();
        manager.SetStaticProtected("system_prompt", () => "prompt");
        manager.SetDynamicProtected("state", () => "state");
        return manager;
    }

    [Fact]
    public void Set_PlainValue_GoesToVolatilePartition()
    {
        var manager = new ContextManager();

        manager.Set("notes", "hello");

        Assert.Equal("hello", manager.Get("notes"));
        Assert.False(manager.IsStatic("notes"));
    }

    [Fact]
    public void SetStatic_GoesToCacheablePartition()
    {
        var manager = new ContextManager();

        manager.SetStatic("rules", "r");

        Assert.True(manager.IsStatic("rules"));
    }

    [Fact]
    public void Get_Provider_NeedsResolvedValue()
    {
        var manager = new ContextManager();
        manager.SetDynamicProvider("clock", () => "now");

        Assert.Throws<DynamicNotResolvedException>(() => manager.Get("clock"));
        manager.UpdateResolved(new Dictionary<string, object?> { ["clock"] = "12:00" });
        Assert.Equal("12:00", manager.Get("clock"));
    }

    [Fact]
    public void Set_InvalidatesResolvedCache()
    {
        var manager = new ContextManager();
        manager.SetDynamicProvider("clock", () => "now");
        manager.UpdateResolved(new Dictionary<string, object?> { ["clock"] = "12:00" });

        manager.SetDynamicProvider("clock", () => "later");

        Assert.Throws<DynamicNotResolvedException>(() => manager.Get("clock"));
    }

    [Fact]
    public void Get_Missing_ThrowsKeyNotFound_AndGetOrDefaultReturnsDefault()
    {
        var manager = new ContextManager();

        Assert.Throws<KeyNotFoundException>(() => manager.Get("x"));
        Assert.Equal("d", manager.GetOrDefault("x", "d"));
    }

    [Fact]
    public void Set_Null_DisablesKeyAndRemovesUnprotectedBlock()
    {
        var manager = new ContextManager();
        manager.Set("a", "1");

        manager.Set("a", null);

        Assert.False(manager.Contains("a"));
        Assert.True(manager.IsDisabled("a"));
    }

    [Fact]
    public void Set_Null_OnProtectedBlock_OnlyDisables()
    {
        var manager = WithProtected();

        manager.Set("system_prompt", null);

        Assert.True(manager.Contains("system_prompt"));
        Assert.True(manager.IsDisabled("system_prompt"));
    }

    [Fact]
    public void ProtectedBlock_RefusesEveryModelFacingChange()
    {
        var manager = WithProtected();

        Assert.Throws<ProtectedBlockException>(() => manager.Set("system_prompt", "x"));
        Assert.Throws<ProtectedBlockException>(() => manager.SetStatic("system_prompt", "x"));
        Assert.Throws<ProtectedBlockException>(() => manager.SetDynamic("state", "x"));
        Assert.Throws<ProtectedBlockException>(() => manager.SetStaticProvider("state", () => "x"));
        Assert.Throws<ProtectedBlockException>(() => manager.SetDynamicProvider("state", () => "x"));
        Assert.Throws<ProtectedBlockException>(() => manager.Remove("state"));
        Assert.Throws<ProtectedBlockException>(() => manager.Pop("state"));
    }

    [Fact]
    public void ProtectedBlockException_NamesKeyAndOperation()
    {
        var manager = WithProtected();

        var e = Assert.Throws<ProtectedBlockException>(() => manager.Remove("state"));

        Assert.Equal("state", e.Key);
        Assert.Equal("remove", e.Operation);
        Assert.Equal("Cannot remove protected block: \"state\"", e.Message);
    }

    [Fact]
    public void RemoveProtected_RemovesTheBlockAndTheProtection()
    {
        var manager = WithProtected();

        manager.RemoveProtected("state");

        Assert.False(manager.Contains("state"));
        Assert.False(manager.IsProtected("state"));
        Assert.Throws<KeyNotFoundException>(() => manager.RemoveProtected("state"));
    }

    [Fact]
    public void SetProtectedValue_KeepsPartitionAndProtection()
    {
        var manager = WithProtected();

        manager.SetProtectedValue("system_prompt", "custom");

        Assert.Equal("custom", manager.Get("system_prompt"));
        Assert.True(manager.IsStatic("system_prompt"));
        Assert.True(manager.IsProtected("system_prompt"));
    }

    [Fact]
    public void Remove_Missing_Throws() =>
        Assert.Throws<KeyNotFoundException>(() => new ContextManager().Remove("x"));

    [Fact]
    public void Pop_ReturnsValueOrDefault_AndRemoves()
    {
        var manager = new ContextManager();
        manager.Set("a", "1");

        Assert.Equal("1", manager.Pop("a"));
        Assert.False(manager.Contains("a"));
        Assert.Equal("d", manager.Pop("a", "d"));
    }

    [Fact]
    public void Pop_Provider_ReturnsLastResolvedValue()
    {
        var manager = new ContextManager();
        manager.SetDynamicProvider("p", () => "x");
        manager.UpdateResolved(new Dictionary<string, object?> { ["p"] = "resolved" });

        Assert.Equal("resolved", manager.Pop("p"));
    }

    [Fact]
    public void DisableEnable_AreReversible_AndAllowedOnProtected()
    {
        var manager = WithProtected();

        manager.Disable("state", "other");

        Assert.Equal(new HashSet<string> { "state", "other" }, manager.Disabled());
        manager.Enable("state");
        Assert.True(manager.IsEnabled("state"));
        Assert.True(manager.IsDisabled("other"));
    }

    [Fact]
    public void Set_ReenablesKey()
    {
        var manager = new ContextManager();
        manager.Disable("a");

        manager.Set("a", "v");

        Assert.True(manager.IsEnabled("a"));
    }

    [Fact]
    public void Keys_AndRawItems_KeepInsertionOrder_AfterRemoval()
    {
        var manager = new ContextManager();
        manager.Set("a", 1);
        manager.Set("b", 2);
        manager.Set("c", 3);
        manager.Remove("a");
        manager.Set("d", 4);

        Assert.Equal(["b", "c", "d"], manager.Keys);
        Assert.Equal(["b", "c", "d"], manager.RawItems().Select(p => p.Key));
        Assert.Equal(3, manager.Count);
    }

    [Fact]
    public void RawItems_ExposeProvidersUnresolved()
    {
        var manager = new ContextManager();
        manager.SetStaticProvider("p", () => "x");

        var entry = Assert.Single(manager.RawItems()).Value;

        Assert.True(entry.IsDynamic);
        Assert.Equal("x", entry.Provider!());
    }
}
