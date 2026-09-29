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

# aspnet_compiler walks the WHOLE site directory, including .vs\, which
# Visual Studio keeps open. Pointed straight at the working tree it fails
# with "ASPRUNTIME: The process cannot access the file ...vsidx" whenever
# the solution is open - a lock, reported in the same channel as a real
# markup error, which is exactly the kind of false negative this script
# exists to prevent.
#
# So: stage a copy without the directories the compiler has no use for,
# and compile that. bin\ is kept because the markup needs the compiled
# code-behind assembly to resolve against. Short paths, because Chrome and
# aspnet_compiler both choke on long ones under this repo's location.
$stage = Join-Path $env:TEMP ('bnpre-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$precompileOut = Join-Path $env:TEMP ('bnout-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))

try {
    $null = robocopy $repo $stage /MIR /NFL /NDL /NJH /NJS /NP /R:1 /W:1 `
        /XD '.vs' '.git' 'obj' 'packages' 'App_Data\Logs' 'BusNova.Tests'

    # robocopy uses exit codes 0-7 for success; 8+ is a real failure.
    #
    # Read it into an ordinary variable and leave $LASTEXITCODE alone.
    #
    # This line used to normalise robocopy's result with
    # "else { $LASTEXITCODE = 0 }", and that one assignment silently
    # disabled the two checks below it. $LASTEXITCODE is an automatic
    # variable living in the GLOBAL scope; assigning to it from inside a
    # script creates a new SCRIPT-scoped variable that shadows it. Native
    # commands go on updating the global one, while every later read in
    # this file returns the shadowing copy - stuck at 0 forever.
    #
    # The effect was that stage 2 could not fail on a markup error and
    # stage 3 could not fail on a failing test: the script printed
    # "RESULT: PASSED - build, markup and tests all clean" directly
    # underneath "Failed: 1". Only the MSBuild check still worked, because
    # it runs before the assignment. A verifier that cannot fail is worse
    # than no verifier, because it is trusted. (WP-14)
    $robocopyCode = $LASTEXITCODE
    if ($robocopyCode -ge 8) {
        $failures.Add('Could not stage a copy of the site for precompilation.')
        Write-Output 'robocopy failed; skipping the markup stage rather than reporting a false result.'
    }

    & $aspnetCompiler -v / -p $stage -f $precompileOut
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
    foreach ($tmp in @($precompileOut, $stage)) {
        if ($tmp -and (Test-Path $tmp)) {
            Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
        }
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
        & $vstest $testDll /Logger:console`;verbosity=minimal 2>&1 | Tee-Object -Variable testOutput
        if ($LASTEXITCODE -ne 0) { $failures.Add('Unit tests failed.') }

        # vstest exits 0 when it runs NOTHING. With the project inside a
        # OneDrive folder the MSTest adapter fails to load (0x80131515,
        # "Operation is not supported" - the DLL is treated as a remote
        # file), vstest reports "No test is available", and this script
        # printed PASSED over zero tests. A run only counts if it ran
        # something.
        if (-not ($testOutput -match '(Total tests|Total):\s*[1-9]')) {
            $failures.Add('No tests were executed - the test adapter did not load. Copy the solution out of OneDrive, or run BusNova.Tests from a local folder.')
        }
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
