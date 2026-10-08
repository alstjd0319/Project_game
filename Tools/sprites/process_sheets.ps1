# ChatGPT로 뽑은 캐릭터 동작 시트(한 줄, 흰 배경)를 게임용 도트 프레임으로 가공한다.
#   1. 프레임 자르기 (빈 열 기준, 붙은 프레임은 가장 빈 열에서 나눔)
#   2. 정렬: 가로 = 삿갓 중심, 세로 = 시트 전체의 땅 높이 (공중 프레임은 떠 있는 그대로)
#   3. 크기: 모든 동작을 삿갓 폭으로 맞춤 (자세가 달라도 삿갓 크기는 같으므로)
#   4. 축소 + 색 줄이기 (회색 5단계 + 빨강)
# 결과: Assets/Art/Characters/<캐릭터>/<캐릭터>_<동작>_<번호>.png  (임포트 설정은 PrototypeSceneBuilder)
#       Captures/art_test/<캐릭터>_anim_preview.html  (움직이는 미리보기)
#
# 사용법:  powershell -ExecutionPolicy Bypass -File Tools\sprites\process_sheets.ps1
# 원본 시트는 ArtSource/<캐릭터>/<동작>.webp|png. 시트를 바꾸면 이 스크립트를 다시 돌리고 씬을 다시 생성한다.
# 동작 여러 줄이 한 장에 든 아틀라스(atlas = 파일 이름)는 먼저 줄마다 나눈 뒤 sheets 순서(위→아래)대로 같은 과정을 거친다.
#   -Only melee  → 그 캐릭터만 다시 가공 / -Only fx → 공격 이펙트만
#
# 공격 이펙트(ArtSource/fx, 검은 배경에 밝은 먹물 한 줄)는 판정 박스 크기(45px/유닛)로 늘려 맞춘다
# → Assets/Art/Fx/<이름>_<번호>.png. 게임에서 박스 크기로 그려지므로 보이는 그림 = 판정 범위.

param([string]$Only = '')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, System.Drawing
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
Add-Type -ReferencedAssemblies System.Drawing -Path (Join-Path $PSScriptRoot 'SheetTool.cs')

# 프레임 칸 규격 — PrototypeSceneBuilder의 피벗 계산과 같아야 한다. 캐릭터마다 cell로 바꿀 수 있다.
$CW = 160; $CH = 88; $AnchorX = 92; $FootY = 84
$BrimToFeet = 56   # 첫 시트(대기)의 삿갓~발끝 높이 (px). 나머지 시트는 삿갓 폭으로 여기에 맞춘다.

$characters = @(
  @{
    name = 'warrior'
    sheets = @(
      # 첫 줄 = 크기 기준 시트. game = $false 는 미리보기에만 넣는다.
      # frames = 시트의 프레임 수 (떨어진 칼·삿갓을 합치고, 붙은 프레임을 나누는 기준)
      # x = 'hat'(삿갓 중심) | 'mass'(몸 무게중심 — 삿갓이 벗겨지는 사망)
      # height = 첫 프레임 전체 키(px)로 크기 맞춤 (삿갓이 기울거나 벗겨져 삿갓 폭을 못 쓸 때)
      # y = 'ground'(시트 전체 땅 높이 — 달리기의 뜬 프레임 유지) | 'feet'(프레임마다 발끝 — 점프 높이는 물리가 정함)
      @{ name = 'idle';    label = '대기';            fps = 6;  frames = 4; x = 'hat';  y = 'ground'; game = $true },
      @{ name = 'run';     label = '이동 (달리기)';    fps = 12; frames = 6; x = 'hat';  y = 'ground'; game = $true },
      @{ name = 'parry';   label = '패링';            fps = 12; frames = 4; x = 'hat';  y = 'ground'; game = $true },
      @{ name = 'dash';    label = '대시 (0.15초)';   fps = 20; frames = 3; x = 'hat';  y = 'ground'; game = $true },
      @{ name = 'counter'; label = '반격 베기';        fps = 14; frames = 4; x = 'hat';  y = 'ground'; game = $true },
      @{ name = 'attack';  label = '공격 스킬';        fps = 14; frames = 5; x = 'hat';  y = 'ground'; game = $true },
      @{ name = 'jump';    label = '점프 (도약·상승·정점·낙하·착지)'; fps = 6; frames = 5; x = 'hat'; y = 'feet'; game = $true },
      @{ name = 'hit';     label = '피격';            fps = 12; frames = 3; x = 'hat';  y = 'ground'; game = $true },
      @{ name = 'death';   label = '사망';            fps = 6;  frames = 5; x = 'mass'; y = 'ground'; game = $true; height = 58 },
      @{ name = 'enter';   label = '교대 등장';        fps = 12; frames = 3; x = 'hat';  y = 'feet';   game = $true },
      @{ name = 'walk';    label = '걷기 (보관용)';    fps = 10; frames = 6; x = 'hat';  y = 'ground'; game = $false }
    )
  },
  @{
    # 근접 몬스터 (타락한 낭인). 판정 네모 1×1.6 유닛 = 45×72px에 몸이 들어가게,
    # 칼을 머리 위로 치켜든 강공격 예비 모션이 잘리지 않게 칸을 키웠다.
    name = 'melee'; atlas = 'atlas'; out = 'Assets\Art\Enemies\Melee'
    cell = @{ w = 176; h = 120; anchor = 80; foot = 116 }; brimToFeet = 57
    sheets = @(
      @{ name = 'idle';        label = '대기';                 fps = 6;  frames = 4; x = 'hat'; y = 'ground'; game = $true },
      @{ name = 'walk';        label = '걷기';                 fps = 8;  frames = 6; x = 'hat'; y = 'ground'; game = $true },
      @{ name = 'windup';      label = '일반 예비 모션 (0.4초에 맞춤)'; fps = 7.5; frames = 3; x = 'hat'; y = 'ground'; game = $true },
      @{ name = 'slash';       label = '일반 베기';            fps = 12; frames = 3; x = 'hat'; y = 'ground'; game = $true },
      @{ name = 'heavywindup'; label = '강공격 예비 모션 (0.9초에 맞춤)'; fps = 4.5; frames = 4; x = 'hat'; y = 'ground'; game = $true },
      @{ name = 'heavyslash';  label = '강공격 베기';          fps = 12; frames = 4; x = 'hat'; y = 'ground'; game = $true },
      @{ name = 'stun';        label = '기절';                 fps = 6;  frames = 3; x = 'hat'; y = 'ground'; game = $true },
      @{ name = 'hit';         label = '피격';                 fps = 8;  frames = 2; x = 'hat'; y = 'ground'; game = $true },
      @{ name = 'death';       label = '사망';                 fps = 8;  frames = 5; x = 'mass'; y = 'ground'; game = $true }
    )
  }
)

