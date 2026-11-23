# Crea una migración de EF Core. Uso: .\scripts\crear-migracion.ps1 -Nombre InitialCreate
param([string]$Nombre = "InitialCreate")
dotnet ef migrations add $Nombre `
  --project src/GymTrack.Infrastructure `
  --startup-project src/GymTrack.Web `
  --output-dir Migrations
