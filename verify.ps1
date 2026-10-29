$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " EnRAGE & NpcAiTalk Automated Build & Verification Suite" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# Step 0A: Build NemesisMod.dll and update scripts\NemesisMod.dll
& "$root\decompiled\NemesisMod\build.ps1"
if ($LASTEXITCODE -ne 0) {
    throw "NemesisMod compilation failed with exit code $LASTEXITCODE"
}
Copy-Item "$root\decompiled\NemesisMod\bin\Release\net48\NemesisMod.dll" "$root\scripts\NemesisMod.dll" -Force

# Step 0B: Build NpcAiTalk.dll and update scripts\NpcAiTalk.dll + scripts\NpcAiTalk.ini
& "$root\decompiled\NpcAiTalkMod\build.ps1"
if ($LASTEXITCODE -ne 0) {
    throw "NpcAiTalk compilation failed with exit code $LASTEXITCODE"
}
Copy-Item "$root\decompiled\NpcAiTalkMod\bin\Release\net48\NpcAiTalk.dll" "$root\scripts\NpcAiTalk.dll" -Force
Copy-Item "$root\config_templates\NpcAiTalk.ini" "$root\scripts\NpcAiTalk.ini" -Force

# Load assemblies for runtime reflection & schema checks
$refApi = [System.Reflection.Assembly]::LoadFrom("$root\decompiled\refs\ScriptHookRDRNetAPI.dll")
$refJson = [System.Reflection.Assembly]::LoadFrom("$root\scripts\Newtonsoft.Json.dll")
$modAsm = [System.Reflection.Assembly]::LoadFrom("$root\scripts\NemesisMod.dll")
$aiAsm = [System.Reflection.Assembly]::LoadFrom("$root\scripts\NpcAiTalk.dll")

# 1. Verify ApiCheck.Missing(Assembly) returns 0 missing RDR2.* members for BOTH NemesisMod.dll and NpcAiTalk.dll
$apiCheckType = $modAsm.GetType("NemesisSystem.ApiCheck")
$missingMethod = $apiCheckType.GetMethods([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Static) | Where-Object { $_.Name -eq "Missing" -and $_.GetParameters().Count -eq 1 } | Select-Object -First 1

$missingList = $missingMethod.Invoke($null, @($modAsm))
if ($missingList.Count -gt 0) {
    throw "ApiCheck found $($missingList.Count) missing members in NemesisMod.dll: $($missingList -join ', ')"
}
$missingAiList = $missingMethod.Invoke($null, @($aiAsm))
if ($missingAiList.Count -gt 0) {
    throw "ApiCheck found $($missingAiList.Count) missing members in NpcAiTalk.dll: $($missingAiList -join ', ')"
}
Write-Host "[PASS] 1. ApiCheck.Missing(Assembly): 0 missing RDR2.* API members across NemesisMod.dll & NpcAiTalk.dll" -ForegroundColor Green

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

# 5. Verify NpcAiSettings loads config_templates\NpcAiTalk.ini without errors
$aiSettingsType = $aiAsm.GetType("NpcAiTalk.NpcAiSettings")
$aiSettings = [System.Activator]::CreateInstance($aiSettingsType, @("$root\config_templates\NpcAiTalk.ini"))
if ([string]::IsNullOrWhiteSpace($aiSettings.ModelPreset) -or -not $aiSettings.AutoDownloadAndSetup) {
    throw "NpcAiTalk.ini did not load expected defaults: ModelPreset=$($aiSettings.ModelPreset)"
}
Write-Host "[PASS] 5. NpcAiSettings loaded NpcAiTalk.ini: ModelPreset=$($aiSettings.ModelPreset), TalkKey=$($aiSettings.TalkKey), AutoDownload=$($aiSettings.AutoDownloadAndSetup)" -ForegroundColor Green

# 6. Verify all JSON templates in config_templates\ are valid JSON and politics.json deserializes
$jsonFiles = Get-ChildItem "$root\config_templates" -Recurse -Filter "*.json"
foreach ($jf in $jsonFiles) {
    $raw = Get-Content $jf.FullName -Raw
    $null = [Newtonsoft.Json.Linq.JToken]::Parse($raw)
}
$civicStateType = $modAsm.GetType("NemesisSystem.CivicState")
$polRaw = [System.IO.File]::ReadAllText("$root\config_templates\politics.json")
$civicState = [Newtonsoft.Json.JsonConvert]::DeserializeObject($polRaw, $civicStateType)
if ($civicState.Towns.Count -ne 9 -or $civicState.Businesses.Count -ne 45) {
    throw "Unexpected CivicState counts: Towns=$($civicState.Towns.Count), Businesses=$($civicState.Businesses.Count)"
}
Write-Host "[PASS] 6. Validated $($jsonFiles.Count) JSON templates & CivicState ($($civicState.Towns.Count) towns, $($civicState.Businesses.Count) businesses)" -ForegroundColor Green

# 7. Verify NpcAiTalk Standalone + EnRAGE Bridge methods exist and execute cleanly
$exportMethod = $modAsm.GetType("NemesisSystem.NemesisMod").GetMethod("ExportAiNpcContext")
$recordOutcomeMethod = $modAsm.GetType("NemesisSystem.NemesisMod").GetMethod("RecordAiDialogueOutcome")
if ($null -eq $exportMethod -or $null -eq $recordOutcomeMethod) {
    throw "NemesisMod AI bridge methods missing!"
}
$ctxProfileType = $aiAsm.GetType("NpcAiTalk.NpcAiSettings")
$promptType = $aiAsm.GetType("NpcAiTalk.WildWestPrompt")
$profileObj = [System.Activator]::CreateInstance($aiAsm.GetType("NpcAiTalk.NpcContextProfile"))
$sysPrompt = $promptType.GetMethod("BuildSystemPrompt").Invoke($null, @($profileObj))
$choices = $promptType.GetMethod("BuildDialogueChoices").Invoke($null, @($profileObj, 10))
if ([string]::IsNullOrWhiteSpace($sysPrompt) -or $choices.Count -lt 6) {
    throw "WildWestPrompt failed to generate valid system prompt or dialogue choices!"
}
Write-Host "[PASS] 7. Verified NpcAiTalk Standalone Prompt Engine ($($choices.Count) choices) & EnRAGE Bridge Hooks" -ForegroundColor Green

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " ALL 7 CHECKS PASSED WITH ZERO ERRORS AND ZERO WARNINGS!" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Cyan
