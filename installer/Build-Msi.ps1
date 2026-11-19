$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $projectRoot 'TreeScan.csproj'
$version = ([xml](Get-Content -LiteralPath $project -Raw)).Project.PropertyGroup.Version
$wixVersion = '6.0.2'
$toolPath = Join-Path $projectRoot "obj/wix-$wixVersion"
$wix = Join-Path $toolPath 'wix.exe'

if (-not (Test-Path -LiteralPath $wix)) {
    dotnet tool install wix --version $wixVersion --tool-path $toolPath
    if ($LASTEXITCODE -ne 0) { throw 'WiX installeren is mislukt.' }
}

dotnet build $project -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'TreeScan bouwen is mislukt.' }

$app = Join-Path $projectRoot 'bin/Release/net48/TreeScan.exe'
$output = Join-Path $projectRoot "bin/installer/TreeScan-$version-x64.msi"
New-Item -ItemType Directory -Path (Split-Path -Parent $output) -Force | Out-Null
& $wix build (Join-Path $PSScriptRoot 'TreeScan.wxs') -arch x64 -d "AppPath=$app" -d "AppVersion=$version" -o $output
if ($LASTEXITCODE -ne 0) { throw 'De MSI bouwen is mislukt.' }
Write-Output $output
