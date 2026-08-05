package main

import (
	"context"
	"flag"
	"log/slog"
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
	flag.Parse()

	logger := slog.New(slog.NewJSONHandler(os.Stdout, &slog.HandlerOptions{Level: slog.LevelInfo}))
	gameServer, err := game.NewServer(*dataPath, logger)
	if err != nil {
		logger.Error("initialize backend", "error", err)
		os.Exit(1)
	}
	httpServer := &http.Server{
		Addr:              *address,
		Handler:           gameServer.Handler(),
		ReadHeaderTimeout: 4 * time.Second,
		ReadTimeout:       8 * time.Second,
		WriteTimeout:      8 * time.Second,
		IdleTimeout:       60 * time.Second,
	}

	go func() {
		logger.Info("backend listening", "address", *address)
		if err := httpServer.ListenAndServe(); err != nil && err != http.ErrServerClosed {
			logger.Error("backend stopped", "error", err)
			os.Exit(1)
		}
	}()

	stop := make(chan os.Signal, 1)
	signal.Notify(stop, os.Interrupt, syscall.SIGTERM)
	<-stop
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	if err := httpServer.Shutdown(ctx); err != nil {
		logger.Error("shutdown", "error", err)
	}
}
