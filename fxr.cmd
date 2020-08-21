@echo off
setlocal
set PROJECT_FILE=%~dp0src\fxr\fxr.csproj
dotnet run --project %PROJECT_FILE% -- %*