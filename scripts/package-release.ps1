param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern("^v[0-9]+\.[0-9]+\.[0-9]+(?:[-.][0-9A-Za-z.-]+)?$")]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$ModAssembly,

    [Parameter(Mandatory = $true)]
    [string]$UtilityAssembly,

    [Parameter(Mandatory = $true)]
    [string]$FontBundle
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$releaseRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot "artifacts/release"))
$outputDirectory = [System.IO.Path]::GetFullPath((Join-Path $releaseRoot $Version))
$archivePath = Join-Path $outputDirectory "MuvluvMod-Android.zip"
$archiveTempPath = $archivePath + ".tmp"
$sumsPath = Join-Path $outputDirectory "SHA256SUMS.txt"

if (-not $outputDirectory.StartsWith($releaseRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Release output escaped the repository artifacts directory: $outputDirectory"
}

function Resolve-InputFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    $resolved = Resolve-Path -LiteralPath $Path -ErrorAction Stop
    if (-not (Test-Path -LiteralPath $resolved.Path -PathType Leaf)) {
        throw "$Label is not a file: $($resolved.Path)"
    }

    return $resolved.Path
}

function Get-StreamSha256 {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Stream]$Stream
    )

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return [System.Convert]::ToHexString($sha256.ComputeHash($Stream)).ToLowerInvariant()
    } finally {
        $sha256.Dispose()
    }
}

$inputs = [ordered]@{
    "Mods/MuvluvMod/MuvluvMod.dll" = Resolve-InputFile -Path $ModAssembly -Label "Mod assembly"
    "Mods/MuvluvMod/Utility.dll" = Resolve-InputFile -Path $UtilityAssembly -Label "Utility assembly"
    "UserData/MuvluvMod/sarasagothicsc-bold" = Resolve-InputFile -Path $FontBundle -Label "Font bundle"
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$unexpectedFiles = @(
    Get-ChildItem -LiteralPath $outputDirectory -Force |
        Where-Object { $_.Name -notin @("MuvluvMod-Android.zip", "MuvluvMod-Android.zip.tmp", "SHA256SUMS.txt") }
)
if ($unexpectedFiles.Count -ne 0) {
    throw "Release output contains files not owned by this script: $($unexpectedFiles.Name -join ', ')"
}

$archive = $null
try {
    if (Test-Path -LiteralPath $archiveTempPath -PathType Leaf) {
        Remove-Item -LiteralPath $archiveTempPath -Force
    }

    $archiveStream = [System.IO.File]::Open(
        $archiveTempPath,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::ReadWrite,
        [System.IO.FileShare]::None
    )
    try {
        $archive = [System.IO.Compression.ZipArchive]::new(
            $archiveStream,
            [System.IO.Compression.ZipArchiveMode]::Create,
            $false
        )
        foreach ($entryName in $inputs.Keys) {
            $entry = $archive.CreateEntry(
                $entryName,
                [System.IO.Compression.CompressionLevel]::Optimal
            )
            $entry.LastWriteTime = [System.DateTimeOffset]::new(
                1980,
                1,
                1,
                0,
                0,
                0,
                [System.TimeSpan]::Zero
            )

            $inputStream = [System.IO.File]::OpenRead($inputs[$entryName])
            $entryStream = $entry.Open()
            try {
                $inputStream.CopyTo($entryStream)
            } finally {
                $entryStream.Dispose()
                $inputStream.Dispose()
            }
        }
    } finally {
        if ($null -ne $archive) {
            $archive.Dispose()
            $archive = $null
        }
        if ($null -ne $archiveStream) {
            $archiveStream.Dispose()
        }
    }

    [System.IO.File]::Move($archiveTempPath, $archivePath, $true)
} finally {
    if ($null -ne $archive) {
        $archive.Dispose()
    }
    if (Test-Path -LiteralPath $archiveTempPath -PathType Leaf) {
        Remove-Item -LiteralPath $archiveTempPath -Force -ErrorAction SilentlyContinue
    }
}

$expectedEntries = @($inputs.Keys)
$verificationArchive = [System.IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    $actualEntries = @($verificationArchive.Entries | ForEach-Object { $_.FullName })
    if ([string]::Join("`n", $actualEntries) -ne [string]::Join("`n", $expectedEntries)) {
        throw "Release archive entries do not match the expected deployment layout."
    }

    foreach ($entryName in $expectedEntries) {
        $entry = $verificationArchive.GetEntry($entryName)
        $entryStream = $entry.Open()
        try {
            $entryHash = Get-StreamSha256 -Stream $entryStream
        } finally {
            $entryStream.Dispose()
        }

        $inputHash = (Get-FileHash -LiteralPath $inputs[$entryName] -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($entryHash -ne $inputHash) {
            throw "Release archive content hash mismatch: $entryName"
        }
    }
} finally {
    $verificationArchive.Dispose()
}

$archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText(
    $sumsPath,
    "$archiveHash  MuvluvMod-Android.zip`n",
    [System.Text.UTF8Encoding]::new($false)
)

[ordered]@{
    Version = $Version
    Archive = $archivePath
    ArchiveSha256 = $archiveHash
    Checksums = $sumsPath
    Entries = $expectedEntries
} | ConvertTo-Json -Depth 4
