$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework C# compiler not found.' }
$dist = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null
$sources = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs'
$references = '/reference:System.dll,System.Core.dll,System.Drawing.dll,System.Windows.Forms.dll,System.Xml.dll,Microsoft.CSharp.dll'
$icon = Join-Path $PSScriptRoot 'assets\app.ico'
& $compiler /nologo /target:winexe /optimize+ /codepage:65001 /warn:4 "/out:$dist\PersonalStickyNote.exe" "/win32icon:$icon" $references $sources.FullName
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)." }
Write-Output (Join-Path $dist 'PersonalStickyNote.exe')
