FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY NuGet.Config .
COPY backend/src backend/src
COPY data/real-2026/calendar-2026.json data/real-2026/calendar-2026.json
COPY data/real-2026/clubs.json data/real-2026/clubs.json
COPY data/real-2026/series-2026.json data/real-2026/series-2026.json
COPY data/real-2026/candidates-playable.json data/real-2026/candidates-playable.json
COPY data/south-america/clubs-2026.json data/south-america/clubs-2026.json
RUN dotnet publish backend/src/Api/Api.csproj -c Release -o /app
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet","Api.dll"]
