param(
    [Parameter(Mandatory = $true)]
    [string]$UnityPath,
    [Parameter(Mandatory = $true)]
    [string]$UnityVersion,
    [Parameter(Mandatory = $true)]
    [string]$UrpVersion,
    [Parameter(Mandatory = $true)]
    [string]$ShaderGraphVersion,
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,
    [string]$EvidencePath = ""
)

$ErrorActionPreference = "Stop"
$unityExecutable = (Resolve-Path -LiteralPath $UnityPath).Path
$unityProductVersion = [string](
    (Get-Item -LiteralPath $unityExecutable).VersionInfo.ProductVersion)
$unityProductMatch = [regex]::Match(
    $unityProductVersion.Trim(),
    '^(?<version>[^_]+)_(?<revision>[0-9A-Fa-f]+)$')
if (-not $unityProductMatch.Success) {
    throw (
        "MIKU_UNITY_EXECUTABLE_VERSION_INVALID:" +
        "${unityProductVersion}:$unityExecutable")
}
$actualUnityVersion = $unityProductMatch.Groups['version'].Value
$actualUnityRevision = $unityProductMatch.Groups['revision'].Value.ToLowerInvariant()
if ($actualUnityVersion -ne $UnityVersion) {
    throw (
        "MIKU_UNITY_EDITOR_VERSION_MISMATCH:" +
        "actual=$actualUnityVersion;expected=$UnityVersion;" +
        "revision=$actualUnityRevision;executable=$unityExecutable")
}
$packageArchive = (Resolve-Path -LiteralPath $PackagePath).Path
if ([System.IO.Path]::GetExtension($packageArchive) -ne ".tgz") {
    throw "MIKU_UNITY_PACKAGE_ARCHIVE_REQUIRED:$packageArchive"
}
$scratchRoot = Join-Path $env:SystemDrive "miku-unity-editmode-tmp"
$runRoot = Join-Path $scratchRoot ("unity-editmode-" + [guid]::NewGuid().ToString("N"))
$projectRoot = Join-Path $runRoot "unityproject"
$scratchPackage = Join-Path $projectRoot "com.miku.shaderconverter.tgz"
$resultFullPath = Join-Path $runRoot "TestResults-EditMode.xml"
$logPath = Join-Path $runRoot "TestResults-EditMode.log"
$preserveRunArtifacts = $false

$evidenceFullPath = $null
$evidenceXmlPath = $null
$evidenceLogPath = $null
if ($EvidencePath) {
    $evidenceFullPath = [System.IO.Path]::GetFullPath($EvidencePath)
    if ([System.IO.Path]::GetExtension($evidenceFullPath) -ne ".json") {
        throw "MIKU_UNITY_EDITMODE_EVIDENCE_JSON_REQUIRED:$evidenceFullPath"
    }
    $evidenceXmlPath = [System.IO.Path]::ChangeExtension(
        $evidenceFullPath, ".xml")
    $evidenceLogPath = [System.IO.Path]::ChangeExtension(
        $evidenceFullPath, ".log")
    $resolvedScratchPrefix = [System.IO.Path]::GetFullPath(
        $scratchRoot).TrimEnd('\') + '\'
    if ($evidenceFullPath.StartsWith(
        $resolvedScratchPrefix,
        [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "MIKU_UNITY_EDITMODE_EVIDENCE_PATH_UNSAFE:$evidenceFullPath"
    }
    New-Item -ItemType Directory -Path (
        Split-Path -Parent $evidenceFullPath) -Force | Out-Null
    foreach ($staleEvidence in @(
        $evidenceFullPath, $evidenceXmlPath, $evidenceLogPath)) {
        if (Test-Path -LiteralPath $staleEvidence) {
            Remove-Item -LiteralPath $staleEvidence -Force
        }
    }
}

try {
    New-Item -ItemType Directory -Path $projectRoot -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $projectRoot "Assets") -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $projectRoot "Packages") -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $projectRoot "ProjectSettings") -Force | Out-Null
    Copy-Item -LiteralPath $packageArchive -Destination $scratchPackage
    $manifestPath = Join-Path $projectRoot "Packages\manifest.json"
    $manifestText = @"
{
  "dependencies": {
    "com.miku.shaderconverter": "file:../com.miku.shaderconverter.tgz",
    "com.unity.render-pipelines.universal": "$UrpVersion",
    "com.unity.shadergraph": "$ShaderGraphVersion",
    "com.unity.test-framework": "1.6.0",
    "com.unity.modules.jsonserialize": "1.0.0"
  },
  "testables": ["com.miku.shaderconverter"]
}
"@
    [System.IO.File]::WriteAllText(
        $manifestPath,
        $manifestText,
        [System.Text.UTF8Encoding]::new($false)
    )
    $projectVersionPath = Join-Path $projectRoot "ProjectSettings\ProjectVersion.txt"
    [System.IO.File]::WriteAllText(
        $projectVersionPath,
        "m_EditorVersion: $UnityVersion`r`n",
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
            "-batchmode", "-nographics",
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
    if (-not (Test-Path -LiteralPath $lockPath)) {
        $preserveRunArtifacts = $true
        throw "Unity did not produce Packages/packages-lock.json. See $logPath"
    }
    $packageLock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
    $resolvedUrp = $packageLock.dependencies.'com.unity.render-pipelines.universal'.version
    $resolvedShaderGraph = $packageLock.dependencies.'com.unity.shadergraph'.version
    if ($resolvedUrp -ne $UrpVersion -or $resolvedShaderGraph -ne $ShaderGraphVersion) {
        $preserveRunArtifacts = $true
        throw (
            "MIKU_UNITY_PACKAGE_VERSION_MISMATCH:" +
            "unity=${UnityVersion}:urp=${resolvedUrp}:shadergraph=${resolvedShaderGraph}:" +
            "expectedUrp=${UrpVersion}:expectedShaderGraph=${ShaderGraphVersion}"
        )
    }
    $logText = Get-Content -LiteralPath $logPath -Raw
    if ($logText -match "Aborting batchmode due to failure|Couldn't set project path") {
        $preserveRunArtifacts = $true
        throw "Unity EditMode tests did not start successfully. See $logPath"
    }
    $passedCount = [int]$testResults.'test-run'.passed
    $skippedCount = [int]$testResults.'test-run'.skipped
    if ($evidenceFullPath) {
        $evidence = [ordered]@{
            unity = $actualUnityVersion
            unityRevision = $actualUnityRevision
            urp = $resolvedUrp
            shaderGraph = $resolvedShaderGraph
            packageSha256 = (Get-FileHash -LiteralPath $packageArchive -Algorithm SHA256).Hash.ToLowerInvariant()
            total = $testCount
            passed = $passedCount
            failed = $failedCount
            skipped = $skippedCount
            completedUtc = [DateTime]::UtcNow.ToString("o")
        }
        [System.IO.File]::WriteAllText(
            $evidenceFullPath,
            (($evidence | ConvertTo-Json) + "`n"),
            [System.Text.UTF8Encoding]::new($false)
        )
        Copy-Item -LiteralPath $resultFullPath -Destination $evidenceXmlPath
        Copy-Item -LiteralPath $logPath -Destination $evidenceLogPath
    }
    Write-Output (
        "Unity EditMode tests passed: unity=$actualUnityVersion " +
        "revision=$actualUnityRevision urp=$resolvedUrp " +
        "shaderGraph=$resolvedShaderGraph total=$testCount passed=$passedCount " +
        "failed=$failedCount skipped=$skippedCount"
    )
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
