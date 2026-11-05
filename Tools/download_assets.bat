@echo off
setlocal EnableDelayedExpansion
rem ---------------------------------------------------------------
rem  Downloads large assets that are excluded from git (.gitignore).
rem  Usage: run from anywhere - Tools\download_assets.bat
rem  Add new entries with:  call :fetch "<url>" "<repo-relative path>" <bytes>
rem  Temp files go to %TEMP% so the Unity Editor never imports partial files.
rem ---------------------------------------------------------------
cd /d "%~dp0.."
set "FAILED=0"

call :fetch "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin" "Assets\StreamingAssets\Whisper\ggml-base.bin" 147951465

echo.
if "%FAILED%"=="0" (echo Done.) else (echo Some downloads FAILED.)
exit /b %FAILED%

:fetch
set "URL=%~1"
set "DEST=%~2"
set "SIZE=%~3"
if exist "%DEST%" (
    for %%F in ("%DEST%") do set "CUR=%%~zF"
    if "!CUR!"=="%SIZE%" (
        echo [skip] %DEST% ^(already present^)
        exit /b 0
    )
    echo [redo] %DEST% size mismatch ^(!CUR! vs %SIZE%^)
)
for %%D in ("%DEST%") do if not exist "%%~dpD" mkdir "%%~dpD"
set "TMPF=%TEMP%\dl_%RANDOM%_%~nx2"
echo [get ] %DEST%
curl.exe -L --fail --retry 3 -o "%TMPF%" "%URL%"
if errorlevel 1 (
    echo [FAIL] %URL%
    del /q "%TMPF%" 2>nul
    set "FAILED=1"
    exit /b 1
)
move /y "%TMPF%" "%DEST%" >nul
exit /b 0
