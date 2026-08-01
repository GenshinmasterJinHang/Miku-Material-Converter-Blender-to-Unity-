param(
    [Parameter(Mandatory = $true)]
    [string]$UnityPath
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$unityExecutable = (Resolve-Path -LiteralPath $UnityPath).Path
$scratchRoot = Join-Path $env:SystemDrive "miku-unity-editmode-tmp"
$runRoot = Join-Path $scratchRoot ("unity-editmode-" + [guid]::NewGuid().ToString("N"))
$projectRoot = Join-Path $runRoot "unityproject"
$packageRoot = Join-Path $runRoot "unity\Packages\com.miku.shaderconverter"
$resultFullPath = Join-Path $runRoot "TestResults-EditMode.xml"
$logPath = Join-Path $runRoot "TestResults-EditMode.log"
$preserveRunArtifacts = $false

try {
    New-Item -ItemType Directory -Path $projectRoot -Force | Out-Null
    New-Item -ItemType Directory -Path (Split-Path $packageRoot) -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $projectRoot "Assets") -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $projectRoot "Packages") -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $projectRoot "ProjectSettings") -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $repoRoot "unity\Packages\com.miku.shaderconverter") -Destination (Split-Path $packageRoot) -Recurse
    $manifestPath = Join-Path $projectRoot "Packages\manifest.json"
    $manifestText = @'
{
  "dependencies": {
    "com.miku.shaderconverter": "file:../../unity/Packages/com.miku.shaderconverter",
    "com.unity.render-pipelines.universal": "17.4.0",
    "com.unity.test-framework": "1.6.0",
    "com.unity.modules.jsonserialize": "1.0.0"
  },
  "testables": ["com.miku.shaderconverter"]
}
'@
    [System.IO.File]::WriteAllText(
        $manifestPath,
        $manifestText,
        [System.Text.UTF8Encoding]::new($false)
    )
    $projectVersionPath = Join-Path $projectRoot "ProjectSettings\ProjectVersion.txt"
    [System.IO.File]::WriteAllText(
        $projectVersionPath,
        "m_EditorVersion: 6000.4.5f1`r`nm_EditorVersionWithRevision: 6000.4.5f1 (cc83ebd631f8)`r`n",
        [System.Text.UTF8Encoding]::new($false)
    )
    $lockPath = Join-Path $projectRoot "Packages\packages-lock.json"
    if (Test-Path -LiteralPath $lockPath) {
        Remove-Item -LiteralPath $lockPath -Force
    }

    # Unity 6000.4 on Windows can misclassify an absolute path when the caller's
    # working directory contains non-ASCII characters. Run from the ASCII-only
    # scratch directory and pass scratch-relative paths to avoid that parser bug.
    Push-Location -LiteralPath $runRoot
    try {
        $arguments = @(
            "-batchmode", "-nographics", "-force-d3d11",
            "-projectPath", $projectRoot, "-runTests",
            "-testPlatform", "EditMode", "-testResults", $resultFullPath,
            "-logFile", $logPath
        )
        $unityProcess = Start-Process -FilePath $unityExecutable -ArgumentList $arguments `
            -WorkingDirectory $runRoot -WindowStyle Hidden -PassThru
        # Start-Process -Wait waits for the complete descendant process tree on
        # Windows. Unity package-manager/licensing children can be shared with
        # an already-open editor, so wait only for the batch process itself.
        $unityProcess.WaitForExit()
        $unityExitCode = $unityProcess.ExitCode
    }
    finally {
        Pop-Location
    }
    if ($unityExitCode -ne 0 -or -not (Test-Path -LiteralPath $resultFullPath)) {
        $preserveRunArtifacts = $true
        throw "Unity EditMode tests failed with exit code $unityExitCode. See $logPath"
    }
    [xml]$testResults = Get-Content -LiteralPath $resultFullPath -Raw
    $testCount = [int]$testResults.'test-run'.total
    $failedCount = [int]$testResults.'test-run'.failed
    if ($testCount -le 0) {
        $preserveRunArtifacts = $true
        throw "Unity EditMode test discovery returned zero tests. See $logPath"
    }
    if ($failedCount -ne 0) {
        $preserveRunArtifacts = $true
        throw "Unity EditMode tests reported $failedCount failed tests. See $logPath"
    }
    $logText = Get-Content -LiteralPath $logPath -Raw
    if ($logText -match "Aborting batchmode due to failure|Couldn't set project path") {
        $preserveRunArtifacts = $true
        throw "Unity EditMode tests did not start successfully. See $logPath"
    }
    $passedCount = [int]$testResults.'test-run'.passed
    $skippedCount = [int]$testResults.'test-run'.skipped
    Write-Output "Unity EditMode tests passed: total=$testCount passed=$passedCount failed=$failedCount skipped=$skippedCount"
}
finally {
    if (-not $preserveRunArtifacts -and (Test-Path -LiteralPath $runRoot)) {
        $resolvedRun = [System.IO.Path]::GetFullPath($runRoot)
        $resolvedScratch = [System.IO.Path]::GetFullPath($scratchRoot).TrimEnd('\') + '\'
        if (-not $resolvedRun.StartsWith($resolvedScratch, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove unexpected Unity test directory: $resolvedRun"
        }
        for ($attempt = 0; $attempt -lt 5; $attempt++) {
            try {
                Remove-Item -LiteralPath $resolvedRun -Recurse -Force
                break
            }
            catch [System.IO.IOException] {
                if ($attempt -eq 4) { throw }
                Start-Sleep -Milliseconds 500
            }
        }
    }
    if (-not $preserveRunArtifacts -and (Test-Path -LiteralPath $scratchRoot)) {
        $scratchChildren = @(Get-ChildItem -LiteralPath $scratchRoot -Force)
        if ($scratchChildren.Count -eq 0) {
            $resolvedScratchRoot = [System.IO.Path]::GetFullPath($scratchRoot)
            if ($resolvedScratchRoot -ne $env:SystemDrive + "\miku-unity-editmode-tmp") {
                throw "Refusing to remove unexpected Unity scratch directory: $resolvedScratchRoot"
            }
            Remove-Item -LiteralPath $resolvedScratchRoot -Force
        }
    }
}
