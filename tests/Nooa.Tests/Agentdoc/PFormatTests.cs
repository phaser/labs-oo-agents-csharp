// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/agentdoc/test_pformat_cycles.py, test_pformat_head_tail.py, test_pformat_remove_budget.py, test_truncating_pformat.py

using System.Text.RegularExpressions;
using Nooa.Agentdoc;
using Nooa.Config;

namespace Nooa.Tests.Agentdoc;

public class PFormatTests
{
    private static string Range(int count, int? maxLength = null, bool expandAll = false) => PFormat.Format(Enumerable.Range(0, count).ToList(), maxLength: maxLength, expandAll: expandAll);

    // ---- cycles ----

    [Fact]
    public void Format_SelfReferentialList_ShowsCycle()
    {
        var x = new List<object>();
        x.Add(x);
        Assert.Equal("[<cycle>]", PFormat.Format(x));
    }

    [Fact]
    public void Format_SelfReferentialDictionary_ShowsCycle()
    {
        var d = new Dictionary<string, object>();
        d["self"] = d;
        Assert.Equal("{\"self\": <cycle>}", PFormat.Format(d));
    }

    [Fact]
    public void Format_TupleWithListPointingBack_ShowsCycle()
    {
        var list = new List<object>();
        var t = new Tuple<List<object>>(list);
        list.Add(t);
        Assert.Equal("([<cycle>])", PFormat.Format(t));
    }

    [Fact]
    public void Format_TwoListsPointingAtEachOther_ShowsCycle()
    {
        var a = new List<object>();
        var b = new List<object>();
        a.Add(b);
        b.Add(a);
        Assert.Equal("[[<cycle>]]", PFormat.Format(a));
    }

