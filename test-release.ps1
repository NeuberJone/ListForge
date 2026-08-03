param(
    [string]$Version,
    [switch]$SkipDotnetTest,
    [switch]$SkipUpdateManifest
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Assert-File {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Arquivo obrigatorio nao encontrado: $Path"
    }
    Write-Host "OK  $Path"
}

function Get-ProjectVersion {
    [xml]$project = Get-Content -LiteralPath "ListForge.csproj"
    $versions = @($project.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($versions.Count -eq 0) {
        throw "Nao foi possivel detectar a versao em ListForge.csproj."
    }
    return [string]$versions[0]
}

function Assert-HashListed {
    param(
        [string]$FilePath,
        [string]$SumsPath
    )

    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $FilePath).Hash.ToUpperInvariant()
    $name = Split-Path -Leaf $FilePath
    $sums = Get-Content -LiteralPath $SumsPath
    $found = $false

    foreach ($line in $sums) {
        if ($line.ToUpperInvariant().Contains($hash) -and $line.Contains($name)) {
            $found = $true
            break
        }
    }

    if (-not $found) {
        throw "Hash de $name nao encontrado ou divergente em $SumsPath."
    }

    Write-Host "OK  SHA256 $name"
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-ProjectVersion
}

Write-Step "Validando release ListForge $Version"

$distRoot = Join-Path "bin\Release\dist" $Version
$releaseRoot = Join-Path $distRoot "Release"

$portable = Join-Path $distRoot "ListForge-Portable-OneFile\ListForge-v$Version.exe"
$trial = Join-Path $distRoot "ListForge-Trial-OneFile\ListForge-Trial-v$Version.exe"
$installable = Join-Path $distRoot "ListForge-Installable\ListForge.exe"
$installer = Join-Path $distRoot "Installer\ListForge-Setup-$Version.exe"
$rootSums = Join-Path $distRoot "SHA256SUMS.txt"

$releasePortable = Join-Path $releaseRoot "ListForge-v$Version.exe"
$releaseTrial = Join-Path $releaseRoot "ListForge-Trial-v$Version.exe"
$releaseInstaller = Join-Path $releaseRoot "ListForge-Setup-$Version.exe"
$releaseSums = Join-Path $releaseRoot "SHA256SUMS.txt"
$releaseNotes = Join-Path $releaseRoot "RELEASE_NOTES_$Version.txt"
$updateManifest = Join-Path $releaseRoot "update.json"

Write-Step "Conferindo artefatos obrigatorios"
Assert-File $portable
Assert-File $trial
Assert-File $installable
Assert-File $installer
Assert-File $rootSums
Assert-File $releasePortable
Assert-File $releaseTrial
Assert-File $releaseInstaller
Assert-File $releaseSums
Assert-File $releaseNotes

if (-not $SkipUpdateManifest) {
    Assert-File $updateManifest
}

Write-Step "Conferindo hashes da pasta Release"
Assert-HashListed $releasePortable $releaseSums
Assert-HashListed $releaseTrial $releaseSums
Assert-HashListed $releaseInstaller $releaseSums

Write-Step "Conferindo nomes versionados"
$expectedNames = @(
    "ListForge-v$Version.exe",
    "ListForge-Trial-v$Version.exe",
    "ListForge-Setup-$Version.exe"
)

foreach ($name in $expectedNames) {
    if (-not (Test-Path -LiteralPath (Join-Path $releaseRoot $name) -PathType Leaf)) {
        throw "Nome versionado ausente na pasta Release: $name"
    }
    Write-Host "OK  $name"
}

if (-not $SkipUpdateManifest) {
    Write-Step "Conferindo update.json"
    $json = Get-Content -LiteralPath $updateManifest -Raw | ConvertFrom-Json
    if ([string]$json.version -ne $Version) {
        throw "update.json aponta para versao '$($json.version)', esperado '$Version'."
    }

    $jsonText = Get-Content -LiteralPath $updateManifest -Raw
    if ($jsonText -notmatch [regex]::Escape("ListForge-Setup-$Version.exe")) {
        throw "update.json nao referencia ListForge-Setup-$Version.exe."
    }

    Write-Host "OK  update.json"
}

if (-not $SkipDotnetTest) {
    Write-Step "Executando dotnet test"
    dotnet test --configuration Release
}

Write-Step "Smoke test automatico concluido"
Write-Host "Release local validada: $releaseRoot" -ForegroundColor Green
