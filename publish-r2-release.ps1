param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$Bucket = "listforge-releases",

    [string]$PublicBaseUrl = "https://pub-62303cd1120248b08beb3454fe0c6316.r2.dev",

    [switch]$Publish,

    [switch]$RemovePreviousVersion
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)

    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Resolve-NpxCommand {
    $command = Get-Command npx.cmd, npx -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $command) {
        $nodeDirectory = Split-Path -Parent $command.Source
        if (-not (($env:Path -split ';') -contains $nodeDirectory)) {
            $env:Path = "$nodeDirectory;$env:Path"
        }

        return $command.Source
    }

    $defaultPath = "C:\Program Files\nodejs\npx.cmd"
    if (Test-Path -LiteralPath $defaultPath) {
        $nodeDirectory = Split-Path -Parent $defaultPath
        if (-not (($env:Path -split ';') -contains $nodeDirectory)) {
            $env:Path = "$nodeDirectory;$env:Path"
        }

        return $defaultPath
    }

    throw "npx nao foi encontrado. Instale o Node.js LTS e abra um novo terminal."
}

function Invoke-Wrangler {
    param(
        [string[]]$Arguments,
        [string]$Display
    )

    Write-Host $Display -ForegroundColor DarkGray
    & $script:NpxCommand wrangler @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Comando Wrangler falhou: $Display"
    }
}

function Get-ContentType {
    param([string]$Name)

    if ($Name.EndsWith(".exe", [System.StringComparison]::OrdinalIgnoreCase)) {
        return "application/octet-stream"
    }

    if ($Name.EndsWith(".json", [System.StringComparison]::OrdinalIgnoreCase)) {
        return "application/json"
    }

    return "text/plain;charset=utf-8"
}

function Get-RemoteManifest {
    param([switch]$Required)

    $cacheBuster = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $manifestUri = "$PublicBaseUrl/update.json?ts=$cacheBuster"

    try {
        return Invoke-RestMethod -Uri $manifestUri -Method Get -TimeoutSec 30
    }
    catch {
        if ($Required) {
            throw "Nao foi possivel validar o manifest publico em $manifestUri. $($_.Exception.Message)"
        }

        Write-Host "Nenhum manifest publico anterior foi encontrado." -ForegroundColor Yellow
        return $null
    }
}

function Assert-PublicAsset {
    param(
        [string]$Name,
        [long]$ExpectedLength
    )

    $encodedName = [Uri]::EscapeDataString($Name)
    $cacheBuster = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $assetUri = "$PublicBaseUrl/$encodedName?ts=$cacheBuster"
    $response = Invoke-WebRequest -Uri $assetUri -Method Head -UseBasicParsing -TimeoutSec 30

    if ($response.StatusCode -ne 200) {
        throw "O arquivo publicado nao retornou HTTP 200: $assetUri"
    }

    $contentLength = $response.Headers["Content-Length"]
    if (-not [string]::IsNullOrWhiteSpace($contentLength) -and [long]$contentLength -ne $ExpectedLength) {
        throw "Tamanho remoto divergente para ${Name}. Esperado: $ExpectedLength; remoto: $contentLength"
    }
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Versao invalida. Use o formato X.Y.Z, por exemplo: 2.1.40"
}

$publicUri = $null
if (-not [Uri]::TryCreate($PublicBaseUrl.Trim(), [UriKind]::Absolute, [ref]$publicUri) -or $publicUri.Scheme -ne "https") {
    throw "PublicBaseUrl invalida. Use uma URL HTTPS publica."
}

$PublicBaseUrl = $PublicBaseUrl.Trim().TrimEnd('/')
$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$releaseDir = Join-Path $repoRoot "bin\Release\dist\$Version\Release"
$checksumsPath = Join-Path $releaseDir "SHA256SUMS.txt"
$manifestPath = Join-Path $releaseDir "update.json"

