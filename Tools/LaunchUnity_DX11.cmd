@echo off
REM Uruchom Unity w trybie Direct3D11 — stabilniejszy na NVIDIA niż domyślny D3D12 w edytorze.
set "UNITY_EXE=C:\Program Files\Unity\Hub\Editor\6000.2.6f2\Editor\Unity.exe"
set "PROJECT=%~dp0.."

if not exist "%UNITY_EXE%" (
    echo Nie znaleziono Unity pod: %UNITY_EXE%
    echo Zaktualizuj sciezke w Tools\LaunchUnity_DX11.cmd
    pause
    exit /b 1
)

start "" "%UNITY_EXE%" -projectPath "%PROJECT%" -force-d3d11
