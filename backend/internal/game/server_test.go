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

func testServer(t *testing.T) (*Server, *httptest.Server) {
	t.Helper()
	logger := slog.New(slog.NewTextHandler(io.Discard, nil))
	server, err := NewServer(filepath.Join(t.TempDir(), "state.json"), logger)
	if err != nil {
		t.Fatal(err)
	}
	httpServer := httptest.NewServer(server.Handler())
	t.Cleanup(httpServer.Close)
	return server, httpServer
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
