param(
    [string]$Version = "0.0.0",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root "release_local"
$zipPath = Join-Path $root "CampusNetAssistant.local.zip"
$extractDir = Join-Path $root "release_local_extracted"
$exeName = "CampusNetAssistant.exe"

Write-Host "==> [1/6] Clean old outputs"
Remove-Item -Recurse -Force $publishDir -ErrorAction SilentlyContinue
Remove-Item -Force $zipPath -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force $extractDir -ErrorAction SilentlyContinue

Write-Host "==> [2/6] Build Release"
Push-Location $root
try {
    dotnet build -c Release

    Write-Host "==> [3/6] Publish (same as CI)"
    dotnet publish -c Release -r $Runtime --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:PublishTrimmed=true `
        -p:EnableCompressionInSingleFile=true `
        -p:_SuppressWinFormsTrimError=true `
        -p:Version=$Version `
        -p:DebugType=embedded `
        -o $publishDir

    $exePath = Join-Path $publishDir $exeName
    if (-not (Test-Path $exePath)) {
        throw "Publish output missing: $exePath"
    }

    $sizeMb = [math]::Round((Get-Item $exePath).Length / 1MB, 2)
    Write-Host "Published EXE size: $sizeMb MB"

    Write-Host "==> [4/6] Smoke test published EXE"
    $p1 = Start-Process -FilePath $exePath -PassThru
    Start-Sleep -Seconds 8
    if ($p1.HasExited) {
        throw "Published EXE exited unexpectedly within 8 seconds (possible startup crash)."
    }
    Stop-Process -Id $p1.Id -Force

    Write-Host ""
    Write-Host "✅ Local release test passed."
    Write-Host "Output dir : $publishDir"
    Write-Host "Output exe : $exePath"
}
finally {
    Pop-Location
}
