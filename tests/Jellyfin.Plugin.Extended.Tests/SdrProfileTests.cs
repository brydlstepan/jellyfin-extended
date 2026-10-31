using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Jellyfin.Plugin.Extended.Tests;

public class SdrProfileTests
{
    [Fact]
    public void Rewrite_ReplacesExistingRangeConditionsAndKeepsOthers()
    {
        var result = Rewrite("""
            {"UserId":"u","DeviceProfile":{"CodecProfiles":[{"Type":"Video","Codec":"av1","Conditions":[
              {"Condition":"EqualsAny","Property":"VideoRangeType","Value":"SDR|HDR10|HLG"},
              {"Condition":"LessThanEqual","Property":"VideoLevel","Value":"19"}]}]}}
            """);

        var conditions = result["DeviceProfile"]!["CodecProfiles"]![0]!["Conditions"]!;
        Assert.Equal("SDR", (string?)conditions[0]!["Value"]);
        Assert.Equal("19", (string?)conditions[1]!["Value"]);
        Assert.Equal("u", (string?)result["UserId"]);
    }

    [Fact]
    public void Rewrite_AddsARuleForAppsWithNoRangeConditions()
    {
        var result = Rewrite("""{"DeviceProfile":{"Name":"ExoPlayer"}}""");

        var added = result["DeviceProfile"]!["CodecProfiles"]!.AsArray().Single()!;
        Assert.Equal("Video", (string?)added["Type"]);
        Assert.Null(added["Codec"]);
        Assert.Equal("VideoRangeType", (string?)added["Conditions"]![0]!["Property"]);
        Assert.Equal("SDR", (string?)added["Conditions"]![0]!["Value"]);
    }

    [Fact]
    public void Rewrite_HandlesCamelCaseClients()
    {
        var result = Rewrite("""
            {"deviceProfile":{"codecProfiles":[{"type":"Video","applyConditions":[
              {"condition":"EqualsAny","property":"videoRangeType","value":"HDR10"}]}]}}
            """);

        var profile = result["deviceProfile"]!;
        Assert.Equal("SDR", (string?)profile["codecProfiles"]![0]!["applyConditions"]![0]!["value"]);
        Assert.Equal(2, profile["codecProfiles"]!.AsArray().Count);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"UserId":"u"}""")]
    [InlineData("""{"DeviceProfile":null}""")]
    [InlineData("[]")]
    public void Rewrite_LeavesBodiesWithoutAProfileAlone(string json)
    {
        Assert.Null(SdrProfile.Rewrite(json));
    }

    [Fact]
    public void Rewrite_RejectsMalformedBodies()
    {
        Assert.ThrowsAny<JsonException>(() => SdrProfile.Rewrite("{not json"));
    }

    private static JsonNode Rewrite(string json) => JsonNode.Parse(SdrProfile.Rewrite(json)!)!;
}
