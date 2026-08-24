# Сборка идёт внутри образа: разработчику не нужен установленный .NET SDK,
# достаточно docker compose up.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Сначала только csproj — слой restore кэшируется и не пересобирается
# при каждой правке исходников.
COPY Directory.Build.props ./
COPY src/Timesheet.Domain/Timesheet.Domain.csproj src/Timesheet.Domain/
COPY src/Timesheet.Api/Timesheet.Api.csproj src/Timesheet.Api/
RUN dotnet restore src/Timesheet.Api/Timesheet.Api.csproj

COPY src/ src/
RUN dotnet publish src/Timesheet.Api/Timesheet.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Timesheet.Api.dll"]
