param([switch]$PrepareUpdate)

$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework C# compiler not found.' }
$dist = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null
$sources = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs'
$references = '/reference:System.dll,System.Core.dll,System.Drawing.dll,System.Windows.Forms.dll,System.Xml.dll,Microsoft.CSharp.dll,System.Web.Extensions.dll,System.Security.dll'
$icon = Join-Path $PSScriptRoot 'assets\app.ico'
$qr = Join-Path $PSScriptRoot 'assets\reward.png'
$resources = @()
if (Test-Path -LiteralPath $qr) { $resources += "/resource:$qr,PersonalStickyNote.Reward" }
& $compiler /nologo /target:winexe /optimize+ /codepage:65001 /warn:4 "/out:$dist\PersonalStickyNote.exe" "/win32icon:$icon" $references $resources $sources.FullName
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)." }
$binary = Join-Path $dist 'PersonalStickyNote.exe'
if ($PrepareUpdate) {
    $blob = git hash-object --no-filters -- $binary
    if ($LASTEXITCODE -ne 0) { throw 'Could not calculate the Git blob ID.' }
    $manifest = [ordered]@{
        version = [Reflection.AssemblyName]::GetAssemblyName($binary).Version.ToString()
        sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $binary).Hash.ToLowerInvariant()
        blob = $blob
        size = (Get-Item -LiteralPath $binary).Length
    }
    $updates = Join-Path $PSScriptRoot 'updates'
    New-Item -ItemType Directory -Path $updates -Force | Out-Null
    Copy-Item -LiteralPath $binary -Destination (Join-Path $updates 'PersonalStickyNote.exe') -Force
    [IO.File]::WriteAllText((Join-Path $updates 'version.json'), (($manifest | ConvertTo-Json) + [Environment]::NewLine))
}
Write-Output $binary
