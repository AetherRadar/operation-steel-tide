package game

import (
	"crypto/rand"
	"encoding/hex"
	"encoding/json"
	"errors"
	"fmt"
	"log/slog"
	"math"
	mathrand "math/rand/v2"
	"net/http"
	"os"
	"path/filepath"
	"strings"
	"sync"
	"time"
)

type Server struct {
	mu       sync.RWMutex
	dataPath string
	instance string
	logger   *slog.Logger
	profiles map[string]Profile
	sessions map[string]Session
	missions []Mission
}

type ServerOption func(*Server)

func WithInstance(instance string) ServerOption {
	return func(server *Server) {
		server.instance = instance
	}
}

func NewServer(dataPath string, logger *slog.Logger, options ...ServerOption) (*Server, error) {
	if logger == nil {
		logger = slog.New(slog.NewTextHandler(os.Stdout, nil))
	}
	s := &Server{
		dataPath: dataPath,
		logger:   logger,
		profiles: make(map[string]Profile),
		sessions: make(map[string]Session),
		missions: defaultMissions(),
	}
	for _, configure := range options {
		if configure != nil {
			configure(s)
		}
	}
	if err := s.load(); err != nil {
		return nil, err
	}
	return s, nil
}

func defaultMissions() []Mission {
	return []Mission{
		{
			ID: "steel-tide-terminal", Name: "Operation Steel Tide", Map: "Qinhai Freight Terminal",
			Briefing:   "Enter through the southern maintenance lane, identify patrol routes, then secure the terminal before extraction.",
			Objectives: []string{"DISABLE THE COMMUNICATIONS RELAY", "RECOVER THE SHIPPING MANIFEST"},
			EnemyCount: 20, SpawnProtectionSeconds: 12, BaseDetectionRange: 34,
			ReinforcementThreshold: 70, RewardCredits: 1800, RewardXP: 950,
		},
		{
			ID: "silent-ledger", Name: "Silent Ledger", Map: "Customs Warehouse",
			Briefing:   "Recover the manifest without raising the terminal-wide alarm. Suppressed engagements are authorized.",
			Objectives: []string{"BYPASS THE CUSTOMS TERMINAL", "RECOVER THE SILENT LEDGER"},
			EnemyCount: 7, SpawnProtectionSeconds: 15, BaseDetectionRange: 30,
			ReinforcementThreshold: 85, RewardCredits: 2300, RewardXP: 1200,
		},
		{
			ID: "broken-crane", Name: "Broken Crane", Map: "North Gantry",
			Briefing:   "Disable the communications relay, hold the gantry, and extract before the response team arrives.",
			Objectives: []string{"DISABLE THE GANTRY RELAY", "SECURE THE CRANE CONTROL LOG"},
			EnemyCount: 11, SpawnProtectionSeconds: 10, BaseDetectionRange: 38,
			ReinforcementThreshold: 55, RewardCredits: 2700, RewardXP: 1450,
		},
		{
			ID: "falltide-recovery-array", Name: "Operation Falltide", Map: "Falltide Recovery Array",
			Briefing: "Cross the storm barrier, restore the isolated breaker grid, and authorize quarantine release before the array overloads.",
			// Objective order is the canonical contract. The client may project this list into
			// the deterministic shared-world order selected by the extraction world seed.
			Objectives:                []string{"STABILIZE THE STORM-GRID BREAKERS", "AUTHORIZE THE QUARANTINE RELEASE"},
			ObjectiveIDs:              []string{"reroute_breaker_bus", "purge_quarantine_archive"},
			ObjectiveLocalizationKeys: []string{"falltide_objective_breakers", "falltide_objective_quarantine"},
			EnemyCount:                24, SpawnProtectionSeconds: 12, BaseDetectionRange: 39,
			ReinforcementThreshold: 62, RewardCredits: 3600, RewardXP: 1900,
		},
	}
}

func (s *Server) Handler() http.Handler {
	mux := http.NewServeMux()
	mux.HandleFunc("GET /api/v1/health", s.health)
	mux.HandleFunc("GET /api/v1/missions", s.listMissions)
	mux.HandleFunc("GET /api/v1/players/{id}", s.getProfile)
	mux.HandleFunc("PUT /api/v1/players/{id}", s.putProfile)
	mux.HandleFunc("POST /api/v1/sessions", s.startSession)
	mux.HandleFunc("POST /api/v1/sessions/{id}/complete", s.completeSession)
	return s.middleware(mux)
}

