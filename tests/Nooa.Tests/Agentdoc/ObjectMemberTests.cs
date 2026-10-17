// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Agentdoc;

namespace Nooa.Tests.Agentdoc;

public class ObjectMemberTests
{
    [Fact]
    public void Constructor_SourceIsOptional()
    {
        var member = new ObjectMember("Name", 5);

        Assert.Equal(("Name", 5), (member.Name, member.Value));
        Assert.Null(member.Source);
    }

    [Fact]
    public void WriteMembers_WritesTheGivenNameAndMembers()
    {
        var writer = new StringWriter();

        new ValueRenderer(null, null, null, false).WriteMembers(writer, "Thing", [new ObjectMember("A", 1), new ObjectMember("B", "x")], 0);

        Assert.Equal("Thing { A = 1, B = \"x\" }", writer.ToString());
    }

    [Fact]
    public void WriteMembers_NoMembers_WritesAnEmptyBody()
    {
        var writer = new StringWriter();

        new ValueRenderer(null, null, null, false).WriteMembers(writer, "Thing", [], 0);

        Assert.Equal("Thing { }", writer.ToString());
    }

    [Fact]
    public void WriteMembers_MoreMembersThanTheLimit_ShowsAnEllipsis()
    {
        var writer = new StringWriter();

        new ValueRenderer(1, null, null, false).WriteMembers(writer, "T", [new ObjectMember("A", 1), new ObjectMember("B", 2)], 0);

        Assert.Equal("T { A = 1, ... }", writer.ToString());
    }
}
