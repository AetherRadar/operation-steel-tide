package main

import (
	"context"
	"errors"
	"flag"
	"fmt"
	"log/slog"
	"net"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"

	"operation-steel-tide/backend/internal/game"
)

func main() {
	address := flag.String("addr", "127.0.0.1:8787", "HTTP listen address")
	dataPath := flag.String("data", "data/state.json", "persistent state file")
	instance := flag.String("instance", "", "backend instance identity")
	flag.Parse()

	logger := slog.New(slog.NewJSONHandler(os.Stdout, &slog.HandlerOptions{Level: slog.LevelInfo}))
	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	exitCode := run(ctx, *address, *dataPath, *instance, logger)
	stop()
	if exitCode != 0 {
		os.Exit(exitCode)
	}
}

func run(ctx context.Context, address string, dataPath string, instance string, logger *slog.Logger) int {
	if err := serveBackend(ctx, address, dataPath, instance, logger); err != nil {
		logger.Error("backend failed", "error", err)
		return 1
	}
	return 0
}

func serveBackend(ctx context.Context, address string, dataPath string, instance string, logger *slog.Logger) error {
	gameServer, err := game.NewServer(dataPath, logger, game.WithInstance(instance))
	if err != nil {
		return fmt.Errorf("initialize backend: %w", err)
	}
	httpServer := &http.Server{
		Addr:              address,
		Handler:           gameServer.Handler(),
		ReadHeaderTimeout: 4 * time.Second,
		ReadTimeout:       8 * time.Second,
		WriteTimeout:      8 * time.Second,
		IdleTimeout:       60 * time.Second,
	}

	listener, err := net.Listen("tcp", address)
	if err != nil {
		return fmt.Errorf("listen on %q: %w", address, err)
	}
	defer listener.Close()

	serveErr := make(chan error, 1)
	go func() {
		serveErr <- httpServer.Serve(listener)
	}()
	logger.Info("backend listening", "address", listener.Addr().String())

	select {
	case err := <-serveErr:
		if err != nil && !errors.Is(err, http.ErrServerClosed) {
			return fmt.Errorf("serve backend: %w", err)
		}
		return nil
	case <-ctx.Done():
	}

	shutdownContext, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	if err := httpServer.Shutdown(shutdownContext); err != nil {
		return fmt.Errorf("shutdown backend: %w", err)
	}
	if err := <-serveErr; err != nil && !errors.Is(err, http.ErrServerClosed) {
		return fmt.Errorf("serve backend: %w", err)
	}
	return nil
}
