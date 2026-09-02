using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace OperationSteelTide;

[GlobalClass]
public partial class MissionDirector : Node
{
    public const string DefaultBackendMissionId = "steel-tide-terminal";
    public const string FalltideBackendMissionId = "falltide-recovery-array";

    private static readonly string[] DefaultOfflineObjectives =
    {
        "DISABLE THE COMMUNICATIONS RELAY",
        "RECOVER THE SHIPPING MANIFEST"
    };

    [Signal]
    public delegate void MissionLoadedEventHandler(
        int spawnProtectionSeconds,
        float detectionRange,
        int reinforcementThreshold,
        bool online);

    [Signal]
    public delegate void PhaseChangedEventHandler(string phase, float remaining, bool online);

    [Signal]
    public delegate void GunshotEventHandler(Vector3 origin, float radius);

    [Signal]
    public delegate void BackendStatusEventHandler(bool online, string status);

    [Signal]
    public delegate void ObjectiveChangedEventHandler(int index, string objective, bool extractionAvailable);

    private BackendClient? _backend;
    private string _sessionId = string.Empty;
    private string _phase = "DEPLOYMENT";
    private float _deploymentRemaining = 12.0f;
    private bool _deploymentProtectionActive = true;
    private bool _online;
    private int _lastReportedSecond = -1;
    private double _missionStartedAt;
    private bool _resultSubmitted;
    private bool _configurationLocked;
    private string _backendMissionId = DefaultBackendMissionId;
    private List<string> _objectives = new(DefaultOfflineObjectives);
    private List<string> _objectiveIds = new();
    private List<string> _objectiveLocalizationKeys = new();
    private bool _backendObjectiveContractValid = true;
    private int _objectiveIndex;

    public int SpawnProtectionSeconds { get; private set; } = 12;
    public float DetectionRange { get; private set; } = 34.0f;
    public int ReinforcementThreshold { get; private set; } = 70;
    public string BackendMissionId => _backendMissionId;
    public IReadOnlyList<string> Objectives => _objectives;
    public IReadOnlyList<string> ObjectiveIds => _objectiveIds;
    public IReadOnlyList<string> ObjectiveLocalizationKeys => _objectiveLocalizationKeys;
    public bool BackendObjectiveContractValid => _backendObjectiveContractValid;
    public bool IsOnline => _online;

    /// <summary>
    /// Selects the backend mission and the ordered objectives used when the backend is unavailable.
    /// Configure the director before adding it to the scene tree; inputs are copied for isolation.
    /// </summary>
    public void ConfigureMission(
        string backendMissionId,
        IReadOnlyList<string> offlineObjectives,
        IReadOnlyList<string>? offlineObjectiveIds = null,
        IReadOnlyList<string>? offlineObjectiveLocalizationKeys = null)
    {
        if (_configurationLocked || IsInsideTree())
        {
            throw new InvalidOperationException(
                "MissionDirector must be configured before it enters the scene tree.");
        }
        if (string.IsNullOrWhiteSpace(backendMissionId))
        {
            throw new ArgumentException("Backend mission ID is required.", nameof(backendMissionId));
        }
        ArgumentNullException.ThrowIfNull(offlineObjectives);

        var objectives = CopyNonEmptyObjectives(offlineObjectives);
        if (objectives.Count == 0)
        {
            throw new ArgumentException("At least one offline objective is required.", nameof(offlineObjectives));
        }

        var objectiveIds = offlineObjectiveIds is null
            ? new List<string>()
            : CopyNonEmptyObjectives(offlineObjectiveIds);
        if (objectiveIds.Count > 0 && objectiveIds.Count != objectives.Count)
        {
            throw new ArgumentException(
                "Offline objective IDs must match the objective count.",
                nameof(offlineObjectiveIds));
        }
        var localizationKeys = offlineObjectiveLocalizationKeys is null
            ? new List<string>()
            : CopyNonEmptyObjectives(offlineObjectiveLocalizationKeys);
        if (localizationKeys.Count > 0 && localizationKeys.Count != objectives.Count)
        {
            throw new ArgumentException(
                "Offline objective localization keys must match the objective count.",
                nameof(offlineObjectiveLocalizationKeys));
        }

        _backendMissionId = backendMissionId.Trim();
        _objectives = objectives;
        _objectiveIds = objectiveIds;
        _objectiveLocalizationKeys = localizationKeys;
        _backendObjectiveContractValid = true;
    }

    public override void _Ready()
    {
        _configurationLocked = true;
        ProcessMode = ProcessModeEnum.Always;
        _missionStartedAt = Time.GetTicksMsec() / 1000.0;
        _backend = new BackendClient();
        _ = StartMissionAsync();
    }

    public override void _ExitTree()
    {
        _backend?.Dispose();
    }

