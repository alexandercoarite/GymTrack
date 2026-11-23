# GymTrack

Aplicación web para la gestión de un gimnasio: interfaz de **cliente** (cuestionario, rutinas, catálogo de máquinas,
evaluación corporal opcional y seguimiento del progreso) e interfaz de **administrador** (máquinas, categorías,
usuarios, horarios y mantenimiento).

- Blazor Web App (.NET 10, interactividad Server) · Tailwind CSS 4 · JavaScript solo para la cámara
- Entity Framework Core + **SQLite**
- ASP.NET Core Identity (correo/usuario + contraseña, roles `Admin` y `Client`)
- Interfaz completamente en español

## Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org) para regenerar Tailwind. Ejecuta `npm install` dentro de `src/GymTrack.Web`. La aplicación usa su hoja base si Tailwind aún no se ha generado.
- Herramienta de EF Core: `dotnet tool install --global dotnet-ef`

## Estructura

```
GymTrack/
├─ src/
│  ├─ GymTrack.Domain/          Entidades y enums (sin dependencias)
│  ├─ GymTrack.Application/     Interfaces, DTOs, validaciones y reglas (generador de rutinas)
│  ├─ GymTrack.Infrastructure/  EF Core + SQLite, Identity, servicios, almacenamiento, seed
│  └─ GymTrack.Web/             Blazor, Tailwind, Program.cs, endpoints
├─ tests/GymTrack.Tests/        Pruebas xUnit (SQLite en memoria)
└─ scripts/                     Administrador inicial y migraciones
```

Los componentes Blazor solo llaman a interfaces (`IMachineService`, `IRoutineService`...). Cada servicio crea un
`DbContext` corto por operación (fábrica), que es lo recomendable en Blazor Server.

## Primer arranque

```bash
# 1) Administrador inicial (se guarda en user-secrets, NO en el repositorio)
./scripts/configurar-admin.sh admin@tudominio.com 'UnaClaveSegura1'      # Windows: .\scripts\configurar-admin.ps1

# 2) Ejecutar
dotnet run --project src/GymTrack.Web
```

Abre la URL que muestre la consola (por defecto `https://localhost:7180`). Con la base de datos vacía se crean:
roles, el administrador (si configuraste el paso 1), categorías, grupos musculares, 15 máquinas de demostración,
ejercicios y un horario semanal. Para desactivar los datos de demostración usa `Seed:DemoData = false`.

Si no configuras el administrador, la aplicación arranca igual y deja una advertencia en el registro.
Los clientes se registran desde la web.

## Migraciones de Entity Framework Core

No hay migraciones incluidas. Mientras no existan, la aplicación crea la base con `EnsureCreated` y lo avisa en el registro.
Para generar la migración inicial (recomendado antes de usar la aplicación en serio):

```bash
rm -f src/GymTrack.Web/App_Data/gymtrack.db*          # borra la base creada con EnsureCreated
./scripts/crear-migracion.sh InitialCreate            # Windows: .\scripts\crear-migracion.ps1
dotnet run --project src/GymTrack.Web                 # aplica la migración al arrancar (MigrateAsync)
```

Para cambios posteriores: `./scripts/crear-migracion.sh NombreDelCambio`.

## Configuración (`appsettings.json`)

| Clave | Uso |
|---|---|
| `ConnectionStrings:DefaultConnection` | Archivo SQLite (`Data Source=App_Data/gymtrack.db`) |
| `Storage:PrivatePhotosPath` | Carpeta privada de fotografías corporales (fuera de `wwwroot`) |
| `Gym:*` | Nombre, dirección, teléfono, correo y zona horaria (`America/La_Paz`) del gimnasio |
| `InitialAdmin:*` | Administrador inicial: usa **user-secrets** o variables de entorno (`InitialAdmin__Password`) |
| `Seed:DemoData` | Crear máquinas y ejercicios de demostración |
| `RoutineRules` | (Opcional) Series, repeticiones y descansos por objetivo; ver `RoutineRules` en `RoutineGenerator.cs` |

