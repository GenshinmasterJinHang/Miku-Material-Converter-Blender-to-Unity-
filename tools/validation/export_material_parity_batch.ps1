param(
    [string]$BlenderExe = "",
    [string]$OutputRoot = "",
    [ValidateSet(256, 512, 1024, 2048, 4096)]
    [int]$Resolution = 2048,
    [ValidateRange(1, 4096)]
    [int]$Samples = 32,
    [ValidateRange(0, 256)]
    [int]$Margin = 32,
    [ValidateRange(128, 4096)]
    [int]$ReferenceSize = 768,
    [string[]]$Keys = @()
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$certifiedBlender = "C:\SteamLibrary\steamapps\common\Blender\blender.exe"
$BlenderExe = if ($BlenderExe) { $BlenderExe } else { $certifiedBlender }
if (-not (Test-Path -LiteralPath $certifiedBlender -PathType Leaf)) {
    throw "MIKU_BLENDER_EXECUTABLE_MISSING:$certifiedBlender"
}
$selectedBlender = (Resolve-Path -LiteralPath $BlenderExe).Path
$expectedBlender = (Resolve-Path -LiteralPath $certifiedBlender).Path
if (-not [string]::Equals(
        $selectedBlender,
        $expectedBlender,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "MIKU_BLENDER_EXECUTABLE_MISMATCH:expected=$expectedBlender:got=$selectedBlender"
}
$BlenderExe = $expectedBlender
$OutputRoot = if ($OutputRoot) { $OutputRoot } else { Join-Path $repoRoot "outputs\material-parity-batch" }
$manifestPath = Join-Path $PSScriptRoot "material_parity_batch.json"
$driverPath = Join-Path $PSScriptRoot "export_blender_material_reference.py"
$manifest = Get-Content -Raw -Encoding UTF8 -LiteralPath $manifestPath | ConvertFrom-Json
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$materials = @($manifest.materials)
if ($Keys.Count -gt 0) {
    $requested = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($key in $Keys) {
        if (-not [string]::IsNullOrWhiteSpace($key)) {
            [void]$requested.Add($key.Trim())
        }
    }
    $materials = @($materials | Where-Object { $requested.Contains([string]$_.key) })
    if ($materials.Count -ne $requested.Count) {
        $found = @($materials | ForEach-Object { [string]$_.key })
        $missing = @($requested | Where-Object { $_ -notin $found })
        throw "Unknown material parity key(s): $($missing -join ', ')"
    }
}

foreach ($item in $materials) {
    $blendPath = Join-Path $repoRoot ($item.blend -replace "/", "\")
    $arguments = @(
        $blendPath,
        "--background",
        "--python-exit-code", "1",
        "--python", $driverPath,
        "--",
        "--object-name", $item.object,
        "--output-root", $OutputRoot,
        "--bundle-name", $item.key,
        "--resolution", $Resolution.ToString(),
        "--samples", $Samples.ToString(),
        "--margin", $Margin.ToString(),
        "--reference-size", $ReferenceSize.ToString(),
        "--expect-bake", $(if ($item.expectBake) { "yes" } else { "no" })
    )
    if ($item.expectRelief) {
        $arguments += "--expect-relief"
    }
    & $BlenderExe @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Material parity export failed for $($item.object) with exit code $LASTEXITCODE"
    }
}
