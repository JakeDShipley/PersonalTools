@echo off
setlocal EnableExtensions
title PersonalTools MariaDB Tunnel

set "PLINK=C:\Program Files\PuTTY\plink.exe"
set "SSH_KEY=C:\Users\jakeh\OneDrive\Documents\SSH Keys\jake-ionos.ppk"
set "SSH_USER=jake"
set "SSH_HOST=194.164.122.39"
set "SSH_PORT=34922"
set "LOCAL_PORT=3307"
set "DATABASE_PORT=3306"

if not exist "%PLINK%" (
    echo.
    echo PuTTY's plink.exe was not found:
    echo %PLINK%
    echo.
    echo Install PuTTY or update the PLINK path near the top of this file.
    goto :finish
)

if not exist "%SSH_KEY%" (
    echo.
    echo The SSH private key was not found:
    echo %SSH_KEY%
    echo.
    echo Update the SSH_KEY path near the top of this file.
    goto :finish
)

set "TUNNEL_PID="
for /f "delims=" %%P in ('powershell.exe -NoProfile -Command "$connection = Get-NetTCPConnection -State Listen -LocalPort %LOCAL_PORT% -ErrorAction SilentlyContinue ^| Select-Object -First 1; if ($connection) { $process = Get-Process -Id $connection.OwningProcess -ErrorAction SilentlyContinue; if ($process.Name -eq 'plink') { $process.Id } }"') do set "TUNNEL_PID=%%P"

if defined TUNNEL_PID goto :stopTunnel

powershell.exe -NoProfile -Command "if (Get-NetTCPConnection -State Listen -LocalPort %LOCAL_PORT% -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }"
if errorlevel 1 (
    echo.
    echo Local port %LOCAL_PORT% is already being used by another application.
    echo The tunnel was not started.
    goto :finish
)

echo.
echo Starting MariaDB tunnel...
echo 127.0.0.1:%LOCAL_PORT%  -- SSH --^>  127.0.0.1:%DATABASE_PORT%
echo SSH destination: %SSH_USER%@%SSH_HOST%:%SSH_PORT%
echo.
echo A separate PuTTY window will remain open while the tunnel is running.
echo If PuTTY asks you to trust the server key, verify and accept it once.

start "PersonalTools MariaDB Tunnel" "%PLINK%" -ssh -N -agent -P %SSH_PORT% -i "%SSH_KEY%" -L %LOCAL_PORT%:127.0.0.1:%DATABASE_PORT% %SSH_USER%@%SSH_HOST%

timeout /t 5 /nobreak >nul
powershell.exe -NoProfile -Command "if (Get-NetTCPConnection -State Listen -LocalPort %LOCAL_PORT% -ErrorAction SilentlyContinue) { exit 0 } else { exit 1 }"
if errorlevel 1 (
    echo.
    echo The tunnel did not open.
    echo Check the separate "PersonalTools MariaDB Tunnel" PuTTY window for a
    echo host-key confirmation, private-key passphrase prompt, or connection error.
) else (
    echo.
    echo MariaDB tunnel started successfully on 127.0.0.1:%LOCAL_PORT%.
    echo You can now run PersonalTools locally.
)
goto :finish

:stopTunnel
echo.
echo MariaDB tunnel is running as process %TUNNEL_PID%.
echo Stopping it now...
taskkill /PID %TUNNEL_PID% /T /F >nul 2>&1

if errorlevel 1 (
    echo The tunnel could not be stopped. Try running this file as your normal user.
) else (
    echo MariaDB tunnel stopped successfully.
)

:finish
echo.
pause
endlocal
