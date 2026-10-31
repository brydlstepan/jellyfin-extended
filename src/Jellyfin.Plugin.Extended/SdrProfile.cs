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
    public static string? Rewrite(string json)
    {
        if (JsonNode.Parse(json) is not JsonObject body
            || Get(body, "DeviceProfile") is not JsonObject profile)
        {
            return null;
        }

        if (Get(profile, "CodecProfiles") is not JsonArray codecProfiles)
        {
            codecProfiles = [];
            profile["CodecProfiles"] = codecProfiles;
        }

        foreach (var codecProfile in codecProfiles.OfType<JsonObject>())
        {
            SetRangeConditions(Get(codecProfile, "Conditions"));
            SetRangeConditions(Get(codecProfile, "ApplyConditions"));
        }

        // Apps like ExoPlayer send no range conditions at all, so add one that covers every codec.
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

    private static void SetRangeConditions(JsonNode? conditions)
    {
        if (conditions is not JsonArray array)
        {
            return;
        }

        foreach (var condition in array.OfType<JsonObject>())
        {
            if (Get(condition, "Property") is JsonValue property
                && property.GetValueKind() == JsonValueKind.String
                && string.Equals(property.GetValue<string>(), RangeType, StringComparison.OrdinalIgnoreCase))
            {
                var key = condition.FirstOrDefault(p => string.Equals(p.Key, "Value", StringComparison.OrdinalIgnoreCase)).Key ?? "Value";
                condition[key] = "SDR";
            }
        }
    }

    // ASP.NET binds these names case-insensitively, so clients may send any casing.
    private static JsonNode? Get(JsonObject parent, string name)
        => parent.FirstOrDefault(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
}
