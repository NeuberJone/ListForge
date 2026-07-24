param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [switch]$Force,

    [string]$InnoSetupPath
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Invoke-ReleaseCommand {
    param(
        [string]$Display,
        [scriptblock]$Command
    )

    $script:ExecutedCommands.Add($Display) | Out-Null
    Write-Host $Display -ForegroundColor DarkGray
    & $Command
    if ($LASTEXITCODE -ne $null -and $LASTEXITCODE -ne 0) {
        throw "Comando falhou: $Display"
    }
}

function Update-TextFile {
    param(
        [string]$Path,
        [scriptblock]$Update
    )

    $text = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    $updated = & $Update $text
    if ($updated -ne $text) {
        Set-Content -LiteralPath $Path -Value $updated -Encoding UTF8 -NoNewline
        $script:UpdatedFiles.Add($Path) | Out-Null
    }
}

function Assert-UnderDirectory {
    param(
        [string]$ChildPath,
        [string]$ParentPath
    )

    $fullChild = [System.IO.Path]::GetFullPath($ChildPath)
    $fullParent = [System.IO.Path]::GetFullPath($ParentPath).TrimEnd('\') + '\'
    if (-not $fullChild.StartsWith($fullParent, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Caminho fora da pasta esperada: $fullChild"
    }
}

function Get-ReleaseRelativePath {
    param(
        [string]$BasePath,
        [string]$TargetPath
    )

    $fullBase = [System.IO.Path]::GetFullPath($BasePath).TrimEnd('\') + '\'
    $fullTarget = [System.IO.Path]::GetFullPath($TargetPath)
    $baseUri = [System.Uri]::new($fullBase)
    $targetUri = [System.Uri]::new($fullTarget)
    [System.Uri]::UnescapeDataString($baseUri.MakeRelativeUri($targetUri).ToString()).Replace('/', '\')
}

function Copy-ReleaseAsset {
    param(
        [string]$SourcePath,
        [string]$DestinationPath
    )

    if (-not (Test-Path -LiteralPath $SourcePath)) {
        throw "Artefato de origem não encontrado: $SourcePath"
    }

    if (Test-Path -LiteralPath $DestinationPath) {
        throw "Arquivo de Release já existe: $DestinationPath"
    }

    Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Versão inválida. Use o formato X.Y.Z, por exemplo: 2.1.16"
}

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $repoRoot

$csprojPath = Join-Path $repoRoot "ListForge.csproj"
$installerPath = Join-Path $repoRoot "installer\ListForge.iss"
$readmePath = Join-Path $repoRoot "README.md"
$changelogPath = Join-Path $repoRoot "CHANGELOG.md"
$distRoot = Join-Path $repoRoot "bin\Release\dist"
$versionDist = Join-Path $distRoot $Version
$installableDir = Join-Path $versionDist "ListForge-Installable"
$portableDir = Join-Path $versionDist "ListForge-Portable-OneFile"
$trialDir = Join-Path $versionDist "ListForge-Trial-OneFile"
$installerDir = Join-Path $versionDist "Installer"
$releaseDir = Join-Path $versionDist "Release"
$checksumsPath = Join-Path $versionDist "SHA256SUMS.txt"

$script:ExecutedCommands = [System.Collections.Generic.List[string]]::new()
$script:UpdatedFiles = [System.Collections.Generic.List[string]]::new()
$changelogNote = ""

Write-Step "Validando versão e pasta de saída"
New-Item -ItemType Directory -Path $distRoot -Force | Out-Null
Assert-UnderDirectory -ChildPath $versionDist -ParentPath $distRoot

if (Test-Path -LiteralPath $versionDist) {
    $existingFiles = Get-ChildItem -LiteralPath $versionDist -Recurse -File -Force
    if ($existingFiles.Count -eq 0 -and -not $Force) {
        Write-Host "A pasta da versão já existe, mas não contém arquivos. Continuando sem sobrescrever artefatos." -ForegroundColor Yellow
    }
    elseif (-not $Force) {
        throw "A pasta da versão já existe: $versionDist. Use -Force para recriar somente essa pasta."
    }
    else {
        Write-Host "Recriando somente a pasta da versão atual: $versionDist" -ForegroundColor Yellow
        Remove-Item -LiteralPath $versionDist -Recurse -Force
    }
}

New-Item -ItemType Directory -Path $installableDir, $portableDir, $trialDir, $installerDir, $releaseDir -Force | Out-Null

Write-Step "Atualizando arquivos de versão"
$projectText = Get-Content -LiteralPath $csprojPath -Raw -Encoding UTF8
$oldVersionMatch = [regex]::Match($projectText, '<Version>([0-9]+\.[0-9]+\.[0-9]+)</Version>')
$oldVersion = if ($oldVersionMatch.Success) { $oldVersionMatch.Groups[1].Value } else { "" }
Update-TextFile -Path $csprojPath -Update {
    param($text)
    $updated = $text -replace '<Version>[0-9]+\.[0-9]+\.[0-9]+</Version>', "<Version>$Version</Version>"
    $updated = $updated -replace '<FileVersion>[0-9]+\.[0-9]+\.[0-9]+\.0</FileVersion>', "<FileVersion>$Version.0</FileVersion>"
    $updated = $updated -replace '<AssemblyVersion>[0-9]+\.[0-9]+\.[0-9]+\.0</AssemblyVersion>', "<AssemblyVersion>$Version.0</AssemblyVersion>"
    $updated
}

Update-TextFile -Path $installerPath -Update {
    param($text)
    $text -replace '#define\s+MyAppVersion\s+"[0-9]+\.[0-9]+\.[0-9]+"', "#define MyAppVersion `"$Version`""
}

Update-TextFile -Path $readmePath -Update {
    param($text)
    $updated = $text -replace 'version-[0-9]+\.[0-9]+\.[0-9]+-', "version-$Version-"
    if ($oldVersion -match '^\d+\.\d+\.\d+$') {
        $escapedOld = [regex]::Escape($oldVersion)
        $updated = $updated -replace "bin\\Release\\dist\\$escapedOld", "bin\Release\dist\$Version"
        $updated = $updated -replace 'versão `[0-9]+\.[0-9]+\.[0-9]+`', "versão ``$Version``"
        $updated = $updated -replace 'O projeto está configurado para Windows x64 e versão `[^`]+`\.', "O projeto está configurado para Windows x64 e versão ``$Version``."
    }
    $updated
}

if (Test-Path -LiteralPath $changelogPath) {
    $changelog = Get-Content -LiteralPath $changelogPath -Raw -Encoding UTF8
    if ($changelog -match '(?m)^## \[Unreleased\]') {
        $today = Get-Date -Format 'yyyy-MM-dd'
        $updated = $changelog -replace '(?m)^## \[Unreleased\]', "## [$Version] - $today"
        Set-Content -LiteralPath $changelogPath -Value $updated -Encoding UTF8 -NoNewline
        $script:UpdatedFiles.Add($changelogPath) | Out-Null
    }
    elseif ($changelog -notmatch [regex]::Escape("## [$Version]")) {
        $changelogNote = "Revise o CHANGELOG.md manualmente: não há seção [Unreleased] e não foi encontrada seção [$Version]."
    }
}

Write-Step "Executando restore, build e testes"
Invoke-ReleaseCommand "dotnet restore" { dotnet restore }
Invoke-ReleaseCommand "dotnet build" { dotnet build }
Invoke-ReleaseCommand "dotnet test" { dotnet test }
Invoke-ReleaseCommand "dotnet build -c Release" { dotnet build -c Release }

Write-Step "Publicando versão instalável"
Invoke-ReleaseCommand "dotnet publish ListForge.csproj -c Release -r win-x64 --self-contained true -p:ListForgeDistribution=Installed -p:DebugType=None -p:DebugSymbols=false -o $installableDir" {
    dotnet publish ListForge.csproj -c Release -r win-x64 --self-contained true -p:ListForgeDistribution=Installed -p:DebugType=None -p:DebugSymbols=false -o $installableDir
}

Write-Step "Publicando onefile completo"
Invoke-ReleaseCommand "dotnet publish ListForge.csproj -c Release -r win-x64 --self-contained true -p:ListForgeDistribution=PortableOneFile -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $portableDir" {
    dotnet publish ListForge.csproj -c Release -r win-x64 --self-contained true -p:ListForgeDistribution=PortableOneFile -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $portableDir
}

$portableExe = Join-Path $portableDir "ListForge.exe"
$portableVersionedExe = Join-Path $portableDir "ListForge-v$Version.exe"
if (-not (Test-Path -LiteralPath $portableExe)) {
    throw "Executável onefile completo não encontrado para renomear: $portableExe"
}
Move-Item -LiteralPath $portableExe -Destination $portableVersionedExe

Write-Step "Publicando onefile Trial"
Invoke-ReleaseCommand "dotnet publish ListForge.csproj -c Release -r win-x64 --self-contained true -p:ListForgeDistribution=TrialPortableOneFile -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:DefineConstants=TRIAL_BUILD -p:DebugType=None -p:DebugSymbols=false -o $trialDir" {
    dotnet publish ListForge.csproj -c Release -r win-x64 --self-contained true -p:ListForgeDistribution=TrialPortableOneFile -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:DefineConstants=TRIAL_BUILD -p:DebugType=None -p:DebugSymbols=false -o $trialDir
}

$trialExe = Join-Path $trialDir "ListForge.exe"
$trialVersionedExe = Join-Path $trialDir "ListForge-Trial-v$Version.exe"
if (-not (Test-Path -LiteralPath $trialExe)) {
    throw "Executável onefile Trial não encontrado para renomear: $trialExe"
}
Move-Item -LiteralPath $trialExe -Destination $trialVersionedExe

Write-Step "Compilando instalador"
$candidateInnoPaths = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 7\ISCC.exe",
    "C:\Program Files\Inno Setup 7\ISCC.exe"
)

$iscc = $null
if (-not [string]::IsNullOrWhiteSpace($InnoSetupPath)) {
    if (-not (Test-Path -LiteralPath $InnoSetupPath)) {
        throw "Inno Setup não encontrado no caminho informado: $InnoSetupPath"
    }
    $iscc = $InnoSetupPath
}
else {
    $iscc = $candidateInnoPaths | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($iscc)) {
    Write-Host "Inno Setup não foi encontrado. Instale o Inno Setup e execute manualmente:" -ForegroundColor Yellow
    Write-Host '& "C:\Program Files\Inno Setup 7\ISCC.exe" "installer\ListForge.iss"'
    throw "Instalador obrigatório não foi gerado porque o ISCC.exe não foi encontrado."
}

Invoke-ReleaseCommand "& `"$iscc`" installer\ListForge.iss" {
    & $iscc installer\ListForge.iss
}

Write-Step "Conferindo artefatos finais"
$expectedArtifacts = @(
    $portableVersionedExe,
    $trialVersionedExe,
    (Join-Path $installableDir "ListForge.exe"),
    (Join-Path $installerDir "ListForge-Setup-$Version.exe")
)

$missingArtifacts = $expectedArtifacts | Where-Object { -not (Test-Path -LiteralPath $_) }
if ($missingArtifacts.Count -gt 0) {
    throw "Artefato(s) obrigatório(s) ausente(s):`n$($missingArtifacts -join "`n")"
}

Write-Step "Gerando checksums SHA256"
$checksumLines = foreach ($artifact in $expectedArtifacts) {
    $relativePath = Get-ReleaseRelativePath -BasePath $versionDist -TargetPath $artifact
    $hash = (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash.ToUpperInvariant()
    "$hash $relativePath"
}

Set-Content -LiteralPath $checksumsPath -Value $checksumLines -Encoding ASCII

Write-Step "Preparando pasta Release para GitHub"
$releaseArtifacts = @(
    @{ Source = Join-Path $installerDir "ListForge-Setup-$Version.exe"; Name = "ListForge-Setup-$Version.exe" },
    @{ Source = $trialVersionedExe; Name = "ListForge-Trial-v$Version.exe" },
    @{ Source = $portableVersionedExe; Name = "ListForge-v$Version.exe" }
)

foreach ($artifact in $releaseArtifacts) {
    Copy-ReleaseAsset -SourcePath $artifact.Source -DestinationPath (Join-Path $releaseDir $artifact.Name)
}

$releaseChecksumLines = foreach ($artifact in $releaseArtifacts) {
    $hash = (Get-FileHash -LiteralPath $artifact.Source -Algorithm SHA256).Hash.ToUpperInvariant()
    "$hash $($artifact.Name)"
}

$releaseChecksumsPath = Join-Path $releaseDir "SHA256SUMS.txt"
Set-Content -LiteralPath $releaseChecksumsPath -Value $releaseChecksumLines -Encoding ASCII

Write-Host ""
Write-Host "Release gerado com sucesso." -ForegroundColor Green
Write-Host "Versão: $Version"
Write-Host ""
Write-Host "Arquivos de versão atualizados:"
$script:UpdatedFiles | Sort-Object -Unique | ForEach-Object { Write-Host " - $_" }
Write-Host ""
Write-Host "Comandos executados:"
$script:ExecutedCommands | ForEach-Object { Write-Host " - $_" }
Write-Host ""
Write-Host "Artefatos finais:"
$expectedArtifacts | ForEach-Object { Write-Host " - $_" }
Write-Host ""
Write-Host "Checksums:"
Write-Host " - $checksumsPath"
Write-Host " - $releaseChecksumsPath"
Write-Host ""
Write-Host "Arquivos para anexar no GitHub:"
Get-ChildItem -LiteralPath $releaseDir -File | Sort-Object Name | ForEach-Object { Write-Host " - $($_.FullName)" }
Write-Host ""
Write-Host "Testes: concluídos com sucesso."
Write-Host "Instalador: gerado com sucesso."
if (-not [string]::IsNullOrWhiteSpace($changelogNote)) {
    Write-Host $changelogNote -ForegroundColor Yellow
}
