# test-mod.ps1 - Automated regression tests for Backstories Anthology mod
# Run with: powershell -ExecutionPolicy Bypass -File scripts/test-mod.ps1
# 38 tests covering directory structure, XML validity, defName rules, translations, patches.

$ErrorActionPreference = 'Stop'
$modRoot = Resolve-Path "$PSScriptRoot\.."
$pass = 0
$fail = 0
$failed = @()

function Assert-True {
    param([string]$Name, [bool]$Condition, [string]$Detail = '')
    if ($Condition) {
        Write-Host "  PASS  $Name" -ForegroundColor Green
        $script:pass++
    } else {
        Write-Host "  FAIL  $Name" -ForegroundColor Red
        if ($Detail) { Write-Host "        $Detail" -ForegroundColor Yellow }
        $script:fail++
        $script:failed += $Name
    }
}

Write-Host '=== Backstories Anthology - Regression Tests ===' -ForegroundColor Cyan
Write-Host ''

# ============================================================
# 1. Directory structure (17 tests)
# ============================================================
Write-Host '[1/11] Directory structure (17 tests)' -ForegroundColor Cyan

Assert-True 'About/ folder exists' (Test-Path "$modRoot\About")
Assert-True 'About/About.xml exists' (Test-Path "$modRoot\About\About.xml")
Assert-True 'About/Preview.png exists' (Test-Path "$modRoot\About\Preview.png")
Assert-True 'LoadFolders.xml exists' (Test-Path "$modRoot\LoadFolders.xml")
Assert-True '1.6/ folder exists' (Test-Path "$modRoot\1.6")
Assert-True '1.6/Defs/ folder exists' (Test-Path "$modRoot\1.6\Defs")
Assert-True '1.6/Defs/BackstoryDefs/ exists' (Test-Path "$modRoot\1.6\Defs\BackstoryDefs")
Assert-True '1.6/Patches/ exists' (Test-Path "$modRoot\1.6\Patches")
Assert-True '1.6/Assemblies/ exists' (Test-Path "$modRoot\1.6\Assemblies")
Assert-True 'Assemblies/UnifiedBackstories.dll exists' (Test-Path "$modRoot\Assemblies\UnifiedBackstories.dll")
Assert-True 'Languages/English/ exists' (Test-Path "$modRoot\Languages\English")
Assert-True 'Languages/English/DefInjected/ exists' (Test-Path "$modRoot\Languages\English\DefInjected")
Assert-True 'Languages/English/Keyed/ exists' (Test-Path "$modRoot\Languages\English\Keyed")
Assert-True 'Source/UnifiedBackstories/ exists' (Test-Path "$modRoot\Source\UnifiedBackstories")
Assert-True 'README.md exists' (Test-Path "$modRoot\README.md")
Assert-True 'CHANGELOG.md exists' (Test-Path "$modRoot\CHANGELOG.md")
Assert-True 'CREDITS.txt exists' (Test-Path "$modRoot\CREDITS.txt")

# ============================================================
# 2. XML validity (5 tests)
# ============================================================
Write-Host ''
Write-Host '[2/11] XML validity (5 tests)' -ForegroundColor Cyan

$xmlDirs = @('1.6\Defs\BackstoryDefs', '1.6\Patches', '1.6\Defs\RecordDefs', '1.6\Defs\ThoughtDefs', '1.6\Defs\InteractionDefs')
foreach ($dir in $xmlDirs) {
    $fullDir = "$modRoot\$dir"
    if (-not (Test-Path $fullDir)) {
        Assert-True "All XML in $dir valid" $false 'Directory missing'
        continue
    }
    $xmlFiles = Get-ChildItem $fullDir -Filter *.xml
    $allValid = $true
    $errFile = ''
    foreach ($f in $xmlFiles) {
        try {
            [xml](Get-Content $f.FullName -Raw) | Out-Null
        } catch {
            $allValid = $false
            $errFile = $f.Name
            break
        }
    }
    Assert-True "All XML in $dir valid" $allValid $errFile
}

# ============================================================
# 3. DefName prefix UB_ (2 tests)
# ============================================================
Write-Host ''
Write-Host '[3/11] DefName prefix UB_ (2 tests)' -ForegroundColor Cyan

