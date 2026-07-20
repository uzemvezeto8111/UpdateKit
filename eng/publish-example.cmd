@echo off
setlocal

for %%I in ("%~dp0..") do set "REPOSITORY_ROOT=%%~fI"
set "PUBLISH_OUTPUT=%REPOSITORY_ROOT%\artifacts\publish\UpdateKit.Example.WinForms\win-x64"
set "DOTNET_COMMAND=dotnet"

if exist "%USERPROFILE%\.dotnet\sdk" set "DOTNET_COMMAND=%USERPROFILE%\.dotnet\dotnet.exe"
if defined DOTNET_ROOT if exist "%DOTNET_ROOT%\sdk" set "DOTNET_COMMAND=%DOTNET_ROOT%\dotnet.exe"

"%DOTNET_COMMAND%" --version >nul 2>nul
if errorlevel 1 (
    echo The .NET 8 SDK could not be found. Install it or set DOTNET_ROOT before running this script.
    exit /b 1
)

pushd "%REPOSITORY_ROOT%"
if exist "%PUBLISH_OUTPUT%" rmdir /s /q "%PUBLISH_OUTPUT%"
if exist "%PUBLISH_OUTPUT%" (
    echo The existing publish output could not be removed: %PUBLISH_OUTPUT%
    popd
    exit /b 1
)

"%DOTNET_COMMAND%" publish samples\UpdateKit.Example.WinForms\UpdateKit.Example.WinForms.csproj ^
  --configuration Release ^
  --runtime win-x64 ^
  --self-contained true ^
  -p:PublishProfile=WinX64SelfContained ^
  -p:PublishSingleFile=true ^
  -p:PublishTrimmed=false ^
  -p:DebugType=None ^
  -p:DebugSymbols=false ^
  --output "%PUBLISH_OUTPUT%"

if errorlevel 1 goto publish_failed

echo.
echo Published UpdateKit.Example.WinForms to:
echo   %PUBLISH_OUTPUT%
echo.
echo Launch UpdateKit.Example.WinForms.exe from that directory.
popd
exit /b 0

:publish_failed
set "PUBLISH_EXIT_CODE=%ERRORLEVEL%"
popd
exit /b %PUBLISH_EXIT_CODE%
