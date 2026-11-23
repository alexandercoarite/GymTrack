#!/usr/bin/env bash
# Crea una migración de EF Core. Uso: ./scripts/crear-migracion.sh InitialCreate
set -euo pipefail
NAME="${1:-InitialCreate}"
dotnet tool restore >/dev/null 2>&1 || true
dotnet ef migrations add "$NAME" \
  --project src/GymTrack.Infrastructure \
  --startup-project src/GymTrack.Web \
  --output-dir Migrations
