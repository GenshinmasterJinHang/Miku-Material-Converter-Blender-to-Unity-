param(
    [Parameter(Mandatory = $true)]
    [string]$UnityExecutable,
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
$unity = (Resolve-Path -LiteralPath $UnityExecutable).Path
$unityProductVersion = [string](
    (Get-Item -LiteralPath $unity).VersionInfo.ProductVersion)
$unityProductMatch = [regex]::Match(
    $unityProductVersion.Trim(),
    '^(?<version>[^_]+)_(?<revision>[0-9A-Fa-f]+)$')
if (-not $unityProductMatch.Success) {
    throw "MIKU_UNITY_EXECUTABLE_VERSION_INVALID:${unityProductVersion}:$unity"
}
$actualUnityVersion = $unityProductMatch.Groups['version'].Value
$actualUnityRevision = $unityProductMatch.Groups['revision'].Value.ToLowerInvariant()
if ($actualUnityVersion -ne $UnityVersion) {
    throw (
        "MIKU_UNITY_EDITOR_VERSION_MISMATCH:" +
        "actual=$actualUnityVersion;expected=$UnityVersion;" +
        "revision=$actualUnityRevision;executable=$unity")
}
$packageArchive = (Resolve-Path -LiteralPath $PackagePath).Path
if ([System.IO.Path]::GetExtension($packageArchive) -ne ".tgz") {
    throw "MIKU_UNITY_PACKAGE_ARCHIVE_REQUIRED:$packageArchive"
}

$scratchRoot = Join-Path $env:SystemDrive "miku-unity-dx12-tmp"
$runRoot = Join-Path $scratchRoot (
    "unity-dx12-" + [guid]::NewGuid().ToString("N"))
$projectRoot = Join-Path $runRoot "unityproject"
$scratchPackage = Join-Path $projectRoot "com.miku.shaderconverter.tgz"
$result = Join-Path $runRoot "TestResults-D3D12.xml"
$log = Join-Path $runRoot "TestResults-D3D12.log"
$preserveRunArtifacts = $false

$evidenceFullPath = $null
$evidenceXmlPath = $null
$evidenceLogPath = $null
if ($EvidencePath) {
    $evidenceFullPath = [System.IO.Path]::GetFullPath($EvidencePath)
    if ([System.IO.Path]::GetExtension($evidenceFullPath) -ne ".json") {
        throw "MIKU_D3D12_GPU_EVIDENCE_JSON_REQUIRED:$evidenceFullPath"
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
        throw "MIKU_D3D12_GPU_EVIDENCE_PATH_UNSAFE:$evidenceFullPath"
    }
    New-Item -ItemType Directory -Force -Path (
        Split-Path -Parent $evidenceFullPath) | Out-Null
    foreach ($staleEvidence in @(
        $evidenceFullPath, $evidenceXmlPath, $evidenceLogPath)) {
        if (Test-Path -LiteralPath $staleEvidence) {
            Remove-Item -LiteralPath $staleEvidence -Force
        }
    }
}

$requiredTests = @(
    "Miku.ShaderConverter.Editor.Tests.MikuDx12GraphicsTests.GraphicsAcceptanceRunsOnDirect3D12"
    "Miku.ShaderConverter.Editor.Tests.MikuDx12GraphicsTests.ZeroWidthVertexMaskAndNonFiniteCoverageProduceNoOutlinePixels"
    "Miku.ShaderConverter.Editor.Tests.MikuDx12GraphicsTests.ZeroEndfieldTextureMaskAndDisabledStateProduceNoOutlinePixels"
    "Miku.ShaderConverter.Editor.Tests.MikuDx12GraphicsTests.WuwaBodyForwardPlusUsesDirectionalMainLight"
    "Miku.ShaderConverter.Editor.Tests.MikuDx12GraphicsTests.GenshinBodyAndHairForwardPlusRespondToLightYaw"
    "Miku.ShaderConverter.Editor.Tests.MikuDx12GraphicsTests.GenshinMetalUsesViewNormalAndIgnoresMainLightYaw"
    "Miku.ShaderConverter.Editor.Tests.MikuDx12GraphicsTests.GenshinFaceSdfMaskAndFinalColorRespondToLightYaw"
    "Miku.ShaderConverter.Editor.Tests.MikuDx12GraphicsTests.WuwaFaceSdfMaskAndFinalColorRespondToLightYaw"
    "Miku.ShaderConverter.Editor.Tests.MikuDx12GraphicsTests.WuwaFaceSdfMirrorTransitionIsContinuousInDebugAndFinal"
    "Miku.ShaderConverter.Editor.Tests.MikuDx12GraphicsTests.WuwaEyeParallaxMovesIrisLayersButKeepsSurfaceHighlightFixed"
)

try {
    New-Item -ItemType Directory -Path $projectRoot -Force | Out-Null
    New-Item -ItemType Directory -Path (
        Join-Path $projectRoot "Assets") -Force | Out-Null
    New-Item -ItemType Directory -Path (
        Join-Path $projectRoot "Packages") -Force | Out-Null
    New-Item -ItemType Directory -Path (
        Join-Path $projectRoot "ProjectSettings") -Force | Out-Null
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
        [System.Text.UTF8Encoding]::new($false))
    [System.IO.File]::WriteAllText(
        (Join-Path $projectRoot "ProjectSettings\ProjectVersion.txt"),
        "m_EditorVersion: $UnityVersion`r`n",
        [System.Text.UTF8Encoding]::new($false))

    Push-Location -LiteralPath $runRoot
    try {
        $arguments = @(
            "-batchmode",
            "-force-d3d12",
            "-projectPath", $projectRoot,
            "-runTests",
            "-testPlatform", "EditMode",
            "-testCategory", "MikuGpuAcceptance",
            "-testResults", $result,
            "-logFile", $log
        )
        $process = Start-Process -FilePath $unity -ArgumentList $arguments `
            -WorkingDirectory $runRoot -WindowStyle Hidden -PassThru
        $process.WaitForExit()
        $exitCode = $process.ExitCode
    }
    finally {
        Pop-Location
    }

    if ($exitCode -ne 0 -or -not (Test-Path -LiteralPath $result)) {
        $preserveRunArtifacts = $true
        throw "MIKU_D3D12_GPU_TEST_FAILED:exit=$exitCode;log=$log"
    }
    [xml]$report = Get-Content -Raw -LiteralPath $result
    $run = $report.'test-run'
    if ($null -eq $run) {
        $preserveRunArtifacts = $true
        throw "MIKU_D3D12_GPU_TEST_RESULTS_INVALID:no-test-run:$result"
    }
    $total = [int]$run.total
    $passed = [int]$run.passed
    $failed = [int]$run.failed
    $skipped = [int]$run.skipped
    $inconclusive = [int]$run.inconclusive
    if ($total -le 0 -or $failed -ne 0 -or $skipped -ne 0 -or
        $inconclusive -ne 0 -or $passed -ne $total) {
        $preserveRunArtifacts = $true
        throw (
            "MIKU_D3D12_GPU_TEST_RESULTS_REJECTED:" +
            "total=$total;passed=$passed;failed=$failed;" +
            "skipped=$skipped;inconclusive=$inconclusive;result=$result")
    }

    $cases = @($report.SelectNodes("//test-case"))
    foreach ($requiredTest in $requiredTests) {
        $matching = @($cases | Where-Object {
            $_.fullname -eq $requiredTest
        })
        if ($matching.Count -ne 1) {
            $preserveRunArtifacts = $true
            throw (
                "MIKU_D3D12_GPU_REQUIRED_TEST_DISCOVERY_FAILED:" +
                "$requiredTest;count=$($matching.Count);result=$result")
        }
        if ($matching[0].result -ne "Passed") {
            $preserveRunArtifacts = $true
            throw (
                "MIKU_D3D12_GPU_REQUIRED_TEST_NOT_PASSED:" +
                "$requiredTest;result=$($matching[0].result);report=$result")
        }
    }

    $lockPath = Join-Path $projectRoot "Packages\packages-lock.json"
    if (-not (Test-Path -LiteralPath $lockPath)) {
        $preserveRunArtifacts = $true
        throw "MIKU_D3D12_GPU_PACKAGE_LOCK_MISSING:$lockPath"
    }
    $packageLock = Get-Content -Raw -LiteralPath $lockPath | ConvertFrom-Json
    $resolvedUrp = $packageLock.dependencies.
        'com.unity.render-pipelines.universal'.version
    $resolvedShaderGraph = $packageLock.dependencies.
        'com.unity.shadergraph'.version
    if ($resolvedUrp -ne $UrpVersion -or
        $resolvedShaderGraph -ne $ShaderGraphVersion) {
        $preserveRunArtifacts = $true
        throw (
            "MIKU_UNITY_PACKAGE_VERSION_MISMATCH:" +
            "unity=${UnityVersion}:urp=${resolvedUrp}:" +
            "shadergraph=${resolvedShaderGraph}:expectedUrp=${UrpVersion}:" +
            "expectedShaderGraph=${ShaderGraphVersion}")
    }

    $packageSha = (Get-FileHash -LiteralPath $packageArchive `
        -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($evidenceFullPath) {
        $evidence = [ordered]@{
            unity = $actualUnityVersion
            unityRevision = $actualUnityRevision
            urp = $resolvedUrp
            shaderGraph = $resolvedShaderGraph
            graphicsDevice = "Direct3D12"
            packageSha256 = $packageSha
            total = $total
            passed = $passed
            failed = $failed
            skipped = $skipped
            inconclusive = $inconclusive
            required = $requiredTests.Count
            completedUtc = [DateTime]::UtcNow.ToString("o")
        }
        [System.IO.File]::WriteAllText(
            $evidenceFullPath,
            (($evidence | ConvertTo-Json) + "`n"),
            [System.Text.UTF8Encoding]::new($false))
        Copy-Item -LiteralPath $result -Destination $evidenceXmlPath
        Copy-Item -LiteralPath $log -Destination $evidenceLogPath
    }

    Write-Output (
        "MIKU_D3D12_GPU_TESTS_PASSED:" +
        "unity=$actualUnityVersion;revision=$actualUnityRevision;" +
        "urp=$resolvedUrp;" +
        "shaderGraph=$resolvedShaderGraph;packageSha256=$packageSha;" +
        "total=$total;passed=$passed;required=$($requiredTests.Count)")
}
finally {
    if (-not $preserveRunArtifacts -and (Test-Path -LiteralPath $runRoot)) {
        $resolvedRun = [System.IO.Path]::GetFullPath($runRoot)
        $resolvedScratch = [System.IO.Path]::GetFullPath(
            $scratchRoot).TrimEnd('\') + '\'
        if (-not $resolvedRun.StartsWith(
            $resolvedScratch,
            [System.StringComparison]::OrdinalIgnoreCase)) {
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
    if (-not $preserveRunArtifacts -and
        (Test-Path -LiteralPath $scratchRoot) -and
        @(Get-ChildItem -LiteralPath $scratchRoot -Force).Count -eq 0) {
        $resolvedScratchRoot = [System.IO.Path]::GetFullPath($scratchRoot)
        if ($resolvedScratchRoot -ne
            $env:SystemDrive + "\miku-unity-dx12-tmp") {
            throw "Refusing to remove unexpected Unity scratch directory: $resolvedScratchRoot"
        }
        Remove-Item -LiteralPath $resolvedScratchRoot -Force
    }
}
