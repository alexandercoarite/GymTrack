#!/usr/bin/env bash
# Guarda el administrador inicial en user-secrets (fuera del repositorio).
# Uso: ./scripts/configurar-admin.sh correo@ejemplo.com 'ContraseñaSegura1'
set -euo pipefail
if [ $# -lt 2 ]; then echo "Uso: $0 <correo> <contraseña>"; exit 1; fi
dotnet user-secrets set "InitialAdmin:Email" "$1" --project src/GymTrack.Web
dotnet user-secrets set "InitialAdmin:Password" "$2" --project src/GymTrack.Web
echo "Administrador inicial configurado. Se creará al iniciar la aplicación si aún no existe ninguno."
