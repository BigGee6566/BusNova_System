<#
    Verify.ps1 - what "verified" means in this repo.

    Three stages. Any one of them failing fails the script.

        1. MSBuild            compiles the C#
        2. aspnet_compiler    compiles the .aspx MARKUP
        3. vstest.console     runs the unit tests

    Stage 2 is the one people skip, and it is the reason this script
    exists. ASP.NET Web Forms compiles markup on FIRST REQUEST, not at
    build time, so MSBuild reports a clean build for a page that will
    throw an HttpParseException and return 500 the moment anyone opens
    it. aspnet_compiler is the only way to find that before a user does.
    It has caught six defects in this repo that MSBuild could not see.

    So: RUN THIS AFTER EVERY MARKUP EDIT, not once per sitting. The
    DriverWorkspace.aspx 500 in commit 04a27d1 got in precisely because
    the precompile had been run before the edit rather than after it.

    Stage 3 carries the other half. A misspelt data- attribute compiles
    perfectly and breaks silently at runtime, so no compiler can see it;
    BusNova.Tests\MarkupGuardTests.cs is the rule that can.

    Usage:
        .\Verify.ps1                 # Debug
        .\Verify.ps1 -Configuration Release
        .\Verify.ps1 -SkipTests      # build + markup only, for a fast loop

    Exit code 0 only when all requested stages pass.
#>

[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [switch]$SkipTests
)

$ErrorActionPreference = 'Continue'
$repo = $PSScriptRoot
$failures = New-Object System.Collections.Generic.List[string]

function Write-Stage([string]$text) {
    Write-Output ''
    Write-Output ('=' * 70)
    Write-Output "  $text"
    Write-Output ('=' * 70)
}

# ---------------------------------------------------------------------
# Toolchain
#
# Resolved explicitly and reported when missing. A tool that is quietly
# skipped is worse than one that is absent: the run still says PASS.
# ---------------------------------------------------------------------

function Find-Tool([string]$name, [string[]]$candidates) {
    foreach ($c in $candidates) {
        $hit = Get-ChildItem $c -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($hit) { return $hit.FullName }
    }
    $cmd = Get-Command $name -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

$msbuild = Find-Tool 'MSBuild.exe' @(
    'C:\Program Files\Microsoft Visual Studio\*\*\MSBuild\Current\Bin\MSBuild.exe',
    'C:\Program Files (x86)\Microsoft Visual Studio\*\*\MSBuild\Current\Bin\MSBuild.exe'
)

$aspnetCompiler = Find-Tool 'aspnet_compiler.exe' @(
    'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\aspnet_compiler.exe',
    'C:\Windows\Microsoft.NET\Framework\v4.0.30319\aspnet_compiler.exe'
)

$vstest = Find-Tool 'vstest.console.exe' @(
    'C:\Program Files\Microsoft Visual Studio\*\*\Common7\IDE\Extensions\TestPlatform\vstest.console.exe',
    'C:\Program Files (x86)\Microsoft Visual Studio\*\*\Common7\IDE\Extensions\TestPlatform\vstest.console.exe'
)

$missing = @()
if (-not $msbuild)        { $missing += 'MSBuild.exe' }
if (-not $aspnetCompiler) { $missing += 'aspnet_compiler.exe (.NET Framework 4)' }
if (-not $SkipTests -and -not $vstest) { $missing += 'vstest.console.exe' }

if ($missing.Count -gt 0) {
    Write-Output ''
    Write-Output 'Cannot verify - these tools were not found:'
    $missing | ForEach-Object { Write-Output "  - $_" }
    Write-Output ''
    Write-Output 'Install Visual Studio 2022 with the ASP.NET and web development workload,'
    Write-Output 'or run this from a Developer PowerShell so the tools are on PATH.'
    exit 2
}

# ---------------------------------------------------------------------
# 1. Build
# ---------------------------------------------------------------------

Write-Stage "1/3  MSBuild  ($Configuration)"

& $msbuild "$repo\BusNova System.sln" /t:Build /p:Configuration=$Configuration /v:minimal /nologo
if ($LASTEXITCODE -ne 0) {
    $failures.Add('MSBuild failed.')
    Write-Output ''
    Write-Output 'Build failed - stopping. The later stages need its output.'
    Write-Output ''
    Write-Output 'RESULT: FAILED'
    exit 1
}
Write-Output 'Build OK.'

# ---------------------------------------------------------------------
# 2. Markup
#
# -v /  virtual path, -p  physical path, -f  overwrite the target.
# The output is thrown away; the exit code and the error list are the
# whole point.
# ---------------------------------------------------------------------

Write-Stage '2/3  aspnet_compiler  (.aspx markup)'

$precompileOut = Join-Path $env:TEMP ('busnova-precompile-' + [Guid]::NewGuid().ToString('N'))

try {
    & $aspnetCompiler -v / -p $repo -f $precompileOut
    if ($LASTEXITCODE -ne 0) {
        $failures.Add('aspnet_compiler failed - a page would throw at first request.')
        Write-Output ''
        Write-Output 'Markup did NOT compile. MSBuild passed anyway, which is exactly'
        Write-Output 'why this stage exists - the page above would return 500 to a user.'
    }
    else {
        Write-Output 'Markup compiles clean.'
    }
}
finally {
    if (Test-Path $precompileOut) {
        Remove-Item $precompileOut -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# ---------------------------------------------------------------------
# 3. Tests
# ---------------------------------------------------------------------

if ($SkipTests) {
    Write-Stage '3/3  tests  (SKIPPED by -SkipTests)'
    Write-Output 'Not a full verification. Do not report a clean run from this.'
}
else {
    Write-Stage '3/3  vstest.console  (unit tests)'

    $testDll = Join-Path $repo "BusNova.Tests\bin\$Configuration\BusNova.Tests.dll"
    if (-not (Test-Path $testDll)) {
        $failures.Add("Test assembly not found at $testDll.")
    }
    else {
        & $vstest $testDll /Logger:console`;verbosity=minimal
        if ($LASTEXITCODE -ne 0) { $failures.Add('Unit tests failed.') }
    }
}

# ---------------------------------------------------------------------

Write-Output ''
Write-Output ('=' * 70)

if ($failures.Count -eq 0) {
    if ($SkipTests) {
        Write-Output '  RESULT: build and markup OK (tests skipped)'
    }
    else {
        Write-Output '  RESULT: PASSED - build, markup and tests all clean'
    }
    Write-Output ('=' * 70)
    exit 0
}

Write-Output '  RESULT: FAILED'
$failures | ForEach-Object { Write-Output "    - $_" }
Write-Output ('=' * 70)
exit 1