No guardes credenciales reales en el repositorio.

## Roles y seguridad

- Roles `Admin` y `Client`. Las páginas administrativas exigen la política `AdminOnly`, y **además** cada servicio
  administrativo verifica el rol en el servidor (`EnsureAdminAsync`), no solo se oculta en la interfaz.
- Contraseñas con ASP.NET Core Identity (mínimo 8 caracteres, mayúscula, minúscula y número), bloqueo tras 5 intentos fallidos.
- Una cuenta desactivada no puede iniciar sesión y sus sesiones abiertas se cierran en aproximadamente un minuto.
- Las fotografías corporales se guardan en `App_Data/private-photos/{usuario}/` y solo se sirven en `/api/fotos/{id}`
  al propietario autenticado. **El administrador ve fechas y resultados de las evaluaciones, no las fotografías.**
- Se valida tipo real (por contenido) y tamaño de las imágenes: máquinas 2 MB, fotografías 5 MB (JPG, PNG, WEBP).
- Las operaciones administrativas importantes se registran en la tabla `AuditLogs`.
- Cierre de sesión por POST con token antiforgery; cabeceras de seguridad básicas.

## Evaluación corporal y servicio de visión artificial

La evaluación es **opcional** y requiere consentimiento explícito. Por defecto **no hay servicio de análisis
configurado** (`NotConfiguredBodyAnalysisService`): la aplicación lo informa, no simula resultados y permite guardar
fotografías de progreso y registrar medidas manualmente en «Mi progreso».

Para conectar un proveedor:

1. Crea una clase que implemente `IBodyAnalysisService` (`GymTrack.Application/Interfaces`) en
   `GymTrack.Infrastructure/Services`. Debe devolver `IsConfigured = true` y un `BodyAnalysisResult` con un resumen
   y, opcionalmente, indicadores en JSON.
2. Guarda la clave del proveedor con `dotnet user-secrets set "BodyAnalysis:ApiKey" "..." --project src/GymTrack.Web`.
3. En `GymTrack.Infrastructure/DependencyInjection.cs` reemplaza el registro de `NotConfiguredBodyAnalysisService`
   por tu implementación.

Reglas que debe respetar el proveedor: sin reconocimiento facial ni identificación de personas, sin presentar
estimaciones como medidas clínicas o valores exactos de peso, masa muscular o grasa corporal.

## Correo (recuperación de contraseña)

`LoggingEmailService` **no envía correos**: escribe el enlace de recuperación en el registro de la aplicación
(útil en desarrollo). Para producción implementa `IEmailService` con SMTP o un proveedor de correo y cambia su registro en
`DependencyInjection.cs`.

## Rutinas

El generador (`RoutineGenerator`) es un sistema de reglas configurable, sin IA. Usa el último cuestionario del cliente
y solo propone ejercicios cuyas máquinas existen y están **disponibles** (una máquina en mantenimiento no se asigna).
Cada ejercicio se enlaza con máquinas por grupo muscular, tipo y, opcionalmente, una palabra clave del nombre
(`Exercise.MaquinaReferencia`). Las rutinas no son prescripciones médicas; si el cliente declara limitaciones se recomienda consultar a un profesional.

## Pruebas

```bash
dotnet test
```

## Limitaciones conocidas

- No hay CRUD de ejercicios en la interfaz (se siembran en `DbSeeder`).
- El cliente no puede eliminar su cuenta completa desde la web (sí sus evaluaciones, fotografías y progreso); debe solicitarlo al personal.
- Las imágenes de máquinas se guardan en `wwwroot/uploads/machines` (públicas, no son datos personales).
- La imagen principal de la página de inicio es una ilustración vectorial; sustitúyela por una fotografía propia en `Home.razor`.
- No hay confirmación de correo al registrarse (`RequireConfirmedAccount = false`).
