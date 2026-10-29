$ErrorActionPreference = "Stop"
$projDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent (Split-Path -Parent $projDir)
$outDir = Join-Path $projDir "bin\Release\net48"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$csc = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\Roslyn\csc.exe"
if (Test-Path (Join-Path $rootDir "tools\roslyn\csc.exe")) { $csc = Join-Path $rootDir "tools\roslyn\csc.exe" }
if ($env:CSC_PATH) { $csc = $env:CSC_PATH }
$net48 = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$refsDir = Join-Path (Split-Path -Parent $projDir) "refs"

$csFiles = Get-ChildItem -Recurse -Path $projDir -Filter "*.cs" |
  Where-Object { $_.FullName -notlike "*\bin\*" -and $_.FullName -notlike "*\obj\*" } |
  ForEach-Object { '"' + $_.FullName + '"' }

$outDll = Join-Path $outDir "NpcAiTalk.dll"
$rspPath = Join-Path $projDir "build.rsp"

$rspLines = @(
  '/target:library',
  "/out:`"$outDll`"",
  '/langversion:latest',
  '/nullable:annotations',
  '/unsafe',
  '/optimize+',
  '/nostdlib+',
  "/r:`"$net48\mscorlib.dll`"",
  "/r:`"$net48\System.dll`"",
  "/r:`"$net48\System.Core.dll`"",
  "/r:`"$net48\System.Net.Http.dll`"",
  "/r:`"$net48\System.IO.Compression.dll`"",
  "/r:`"$net48\System.IO.Compression.FileSystem.dll`"",
  "/r:`"$net48\System.Windows.Forms.dll`"",
  "/r:`"$net48\Microsoft.CSharp.dll`"",
  "/r:`"$refsDir\ScriptHookRDRNetAPI.dll`"",
  "/r:`"$refsDir\Newtonsoft.Json.dll`"",
  '/nologo'
) + $csFiles

[System.IO.File]::WriteAllLines($rspPath, $rspLines)
& $csc "@$rspPath"
if ($LASTEXITCODE -ne 0) {
  throw "Compilation failed with exit code $LASTEXITCODE"
}
Write-Host "Successfully built: $outDll ($((Get-Item $outDll).Length) bytes)"