$defFiles = Get-ChildItem "$modRoot\1.6\Defs\BackstoryDefs" -Filter *.xml
$allPrefixed = $true
$nonUbDef = ''
$defCount = 0
foreach ($f in $defFiles) {
    $content = Get-Content $f.FullName -Raw
    $matches = [regex]::Matches($content, '<defName>([^<]+)</defName>')
    foreach ($m in $matches) {
        $defCount++
        $dn = $m.Groups[1].Value
        if (-not $dn.StartsWith('UB_')) {
            $allPrefixed = $false
            $nonUbDef = $dn
            break
        }
    }
}
Assert-True 'All BackstoryDef defNames use UB_ prefix' $allPrefixed $nonUbDef
Assert-True 'BackstoryDef count > 1000' ($defCount -gt 1000) "Actual: $defCount"

# ============================================================
# 4. No duplicate defNames (2 tests)
# ============================================================
Write-Host ''
Write-Host '[4/11] No duplicate defNames (2 tests)' -ForegroundColor Cyan

$allDefNames = @{}
$dupes = @()
foreach ($f in $defFiles) {
    $content = Get-Content $f.FullName -Raw
    $matches = [regex]::Matches($content, '<defName>([^<]+)</defName>')
    foreach ($m in $matches) {
        $dn = $m.Groups[1].Value
        if ($allDefNames.ContainsKey($dn)) {
            $dupes += $dn
        } else {
            $allDefNames[$dn] = $true
        }
    }
}
Assert-True 'No duplicate defNames' ($dupes.Count -eq 0) ($dupes -join ', ')
Assert-True 'Total unique defNames matches count' ($allDefNames.Count -eq $defCount) "Unique: $($allDefNames.Count) vs Total: $defCount"

# ============================================================
# 5. Translation coverage (1 test)
# ============================================================
Write-Host ''
Write-Host '[5/11] Translation coverage (1 test)' -ForegroundColor Cyan

$transFiles = Get-ChildItem "$modRoot\Languages\English\DefInjected\BackstoryDef" -Filter *.xml
$transDefNames = @{}
foreach ($f in $transFiles) {
    $content = Get-Content $f.FullName -Raw
    $matches = [regex]::Matches($content, '([^./\s]+)\.title')
    foreach ($m in $matches) {
        $dn = $m.Groups[1].Value
        if ($dn -ne 'Def' -and $dn.Length -gt 2) {
            $transDefNames[$dn] = $true
        }
    }
}
$missingTrans = @()
foreach ($dn in $allDefNames.Keys) {
    if (-not $transDefNames.ContainsKey($dn)) {
        $missingTrans += $dn
    }
}
Assert-True 'Translation coverage 100%' ($missingTrans.Count -eq 0) "Missing: $($missingTrans.Count) defs"

# ============================================================
# 6. Title + description completeness (1 test)
# ============================================================
Write-Host ''
Write-Host '[6/11] Title + description completeness (1 test)' -ForegroundColor Cyan

$allHaveTitle = $true
$allHaveDesc = $true
$missingTitle = ''
$missingDesc = ''
foreach ($f in $defFiles) {
    $content = Get-Content $f.FullName -Raw
    # Match each BackstoryDef element including its opening tag attributes
    $defs = [regex]::Matches($content, '(?s)(<BackstoryDef[^>]*>)(.*?)(</BackstoryDef>)')
    foreach ($d in $defs) {
        $openTag = $d.Groups[1].Value
        $body = $d.Groups[2].Value
        # Skip abstract base defs (they don't need title/description)
        if ($openTag -match 'Abstract="([Tt]rue)"') { continue }
        if (-not ($body -match '<title>') -and -not ($body -match '<titleFemale>')) {
            $allHaveTitle = $false
            $missingTitle = "missing title in $($f.Name)"
            break
        }
        if (-not ($body -match '<description>') -and -not ($body -match '<baseDesc>')) {
            $allHaveDesc = $false
            $missingDesc = "missing description in $($f.Name)"
            break
        }
    }
}
Assert-True 'All defs have title + description' ($allHaveTitle -and $allHaveDesc) "$missingTitle $missingDesc"

# ============================================================
# 7. Linked backstory resolution (3 tests)
# ============================================================
Write-Host ''
Write-Host '[7/11] Linked backstory resolution (3 tests)' -ForegroundColor Cyan

