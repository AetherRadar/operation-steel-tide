using System;
using System.IO;
using Godot;
using ProcessEnvironment = System.Environment;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private void ValidateLaunchIsolation()
    {
        var originalInstance = ProcessEnvironment.GetEnvironmentVariable(
            RuntimeLaunchIsolation.InstanceEnvironmentVariable);
        var validEnvironment = false;
        var invalidEnvironment = false;
        try
        {
            ProcessEnvironment.SetEnvironmentVariable(
                RuntimeLaunchIsolation.InstanceEnvironmentVariable,
                "20260828-123456-789-1234-A1b2C3d4");
            validEnvironment = RuntimeLaunchIsolation.GetInstanceId()
                    == "20260828-123456-789-1234-a1b2c3d4"
                && !RuntimeLaunchIsolation.ShouldPersistSharedSettings;

            ProcessEnvironment.SetEnvironmentVariable(
                RuntimeLaunchIsolation.InstanceEnvironmentVariable,
                "20260828-123456-789-1234-../unsafe");
            invalidEnvironment = RuntimeLaunchIsolation.GetInstanceId() is null
                && RuntimeLaunchIsolation.ShouldPersistSharedSettings;
        }
        finally
        {
            ProcessEnvironment.SetEnvironmentVariable(
                RuntimeLaunchIsolation.InstanceEnvironmentVariable,
                originalInstance);
        }

        var projectDirectory = ProjectSettings.GlobalizePath("res://");
        var profilePath = RuntimeLaunchIsolation.GetOperatorProfilePath(
            projectDirectory,
            "20260828-123456-789-1234-A1b2C3d4");
        var expectedProfileDirectory = Path.Combine(
            projectDirectory,
            "logs",
            "startup",
            "20260828-123456-789-1234-a1b2c3d4");
        var canonicalExpectedProfileDirectory = Path.GetFullPath(expectedProfileDirectory).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var canonicalProfileDirectory = Path.GetFullPath(
            Path.GetDirectoryName(profilePath) ?? string.Empty).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
        var profileContained = string.Equals(
                canonicalProfileDirectory,
                canonicalExpectedProfileDirectory,
                StringComparison.OrdinalIgnoreCase)
            && Path.GetFileName(profilePath) == "operator_profile_parallel.json";
        var environmentRestored = string.Equals(
            ProcessEnvironment.GetEnvironmentVariable(RuntimeLaunchIsolation.InstanceEnvironmentVariable),
            originalInstance,
            StringComparison.Ordinal);
        var valid = validEnvironment
            && invalidEnvironment
            && profileContained
            && environmentRestored;

        GD.Print(
            $"LAUNCH_ISOLATION_CHECK valid={valid} valid_env={validEnvironment} "
            + $"invalid_env={invalidEnvironment} profile_contained={profileContained} "
            + $"environment_restored={environmentRestored}");
        GD.Print($"LAUNCH_ISOLATION_PASS valid={valid}");
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }
}