    [Fact]
    public void Format_DictionaryWithBackPointer_ShowsCycleAndKeepsValues()
    {
        var d = new Dictionary<string, object> { ["name"] = "outer", ["kids"] = new List<object>() };
        ((List<object>)d["kids"]).Add(d);
        var result = PFormat.Format(d);
        Assert.Contains("\"name\": \"outer\"", result, StringComparison.Ordinal);
        Assert.Contains("<cycle>", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_SharedReferences_AreNotCycles()
    {
        var shared = new List<int> { 1, 2, 3 };
        Assert.Equal("[[1, 2, 3], [1, 2, 3], [1, 2, 3]]", PFormat.Format(new List<object> { shared, shared, shared }));
        var sharedDict = new Dictionary<string, int> { ["a"] = 1 };
        Assert.Equal("{\"x\": {\"a\": 1}, \"y\": {\"a\": 1}}", PFormat.Format(new Dictionary<string, object> { ["x"] = sharedDict, ["y"] = sharedDict }));
    }

    [Fact]
    public void Format_CycleInsideTruncatedList_ShowsMarkerAndCycle()
    {
        var x = new List<object>();
        x.Add(x);
        var result = PFormat.Format(Enumerable.Repeat<object>(x, 100).ToList(), maxLength: 10);
        Assert.Contains("List<object>(len=100", result, StringComparison.Ordinal);
        Assert.Contains("<cycle>", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_CycleInsideTruncatedDictionary_ShowsMarkerAndCycle()
    {
        var x = new List<object>();
        x.Add(x);
        var d = Enumerable.Range(0, 50).ToDictionary(i => "k" + i, _ => (object)x);
        var result = PFormat.Format(d, maxLength: 4);
        Assert.Contains("Dictionary<string, object>(len=50, items=", result, StringComparison.Ordinal);
        Assert.Contains("<cycle>", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_CalledTwice_DoesNotLeakCycleState()
    {
        var x = new List<int> { 1, 2, 3 };
        Assert.Equal("[1, 2, 3]", PFormat.Format(x));
        Assert.Equal("[1, 2, 3]", PFormat.Format(x));
    }

    // ---- head and tail markers ----

    [Fact]
    public void Format_TruncatedList_UsesSliceMarker()
    {
        var result = Range(100, maxLength: 10);
        Assert.Equal("List<int>(len=100, [:5]=[0, 1, 2, 3, 4], [-5:]=[95, 96, 97, 98, 99])", result);
    }

    [Fact]
    public void Format_TruncatedList_ExcludesElidedItems()
    {
        Assert.DoesNotMatch(new Regex(@"\b194\b"), Range(200, maxLength: 10));
    }

    [Fact]
    public void Format_CompleteList_IsPlainLiteral()
    {
        Assert.Equal("[1, 2, 3]", PFormat.Format(new List<int> { 1, 2, 3 }, maxLength: 10));
    }

    [Fact]
    public void Format_ListWithExactlyMaxLength_HasNoMarker()
    {
        var result = Range(10, maxLength: 10);
        Assert.DoesNotContain("len=", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_ListOneOverMaxLength_HasMarker()
    {
        Assert.StartsWith("List<int>(len=11,", Range(11, maxLength: 10), StringComparison.Ordinal);
    }

    [Fact]
    public void Format_TruncatedArray_UsesArrayTypeName()
    {
        Assert.StartsWith("int[](len=100,", PFormat.Format(new int[100], maxLength: 10), StringComparison.Ordinal);
    }

    [Fact]
    public void Format_TruncatedTuple_UsesTupleMarker()
    {
        var tuple = Tuple.Create(1, 2, 3, 4, 5, 6, 7);
        Assert.Equal("tuple(len=7, [:2]=(1, 2), [-1:]=(7))", PFormat.Format(tuple, maxLength: 3));
    }

    [Fact]
    public void Format_CompleteTuple_IsPlain()
    {
        Assert.Equal("(1, \"a\")", PFormat.Format((1, "a")));
    }

    [Fact]
    public void Format_TruncatedDictionary_UsesItemsMarker()
    {
        var d = Enumerable.Range(0, 100).ToDictionary(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture), i => i);
        var result = PFormat.Format(d, maxLength: 10);
        Assert.StartsWith("Dictionary<string, int>(len=100, items={", result, StringComparison.Ordinal);
        Assert.EndsWith("})", result, StringComparison.Ordinal);
        Assert.Contains("\"0\"", result, StringComparison.Ordinal);
        Assert.Contains("\"99\"", result, StringComparison.Ordinal);
        Assert.Contains("...", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_CompleteDictionary_IsPlain()
    {
        Assert.Equal("{\"a\": 1, \"b\": 2}", PFormat.Format(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 }, maxLength: 10));
    }

    [Fact]
    public void Format_TruncatedSet_UsesItemsMarker()
    {
        var result = PFormat.Format(Enumerable.Range(0, 100).ToHashSet(), maxLength: 10);
        Assert.Equal("HashSet<int>(len=100, items={0, 1, 2, 3, 4, 5, 6, 7, 8, 9, ...})", result);
    }

    [Fact]
    public void Format_CompleteSet_IsPlain()
    {
        Assert.Equal("{1, 2, 3}", PFormat.Format(new SortedSet<int> { 1, 2, 3 }, maxLength: 10));
    }

    [Fact]
    public void Format_EmptyCollections_ShowBrackets()
    {
        Assert.Equal("[]", PFormat.Format(new List<int>()));
        Assert.Equal("{}", PFormat.Format(new Dictionary<string, int>()));
    }

    [Fact]
    public void Format_MaxLengthOne_ShowsHeadOnly()
    {
        Assert.Equal("List<int>(len=100, [:1]=[0])", Range(100, maxLength: 1));
    }

    [Fact]
    public void Format_MaxLengthTwo_ShowsHeadAndTail()
    {
        Assert.Equal("List<int>(len=100, [:1]=[0], [-1:]=[99])", Range(100, maxLength: 2));
    }

    [Fact]
    public void Format_NoMaxLength_ShowsAllItems()
    {
        Assert.Contains("199", Range(200), StringComparison.Ordinal);
    }

    [Fact]
    public void Format_ExpandedList_WritesOneChunkPerLine()
    {
        var result = Range(20, maxLength: 6, expandAll: true);
        Assert.StartsWith("List<int>(len=20,\n    [:3]=[\n        0,\n", result, StringComparison.Ordinal);
        Assert.Contains("\n    [-3:]=[\n        17,\n        18,\n        19,\n    ],\n)", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_ExpandedDictionary_KeepsMarkerShape()
    {
        var d = Enumerable.Range(0, 20).ToDictionary(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture), i => i);
        var result = PFormat.Format(d, maxLength: 6, expandAll: true);
        Assert.StartsWith("Dictionary<string, int>(len=20, items={\n    \"0\": 0,\n", result, StringComparison.Ordinal);
        Assert.Contains("    ...\n", result, StringComparison.Ordinal);
        Assert.EndsWith("    \"19\": 19,\n})", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_LongCompleteList_WritesOneItemPerLine()
    {
        var items = Enumerable.Range(0, 60).Select(i => "item-" + i).ToList();
        var result = PFormat.Format(items);
        Assert.StartsWith("[\n    \"item-0\",\n", result, StringComparison.Ordinal);
        Assert.EndsWith("    \"item-59\",\n]", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_NestedContainers_UseOwnMaxLength()
    {
        var outer = Enumerable.Range(0, 50).Select(i => new List<int> { i, i + 1 }).ToList();
        var result = PFormat.Format(outer, maxLength: 10);
        Assert.Contains("List<List<int>>(len=50,", result, StringComparison.Ordinal);
        Assert.Contains("[0, 1]", result, StringComparison.Ordinal);
        Assert.Contains("[49, 50]", result, StringComparison.Ordinal);
    }

    // ---- limits ----

    [Theory]
    [InlineData(0, null, null)]
    [InlineData(null, -1, null)]
    [InlineData(null, null, 0)]
    public void Format_NonPositiveLimit_Throws(int? maxLength, int? maxString, int? maxDepth)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PFormat.Format(1, maxLength, maxString, maxDepth));
    }

    [Fact]
    public void Format_FormatConfig_AppliesItsLimits()
    {
        var config = new FormatConfig { MaxString = 10, MaxLength = 2, MaxDepth = 3 };
        Assert.Equal("List<int>(len=5, [:1]=[1], [-1:]=[5])", PFormat.Format(new List<int> { 1, 2, 3, 4, 5 }, config));
    }

    [Fact]
    public void Format_UnlimitedFormatConfig_HasNoMarkers()
    {
        var config = new FormatConfig { MaxString = null, MaxLength = null, MaxDepth = null };
        Assert.DoesNotContain("len=", PFormat.Format(Enumerable.Range(0, 500).ToList(), config), StringComparison.Ordinal);
    }

    [Fact]
    public void Format_UnquoteStrings_ReturnsUntruncatedStringAsIs()
    {
        Assert.Equal("a\"b\nc", PFormat.Format("a\"b\nc", unquoteStrings: true));
    }

    [Fact]
    public void Format_UnquoteStringsOverLimit_KeepsMarker()
    {
        Assert.StartsWith("string(len=20,", PFormat.Format(new string('x', 20), maxString: 10, unquoteStrings: true), StringComparison.Ordinal);
    }

    [Fact]
    public void Format_VeryDeepNesting_DoesNotOverflowTheStack()
    {
        object nested = 1;
        for (var i = 0; i < 20_000; i++)
        {
            nested = new List<object> { nested };
        }

        Assert.Contains("[List<object>: 1 items]", PFormat.Format(nested), StringComparison.Ordinal);
    }

    // ---- TruncatingFormat ----

    [Fact]
    public void TruncatingFormat_SmallValue_PassesThrough()
    {
        var result = PFormat.TruncatingFormat(new List<int> { 1, 2, 3 }, maxChars: 1000);
        Assert.Equal("[1, 2, 3]", result);
    }

    [Fact]
    public void TruncatingFormat_LargeValue_UsesNotice()
    {
        var result = PFormat.TruncatingFormat(Enumerable.Range(0, 100_000).ToList(), maxChars: 200);
        Assert.StartsWith("<truncated-output>", result, StringComparison.Ordinal);
        Assert.Contains("Output too large", result, StringComparison.Ordinal);
        Assert.Contains("chars not shown", result, StringComparison.Ordinal);
        Assert.Contains("Showing first", result, StringComparison.Ordinal);
    }

    [Fact]
    public void TruncatingFormat_LargeValue_KeepsHeadAndTail()
    {
        var big = new List<string> { "START_UNIQUE_MARKER" };
        big.AddRange(Enumerable.Repeat("x", 10_000));
        big.Add("END_UNIQUE_MARKER");
        var result = PFormat.TruncatingFormat(big, maxChars: 300);
        Assert.Contains("START_UNIQUE_MARKER", result, StringComparison.Ordinal);
        Assert.Contains("END_UNIQUE_MARKER", result, StringComparison.Ordinal);
    }

    [Fact]
    public void TruncatingFormat_String_PassesThroughVerbatim()
    {
        var big = "START" + new string('x', 10_000) + "END";
        Assert.Equal(big, PFormat.TruncatingFormat(big, maxChars: 100));
    }

    [Fact]
    public void TruncatingFormat_NoCap_FormatsEverything()
    {
        Assert.Contains("999", PFormat.TruncatingFormat(Enumerable.Range(0, 1000).ToList()), StringComparison.Ordinal);
    }

    [Fact]
    public void TruncatingFormat_ManyItems_StaysSmall()
    {
        var result = PFormat.TruncatingFormat(Enumerable.Range(0, 1_000_000).ToList(), maxChars: 1000);
        Assert.True(result.Length < 10_000);
    }

    [Fact]
    public void TruncatingFormat_NonPositiveCap_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PFormat.TruncatingFormat(1, maxChars: 0));
    }
}