func (s *Server) middleware(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json; charset=utf-8")
		w.Header().Set("Access-Control-Allow-Origin", "http://127.0.0.1")
		if r.Method == http.MethodOptions {
			w.Header().Set("Access-Control-Allow-Methods", "GET, PUT, POST, OPTIONS")
			w.Header().Set("Access-Control-Allow-Headers", "Content-Type")
			w.WriteHeader(http.StatusNoContent)
			return
		}
		started := time.Now()
		next.ServeHTTP(w, r)
		s.logger.Debug("request", "method", r.Method, "path", r.URL.Path, "duration", time.Since(started))
	})
}

func (s *Server) health(w http.ResponseWriter, _ *http.Request) {
	s.writeJSON(w, http.StatusOK, map[string]any{
		"status":   "ok",
		"service":  "steel-tide-backend",
		"version":  "1.0.0",
		"instance": s.instance,
	})
}

func (s *Server) listMissions(w http.ResponseWriter, _ *http.Request) {
	s.writeJSON(w, http.StatusOK, map[string]any{"missions": s.missions})
}

func (s *Server) getProfile(w http.ResponseWriter, r *http.Request) {
	id := strings.TrimSpace(r.PathValue("id"))
	if id == "" {
		s.writeError(w, http.StatusBadRequest, "player id is required")
		return
	}
	s.mu.Lock()
	defer s.mu.Unlock()
	profile := s.ensureProfileLocked(id)
	s.writeJSON(w, http.StatusOK, profile)
}

func (s *Server) putProfile(w http.ResponseWriter, r *http.Request) {
	id := strings.TrimSpace(r.PathValue("id"))
	var profile Profile
	if err := decodeJSON(r, &profile); err != nil {
		s.writeError(w, http.StatusBadRequest, err.Error())
		return
	}
	profile.ID = id
	profile.Level = max(1, profile.Level)
	profile.UpdatedAt = time.Now().UTC()
	s.mu.Lock()
	s.profiles[id] = profile
	err := s.saveLocked()
	s.mu.Unlock()
	if err != nil {
		s.writeError(w, http.StatusInternalServerError, "profile persistence failed")
		return
	}
	s.writeJSON(w, http.StatusOK, profile)
}

func (s *Server) startSession(w http.ResponseWriter, r *http.Request) {
	var request StartRequest
	if err := decodeJSON(r, &request); err != nil {
		s.writeError(w, http.StatusBadRequest, err.Error())
		return
	}
	request.PlayerID = strings.TrimSpace(request.PlayerID)
	if request.PlayerID == "" {
		s.writeError(w, http.StatusBadRequest, "playerId is required")
		return
	}
	mission, ok := s.findMission(request.MissionID)
	if !ok {
		s.writeError(w, http.StatusNotFound, "mission not found")
		return
	}
	if request.Difficulty == "" {
		request.Difficulty = "normal"
	}
	now := time.Now().UTC()
	session := Session{
		ID: newID(), PlayerID: request.PlayerID, MissionID: mission.ID,
		Seed: mathrand.Int64(), Difficulty: request.Difficulty, Status: "active", StartedAt: now,
	}
	s.mu.Lock()
	profile := s.ensureProfileLocked(request.PlayerID)
	s.sessions[session.ID] = session
	err := s.saveLocked()
	s.mu.Unlock()
	if err != nil {
		s.writeError(w, http.StatusInternalServerError, "session persistence failed")
		return
	}
	s.writeJSON(w, http.StatusCreated, StartResponse{Session: session, Mission: mission, Profile: profile})
}