    public override void _Process(double delta)
    {
        if (_phase != "DEPLOYMENT" || GetTree().Paused)
        {
            return;
        }
        _deploymentRemaining = Math.Max(0.0f, _deploymentRemaining - (float)delta);
        var second = Mathf.CeilToInt(_deploymentRemaining);
        if (second != _lastReportedSecond)
        {
            _lastReportedSecond = second;
            EmitSignal(SignalName.PhaseChanged, _phase, _deploymentRemaining, _online);
        }
        if (_deploymentRemaining <= 0.0f)
        {
			_deploymentRemaining = 0.0f;
        }
    }

    public bool IsDeploymentProtected() => _deploymentProtectionActive;

    public string CurrentPhase() => _phase;

    public void ApplyExtractionNetworkState(
        string phase,
        float remaining,
        int objectiveIndex,
        bool deploymentProtected,
        bool missionEnded)
    {
        _phase = phase;
        _deploymentRemaining = Mathf.Max(0.0f, remaining);
        _objectiveIndex = Mathf.Max(0, objectiveIndex);
        _deploymentProtectionActive = deploymentProtected;
        _resultSubmitted = missionEnded;
    }

	public void ExitDeploymentZone()
	{
		_deploymentProtectionActive = false;
		if (_phase == "DEPLOYMENT")
		{
			SetPhase("INFILTRATION");
		}
	}

    public void ReportGunshot(Vector3 origin, float radius)
    {
        if (_phase == "DEPLOYMENT" || _phase == "INFILTRATION")
        {
            SetPhase("CONTACT");
        }
        EmitSignal(SignalName.Gunshot, origin, radius);
    }

    public void RaiseConfirmedAlarm()
    {
        if (_phase is not "EXTRACTION" and not "COMPLETE")
        {
            SetPhase("COMBAT");
        }
    }

    public void BeginExtraction()
    {
        _deploymentProtectionActive = false;
        SetPhase("EXTRACTION");
    }

    public void AdvanceObjective()
    {
        if (_objectiveIndex >= _objectives.Count || _resultSubmitted)
        {
            return;
        }

        _objectiveIndex++;
        if (_objectiveIndex >= _objectives.Count)
        {
            BeginExtraction();
            EmitSignal(SignalName.ObjectiveChanged, _objectiveIndex, "REACH THE EXTRACTION ZONE", true);
            return;
        }

        EmitSignal(SignalName.ObjectiveChanged, _objectiveIndex, _objectives[_objectiveIndex], false);
    }

    public void CompleteMission(
        bool success,
        int kills,
        int headshots,
        int shotsFired,
        int shotsHit)
    {
        if (_resultSubmitted)
        {
            return;
        }
        _resultSubmitted = true;
        _deploymentProtectionActive = false;
        SetPhase(success ? "COMPLETE" : "FAILED");
        if (_online && !string.IsNullOrEmpty(_sessionId) && _backend is not null)
        {
            var duration = Time.GetTicksMsec() / 1000.0 - _missionStartedAt;
            _ = _backend.CompleteSessionAsync(
                _sessionId,
                new CompleteRequest(success, kills, headshots, shotsFired, shotsHit, duration));
        }
    }

    private async Task StartMissionAsync()
    {
        StartPayload? payload = null;
        try
        {
            payload = await _backend!.StartSessionAsync("local-operator", _backendMissionId);
        }
        catch (Exception exception)
        {
            GD.Print($"Backend unavailable, using offline mission: {exception.Message}");
        }

        if (payload is not null)
        {
            // JSON clients are allowed to omit optional objects/arrays. Keep a
            // malformed-but-successful response from taking the mission loop
            // down; the locally configured map contract remains authoritative.
            var session = payload.Session ?? new SessionPayload();
            var mission = payload.Mission ?? new MissionPayload();
            _online = true;
            _sessionId = session.Id ?? string.Empty;
            SpawnProtectionSeconds = Math.Max(8, mission.SpawnProtectionSeconds);
            DetectionRange = Math.Clamp(mission.BaseDetectionRange, 20, 45);
            ReinforcementThreshold = Math.Clamp(mission.ReinforcementThreshold, 40, 95);
            var onlineObjectives = CopyNonEmptyObjectives(
                mission.Objectives ?? new List<string>());
            var onlineObjectiveIds = CopyNonEmptyObjectives(
                mission.ObjectiveIds ?? new List<string>());
            var onlineLocalizationKeys = CopyNonEmptyObjectives(
                mission.ObjectiveLocalizationKeys ?? new List<string>());
            if (string.Equals(
                    _backendMissionId,
                    FalltideBackendMissionId,
                    StringComparison.OrdinalIgnoreCase))
            {
                // Falltide's world seed chooses which district is first. Keep the locally
                // configured seeded order and validate the backend's canonical ID/text/key
                // contract instead of replacing it with the backend's fixed canonical order.
                _backendObjectiveContractValid = ValidateFalltideObjectiveContract(
                    mission,
                    onlineObjectives,
                    onlineObjectiveIds,
                    onlineLocalizationKeys);
                if (_objectiveIds.Count == 0 && onlineObjectiveIds.Count == _objectives.Count)
                {
                    _objectiveIds = onlineObjectiveIds;
                }
                if (_objectiveLocalizationKeys.Count == 0
                    && onlineLocalizationKeys.Count == _objectives.Count)
                {
                    _objectiveLocalizationKeys = onlineLocalizationKeys;
                }
                if (_objectives.Count == 0 && onlineObjectives.Count > 0)
                {
                    _objectives = onlineObjectives;
                }
            }
            else if (onlineObjectives.Count > 0)
            {
                _objectives = onlineObjectives;
                if (onlineObjectiveIds.Count == onlineObjectives.Count)
                {
                    _objectiveIds = onlineObjectiveIds;
                }
                if (onlineLocalizationKeys.Count == onlineObjectives.Count)
                {
                    _objectiveLocalizationKeys = onlineLocalizationKeys;
                }
            }
            EmitSignal(SignalName.BackendStatus, true, "ONLINE");
        }
        else
        {
            _online = false;
            EmitSignal(SignalName.BackendStatus, false, "LOCAL");
        }

        _deploymentRemaining = SpawnProtectionSeconds;
        CallDeferred(MethodName.PublishMission);
    }

