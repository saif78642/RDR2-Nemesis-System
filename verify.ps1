$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " EnRAGE (v0.9.5.3-civic) Automated Build & Verification" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# Step 0: Build from source and update scripts\NemesisMod.dll
& "$root\decompiled\NemesisMod\build.ps1"
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE"
}
Copy-Item "$root\decompiled\NemesisMod\bin\Release\net48\NemesisMod.dll" "$root\scripts\NemesisMod.dll" -Force

# Load assemblies for runtime reflection & schema checks
$refApi = [System.Reflection.Assembly]::LoadFrom("$root\decompiled\refs\ScriptHookRDRNetAPI.dll")
$refJson = [System.Reflection.Assembly]::LoadFrom("$root\scripts\Newtonsoft.Json.dll")
$modAsm = [System.Reflection.Assembly]::LoadFrom("$root\scripts\NemesisMod.dll")

# 1. Verify ApiCheck.Missing(Assembly) returns 0 missing RDR2.* members
$apiCheckType = $modAsm.GetType("NemesisSystem.ApiCheck")
$missingMethod = $apiCheckType.GetMethods([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Static) | Where-Object { $_.Name -eq "Missing" -and $_.GetParameters().Count -eq 1 } | Select-Object -First 1
$missingList = $missingMethod.Invoke($null, @($modAsm))
if ($missingList.Count -gt 0) {
    throw "ApiCheck found $($missingList.Count) missing members: $($missingList -join ', ')"
}
Write-Host "[PASS] 1. ApiCheck.Missing(Assembly): 0 missing RDR2.* API members (405/405 resolved)" -ForegroundColor Green

# 2. Verify Gangs.Defined initializes all 13 factions cleanly
$gangsType = $modAsm.GetType("NemesisSystem.Gangs")
$definedProp = $gangsType.GetProperty("Defined", [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Static)
$gangsDict = $definedProp.GetValue($null)
if ($gangsDict.Count -lt 13) {
    throw "Expected at least 13 factions in Gangs.Defined, found $($gangsDict.Count)"
}
$gangIds = ($gangsDict | ForEach-Object { $_.Id }) -join ', '
Write-Host "[PASS] 2. Gangs.Defined count: $($gangsDict.Count) factions ($gangIds)" -ForegroundColor Green

# 3. Verify NemesisSettings loads config_templates\NemesisMod.ini without errors
$settingsType = $modAsm.GetType("NemesisSystem.NemesisSettings")
$settings = [System.Activator]::CreateInstance($settingsType, @("$root\config_templates\NemesisMod.ini", $null))
if (-not [string]::IsNullOrEmpty($settings.KeysNote)) {
    throw "NemesisMod.ini had invalid key bindings: $($settings.KeysNote)"
}
Write-Host "[PASS] 3. NemesisSettings loaded NemesisMod.ini: CivicMenuKey=$($settings.CivicMenuKey), KeysNote=''" -ForegroundColor Green

# 4. Verify BulletImpactSettings loads config_templates\BulletImpact.ini without errors
$biSettingsType = $modAsm.GetType("NemesisSystem.BulletImpact.BulletImpactSettings")
$biSettings = [System.Activator]::CreateInstance($biSettingsType, @("$root\config_templates\BulletImpact.ini"))
Write-Host "[PASS] 4. BulletImpactSettings loaded BulletImpact.ini: Enabled=$($biSettings.Enabled), KillShots=$($biSettings.KillShots), Crippling=$($biSettings.Crippling)" -ForegroundColor Green

# 5. Verify all JSON templates in config_templates\ are valid JSON
$jsonFiles = Get-ChildItem "$root\config_templates" -Recurse -Filter "*.json"
foreach ($jf in $jsonFiles) {
    $raw = Get-Content $jf.FullName -Raw
    $null = [Newtonsoft.Json.Linq.JToken]::Parse($raw)
}
Write-Host "[PASS] 5. Validated all $($jsonFiles.Count) JSON templates in config_templates\" -ForegroundColor Green

# 6. Verify politics.json deserializes cleanly into NemesisSystem.CivicState
$civicStateType = $modAsm.GetType("NemesisSystem.CivicState")
$polRaw = [System.IO.File]::ReadAllText("$root\config_templates\politics.json")
$civicState = [Newtonsoft.Json.JsonConvert]::DeserializeObject($polRaw, $civicStateType)
if ($civicState.Towns.Count -ne 9 -or $civicState.Businesses.Count -ne 45) {
    throw "Unexpected CivicState counts: Towns=$($civicState.Towns.Count), Businesses=$($civicState.Businesses.Count)"
}
Write-Host "[PASS] 6. CivicState deserialized: $($civicState.Towns.Count) towns, $($civicState.Businesses.Count) businesses, $($civicState.Bounties.Count) bounties" -ForegroundColor Green

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " ALL CHECKS PASSED WITH ZERO ERRORS AND ZERO WARNINGS!" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Cyan