function Convert-ToPng([string]$src, [string]$dst) {
  if ($src.EndsWith('.png')) { Copy-Item $src $dst -Force; return }
  $dec = [System.Windows.Media.Imaging.BitmapDecoder]::Create([uri]$src, 'None', 'OnLoad')
  $enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
  $enc.Frames.Add($dec.Frames[0])
  $fs = [IO.File]::Create($dst); $enc.Save($fs); $fs.Close()
}

$tmp = Join-Path $env:TEMP 'ParryRL_sprites'
New-Item -ItemType Directory -Force $tmp | Out-Null
$previewDir = Join-Path $root 'Captures\art_test'
New-Item -ItemType Directory -Force $previewDir | Out-Null

foreach ($chr in $characters) {
  if ($Only -and $chr.name -ne $Only) { continue }
  $outDir = if ($chr.out) { Join-Path $root $chr.out } else { Join-Path $root "Assets\Art\Characters\$((Get-Culture).TextInfo.ToTitleCase($chr.name))" }
  New-Item -ItemType Directory -Force $outDir | Out-Null
  $CW = 160; $CH = 88; $AnchorX = 92; $FootY = 84; $b2f = $BrimToFeet
  if ($chr.cell) { $CW = $chr.cell.w; $CH = $chr.cell.h; $AnchorX = $chr.cell.anchor; $FootY = $chr.cell.foot }
  if ($chr.brimToFeet) { $b2f = $chr.brimToFeet }
  $hat = 0.0; $firstScale = 0.0
  $anim2 = ''; $anim6 = ''; $strips = ''

  # 아틀라스: 줄마다 한 장씩 나눠 둔다 (sheets 순서 = 위에서부터 줄 순서)
  $rowPngs = $null
  if ($chr.atlas) {
    $src = Get-ChildItem (Join-Path $root "ArtSource\$($chr.name)") -Filter "$($chr.atlas).*" | Select-Object -First 1
    if ($null -eq $src) { throw "원본 아틀라스 없음: ArtSource\$($chr.name)\$($chr.atlas).webp" }
    $atlasPng = Join-Path $tmp "$($chr.name)_atlas.png"
    Convert-ToPng $src.FullName $atlasPng
    $rowPngs = [string[]]($chr.sheets | ForEach-Object { Join-Path $tmp "$($chr.name)_$($_.name).png" })
    Write-Output "$($chr.name)/atlas: $([SheetTool]::SplitRows($atlasPng, $rowPngs, 238))"
  }

  for ($si = 0; $si -lt $chr.sheets.Count; $si++) {
    $sh = $chr.sheets[$si]
    if ($rowPngs) { $png = $rowPngs[$si] }
    else {
      $src = Get-ChildItem (Join-Path $root "ArtSource\$($chr.name)") -Filter "$($sh.name).*" | Select-Object -First 1
      if ($null -eq $src) { throw "원본 시트 없음: ArtSource\$($chr.name)\$($sh.name).webp" }
      $png = Join-Path $tmp "$($chr.name)_$($sh.name).png"
      Convert-ToPng $src.FullName $png
    }

    $stripPath = Join-Path $previewDir "$($chr.name)_$($sh.name)_strip.png"
    $framePrefix = if ($sh.game) { Join-Path $outDir "$($chr.name)_$($sh.name)" } else { $null }
    # 아틀라스는 한 장에 같은 크기로 그려졌으므로 첫 줄(대기)의 배율을 그대로 쓴다 (기절 별·쓰러진 삿갓에 휘둘리지 않게)
    $fixed = if ($rowPngs) { $firstScale } else { 0.0 }
    $info = [SheetTool]::Run($png, $stripPath, $framePrefix, $b2f, $hat, $CW, $CH, $AnchorX, $FootY, 238,
      $sh.frames, ($sh.x -eq 'mass'), ($sh.y -eq 'feet'), [int]$sh.height, $fixed)
    Write-Output "$($chr.name)/$($sh.name): $info"
    if ($hat -eq 0 -and $info -match '\(=([\d.]+)px\)') { $hat = [double]$Matches[1] }
    if ($firstScale -eq 0 -and $info -match 'scale=([\d.]+)') { $firstScale = [double]$Matches[1] }

    $img = [Drawing.Image]::FromFile($stripPath); $n = [int]($img.Width / $CW); $img.Dispose()

    # 프레임 수가 줄었으면 남은 옛 프레임(+meta) 정리
    if ($sh.game) {
      Get-ChildItem $outDir -Filter "$($chr.name)_$($sh.name)_*.png" | Where-Object {
        [int]($_.BaseName -replace '.*_', '') -ge $n } | ForEach-Object {
          Remove-Item $_.FullName; if (Test-Path "$($_.FullName).meta") { Remove-Item "$($_.FullName).meta" } }
    }

    # 미리보기 (CSS steps 애니메이션)
    $b64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($stripPath))
    $dur = [Math]::Round($n / $sh.fps, 3)
    foreach ($z in 2, 6) {
      $w = $CW * $z; $h = $CH * $z; $end = - $CW * $n * $z
      $div = "<div class='cell'><b>$($sh.label)</b><span>$n" + "f / $($sh.fps)fps</span><div class='anim' style=""width:${w}px;height:${h}px;background-image:url(data:image/png;base64,$b64);background-size:$($CW*$n*$z)px ${h}px;--end:${end}px;animation:play ${dur}s steps($n) infinite""></div><div class='ground' style='width:${w}px'></div></div>"
      if ($z -eq 2) { $anim2 += $div } else { $anim6 += $div }
    }
    $strips += "<div class='cell' style='margin-bottom:16px'><b>$($sh.label)</b><img class='strip' width='$($CW*$n*4)' height='$($CH*4)' src='data:image/png;base64,$b64'></div>"
  }

  $tpl = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'preview_template.html'), [Text.Encoding]::UTF8)
  $html = $tpl.Replace('%TITLE%', $chr.name).Replace('%ANIM2%', $anim2).Replace('%ANIM6%', $anim6).Replace('%STRIPS%', $strips)
  [IO.File]::WriteAllText((Join-Path $previewDir "$($chr.name)_anim_preview.html"), $html, (New-Object Text.UTF8Encoding $false))
}