func (s *Server) completeSession(w http.ResponseWriter, r *http.Request) {
	var request CompleteRequest
	if err := decodeJSON(r, &request); err != nil {
		s.writeError(w, http.StatusBadRequest, err.Error())
		return
	}
	sessionID := r.PathValue("id")
	s.mu.Lock()
	defer s.mu.Unlock()
	session, ok := s.sessions[sessionID]
	if !ok {
		s.writeError(w, http.StatusNotFound, "session not found")
		return
	}
	if session.Status != "active" {
		s.writeError(w, http.StatusConflict, "session is already complete")
		return
	}
	mission, _ := s.findMission(session.MissionID)
	credits, xp := calculateRewards(mission, request)
	now := time.Now().UTC()
	session.Status = "complete"
	session.CompletedAt = &now
	session.Result = &request
	s.sessions[sessionID] = session
	profile := s.ensureProfileLocked(session.PlayerID)
	profile.Matches++
	profile.Credits += credits
	profile.XP += xp
	if request.Success {
		profile.Wins++
		if !contains(profile.CompletedMissions, mission.ID) {
			profile.CompletedMissions = append(profile.CompletedMissions, mission.ID)
		}
	}
	profile.Level = 1 + profile.XP/2500
	profile.UpdatedAt = now
	s.profiles[profile.ID] = profile
	if err := s.saveLocked(); err != nil {
		s.writeError(w, http.StatusInternalServerError, "result persistence failed")
		return
	}
	s.writeJSON(w, http.StatusOK, CompleteResponse{Session: session, Profile: profile, CreditsEarned: credits, XPEarned: xp})
}

func calculateRewards(mission Mission, result CompleteRequest) (int, int) {
	completion := 0.25
	if result.Success {
		completion = 1.0
	}
	accuracy := 0.0
	if result.ShotsFired > 0 {
		accuracy = math.Min(1, float64(result.ShotsHit)/float64(result.ShotsFired))
	}
	credits := int(float64(mission.RewardCredits)*completion) + result.Kills*35 + int(accuracy*200)
	xp := int(float64(mission.RewardXP)*completion) + result.Kills*30 + result.Headshots*20
	return credits, xp
}

func (s *Server) findMission(id string) (Mission, bool) {
	if id == "" {
		return s.missions[0], true
	}
	for _, mission := range s.missions {
		if mission.ID == id {
			return mission, true
		}
	}
	return Mission{}, false
}

func (s *Server) ensureProfileLocked(id string) Profile {
	if profile, ok := s.profiles[id]; ok {
		return profile
	}
	profile := Profile{ID: id, Level: 1, Credits: 2500, CompletedMissions: []string{}, UpdatedAt: time.Now().UTC()}
	s.profiles[id] = profile
	return profile
}

func (s *Server) load() error {
	if s.dataPath == "" {
		return nil
	}
	data, err := os.ReadFile(s.dataPath)
	if errors.Is(err, os.ErrNotExist) {
		return nil
	}
	if err != nil {
		return fmt.Errorf("read state: %w", err)
	}
	var state persistedState
	if err := json.Unmarshal(data, &state); err != nil {
		return fmt.Errorf("decode state: %w", err)
	}
	if state.Profiles != nil {
		s.profiles = state.Profiles
	}
	if state.Sessions != nil {
		s.sessions = state.Sessions
	}
	return nil
}

func (s *Server) saveLocked() error {
	if s.dataPath == "" {
		return nil
	}
	if err := os.MkdirAll(filepath.Dir(s.dataPath), 0o755); err != nil {
		return err
	}
	data, err := json.MarshalIndent(persistedState{Profiles: s.profiles, Sessions: s.sessions}, "", "  ")
	if err != nil {
		return err
	}
	temporary := s.dataPath + ".tmp"
	if err := os.WriteFile(temporary, data, 0o644); err != nil {
		return err
	}
	_ = os.Remove(s.dataPath)
	return os.Rename(temporary, s.dataPath)
}

func decodeJSON(r *http.Request, target any) error {
	decoder := json.NewDecoder(http.MaxBytesReader(nil, r.Body, 1<<20))
	decoder.DisallowUnknownFields()
	if err := decoder.Decode(target); err != nil {
		return fmt.Errorf("invalid JSON: %w", err)
	}
	return nil
}

func (s *Server) writeJSON(w http.ResponseWriter, status int, value any) {
	w.WriteHeader(status)
	if err := json.NewEncoder(w).Encode(value); err != nil {
		s.logger.Error("write response", "error", err)
	}
}

func (s *Server) writeError(w http.ResponseWriter, status int, message string) {
	s.writeJSON(w, status, map[string]string{"error": message})
}

func newID() string {
	buffer := make([]byte, 10)
	if _, err := rand.Read(buffer); err != nil {
		return fmt.Sprintf("%d", time.Now().UnixNano())
	}
	return hex.EncodeToString(buffer)
}

func contains(values []string, target string) bool {
	for _, value := range values {
		if value == target {
			return true
		}
	}
	return false
}