$assetNames = @(
    "ListForge-Setup-$Version.exe",
    "ListForge-v$Version.exe",
    "ListForge-Trial-v$Version.exe",
    "RELEASE_NOTES_$Version.txt",
    "SHA256SUMS.txt",
    "update.json"
)

Write-Step "Validando artefatos locais"
if (-not (Test-Path -LiteralPath $releaseDir)) {
    throw "Pasta de Release nao encontrada: $releaseDir"
}

$missingAssets = $assetNames | Where-Object { -not (Test-Path -LiteralPath (Join-Path $releaseDir $_)) }
if ($missingAssets.Count -gt 0) {
    throw "Arquivos ausentes na pasta Release:`n$($missingAssets -join [Environment]::NewLine)"
}

$checksumEntries = @{}
foreach ($line in Get-Content -LiteralPath $checksumsPath -Encoding ASCII) {
    if ($line -notmatch '^(?<Hash>[A-Fa-f0-9]{64})\s+(?<Name>.+)$') {
        throw "Linha invalida em SHA256SUMS.txt: $line"
    }

    $checksumEntries[$Matches.Name] = $Matches.Hash.ToUpperInvariant()
}

$hashedAssets = @(
    "ListForge-Setup-$Version.exe",
    "ListForge-v$Version.exe",
    "ListForge-Trial-v$Version.exe"
)

foreach ($name in $hashedAssets) {
    if (-not $checksumEntries.ContainsKey($name)) {
        throw "SHA256SUMS.txt nao contem: $name"
    }

    $actualHash = (Get-FileHash -LiteralPath (Join-Path $releaseDir $name) -Algorithm SHA256).Hash.ToUpperInvariant()
    if ($actualHash -ne $checksumEntries[$name]) {
        throw "Hash divergente para $name"
    }
}

$localManifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($localManifest.version -ne $Version) {
    throw "update.json pertence a versao $($localManifest.version), nao a $Version"
}

foreach ($manifestAsset in @(
    @{ Section = "installer"; Name = "ListForge-Setup-$Version.exe" },
    @{ Section = "portable"; Name = "ListForge-v$Version.exe" },
    @{ Section = "trial"; Name = "ListForge-Trial-v$Version.exe" }
)) {
    $entry = $localManifest.($manifestAsset.Section)
    if ($null -eq $entry -or $entry.name -ne $manifestAsset.Name) {
        throw "Asset $($manifestAsset.Section) invalido no update.json. Esperado: $($manifestAsset.Name)"
    }

    $assetPath = Join-Path $releaseDir $manifestAsset.Name
    $assetInfo = Get-Item -LiteralPath $assetPath
    $assetHash = (Get-FileHash -LiteralPath $assetPath -Algorithm SHA256).Hash.ToUpperInvariant()
    if ($entry.sha256.ToUpperInvariant() -ne $assetHash -or [long]$entry.size -ne $assetInfo.Length) {
        throw "Hash ou tamanho de $($manifestAsset.Name) no update.json nao confere com o arquivo local."
    }
}

Write-Host "Artefatos e hashes locais validados." -ForegroundColor Green

$script:NpxCommand = Resolve-NpxCommand
Write-Step "Conferindo Wrangler e autenticacao"
Invoke-Wrangler -Arguments @("--version") -Display "npx.cmd wrangler --version"
Invoke-Wrangler -Arguments @("whoami") -Display "npx.cmd wrangler whoami"

$previousManifest = Get-RemoteManifest
$previousVersion = if ($null -ne $previousManifest) { [string]$previousManifest.version } else { "" }

Write-Step "Plano de publicacao"
Write-Host "Bucket: $Bucket"
Write-Host "Pasta local: $releaseDir"
Write-Host "URL publica: $PublicBaseUrl"
Write-Host "Versao local: $Version"
Write-Host "Versao publicada: $(if ([string]::IsNullOrWhiteSpace($previousVersion)) { '(nao identificada)' } else { $previousVersion })"
Write-Host ""
$assetNames | ForEach-Object { Write-Host " - $_" }

