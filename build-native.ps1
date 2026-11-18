$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw 'The Windows .NET Framework C# compiler was not found.' }

if (-not ('MyOmniNativePathHelper' -as [type])) {
    Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;
using System.Text;
public static class MyOmniNativePathHelper {
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern uint GetShortPathName(string longPath, StringBuilder shortPath, uint bufferLength);
}
'@
}
function Get-ShortPath([string]$path) {
    $buffer = New-Object System.Text.StringBuilder 1024
    $length = [MyOmniNativePathHelper]::GetShortPathName($path, $buffer, [uint32]$buffer.Capacity)
    if ($length -eq 0 -or $length -ge $buffer.Capacity) { throw "Could not resolve a short path for $path." }
    return $buffer.ToString()
}

$nodeCommand = Get-Command node.exe -ErrorAction Stop
$nodePath = $nodeCommand.Source
$nodeVersionText = (& $nodePath --version).Trim().TrimStart('v')
$nodeVersion = [version]$nodeVersionText
if ($nodeVersion.Major -lt 24 -or ($nodeVersion.Major -eq 24 -and $nodeVersion.Minor -lt 15)) {
    throw "Node $nodeVersionText is too old for the bundled npm runtime. Node 24.15+ is required."
}

$npmRoot = Join-Path (Split-Path $nodePath -Parent) 'node_modules/npm'
$npmCli = Join-Path $npmRoot 'bin/npm-cli.js'
if (-not (Test-Path $npmCli)) { throw "Could not locate npm at $npmRoot." }

$release = Join-Path $root 'release'
$stage = Join-Path $env:TEMP ('my-omni-native-' + [guid]::NewGuid().ToString('N'))
$nodeResource = Join-Path $stage 'MyOmniNative.node.exe'
$npmArchive = Join-Path $stage 'MyOmniNative.npm.zip'
$outputName = if ([String]::IsNullOrWhiteSpace($env:MYOMNI_OUTPUT)) { 'MyOmni-0.1.0-Windows-x64.exe' } else { $env:MYOMNI_OUTPUT }
if ($outputName -notmatch '^[A-Za-z0-9._-]+\.exe$') { throw 'MYOMNI_OUTPUT must be an .exe file name without a path.' }
$output = Join-Path $release $outputName
New-Item -ItemType Directory -Force -Path $release, $stage | Out-Null
$shortRoot = Get-ShortPath $root
$shortStage = Get-ShortPath $stage
$shortRelease = Get-ShortPath $release
$shortNode = Get-ShortPath $nodePath
$shortCsc = Get-ShortPath $csc

try {
    Copy-Item -LiteralPath $nodePath -Destination $nodeResource
    Compress-Archive -Path (Join-Path $npmRoot '*') -DestinationPath $npmArchive -CompressionLevel Optimal

    $arguments = @(
        '/nologo',
        '/target:winexe',
        '/platform:anycpu',
        '/optimize+',
        ('/out:' + $shortRelease + '\' + $outputName),
        '/reference:System.dll',
        '/reference:System.Core.dll',
        '/reference:System.Windows.Forms.dll',
        '/reference:System.Drawing.dll',
        '/reference:System.Net.Http.dll',
        '/reference:System.Web.Extensions.dll',
        '/reference:System.IO.Compression.dll',
        '/reference:System.IO.Compression.FileSystem.dll',
        '/reference:System.Security.dll',
        ('/resource:' + (Get-ShortPath $nodeResource) + ',MyOmniNative.node.exe'),
        ('/resource:' + (Get-ShortPath $npmArchive) + ',MyOmniNative.npm.zip'),
        (Get-ShortPath (Join-Path $root 'MyOmniNative.cs'))
    )
    & $shortCsc @arguments
    if ($LASTEXITCODE -ne 0) { throw "Native compile failed with exit code $LASTEXITCODE." }
    if (-not (Test-Path $output)) { throw "The native compiler did not create $output." }
    Write-Host "Built $output"
    Write-Host "Bundled Node $nodeVersionText and npm from $npmRoot"
    Write-Host 'OmniRoute is installed on demand after user approval or when Install / Start OmniRoute is selected.'
}
finally {
    if (Test-Path $stage) {
        try { Remove-Item -LiteralPath $stage -Recurse -Force }
        catch { Write-Warning "Temporary build files remain at $stage because they are still in use." }
    }
}
