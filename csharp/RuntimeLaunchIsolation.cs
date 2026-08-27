using System;
using System.Globalization;
using System.IO;

namespace OperationSteelTide;

internal static class RuntimeLaunchIsolation
{
    public const string InstanceEnvironmentVariable = "STEEL_TIDE_PARALLEL_INSTANCE";

    public static string? GetInstanceId()
    {
        return NormalizeInstanceId(Environment.GetEnvironmentVariable(InstanceEnvironmentVariable));
    }

    public static bool ShouldPersistSharedSettings => GetInstanceId() is null;

    public static string GetOperatorProfilePath(string projectDirectory, string instanceId)
    {
        var normalizedInstanceId = NormalizeInstanceId(instanceId)
            ?? throw new ArgumentException("Parallel instance ID is not a valid launcher run ID.", nameof(instanceId));
        return Path.Combine(
            projectDirectory,
            "logs",
            "startup",
            normalizedInstanceId,
            "operator_profile_parallel.json");
    }

    internal static string? NormalizeInstanceId(string? candidate)
    {
        if (candidate is null)
        {
            return null;
        }
        var parts = candidate.Split('-');
        if (parts.Length != 5)
        {
            return null;
        }

        var timestamp = $"{parts[0]}-{parts[1]}-{parts[2]}";
        if (!DateTime.TryParseExact(
                timestamp,
                "yyyyMMdd-HHmmss-fff",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _)
            || !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var processId)
            || processId <= 0
            || parts[4].Length != 8)
        {
            return null;
        }
        foreach (var character in parts[4])
        {
            if (character is not (>= '0' and <= '9'
                or >= 'a' and <= 'f'
                or >= 'A' and <= 'F'))
            {
                return null;
            }
        }
        return $"{timestamp}-{parts[3]}-{parts[4].ToLowerInvariant()}";
    }
}
