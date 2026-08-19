using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace OperationSteelTide;

[GlobalClass]
public partial class MissionDirector : Node
{
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
    private List<string> _objectives = new()
    {
        "DISABLE THE COMMUNICATIONS RELAY",
        "RECOVER THE SHIPPING MANIFEST"
    };
    private int _objectiveIndex;

    public int SpawnProtectionSeconds { get; private set; } = 12;
    public float DetectionRange { get; private set; } = 34.0f;
    public int ReinforcementThreshold { get; private set; } = 70;

    public override void _Ready()
    {
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
            payload = await _backend!.StartSessionAsync("local-operator", "steel-tide-terminal");
        }
        catch (Exception exception)
        {
            GD.Print($"Backend unavailable, using offline mission: {exception.Message}");
        }

        if (payload is not null)
        {
            _online = true;
            _sessionId = payload.Session.Id;
            SpawnProtectionSeconds = Math.Max(8, payload.Mission.SpawnProtectionSeconds);
            DetectionRange = Math.Clamp(payload.Mission.BaseDetectionRange, 20, 45);
            ReinforcementThreshold = Math.Clamp(payload.Mission.ReinforcementThreshold, 40, 95);
            if (payload.Mission.Objectives.Count > 0)
            {
                _objectives = payload.Mission.Objectives;
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
}
