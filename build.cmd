@echo off
setlocal
cd /d "%~dp0"
title Elite-O Preview build

where dotnet >nul 2>nul
if errorlevel 1 (
  echo The .NET 10 SDK is not installed.
  echo Get it from https://dotnet.microsoft.com/download/dotnet/10.0  ^(SDK, Windows x64^), install it, then run this file again.
  pause
  exit /b 1
)

echo === Building Elite-O Preview ===
rem One self-contained exe: .NET is bundled, so it runs on a PC without .NET installed.
dotnet publish "src\Eve-O-Preview\Eve-O-Preview.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=embedded -o "Elite-O Preview"
if errorlevel 1 (
  echo.
  echo BUILD FAILED - scroll up for the first error and send it to Claude.
  pause
  exit /b 1
)

echo.
echo === Testing commander detection ===
dotnet test "src\tests\Eve-O-Preview.Tests\Eve-O-Preview.Tests.csproj" --filter "FullyQualifiedName~EliteCommanderResolverTests"
if errorlevel 1 (
  echo.
  echo The app was built, but the detection tests failed - send the output above to Claude.
) else (
  echo Detection tests passed.
)

echo.
echo Done. Start: "%~dp0Elite-O Preview\Elite-O Preview.exe"  ^(it asks for admin rights^)
echo That exe runs on its own: copy just "Elite-O Preview.exe" to another PC, no .NET install needed.
pause
