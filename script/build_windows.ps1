#Requires -Version 5.1
<#
.SYNOPSIS
    Builds the Windows (WPF) build of MutsumiPet.
.DESCRIPTION
    Compiles straight from the .NET Framework 4.8 compiler that ships with Windows,
    so no SDK install is required. The shared PNG artwork is embedded into the
    executable, which therefore needs no side-by-side resource folder.

    Anyone who does have the .NET SDK can instead run
    `dotnet build windows/MutsumiPet.Windows/MutsumiPet.Windows.csproj`;
    both paths compile the exact same sources.
.PARAMETER SignThumbprint
    Thumbprint of a code signing certificate in Cert:\CurrentUser\My or
    Cert:\LocalMachine\My. Preferred over -SignCertificate: nothing secret ever
    reaches a command line or the process list.
.PARAMETER SignCertificate
    Path to a .pfx instead. Its password is read from the MUTSUMIPET_CERT_PASSWORD
    environment variable; the script never prompts for or stores it.
.EXAMPLE
    ./script/build_windows.ps1 -Run
.EXAMPLE
    ./script/build_windows.ps1 -Verify
.EXAMPLE
    ./script/build_windows.ps1 -SignThumbprint A1B2C3...
#>
[CmdletBinding()]
param(
    [switch]$Run,
    [switch]$Test,
    [switch]$Verify,
    [switch]$Clean,
    [string]$SignThumbprint,
    [string]$SignCertificate,
    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$appDir = Join-Path $root 'windows\MutsumiPet.Windows'
$testDir = Join-Path $root 'windows\MutsumiPet.Windows.Tests'
$resourceDir = Join-Path $root 'Sources\MutsumiPet\Resources'
$outDir = Join-Path $root 'build\windows'
$appExe = Join-Path $outDir 'MutsumiPet.exe'
$testExe = Join-Path $outDir 'MutsumiPetTests.exe'
$iconPath = Join-Path $root 'assets\MutsumiPet.ico'

if ($Verify) { $Test = $true; $Run = $true }

if ($Clean -and (Test-Path $outDir)) {
    Remove-Item $outDir -Recurse -Force
}

# --- Locate the compiler -----------------------------------------------------

$frameworkDir = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path $frameworkDir)) {
    $frameworkDir = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
}
$csc = Join-Path $frameworkDir 'csc.exe'
if (-not (Test-Path $csc)) {
    throw "C# compiler not found at $csc. Windows should ship .NET Framework 4.x; install it or use 'dotnet build' instead."
}

