using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Jellyfin.Plugin.Extended;

/// <summary>
/// Rewrites a device's playback request to ask for SDR.
/// </summary>
public static class SdrProfile
{
    private const string RangeType = "VideoRangeType";

    /// <summary>
    /// Limits a PlaybackInfo body's device profile to SDR.
    /// </summary>
    /// <param name="json">The request body.</param>
    /// <returns>The new body, or null when it has no device profile to change.</returns>
    /// <exception cref="JsonException">The body is malformed.</exception>
    public static string? Rewrite(string json)
    {
        if (JsonNode.Parse(json) is not JsonObject body)
        {
            throw new JsonException("The body is not an object.");
        }

        var profile = Get(body, "DeviceProfile");
        if (profile is null)
        {
            return null;
        }

        if (profile is not JsonObject profileObject)
        {
            throw new JsonException("The device profile is not an object.");
        }

        var key = KeyOf(profileObject, "CodecProfiles") ?? "CodecProfiles";
        var codecProfiles = profileObject[key] switch
        {
            null => [],
            JsonArray array => array,
            _ => throw new JsonException("The codec profiles are not a list.")
        };
        profileObject[key] ??= codecProfiles;

        foreach (var codecProfile in codecProfiles.OfType<JsonObject>())
        {
            SetRangeConditions(Get(codecProfile, "Conditions"));
        }

        // This covers every codec, including apps like ExoPlayer that send no range conditions at all.
        codecProfiles.Add(new JsonObject
        {
            ["Type"] = "Video",
            ["Conditions"] = new JsonArray(new JsonObject
            {
                ["Condition"] = "EqualsAny",
                ["Property"] = RangeType,
                ["Value"] = "SDR",
                ["IsRequired"] = false
            })
        });

        return body.ToJsonString();
    }

    // These also decide what a transcode outputs, so an allowed list that includes HDR has to go.
    private static void SetRangeConditions(JsonNode? conditions)
    {
        if (conditions is not JsonArray array)
        {
            return;
        }

        foreach (var condition in array.OfType<JsonObject>())
        {
            if (IsString(Get(condition, "Property"), RangeType)
                && (IsString(Get(condition, "Condition"), "EqualsAny") || IsString(Get(condition, "Condition"), "Equals")))
            {
                condition[KeyOf(condition, "Value") ?? "Value"] = "SDR";
            }
        }
    }

    private static bool IsString(JsonNode? node, string expected)
        => node is JsonValue value
            && value.GetValueKind() == JsonValueKind.String
            && string.Equals(value.GetValue<string>(), expected, StringComparison.OrdinalIgnoreCase);

    // ASP.NET binds these names case-insensitively, so clients may send any casing.
    private static string? KeyOf(JsonObject parent, string name)
        => parent.Select(p => p.Key).FirstOrDefault(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));

    private static JsonNode? Get(JsonObject parent, string name)
        => KeyOf(parent, name) is { } key ? parent[key] : null;
}
