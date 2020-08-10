@echo off
setlocal
set PROJECT_FILE=%~dp0src\fxp\fxp.csproj
dotnet run --project %PROJECT_FILE% -- %~dp0..\runtime\artifacts\bin\ref\net5.0\ -o apis.csv %*