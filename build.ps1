param(
    [switch]$NoZip
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$src = Join-Path $root "src"
$assets = Join-Path $root "assets"
$dist = Join-Path $root "dist\QuickPortal"
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$framework = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"

$webViewBin = "C:\Program Files\Microsoft Office\root\Office16\ADDINS\Microsoft Power Query for Excel Integrated\bin"
$coreDll = Join-Path $webViewBin "Microsoft.Web.WebView2.Core.dll"
$winFormsDll = Join-Path $webViewBin "Microsoft.Web.WebView2.WinForms.dll"
$loaderDll = Join-Path $webViewBin "WebView2Loader.dll"

foreach ($path in @($csc, $coreDll, $winFormsDll, $loaderDll)) {
    if (-not (Test-Path $path)) { throw "Missing build dependency: $path" }
}

if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Force -Path $dist | Out-Null

$exe = Join-Path $dist "QuickPortal.exe"
$sources = Get-ChildItem $src -Filter "*.cs" | ForEach-Object { $_.FullName }

$arguments = @(
    "/nologo",
    "/target:winexe",
    "/platform:anycpu",
    "/optimize+",
    "/debug-",
    "/codepage:65001",
    "/out:$exe",
    "/win32icon:$(Join-Path $assets 'quickportal.ico')",
    "/win32manifest:$(Join-Path $src 'app.manifest')",
    "/resource:$(Join-Path $assets 'quickportal-256.png'),QuickPortal.quickportal-256.png",
    "/reference:$(Join-Path $framework 'System.dll')",
    "/reference:$(Join-Path $framework 'System.Core.dll')",
    "/reference:$(Join-Path $framework 'System.Drawing.dll')",
    "/reference:$(Join-Path $framework 'System.Windows.Forms.dll')",
    "/reference:$(Join-Path $framework 'System.Web.Extensions.dll')",
    "/reference:$(Join-Path $framework 'System.Security.dll')",
    "/reference:$coreDll",
    "/reference:$winFormsDll"
) + $sources

Write-Host "Compiling QuickPortal..."
& $csc $arguments
if ($LASTEXITCODE -ne 0) { throw "Compilation failed with exit code $LASTEXITCODE" }

Copy-Item $coreDll $dist
Copy-Item $winFormsDll $dist
Copy-Item $loaderDll $dist

$appConfig = Join-Path $src "App.config"
if (Test-Path $appConfig) {
    Copy-Item $appConfig (Join-Path $dist "QuickPortal.exe.config")
}

$readmeSrc = Join-Path $root "README.md"
if (Test-Path $readmeSrc) {
    Copy-Item $readmeSrc (Join-Path $dist "README.md")
}

$size = [math]::Round((Get-ChildItem $dist -Recurse | Measure-Object Length -Sum).Sum / 1MB, 2)
Write-Host "Build OK: $exe ($size MB total)"

if (-not $NoZip) {
    $zip = Join-Path $root "dist\QuickPortal-1.0.5-win-x64.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $dist '*') -DestinationPath $zip -CompressionLevel Optimal
    Write-Host "ZIP: $zip"
}
