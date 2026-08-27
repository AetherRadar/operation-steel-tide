package main

import (
	"bytes"
	"context"
	"encoding/json"
	"log/slog"
	"net"
	"net/http"
	"path/filepath"
	"strings"
	"testing"
	"time"
)

type logRecord struct {
	Level   string `json:"level"`
	Message string `json:"msg"`
	Error   string `json:"error"`
	Address string `json:"address"`
}

type channelWriter chan string

func (writer channelWriter) Write(data []byte) (int, error) {
	writer <- strings.TrimSpace(string(data))
	return len(data), nil
}

func TestRunReturnsSingleErrorWhenAddressIsInUse(t *testing.T) {
	listener, err := net.Listen("tcp", "127.0.0.1:0")
	if err != nil {
		t.Fatalf("reserve address: %v", err)
	}
	defer listener.Close()

	var output bytes.Buffer
	logger := slog.New(slog.NewJSONHandler(&output, nil))
	exitCode := run(
		context.Background(),
		listener.Addr().String(),
		filepath.Join(t.TempDir(), "state.json"),
		"occupied-address-test",
		logger,
	)
	if exitCode != 1 {
		t.Fatalf("exit code = %d, want 1", exitCode)
	}

	lines := strings.Split(strings.TrimSpace(output.String()), "\n")
	if len(lines) != 1 {
		t.Fatalf("log line count = %d, want 1; output: %s", len(lines), output.String())
	}
	var record logRecord
	if err := json.Unmarshal([]byte(lines[0]), &record); err != nil {
		t.Fatalf("decode log: %v", err)
	}
	if record.Level != "ERROR" || record.Message != "backend failed" {
		t.Fatalf("unexpected failure log: %+v", record)
	}
	if !strings.Contains(record.Error, "listen on") || !strings.Contains(record.Error, listener.Addr().String()) {
		t.Fatalf("failure is not clear about the occupied address: %q", record.Error)
	}
	if strings.Contains(output.String(), "backend listening") {
		t.Fatalf("reported listening after bind failure: %s", output.String())
	}
}

func TestRunServesHealthAndShutsDown(t *testing.T) {
	const instance = "cmd-server-health-test"
	ctx, cancel := context.WithCancel(context.Background())
	t.Cleanup(cancel)

	logs := make(channelWriter, 4)
	logger := slog.New(slog.NewJSONHandler(logs, nil))
	exitCode := make(chan int, 1)
	dataPath := filepath.Join(t.TempDir(), "state.json")
	go func() {
		exitCode <- run(ctx, "127.0.0.1:0", dataPath, instance, logger)
	}()

	var line string
	select {
	case line = <-logs:
	case <-time.After(3 * time.Second):
		t.Fatal("timed out waiting for listening log")
	}
	var record logRecord
	if err := json.Unmarshal([]byte(line), &record); err != nil {
		t.Fatalf("decode listening log: %v", err)
	}
	if record.Level != "INFO" || record.Message != "backend listening" || record.Address == "" {
		t.Fatalf("unexpected listening log: %+v", record)
	}

	client := &http.Client{Timeout: 2 * time.Second}
	response, err := client.Get("http://" + record.Address + "/api/v1/health")
	if err != nil {
		t.Fatalf("request health endpoint: %v", err)
	}
	t.Cleanup(func() { _ = response.Body.Close() })
	if response.StatusCode != http.StatusOK {
		t.Fatalf("health status = %d, want %d", response.StatusCode, http.StatusOK)
	}
	var health map[string]string
	if err := json.NewDecoder(response.Body).Decode(&health); err != nil {
		t.Fatalf("decode health response: %v", err)
	}
	if err := response.Body.Close(); err != nil {
		t.Fatalf("close health response: %v", err)
	}
	if health["status"] != "ok" || health["service"] != "steel-tide-backend" || health["instance"] != instance {
		t.Fatalf("unexpected health response: %+v", health)
	}

	cancel()
	select {
	case code := <-exitCode:
		if code != 0 {
			t.Fatalf("exit code after shutdown = %d, want 0", code)
		}
	case <-time.After(3 * time.Second):
		t.Fatal("timed out waiting for graceful shutdown")
	}
}
