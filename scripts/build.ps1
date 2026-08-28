param(
    [Parameter(Mandatory = $true)]
    [string]$GameInteropReferenceDirectory,

    [Parameter(Mandatory = $true)]
    [string]$MelonLoaderReferenceDirectory,

    [Parameter(Mandatory = $true)]
    [string]$UtilityProject,

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [ValidateRange(60, 1800)]
    [int]$TimeoutSeconds = 600
)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$modProject = Join-Path $repoRoot "MuvluvMod/MuvluvMod.csproj"

function Resolve-RequiredPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Label,

        [Parameter(Mandatory = $true)]
        [ValidateSet("Leaf", "Container")]
        [string]$PathType
    )

    $resolved = Resolve-Path -LiteralPath $Path -ErrorAction Stop
    if (-not (Test-Path -LiteralPath $resolved.Path -PathType $PathType)) {
        throw "$Label is not a valid $PathType path: $($resolved.Path)"
    }

    return $resolved.Path
}

function Invoke-CheckedProcess {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$ArgumentList,

        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory,

        [Parameter(Mandatory = $true)]
        [int]$TimeoutSeconds
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FilePath
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in $ArgumentList) {
        [void]$startInfo.ArgumentList.Add($argument)
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    try {
        if (-not $process.Start()) {
            throw "Failed to start process: $FilePath"
        }

        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill($true)
            throw "Process timed out after $TimeoutSeconds seconds: $FilePath"
        }

        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        if (-not [string]::IsNullOrWhiteSpace($stdout)) {
            Write-Host $stdout.TrimEnd()
        }
        if (-not [string]::IsNullOrWhiteSpace($stderr)) {
            Write-Error $stderr.TrimEnd()
        }

        if ($process.ExitCode -ne 0) {
            throw "Process failed with exit code $($process.ExitCode): $FilePath"
        }
    } finally {
        if (-not $process.HasExited) {
            $process.Kill($true)
        }
        $process.Dispose()
    }
}

$interopDirectory = Resolve-RequiredPath -Path $GameInteropReferenceDirectory -Label "Game interop directory" -PathType Container
$melonDirectory = Resolve-RequiredPath -Path $MelonLoaderReferenceDirectory -Label "MelonLoader reference directory" -PathType Container
$utilityProjectPath = Resolve-RequiredPath -Path $UtilityProject -Label "Utility project" -PathType Leaf
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source

$requiredReferences = @(
    (Join-Path $interopDirectory "Assembly-CSharp.dll"),
    (Join-Path $interopDirectory "Il2Cppmscorlib.dll"),
    (Join-Path $melonDirectory "MelonLoader.dll")
)
foreach ($reference in $requiredReferences) {
    if (-not (Test-Path -LiteralPath $reference -PathType Leaf)) {
        throw "Required reference is missing: $reference"
    }
}

$utilityBuildParameters = @{
    FilePath = $dotnet
    WorkingDirectory = Split-Path -Parent $utilityProjectPath
    TimeoutSeconds = $TimeoutSeconds
    ArgumentList = @(
        "build",
        $utilityProjectPath,
        "-c", $Configuration,
        "--nologo",
        "--no-incremental",
        "-p:UnityProxyDir=$interopDirectory"
    )
}
Invoke-CheckedProcess @utilityBuildParameters

$utilityAssembly = Join-Path (Split-Path -Parent $utilityProjectPath) "bin/$Configuration/net6.0/Utility.dll"
if (-not (Test-Path -LiteralPath $utilityAssembly -PathType Leaf)) {
    throw "Utility build did not produce the expected assembly: $utilityAssembly"
}

$modBuildParameters = @{
    FilePath = $dotnet
    WorkingDirectory = $repoRoot
    TimeoutSeconds = $TimeoutSeconds
    ArgumentList = @(
        "build",
        $modProject,
        "-c", $Configuration,
        "--nologo",
        "--no-incremental",
        "-p:GameInteropReferenceDirectory=$interopDirectory",
        "-p:MelonLoaderReferenceDirectory=$melonDirectory",
        "-p:UtilityAssemblyPath=$utilityAssembly"
    )
}
Invoke-CheckedProcess @modBuildParameters

$modAssembly = Join-Path $repoRoot "MuvluvMod/bin/$Configuration/MuvluvMod.dll"
if (-not (Test-Path -LiteralPath $modAssembly -PathType Leaf)) {
    throw "Mod build did not produce the expected assembly: $modAssembly"
}

[ordered]@{
    ModAssembly = $modAssembly
    ModSha256 = (Get-FileHash -LiteralPath $modAssembly -Algorithm SHA256).Hash.ToLowerInvariant()
    UtilityAssembly = $utilityAssembly
    UtilitySha256 = (Get-FileHash -LiteralPath $utilityAssembly -Algorithm SHA256).Hash.ToLowerInvariant()
} | ConvertTo-Json -Depth 3
