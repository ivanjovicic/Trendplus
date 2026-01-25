@echo off
cd /d C:\Users\Ivan\source\repos\Trendplus2\Trendplus2
set ASPNETCORE_ENVIRONMENT=Development
set ASPNETCORE_URLS=http://localhost:8080
dotnet run --project Api.csproj --no-launch-profile
pause