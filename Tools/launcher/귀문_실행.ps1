# 귀문 런처 (Windows) — 귀문_실행.bat 을 더블클릭하면 이 스크립트가 돈다.
#   - 빌드(Builds\Windows\귀문.exe)가 없으면 Unity로 먼저 빌드한 뒤 실행한다 (처음 한 번만 몇 분).
#   - 다시 빌드:  powershell -ExecutionPolicy Bypass -File 귀문_실행.ps1 -Rebuild
#   - 바탕화면에 바로가기 만들기:  바탕화면에_설치.bat  (또는 -InstallDesktop) — 바탕화면의 '귀문'을 더블클릭하면 실행된다.
#   - Unity가 이 프로젝트를 열고 있으면 빌드할 수 없다 → 에디터를 닫거나, 메뉴 Tools > ParryRL > 게임 빌드 (Windows) 를 쓴다.
#   - Windows 빌드에는 Unity Hub에서 "Windows Build Support (Mono)" 모듈이 필요하다.
param([switch]$Rebuild, [switch]$InstallDesktop)

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$exe = Join-Path $root 'Builds\Windows\귀문.exe'

if ($Rebuild -and (Test-Path (Split-Path $exe))) { Remove-Item -Recurse -Force (Split-Path $exe) }

if (-not (Test-Path $exe)) {
    $version = (Select-String -Path (Join-Path $root 'ProjectSettings\ProjectVersion.txt') -Pattern '^m_EditorVersion: (.+)$').Matches[0].Groups[1].Value.Trim()
    $unity = $env:UNITY_PATH
    if (-not $unity) { $unity = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe" }
    if (-not (Test-Path $unity)) {
        Write-Host "[귀문] Unity $version 을(를) 찾지 못했습니다: $unity"
        Write-Host "       Unity Hub로 설치하거나 UNITY_PATH 환경변수에 Unity.exe 경로를 넣어 주세요."
        Read-Host '엔터를 누르면 닫힙니다'
        exit 1
    }
    New-Item -ItemType Directory -Force (Join-Path $root 'Logs') | Out-Null
    $log = Join-Path $root 'Logs\build_windows.log'
    Write-Host "[귀문] 빌드 중... (로그: Logs\build_windows.log)"
    $p = Start-Process -FilePath $unity -ArgumentList @('-batchmode', '-nographics', '-quit', '-projectPath', "`"$root`"",
        '-executeMethod', 'ParryRL.EditorTools.GameBuilder.BuildWindows', '-logFile', "`"$log`"") -PassThru -Wait
    if ($p.ExitCode -ne 0 -or -not (Test-Path $exe)) {
        Write-Host '[귀문] 빌드에 실패했습니다. Logs\build_windows.log 를 확인하세요. (Unity가 이 프로젝트를 열고 있으면 먼저 닫아 주세요.)'
        Read-Host '엔터를 누르면 닫힙니다'
        exit 1
    }
}

if ($InstallDesktop) {
    # 바탕화면 바로가기(.lnk) — 실행 파일을 가리킨다 (빌드 폴더를 지우면 바로가기도 끊긴다)
    $desktop = [Environment]::GetFolderPath('Desktop')
    $shell = New-Object -ComObject WScript.Shell
    $lnk = $shell.CreateShortcut((Join-Path $desktop '귀문.lnk'))
    $lnk.TargetPath = $exe
    $lnk.WorkingDirectory = Split-Path $exe
    $lnk.Description = '귀문'
    $lnk.Save()
    Write-Host "[귀문] 바탕화면에 바로가기를 만들었습니다: $desktop\귀문.lnk"
    exit 0
}

Write-Host '[귀문] 실행합니다.'
Start-Process $exe