    private void PublishMission()
    {
        EmitSignal(
            SignalName.MissionLoaded,
            SpawnProtectionSeconds,
            DetectionRange,
            ReinforcementThreshold,
            _online);
        EmitSignal(SignalName.PhaseChanged, _phase, _deploymentRemaining, _online);
        var objective = _objectiveIndex < _objectives.Count
            ? _objectives[_objectiveIndex]
            : "REACH THE EXTRACTION ZONE";
        EmitSignal(SignalName.ObjectiveChanged, _objectiveIndex, objective, _objectiveIndex >= _objectives.Count);
    }

    private void SetPhase(string phase)
    {
        if (_phase == phase)
        {
            return;
        }
        _phase = phase;
        EmitSignal(SignalName.PhaseChanged, _phase, _deploymentRemaining, _online);
    }

    private static List<string> CopyNonEmptyObjectives(IReadOnlyList<string> objectives)
    {
        var copy = new List<string>(objectives.Count);
        foreach (var objective in objectives)
        {
            if (!string.IsNullOrWhiteSpace(objective))
            {
                copy.Add(objective.Trim());
            }
        }
        return copy;
    }

    private static bool ValidateFalltideObjectiveContract(
        MissionPayload mission,
        IReadOnlyList<string> objectives,
        IReadOnlyList<string> objectiveIds,
        IReadOnlyList<string> localizationKeys)
    {
        if (!string.Equals(
                mission.Id,
                FalltideBackendMissionId,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var expectedIds = new[]
        {
            OrbitalComplexMapDefinition.BreakerObjectiveId,
            OrbitalComplexMapDefinition.QuarantineObjectiveId
        };
        var expectedObjectives = new[]
        {
            OrbitalComplexMapDefinition.BreakerObjectiveEnglishName,
            OrbitalComplexMapDefinition.QuarantineObjectiveEnglishName
        };
        var expectedKeys = new[]
        {
            OrbitalComplexMapDefinition.BreakerObjectiveLocalizationKey,
            OrbitalComplexMapDefinition.QuarantineObjectiveLocalizationKey
        };
        if (objectives.Count != expectedObjectives.Length)
        {
            return false;
        }

        // A pre-ID backend remains compatible as long as it serves the canonical text
        // sequence. Newer backends must also provide a paired ID and localization key.
        if (objectiveIds.Count == 0 && localizationKeys.Count == 0)
        {
            return SequenceEqual(objectives, expectedObjectives);
        }
        if (objectiveIds.Count != expectedIds.Length
            || (localizationKeys.Count != 0
                && localizationKeys.Count != expectedKeys.Length))
        {
            return false;
        }
        for (var index = 0; index < expectedIds.Length; index++)
        {
            var payloadIndex = IndexOf(objectiveIds, expectedIds[index]);
            if (payloadIndex < 0 || objectives[payloadIndex] != expectedObjectives[index])
            {
                return false;
            }
            if (localizationKeys.Count > 0
                && localizationKeys[payloadIndex] != expectedKeys[index])
            {
                return false;
            }
        }
        return true;
    }

    private static bool SequenceEqual(
        IReadOnlyList<string> left,
        IReadOnlyList<string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }
        for (var index = 0; index < left.Count; index++)
        {
            if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    private static int IndexOf(IReadOnlyList<string> values, string expected)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], expected, StringComparison.Ordinal))
            {
                return index;
            }
        }
        return -1;
    }
}
