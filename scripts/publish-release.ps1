param(
  [string]$Configuration = "Release",
  [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root "artifacts\win-x64"
$project = Join-Path $root "src\MiniPrinter\MiniPrinter.csproj"
$iss = Join-Path $root "installer\MiniPrinter.iss"

Write-Host "Publishing self-contained MiniPrinter $Version (win-x64)..."
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
New-Item -ItemType Directory -Path $publishDir | Out-Null

dotnet publish $project `
  -c $Configuration `
  -r win-x64 `
  --self-contained true `
  -p:Version=$Version `
  -p:PublishReadyToRun=true `
  -p:DebugType=none `
  -p:DebugSymbols=false `
  -o $publishDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

$iscc = @(
  "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
  "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
  "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) { throw "Inno Setup not found (ISCC.exe)." }

Write-Host "Building installer with $iscc"
& $iscc $iss
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compile failed" }

Get-ChildItem (Join-Path $root "artifacts") -File | Format-Table Name, Length, LastWriteTime
