param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [switch]$Create
)

$ErrorActionPreference = "Stop"

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Versão inválida. Use o formato X.Y.Z, por exemplo: 2.1.22"
}

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $repoRoot

$tag = "v$Version"
$versionDist = Join-Path $repoRoot "bin\Release\dist\$Version"
$releaseNotesPath = Join-Path $versionDist "RELEASE_NOTES_$Version.txt"

$requiredArtifacts = @(
    (Join-Path $versionDist "ListForge-Portable-OneFile\ListForge-v$Version.exe"),
    (Join-Path $versionDist "ListForge-Trial-OneFile\ListForge-Trial-v$Version.exe"),
    (Join-Path $versionDist "Installer\ListForge-Setup-$Version.exe")
)
$checksumsPath = Join-Path $versionDist "SHA256SUMS.txt"

function Get-ChangelogSection {
    param(
        [string]$ChangelogPath,
        [string]$ReleaseVersion
    )

    if (-not (Test-Path -LiteralPath $ChangelogPath)) {
        return ""
    }

    $lines = Get-Content -LiteralPath $ChangelogPath -Encoding UTF8
    $startPattern = "## [$ReleaseVersion]"
    $start = -1

    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i].StartsWith($startPattern, [System.StringComparison]::Ordinal)) {
            $start = $i
            break
        }
    }

    if ($start -lt 0) {
        return ""
    }

    $end = $lines.Count
    for ($i = $start + 1; $i -lt $lines.Count; $i++) {
        if ($lines[$i].StartsWith("## [", [System.StringComparison]::Ordinal)) {
            $end = $i
            break
        }
    }

    ($lines[$start..($end - 1)] -join [Environment]::NewLine).Trim()
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

function Assert-ChecksumEntries {
    param(
        [string]$ChecksumsPath,
        [string[]]$Artifacts,
        [string]$VersionDist
    )

    if (-not (Test-Path -LiteralPath $ChecksumsPath)) {
        throw "SHA256SUMS.txt não encontrado: $ChecksumsPath"
    }

    $content = Get-Content -LiteralPath $ChecksumsPath -Raw -Encoding ASCII
    $lines = Get-Content -LiteralPath $ChecksumsPath -Encoding ASCII
    foreach ($artifact in $Artifacts) {
        $relative = Get-ReleaseRelativePath -BasePath $VersionDist -TargetPath $artifact
        if ($content -notmatch [regex]::Escape($relative)) {
            throw "SHA256SUMS.txt não contém o artefato esperado: $relative"
        }

        $line = $lines | Where-Object { $_ -match [regex]::Escape($relative) } | Select-Object -First 1
        if ([string]::IsNullOrWhiteSpace($line)) {
            throw "SHA256SUMS.txt não contém linha válida para: $relative"
        }

        $parts = $line.Trim().Split(' ', [System.StringSplitOptions]::RemoveEmptyEntries)
        if ($parts.Count -lt 2 -or $parts[0] -notmatch '^[A-Fa-f0-9]{64}$') {
            throw "SHA256SUMS.txt contém hash inválido para: $relative"
        }

        $expectedHash = $parts[0].ToUpperInvariant()
        $actualHash = (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash.ToUpperInvariant()
        if ($actualHash -ne $expectedHash) {
            throw "SHA256SUMS.txt não confere para ${relative}. Esperado: $expectedHash; atual: $actualHash"
        }
    }
}

if (-not (Test-Path -LiteralPath $versionDist)) {
    throw "Pasta da versão não encontrada: $versionDist. Gere a release local antes com .\build-release.ps1 -Version $Version"
}

$missingArtifacts = $requiredArtifacts | Where-Object { -not (Test-Path -LiteralPath $_) }
if ($missingArtifacts.Count -gt 0) {
    throw "Artefato(s) ausente(s):`n$($missingArtifacts -join [Environment]::NewLine)"
}

Assert-ChecksumEntries -ChecksumsPath $checksumsPath -Artifacts $requiredArtifacts -VersionDist $versionDist

$artifacts = [System.Collections.Generic.List[string]]::new()
$requiredArtifacts | ForEach-Object { $artifacts.Add($_) | Out-Null }
$artifacts.Add($checksumsPath) | Out-Null

$changelogSection = Get-ChangelogSection -ChangelogPath (Join-Path $repoRoot "CHANGELOG.md") -ReleaseVersion $Version
if ([string]::IsNullOrWhiteSpace($changelogSection)) {
    $changelogSection = "ListForge $Version"
}

Set-Content -LiteralPath $releaseNotesPath -Value $changelogSection -Encoding UTF8

Write-Host "Release local validada." -ForegroundColor Green
Write-Host "Versão: $Version"
Write-Host "Tag sugerida: $tag"
Write-Host "Notas de release: $releaseNotesPath"
Write-Host ""
Write-Host "Artefatos para anexar:"
$artifacts | ForEach-Object { Write-Host " - $_" }
Write-Host ""
Write-Host "Comandos sugeridos:"
Write-Host " git tag $tag"
Write-Host " git push origin $tag"
Write-Host ""

$gh = Get-Command gh -ErrorAction SilentlyContinue
if ($null -eq $gh) {
    Write-Host "GitHub CLI não encontrado. Crie a release manualmente no GitHub usando a tag $tag." -ForegroundColor Yellow
    return
}

$artifactArgs = $artifacts | ForEach-Object { "`"$_`"" }
$ghCommand = "gh release create $tag $($artifactArgs -join ' ') --title `"ListForge $Version`" --notes-file `"$releaseNotesPath`""
Write-Host "GitHub CLI encontrado. Comando preparado:"
Write-Host " $ghCommand"

if (-not $Create) {
    Write-Host ""
    Write-Host "Nenhuma release foi publicada. Para publicar, revise tudo e execute novamente com -Create." -ForegroundColor Yellow
    return
}

$existingTag = git tag --list $tag
if ([string]::IsNullOrWhiteSpace($existingTag)) {
    throw "Tag local não encontrada: $tag. Crie e envie a tag antes de publicar a release."
}

& gh release create $tag $artifacts --title "ListForge $Version" --notes-file $releaseNotesPath
