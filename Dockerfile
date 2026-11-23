FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/GymTrack.Domain/GymTrack.Domain.csproj src/GymTrack.Domain/
COPY src/GymTrack.Application/GymTrack.Application.csproj src/GymTrack.Application/
COPY src/GymTrack.Infrastructure/GymTrack.Infrastructure.csproj src/GymTrack.Infrastructure/
COPY src/GymTrack.Web/GymTrack.Web.csproj src/GymTrack.Web/
RUN dotnet restore src/GymTrack.Web/GymTrack.Web.csproj

COPY src/ src/
RUN dotnet publish src/GymTrack.Web/GymTrack.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p App_Data wwwroot/uploads/machines && chown -R app:app App_Data wwwroot/uploads/machines
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "GymTrack.Web.dll"]