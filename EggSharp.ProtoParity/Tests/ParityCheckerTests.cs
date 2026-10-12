namespace EggSharp.ProtoParity.Tests;

public sealed class ParityCheckerTests {
    private const string Baseline = """
        syntax = "proto2";
        package ei;

        message Root {
            optional string user_id = 1;
            optional Child child = 2 [default = 0];
            optional Kind kind = 3;
            oneof choice {
                uint32 count = 4;
                string label = 5;
            }
            message Child {
                repeated double values = 1;
            }
        }

        enum Kind {
            UNKNOWN_KIND = 0;
            FIRST = 1;
        }

        message Unreached {
            optional int32 dropped = 1;
        }
        """;

    private static IReadOnlyList<Violation> Diff(string head) =>
        ParityChecker.Check(ProtoParser.Parse(Baseline), ProtoParser.Parse(head), ["ei.Root"]);

    private static string Head(string from, string to) => Baseline.Replace(from, to, StringComparison.Ordinal);

    [Fact]
    public void IdenticalProto_HasNoViolations() {
        Assert.Empty(Diff(Baseline + "\nmessage Added { optional int32 x = 1; }\n"));
    }

    [Fact]
    public void ChangeOutsideRoots_IsIgnored() {
        Assert.Empty(Diff(Head("optional int32 dropped = 1;", "")));
    }

    [Fact]
    public void RemovedField_IsReported() {
        var violation = Assert.Single(Diff(Head("optional Kind kind = 3;", "")));
        Assert.Equal(ViolationKind.Removed, violation.Kind);
        Assert.Equal("ei.Root.kind", violation.Subject);
    }

    [Fact]
    public void RenamedField_IsReported() {
        var violations = Diff(Head("optional string user_id = 1;", "optional string account_id = 1;"));
        Assert.Contains(violations, v => v is { Kind: ViolationKind.Renamed, Subject: "ei.Root.user_id" });
    }

    [Fact]
    public void RenumberedField_IsReported() {
        var violation = Assert.Single(Diff(Head("optional string user_id = 1;", "optional string user_id = 9;")));
        Assert.Equal(ViolationKind.Renumbered, violation.Kind);
    }

    [Fact]
    public void ChangedFieldType_IsReported() {
        var violation = Assert.Single(Diff(Head("optional string user_id = 1;", "optional bytes user_id = 1;")));
        Assert.Equal(ViolationKind.TypeChanged, violation.Kind);
    }

    [Fact]
    public void RenamedOneof_IsReported() {
        var violations = Diff(Head("oneof choice {", "oneof pick {"));
        Assert.Contains(violations, v => v is { Kind: ViolationKind.Removed, Subject: "ei.Root.choice" });
        Assert.Contains(violations, v => v is { Kind: ViolationKind.TypeChanged, Subject: "ei.Root.label" });
    }

    [Fact]
    public void ChangedNestedFieldType_IsReported() {
        var violation = Assert.Single(Diff(Head("repeated double values = 1;", "repeated float values = 1;")));
        Assert.Equal("ei.Root.Child.values", violation.Subject);
    }

    [Fact]
    public void RemovedReachableMessage_IsReported() {
        var violations = Diff(Head("message Child {", "message Kid {").Replace("optional Child child", "optional Kid child", StringComparison.Ordinal));
        Assert.Contains(violations, v => v is { Kind: ViolationKind.Removed, Subject: "ei.Root.Child" });
    }

    [Fact]
    public void RemovedEnumValue_IsReported() {
        var violation = Assert.Single(Diff(Head("FIRST = 1;", "")));
        Assert.Equal(ViolationKind.Removed, violation.Kind);
        Assert.Equal("ei.Kind.FIRST", violation.Subject);
    }

    [Fact]
    public void RenamedEnumValue_IsReported() {
        var violations = Diff(Head("FIRST = 1;", "PRIMARY = 1;"));
        Assert.Contains(violations, v => v is { Kind: ViolationKind.Renamed, Subject: "ei.Kind.FIRST" });
    }

    [Fact]
    public void RenumberedEnumValue_IsReported() {
        var violation = Assert.Single(Diff(Head("FIRST = 1;", "FIRST = 2;")));
        Assert.Equal(ViolationKind.Renumbered, violation.Kind);
    }

    [Fact]
    public void CasingOnlyFieldRename_ChangesCSharpName() {
        var violations = Diff(Head("optional string user_id = 1;", "optional string userid = 1;"));
        Assert.Contains(violations, v => v is { Kind: ViolationKind.CSharpNameChanged, Detail: "property UserId became Userid" });
    }

    [Fact]
    public void SnakeCaseVariantWithSameCSharpName_OnlyReportsRename() {
        var violation = Assert.Single(Diff(Head("optional string user_id = 1;", "optional string user__id = 1;")));
        Assert.Equal(ViolationKind.Renamed, violation.Kind);
    }

    [Theory]
    [InlineData("ei_user_id", "Root", "EiUserId")]
    [InlineData("v2_items", "Root", "V2Items")]
    [InlineData("root", "Root", "Root_")]
    public void Property_FollowsGoogleProtobufNaming(string field, string message, string expected) {
        Assert.Equal(expected, CSharpNames.Property(field, message));
    }

    [Fact]
    public void Parser_ReadsNestedTypesAndOneofs() {
        var schema = ProtoParser.Parse(Baseline);
        var root = schema.Messages["ei.Root"];
        Assert.Equal("choice", Assert.Single(root.Oneofs));
        Assert.Equal("ei.Root.Child", root.Fields.Single(f => f.Name == "child").Type);
        Assert.Equal("ei.Kind", root.Fields.Single(f => f.Name == "kind").Type);
        Assert.Equal("choice", root.Fields.Single(f => f.Name == "label").Oneof);
    }
}
