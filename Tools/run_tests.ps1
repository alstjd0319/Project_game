# 패링 로그라이크 — 씬 생성 + PlayMode 테스트(캡처 포함) 일괄 실행
# 사용: powershell -ExecutionPolicy Bypass -File Tools\run_tests.ps1 [-Filter ParryRL.Tests.CaptureTests] [-SkipBuild]
# - Unity 에디터가 이 프로젝트를 열고 있으면(프로젝트 잠금) 자동으로 %TEMP%\ParryRL_verify 사본에서 실행하고,
#   캡처(Captures/*.png)는 원본 프로젝트로 복사한다.
# - 이 파일은 한글 경로 때문에 반드시 UTF-8 BOM으로 저장한다.
param(
    [string]$Filter = "",
    [switch]$SkipBuild
)

$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe"
$project = Split-Path -Parent $PSScriptRoot
$logDir = Join-Path $env:TEMP "ParryRL_logs"
New-Item -ItemType Directory -Force $logDir | Out-Null

# 에디터가 이 프로젝트를 열고 있으면 사본으로
$editorOpen = Get-Process Unity -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like "*ParryRoguelike*" }
$runPath = $project
if ($editorOpen) {
    $runPath = Join-Path $env:TEMP "ParryRL_verify"
    Write-Output "에디터가 열려 있음 → 검증용 사본에서 실행: $runPath"
    New-Item -ItemType Directory -Force $runPath | Out-Null
    foreach ($d in "Assets", "Packages", "ProjectSettings") {
        Remove-Item -Recurse -Force (Join-Path $runPath $d) -ErrorAction SilentlyContinue
        Copy-Item -Recurse -Force (Join-Path $project $d) (Join-Path $runPath $d)
    }
    Remove-Item -Recurse -Force (Join-Path $runPath "Captures") -ErrorAction SilentlyContinue
}

if (-not $SkipBuild) {
    $p = Start-Process -FilePath $unity -ArgumentList @('-batchmode', '-quit', '-projectPath', "`"$runPath`"",
        '-executeMethod', 'ParryRL.EditorTools.PrototypeSceneBuilder.Build', '-logFile', "`"$logDir\build.log`"") -PassThru -Wait
    Write-Output "build exit: $($p.ExitCode)"
    Select-String -Path "$logDir\build.log" -Pattern "error CS|warning CS" | ForEach-Object { $_.Line } | Sort-Object -Unique
}

$args2 = @('-batchmode', '-projectPath', "`"$runPath`"", '-runTests', '-testPlatform', 'PlayMode',
    '-testResults', "`"$logDir\results.xml`"", '-logFile', "`"$logDir\test.log`"")
if ($Filter) { $args2 += @('-testFilter', $Filter) }
Remove-Item "$logDir\results.xml" -ErrorAction SilentlyContinue
$p = Start-Process -FilePath $unity -ArgumentList $args2 -PassThru -Wait
Write-Output "test exit: $($p.ExitCode)"

if (Test-Path "$logDir\results.xml") {
    [xml]$x = Get-Content "$logDir\results.xml" -Encoding UTF8
    $r = $x.'test-run'
    Write-Output "결과: $($r.result)  전체 $($r.total) / 통과 $($r.passed) / 실패 $($r.failed)"
    $x.SelectNodes("//test-case") | Where-Object { $_.result -ne 'Passed' } | ForEach-Object {
        Write-Output "실패: $($_.name) — $($_.failure.message.'#cdata-section')"
    }
} else {
    Write-Output "결과 파일 없음 — 컴파일 에러 확인:"
    Select-String -Path "$logDir\test.log" -Pattern "error CS" | ForEach-Object { $_.Line }
}
Select-String -Path "$logDir\test.log" -Pattern "warning CS" | ForEach-Object { $_.Line } | Sort-Object -Unique

if ($editorOpen -and (Test-Path (Join-Path $runPath "Captures"))) {
    New-Item -ItemType Directory -Force (Join-Path $project "Captures") | Out-Null
    Copy-Item -Force (Join-Path $runPath "Captures\*.png") (Join-Path $project "Captures")
    Write-Output "캡처를 원본 Captures/ 로 복사함"
}