# ── 공격 이펙트 ──
# w·h = 판정 박스 크기 × 45 (px). 판정 수치를 바꿔도 게임에서는 박스 크기로 늘려 그리므로 깨지진 않는다.
$effects = @(
  @{ src = 'melee_slash';      out = 'enemy_slash';      frames = 4; w = 108; h = 45; label = '몬스터 일반 베기 (2.4×1.0)' },
  @{ src = 'melee_heavyslash'; out = 'enemy_heavyslash'; frames = 4; w = 108; h = 63; label = '몬스터 강공격 베기 (2.4×1.4)' },
  @{ src = 'warrior_slash';    out = 'warrior_counter';  frames = 4; w = 81;  h = 72; label = '전사 반격 (1.8×1.6)' },
  @{ src = 'warrior_slash';    out = 'warrior_attack';   frames = 4; w = 108; h = 72; label = '전사 공격 스킬 (2.4×1.6)' }
)
if (-not $Only -or $Only -eq 'fx') {
  $fxDir = Join-Path $root 'Assets\Art\Fx'
  New-Item -ItemType Directory -Force $fxDir | Out-Null
  foreach ($fx in $effects) {
    $src = Get-ChildItem (Join-Path $root 'ArtSource\fx') -Filter "$($fx.src).*" | Select-Object -First 1
    if ($null -eq $src) { throw "원본 이펙트 없음: ArtSource\fx\$($fx.src).webp" }
    $png = Join-Path $tmp "fx_$($fx.src).png"
    Convert-ToPng $src.FullName $png
    $info = [SheetTool]::RunFx($png, (Join-Path $previewDir "fx_$($fx.out)_strip.png"), (Join-Path $fxDir $fx.out), $fx.frames, $fx.w, $fx.h)
    Write-Output "fx/$($fx.out): $info"
  }
}