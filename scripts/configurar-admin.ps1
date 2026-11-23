# Guarda el administrador inicial en user-secrets (fuera del repositorio).
# Uso: .\scripts\configurar-admin.ps1 -Correo correo@ejemplo.com -Clave 'ContraseñaSegura1'
param(
  [Parameter(Mandatory = $true)][string]$Correo,
  [Parameter(Mandatory = $true)][string]$Clave
)
dotnet user-secrets set "InitialAdmin:Email" $Correo --project src/GymTrack.Web
dotnet user-secrets set "InitialAdmin:Password" $Clave --project src/GymTrack.Web
Write-Host "Administrador inicial configurado. Se creará al iniciar la aplicación si aún no existe ninguno."
