package game

import (
	"bytes"
	"encoding/json"
	"io"
	"log/slog"
	"net/http"
	"net/http/httptest"
	"path/filepath"
	"testing"
)

func testServer(t *testing.T, options ...ServerOption) (*Server, *httptest.Server) {
	t.Helper()
	logger := slog.New(slog.NewTextHandler(io.Discard, nil))
	server, err := NewServer(filepath.Join(t.TempDir(), "state.json"), logger, options...)
	if err != nil {
		t.Fatal(err)
	}
	httpServer := httptest.NewServer(server.Handler())
	t.Cleanup(httpServer.Close)
	return server, httpServer
}

func TestHealthReportsConfiguredInstance(t *testing.T) {
	const instance = "startup-health-test"
	_, server := testServer(t, WithInstance(instance))
	response, err := http.Get(server.URL + "/api/v1/health")
	if err != nil {
		t.Fatal(err)
	}
	defer response.Body.Close()
	if response.StatusCode != http.StatusOK {
		t.Fatalf("expected 200, got %d", response.StatusCode)
	}
	var payload struct {
		Status   string `json:"status"`
		Service  string `json:"service"`
		Version  string `json:"version"`
		Instance string `json:"instance"`
	}
	if err := json.NewDecoder(response.Body).Decode(&payload); err != nil {
		t.Fatal(err)
	}
	if payload.Status != "ok" || payload.Service != "steel-tide-backend" || payload.Version != "1.0.0" {
		t.Fatalf("health contract changed: %+v", payload)
	}
	if payload.Instance != instance {
		t.Fatalf("health instance = %q, want %q", payload.Instance, instance)
	}
}

func TestMissionsExposeInfiltrationRules(t *testing.T) {
	_, server := testServer(t)
	response, err := http.Get(server.URL + "/api/v1/missions")
	if err != nil {
		t.Fatal(err)
	}
	defer response.Body.Close()
	var payload struct {
		Missions []Mission `json:"missions"`
	}
	if err := json.NewDecoder(response.Body).Decode(&payload); err != nil {
		t.Fatal(err)
	}
	if len(payload.Missions) < 3 {
		t.Fatalf("expected mission variety, got %d", len(payload.Missions))
	}
	if payload.Missions[0].SpawnProtectionSeconds < 10 || payload.Missions[0].BaseDetectionRange > 40 {
		t.Fatalf("unsafe infiltration rules: %+v", payload.Missions[0])
	}
	if len(payload.Missions[0].Objectives) < 2 {
		t.Fatalf("expected multi-stage objectives: %+v", payload.Missions[0])
	}
}

func TestMissionsExposeFalltideRecoveryArray(t *testing.T) {
	_, server := testServer(t)
	response, err := http.Get(server.URL + "/api/v1/missions")
	if err != nil {
		t.Fatal(err)
	}
	defer response.Body.Close()
	var payload struct {
		Missions []Mission `json:"missions"`
	}
	if err := json.NewDecoder(response.Body).Decode(&payload); err != nil {
		t.Fatal(err)
	}

	var falltide *Mission
	for index := range payload.Missions {
		if payload.Missions[index].ID == "falltide-recovery-array" {
			falltide = &payload.Missions[index]
			break
		}
	}
	if falltide == nil {
		t.Fatal("falltide mission is missing")
	}
	if falltide.SpawnProtectionSeconds != 12 || falltide.BaseDetectionRange != 39 || falltide.ReinforcementThreshold != 62 {
		t.Fatalf("falltide infiltration rules changed: %+v", *falltide)
	}
	wantObjectives := []string{"STABILIZE THE STORM-GRID BREAKERS", "AUTHORIZE THE QUARANTINE RELEASE"}
	wantObjectiveIDs := []string{"reroute_breaker_bus", "purge_quarantine_archive"}
	wantObjectiveKeys := []string{"falltide_objective_breakers", "falltide_objective_quarantine"}
	if len(falltide.Objectives) != len(wantObjectives) {
		t.Fatalf("falltide objectives = %v, want %v", falltide.Objectives, wantObjectives)
	}
	for index, objective := range wantObjectives {
		if falltide.Objectives[index] != objective {
			t.Fatalf("falltide objective %d = %q, want %q", index, falltide.Objectives[index], objective)
		}
	}
	if len(falltide.ObjectiveIDs) != len(wantObjectiveIDs) {
		t.Fatalf("falltide objective IDs = %v, want %v", falltide.ObjectiveIDs, wantObjectiveIDs)
	}
	if len(falltide.ObjectiveLocalizationKeys) != len(wantObjectiveKeys) {
		t.Fatalf("falltide objective localization keys = %v, want %v", falltide.ObjectiveLocalizationKeys, wantObjectiveKeys)
	}
	for index := range wantObjectiveIDs {
		if falltide.ObjectiveIDs[index] != wantObjectiveIDs[index] {
			t.Fatalf("falltide objective ID %d = %q, want %q", index, falltide.ObjectiveIDs[index], wantObjectiveIDs[index])
		}
		if falltide.ObjectiveLocalizationKeys[index] != wantObjectiveKeys[index] {
			t.Fatalf("falltide objective localization key %d = %q, want %q", index, falltide.ObjectiveLocalizationKeys[index], wantObjectiveKeys[index])
		}
	}
}

func TestSessionCompletionUpdatesProfile(t *testing.T) {
	_, server := testServer(t)
	startBody := bytes.NewBufferString(`{"playerId":"local-operator","missionId":"steel-tide-terminal","difficulty":"normal"}`)
	response, err := http.Post(server.URL+"/api/v1/sessions", "application/json", startBody)
	if err != nil {
		t.Fatal(err)
	}
	defer response.Body.Close()
	if response.StatusCode != http.StatusCreated {
		t.Fatalf("expected 201, got %d", response.StatusCode)
	}
	var started StartResponse
	if err := json.NewDecoder(response.Body).Decode(&started); err != nil {
		t.Fatal(err)
	}
	result := bytes.NewBufferString(`{"success":true,"kills":9,"headshots":3,"shotsFired":50,"shotsHit":26,"durationSeconds":318}`)
	completeResponse, err := http.Post(server.URL+"/api/v1/sessions/"+started.Session.ID+"/complete", "application/json", result)
	if err != nil {
		t.Fatal(err)
	}
	defer completeResponse.Body.Close()
	var completed CompleteResponse
	if err := json.NewDecoder(completeResponse.Body).Decode(&completed); err != nil {
		t.Fatal(err)
	}
	if completed.Profile.Wins != 1 || completed.Profile.Matches != 1 {
		t.Fatalf("profile not updated: %+v", completed.Profile)
	}
	if completed.CreditsEarned <= 0 || completed.XPEarned <= 0 {
		t.Fatalf("missing rewards: %+v", completed)
	}
}

func TestUnknownMissionIsRejected(t *testing.T) {
	_, server := testServer(t)
	body := bytes.NewBufferString(`{"playerId":"local-operator","missionId":"missing","difficulty":"normal"}`)
	response, err := http.Post(server.URL+"/api/v1/sessions", "application/json", body)
	if err != nil {
		t.Fatal(err)
	}
	defer response.Body.Close()
	if response.StatusCode != http.StatusNotFound {
		t.Fatalf("expected 404, got %d", response.StatusCode)
	}
}
