@echo off
echo ==================================================
echo   2FA Authenticator - Build Script
echo ==================================================
echo.

set MSBUILD_PATH=C:\Windows\Microsoft.NET\Framework\v3.5\MSBuild.exe

if not exist "%MSBUILD_PATH%" (
    echo [ERROR] .NET Framework 3.5 compiler not found!
    echo Expected path: %MSBUILD_PATH%
    echo Please enable .NET Framework 3.5 in Windows Features.
    echo.
    pause
    exit /b 1
)

echo $png = 'login.png'; if (-not (Test-Path $png)) { $png = 'logo.png' } > temp_ico.ps1
echo if (Test-Path $png) { >> temp_ico.ps1
echo     $bytes = [System.IO.File]::ReadAllBytes($png) >> temp_ico.ps1
echo     $ms = New-Object System.IO.MemoryStream(,$bytes) >> temp_ico.ps1
echo     $bmp = [System.Drawing.Image]::FromStream($ms) >> temp_ico.ps1
echo     $w = $bmp.Width; $h = $bmp.Height; $bmp.Dispose(); $ms.Dispose() >> temp_ico.ps1
echo     $icoW = if ($w -ge 256) { 0 } else { $w } >> temp_ico.ps1
echo     $icoH = if ($h -ge 256) { 0 } else { $h } >> temp_ico.ps1
echo     $header = [byte[]](0,0,1,0,1,0) >> temp_ico.ps1
echo     $sizeBytes = [System.BitConverter]::GetBytes($bytes.Length) >> temp_ico.ps1
echo     $offsetBytes = [System.BitConverter]::GetBytes(22) >> temp_ico.ps1
echo     $entry = [byte[]]($icoW, $icoH, 0, 0, 1, 0, 32, 0) + $sizeBytes + $offsetBytes >> temp_ico.ps1
echo     [System.IO.File]::WriteAllBytes('app.ico', ($header + $entry + $bytes)) >> temp_ico.ps1
echo } >> temp_ico.ps1

powershell -ExecutionPolicy Bypass -File temp_ico.ps1 >nul 2>&1
if exist "temp_ico.ps1" (
    del /f /q temp_ico.ps1
)

echo Starting build (Release configuration)...
echo.

"%MSBUILD_PATH%" 2FA.csproj /t:Rebuild /p:Configuration=Release

if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Build failed! Please check the errors above.
    echo.
    if exist "app.ico" (
        del /f /q "app.ico"
    )
    pause
    exit /b %errorlevel%
)

if exist "app.ico" (
    del /f /q "app.ico"
)
if exist "bin\Release\TwoFA_Authenticator.pdb" (
    del /f /q "bin\Release\TwoFA_Authenticator.pdb"
)
if exist "bin\Release\TwoFA_Authenticator.exe.config" (
    del /f /q "bin\Release\TwoFA_Authenticator.exe.config"
)

echo.
echo ==================================================
echo [SUCCESS] Build completed successfully!
echo.
echo Executable generated at:
echo bin\Release\TwoFA_Authenticator.exe
echo (Other files have been cleaned up)
echo ==================================================
echo.
pause
