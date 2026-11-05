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

rem Local word judge (2단계 말 무게 발판): llama.cpp server (Vulkan) + Kanana 1.5 2.1B Instruct (Apache-2.0), Q4_K_M
call :fetch "https://huggingface.co/DevQuasar/kakaocorp.kanana-1.5-2.1b-instruct-2505-GGUF/resolve/main/kakaocorp.kanana-1.5-2.1b-instruct-2505.Q4_K_M.gguf" "Assets\StreamingAssets\LLM\kanana-2.1b-Q4_K_M.gguf" 1522796768
call :fetch "https://github.com/ggml-org/llama.cpp/releases/download/b12128/llama-b12128-bin-win-vulkan-x64.zip" "Assets\StreamingAssets\LLM\llama.zip" 37684327
if exist "Assets\StreamingAssets\LLM\llama.zip" if not exist "Assets\StreamingAssets\LLM\llama-server.exe" (
    echo [unzip] llama.cpp
    tar -xf "Assets\StreamingAssets\LLM\llama.zip" -C "Assets\StreamingAssets\LLM"
)

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
