# Create libs directory
$libsDir = Join-Path $PSScriptRoot "libs"
if (-not (Test-Path $libsDir)) {
    New-Item -ItemType Directory -Path $libsDir | Out-Null
}

$tempZip = Join-Path $libsDir "zxing_net.zip"
$extractDir = Join-Path $libsDir "zxing_extracted"

# ZXing.Net NuGet package URL
$url = "https://www.nuget.org/api/v2/package/ZXing.Net/0.16.9"

Write-Host "Downloading ZXing.Net package from $url..." -ForegroundColor Cyan

# Force TLS 1.2
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# Download package
try {
    Invoke-WebRequest -Uri $url -OutFile $tempZip -UseBasicParsing
    Write-Host "Download complete. Extracting DLL for .NET 3.5..." -ForegroundColor Cyan

    # Clean extract directory
    if (Test-Path $extractDir) {
        Remove-Item -Recurse -Force $extractDir
    }
    
    # Compatibility with older PowerShell versions
    if (Get-Command Expand-Archive -ErrorAction SilentlyContinue) {
        Expand-Archive -Path $tempZip -DestinationPath $extractDir -Force
    } else {
        $shell = New-Object -ComObject Shell.Application
        $zipFile = $shell.NameSpace($tempZip)
        $destFolder = $shell.NameSpace($extractDir)
        if (-not (Test-Path $extractDir)) {
            New-Item -ItemType Directory -Path $extractDir | Out-Null
        }
        $destFolder.CopyHere($zipFile.Items(), 16)
    }

    # Extract DLL
    $dllSource = Join-Path $extractDir "lib/net35/zxing.dll"
    if (Test-Path $dllSource) {
        $dllDest = Join-Path $libsDir "zxing.dll"
        Copy-Item -Path $dllSource -Destination $dllDest -Force
        Write-Host "Successfully extracted zxing.dll to: $dllDest" -ForegroundColor Green
    } else {
        Write-Error "Could not find lib/net35/zxing.dll in the extracted package!"
    }
} catch {
    Write-Error "Error during download or extraction: $_"
} finally {
    # Clean temporary files
    if (Test-Path $tempZip) {
        Remove-Item -Path $tempZip -Force
    }
    if (Test-Path $extractDir) {
        Remove-Item -Recurse -Force $extractDir
    }
}