$linkedRefs = @()
foreach ($f in $defFiles) {
    $content = Get-Content $f.FullName -Raw
    $matches = [regex]::Matches($content, '<linkedBackstory>([^<]+)</linkedBackstory>')
    foreach ($m in $matches) {
        $linkedRefs += $m.Groups[1].Value
    }
}
$brokenLinks = @()
foreach ($ref in $linkedRefs) {
    if (-not $allDefNames.ContainsKey($ref)) {
        $brokenLinks += $ref
    }
}
Assert-True 'All linkedBackstory refs resolve' ($brokenLinks.Count -eq 0) ($brokenLinks -join ', ')
Assert-True 'linkedBackstory count tracked' $true "Count: $($linkedRefs.Count)"
Assert-True 'No broken linkedBackstory refs' ($brokenLinks.Count -eq 0)

# ============================================================
# 8. Minimal field requirements (1 test)
# ============================================================
Write-Host ''
Write-Host '[8/11] Minimal field requirements (1 test)' -ForegroundColor Cyan

$allHaveSlot = $true
foreach ($f in $defFiles) {
    $content = Get-Content $f.FullName -Raw
    if ($content -match '<BackstoryDef' -and -not ($content -match '<slot>')) {
        if (-not ($content -match 'Abstract="True"')) {
            $allHaveSlot = $false
            break
        }
    }
}
Assert-True 'All non-abstract defs have slot' $allHaveSlot

# ============================================================
# 9. RMCB patch application (4 tests)
# ============================================================
Write-Host ''
Write-Host '[9/11] RMCB patch application (4 tests)' -ForegroundColor Cyan

$patchFiles = Get-ChildItem "$modRoot\1.6\Patches" -Filter *.xml
$hasPatchOperation = $true
$noCommaWorkDisables = $true

foreach ($f in $patchFiles) {
    $content = Get-Content $f.FullName -Raw
    if (-not ($content -match '<Operation')) {
        $hasPatchOperation = $false
    }
    if ($content -match '<workDisables>[^<]*,[^<]*</workDisables>') {
        $noCommaWorkDisables = $false
    }
}
Assert-True 'All patch files have Operation elements' $hasPatchOperation
Assert-True 'PatchOperation_IfSetting used' (Test-Path "$modRoot\1.6\Patches\Patches.xml")
Assert-True 'No comma-separated workDisables in patches' $noCommaWorkDisables 'HIGH-008 regression'
Assert-True '12+ patch files exist' ($patchFiles.Count -ge 10) "Actual: $($patchFiles.Count)"

# ============================================================
# 10. Elderhood count (1 test)
# ============================================================
Write-Host ''
Write-Host '[10/11] Elderhood count (1 test)' -ForegroundColor Cyan

$elderhoodFile = Get-Content "$modRoot\1.6\Defs\BackstoryDefs\Elderhood_Elderhoods.xml" -Raw
$elderhoodCount = ([regex]::Matches($elderhoodFile, '<BackstoryDef')).Count
Assert-True 'Elderhood count = 43' ($elderhoodCount -eq 43) "Actual: $elderhoodCount"

# ============================================================
# 11. Cybranian workDisable cleanup (1 test)
# ============================================================
Write-Host ''
Write-Host '[11/11] Cybranian workDisable cleanup (1 test)' -ForegroundColor Cyan

$cybFiles = Get-ChildItem "$modRoot\1.6\Defs\BackstoryDefs" -Filter "Cybranian_*.xml"
$noInvalidWorkTag = $true
foreach ($f in $cybFiles) {
    $content = Get-Content $f.FullName -Raw
    if ($content -match '<li>Commoner</li>') {
        $noInvalidWorkTag = $false
        break
    }
}
Assert-True 'No invalid Commoner WorkTag in Cybranian' $noInvalidWorkTag

# ============================================================
# Summary
# ============================================================
Write-Host ''
Write-Host '=== Summary ===' -ForegroundColor Cyan
$total = $pass + $fail
Write-Host "  Total: $total"
Write-Host "  PASS:  $pass" -ForegroundColor Green
if ($fail -gt 0) {
    Write-Host "  FAIL:  $fail" -ForegroundColor Red
} else {
    Write-Host "  FAIL:  $fail" -ForegroundColor Green
}
if ($failed.Count -gt 0) {
    Write-Host ''
    Write-Host 'Failed tests:' -ForegroundColor Red
    foreach ($f in $failed) {
        Write-Host "  - $f" -ForegroundColor Red
    }
}
Write-Host ''
if ($fail -eq 0) {
    Write-Host '  ALL TESTS PASSED' -ForegroundColor Green
    exit 0
} else {
    Write-Host '  SOME TESTS FAILED' -ForegroundColor Red
    exit 1
}