$wpfDir = Join-Path $frameworkDir 'WPF'
$references = @(
    (Join-Path $frameworkDir 'System.dll'),
    (Join-Path $frameworkDir 'System.Core.dll'),
    (Join-Path $frameworkDir 'System.Xaml.dll'),
    (Join-Path $wpfDir 'WindowsBase.dll'),
    (Join-Path $wpfDir 'PresentationCore.dll'),
    (Join-Path $wpfDir 'PresentationFramework.dll')
)
foreach ($reference in $references) {
    if (-not (Test-Path $reference)) { throw "Missing reference assembly: $reference" }
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# This script never reads the .csproj files, so a malformed one would otherwise
# only surface in CI. Well-formedness is cheap to check and catches the easy
# mistakes (a '--' inside an XML comment, an unclosed element).
foreach ($project in Get-ChildItem (Join-Path $root 'windows') -Filter '*.csproj' -Recurse) {
    try {
        (New-Object System.Xml.XmlDocument).Load($project.FullName)
    } catch {
        throw ("{0} is not well-formed XML: {1}" -f $project.Name, $_.Exception.InnerException.Message)
    }
}

# --- Icon --------------------------------------------------------------------

if (-not (Test-Path $iconPath)) {
    Write-Host 'Generating assets/MutsumiPet.ico ...'
    & (Join-Path $PSScriptRoot 'make_windows_icon.ps1')
}

# --- Helpers -----------------------------------------------------------------

function Invoke-Csc {
    param(
        [string]$ResponsePath,
        [string[]]$Arguments,
        [string]$Label
    )

    # No BOM: csc treats a leading byte-order mark as part of the first argument.
    [System.IO.File]::WriteAllLines($ResponsePath, [string[]]$Arguments, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "Compiling $Label ..."
    $output = & $csc "@$ResponsePath"
    $exitCode = $LASTEXITCODE
    $output | Where-Object { $_ -and $_ -notmatch '^Microsoft \(R\)' -and $_ -notmatch '^Copyright' -and $_.Trim() } | ForEach-Object { Write-Host $_ }
    if ($exitCode -ne 0) { throw "$Label failed to compile (exit $exitCode)." }
}

function Stop-Pet {
    Get-Process -Name 'MutsumiPet' -ErrorAction SilentlyContinue | ForEach-Object {
        $_.Kill()
        $_.WaitForExit(5000) | Out-Null
    }
}

# --- Authenticode signing ----------------------------------------------------

# signtool gives RFC 3161 timestamps and is what every CI signing provider drives,
# but it only exists if the Windows SDK is installed. Set-AuthenticodeSignature is
# always available and is a fine fallback for a hobby build.
function Get-SignTool {
    $onPath = Get-Command signtool -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }

    foreach ($kitRoot in @("${env:ProgramFiles(x86)}\Windows Kits\10\bin", "$env:ProgramFiles\Windows Kits\10\bin")) {
        if (-not (Test-Path $kitRoot)) { continue }
        $candidate = Get-ChildItem $kitRoot -Recurse -Filter 'signtool.exe' -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($candidate) { return $candidate.FullName }
    }
    return $null
}

function Resolve-SigningCertificate {
    if ($SignThumbprint) {
        $thumb = $SignThumbprint -replace '[^0-9A-Fa-f]', ''
        foreach ($store in @('Cert:\CurrentUser\My', 'Cert:\LocalMachine\My')) {
            $match = Get-ChildItem $store -ErrorAction SilentlyContinue |
                Where-Object { $_.Thumbprint -eq $thumb }
            if ($match) { return $match[0] }
        }
        throw "No certificate with thumbprint $thumb in CurrentUser\My or LocalMachine\My."
    }

    if (-not (Test-Path $SignCertificate)) { throw "Certificate not found: $SignCertificate" }
    $password = $env:MUTSUMIPET_CERT_PASSWORD
    if (-not $password) {
        throw 'Set MUTSUMIPET_CERT_PASSWORD to the .pfx password before signing.'
    }
    $secure = ConvertTo-SecureString $password -AsPlainText -Force
    # Loaded in-process so the password never appears in a command line.
    return New-Object System.Security.Cryptography.X509Certificates.X509Certificate2(
        (Resolve-Path $SignCertificate).Path, $secure,
        [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet)
}

function Invoke-Sign {
    param([string]$Path)
    if (-not $SignThumbprint -and -not $SignCertificate) { return }

    $certificate = Resolve-SigningCertificate
    Write-Host ("Signing {0} with {1}" -f (Split-Path -Leaf $Path), $certificate.Subject)

    $signTool = Get-SignTool
    if ($signTool -and $SignThumbprint) {
        # Kept ASCII on purpose: Windows PowerShell 5.1 decodes a BOM-less .ps1 as
        # ANSI, so a non-ASCII literal here would break the whole script's parse.
        & $signTool sign /sha1 $certificate.Thumbprint /fd SHA256 `
            /tr $TimestampUrl /td SHA256 /d 'MutsumiPet' $Path | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "signtool failed (exit $LASTEXITCODE)." }
    } else {
        if (-not $signTool) {
            Write-Warning 'signtool.exe not found; falling back to Set-AuthenticodeSignature (legacy timestamp format).'
        }
        Set-AuthenticodeSignature -FilePath $Path -Certificate $certificate `
            -HashAlgorithm SHA256 -TimestampServer $TimestampUrl -Force | Out-Null
    }

    # 'UnknownError' here means the bytes were signed fine but this machine cannot
    # chain the certificate to a trusted root - normal for a self-signed test cert
    # or a build agent without the issuer installed. Anything else is a real fault.
    $signature = Get-AuthenticodeSignature -FilePath $Path
    switch ($signature.Status) {
        'Valid' {
            Write-Host ("  signature: Valid ({0})" -f $signature.SignerCertificate.Subject)
        }
        { $_ -in 'UnknownError', 'NotTrusted' } {
            Write-Warning ("Signed, but the chain is not trusted on this machine ({0}). Expected for a self-signed certificate." -f $signature.Status)
        }
        default {
            throw ("Signing failed: {0} - {1}" -f $signature.Status, $signature.StatusMessage)
        }
    }
}

# Smart App Control refuses freshly built, unsigned executables. The build itself
# succeeded in that case, so say so rather than leaving a bare Win32 error.
function Assert-NotBlocked {
    param($ErrorRecord, [string]$Name)
    if ("$ErrorRecord" -notmatch 'Application Control') { return }
    Write-Warning @"
$Name was built successfully but Windows refused to start it.

Smart App Control (Settings > Privacy & security > Windows Security >
App & browser control) blocks unsigned executables it has no reputation for.
Turn it off, or sign the executable, to run locally built binaries.
"@
}

# --- Compile the app ---------------------------------------------------------

# bin/obj are skipped so a previous `dotnet build` cannot leak generated sources in.
function Get-Sources {
    param([string]$Directory)
    Get-ChildItem -Path $Directory -Filter '*.cs' -Recurse |
        Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
        ForEach-Object { $_.FullName }
}

$appSources = Get-Sources $appDir
if (-not $appSources) { throw "No C# sources found under $appDir" }

$resources = Get-ChildItem -Path $resourceDir -Filter '*.png' | ForEach-Object {
    "/resource:`"$($_.FullName)`",$($_.Name)"
}
if (-not $resources) { throw "No PNG resources found under $resourceDir" }

Stop-Pet

$appArgs = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/langversion:5',
    # Sources are UTF-8 and not all of them carry a BOM. Without this the legacy
    # compiler falls back to the machine's ANSI codepage and mangles the dialogue
    # on any system whose locale is not the one the file was written on.
    '/codepage:65001',
    '/optimize+',
    '/debug:pdbonly',
    '/utf8output',
    "/main:MutsumiPet.App.MutsumiPetApp",
    "/win32icon:`"$iconPath`"",
    "/out:`"$appExe`""
)
$appArgs += $references | ForEach-Object { "/reference:`"$_`"" }
$appArgs += $resources
$appArgs += $appSources | ForEach-Object { "`"$_`"" }

Invoke-Csc -ResponsePath (Join-Path $outDir 'app.rsp') -Arguments $appArgs -Label 'MutsumiPet.exe'
Write-Host ("  -> {0} ({1:N0} bytes)" -f $appExe, (Get-Item $appExe).Length)

Invoke-Sign $appExe

# --- Compile and run the tests ----------------------------------------------

if ($Test) {
    $testSources = Get-Sources $testDir
    if (-not $testSources) { throw "No C# sources found under $testDir" }

    # The app sources are compiled straight into the test binary rather than
    # referenced, mirroring Swift's `@testable import MutsumiPet`. /main: picks the
    # test entry point over the app's.
    $testArgs = @(
        '/nologo',
        '/target:exe',
        '/platform:anycpu',
        '/langversion:5',
    # Sources are UTF-8 and not all of them carry a BOM. Without this the legacy
    # compiler falls back to the machine's ANSI codepage and mangles the dialogue
    # on any system whose locale is not the one the file was written on.
    '/codepage:65001',
        '/debug:pdbonly',
        '/utf8output',
        '/main:MutsumiPet.Tests.TestRunner',
        "/out:`"$testExe`""
    )
    $testArgs += $references | ForEach-Object { "/reference:`"$_`"" }
    $testArgs += $resources
    $testArgs += $appSources | ForEach-Object { "`"$_`"" }
    $testArgs += $testSources | ForEach-Object { "`"$_`"" }

    Invoke-Csc -ResponsePath (Join-Path $outDir 'tests.rsp') -Arguments $testArgs -Label 'MutsumiPetTests.exe'

    Write-Host 'Running tests ...'
    try {
        & $testExe
    } catch {
        Assert-NotBlocked $_ 'MutsumiPetTests.exe'
        throw
    }
    if ($LASTEXITCODE -ne 0) { throw "Tests failed (exit $LASTEXITCODE)." }
}

# --- Run ---------------------------------------------------------------------

if ($Run) {
    Stop-Pet
    Write-Host 'Launching MutsumiPet ...'
    try {
        Start-Process -FilePath $appExe -WorkingDirectory $outDir | Out-Null
    } catch {
        Assert-NotBlocked $_ 'MutsumiPet.exe'
        throw
    }

    if ($Verify) {
        Start-Sleep -Seconds 2
        $process = Get-Process -Name 'MutsumiPet' -ErrorAction SilentlyContinue
        if (-not $process) { throw 'MutsumiPet exited within two seconds of launching.' }
        Write-Host ("Verified: MutsumiPet is running (pid {0})." -f $process.Id)
    }
}
