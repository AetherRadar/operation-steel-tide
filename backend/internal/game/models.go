package game

import "time"

type Mission struct {
	ID                     string   `json:"id"`
	Name                   string   `json:"name"`
	Map                    string   `json:"map"`
	Briefing               string   `json:"briefing"`
	Objectives             []string `json:"objectives"`
	EnemyCount             int      `json:"enemyCount"`
	SpawnProtectionSeconds int      `json:"spawnProtectionSeconds"`
	BaseDetectionRange     int      `json:"baseDetectionRange"`
	ReinforcementThreshold int      `json:"reinforcementThreshold"`
	RewardCredits          int      `json:"rewardCredits"`
	RewardXP               int      `json:"rewardXp"`
}

type Profile struct {
	ID                string    `json:"id"`
	Level             int       `json:"level"`
	XP                int       `json:"xp"`
	Credits           int       `json:"credits"`
	Matches           int       `json:"matches"`
	Wins              int       `json:"wins"`
	CompletedMissions []string  `json:"completedMissions"`
	UpdatedAt         time.Time `json:"updatedAt"`
}

type Session struct {
	ID          string           `json:"id"`
	PlayerID    string           `json:"playerId"`
	MissionID   string           `json:"missionId"`
	Seed        int64            `json:"seed"`
	Difficulty  string           `json:"difficulty"`
	Status      string           `json:"status"`
	StartedAt   time.Time        `json:"startedAt"`
	CompletedAt *time.Time       `json:"completedAt,omitempty"`
	Result      *CompleteRequest `json:"result,omitempty"`
}

type StartRequest struct {
	PlayerID   string `json:"playerId"`
	MissionID  string `json:"missionId"`
	Difficulty string `json:"difficulty"`
}

type StartResponse struct {
	Session Session `json:"session"`
	Mission Mission `json:"mission"`
	Profile Profile `json:"profile"`
}

type CompleteRequest struct {
	Success         bool    `json:"success"`
	Kills           int     `json:"kills"`
	Headshots       int     `json:"headshots"`
	ShotsFired      int     `json:"shotsFired"`
	ShotsHit        int     `json:"shotsHit"`
	DurationSeconds float64 `json:"durationSeconds"`
}

type CompleteResponse struct {
	Session       Session `json:"session"`
	Profile       Profile `json:"profile"`
	CreditsEarned int     `json:"creditsEarned"`
	XPEarned      int     `json:"xpEarned"`
}

type persistedState struct {
	Profiles map[string]Profile `json:"profiles"`
	Sessions map[string]Session `json:"sessions"`
}