if (-not $Publish) {
    Write-Host ""
    Write-Host "Simulacao concluida. Nenhum arquivo foi enviado ou removido." -ForegroundColor Yellow
    Write-Host "Para publicar: .\publish-r2-release.ps1 -Version $Version -Publish"
    Write-Host "Para publicar e remover a versao anterior: .\publish-r2-release.ps1 -Version $Version -Publish -RemovePreviousVersion"
    return
}

Write-Step "Enviando nova versao"
foreach ($name in $assetNames) {
    if ($name -eq "update.json") {
        continue
    }

    $path = Join-Path $releaseDir $name
    $contentType = Get-ContentType -Name $name
    $cacheControl = if ($name.EndsWith(".exe", [System.StringComparison]::OrdinalIgnoreCase)) {
        "public,max-age=31536000,immutable"
    }
    else {
        "no-cache"
    }

    $arguments = @(
        "r2", "object", "put", "$Bucket/$name",
        "--file=$path",
        "--content-type=$contentType",
        "--cache-control=$cacheControl",
        "--remote"
    )
    Invoke-Wrangler -Arguments $arguments -Display "wrangler r2 object put $Bucket/$name --remote"
}

Write-Step "Publicando manifest por ultimo"
$manifestArguments = @(
    "r2", "object", "put", "$Bucket/update.json",
    "--file=$manifestPath",
    "--content-type=application/json",
    "--cache-control=no-cache,no-store,must-revalidate",
    "--remote"
)
Invoke-Wrangler -Arguments $manifestArguments -Display "wrangler r2 object put $Bucket/update.json --remote"

Write-Step "Validando publicacao"
$publishedManifest = $null
for ($attempt = 1; $attempt -le 6; $attempt++) {
    try {
        $publishedManifest = Get-RemoteManifest -Required
        if ($publishedManifest.version -eq $Version) {
            break
        }
    }
    catch {
        if ($attempt -eq 6) {
            throw
        }
    }

    Start-Sleep -Seconds 2
}

if ($null -eq $publishedManifest -or $publishedManifest.version -ne $Version) {
    throw "O manifest publico nao confirmou a versao $Version."
}

foreach ($name in $hashedAssets) {
    $path = Join-Path $releaseDir $name
    Assert-PublicAsset -Name $name -ExpectedLength (Get-Item -LiteralPath $path).Length
}

Write-Host "Versao $Version publicada e validada no R2." -ForegroundColor Green

if ($RemovePreviousVersion) {
    if ([string]::IsNullOrWhiteSpace($previousVersion)) {
        Write-Host "A versao anterior nao foi identificada; nenhum arquivo antigo foi removido." -ForegroundColor Yellow
    }
    elseif ($previousVersion -eq $Version) {
        Write-Host "A versao publicada anteriormente ja era $Version; nenhum arquivo versionado foi removido." -ForegroundColor Yellow
    }
    elseif ($previousVersion -notmatch '^\d+\.\d+\.\d+$') {
        Write-Host "Versao anterior invalida no manifest; nenhum arquivo antigo foi removido." -ForegroundColor Yellow
    }
    else {
        Write-Step "Removendo arquivos versionados da versao anterior"
        $oldAssetNames = @(
            "ListForge-Setup-$previousVersion.exe",
            "ListForge-v$previousVersion.exe",
            "ListForge-Trial-v$previousVersion.exe",
            "RELEASE_NOTES_$previousVersion.txt"
        )

        foreach ($name in $oldAssetNames) {
            try {
                Invoke-Wrangler -Arguments @("r2", "object", "delete", "$Bucket/$name", "--remote") -Display "wrangler r2 object delete $Bucket/$name --remote"
            }
            catch {
                Write-Host "Nao foi possivel remover $name. O objeto pode nao existir." -ForegroundColor Yellow
            }
        }
    }
}

Write-Host ""
Write-Host "Publicacao R2 concluida." -ForegroundColor Green
