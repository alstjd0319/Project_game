# AI로 뽑은 캐릭터·이펙트 시트(한 줄, 단색 배경)를 게임용 도트 프레임으로 가공한다.
#   1. 배경 지우기 (흰색 / 단색 키(마젠타·초록) / 검정 = 빛나는 이펙트)
#   2. 프레임 자르기 (빈 열 기준 → 시트마다 정한 프레임 수에 맞게 합치기·나누기)
#   3. 정렬 (가로: 갓 중심·몸 무게중심·발 무게중심·칸 중심 / 세로: 땅 높이·프레임별 발끝·무게중심)
#   4. 크기 맞춤 (갓 폭 · 키 · 고정 배율)
#   5. 축소 (컬러: 칸마다 가장 많은 색 / 옛 수묵 전사: 회색 5단계)
# 결과: 미리보기 Captures/art_test/<이름>_anim_preview.html + <이름>_<시트>_strip.png
#       game = $true 인 캐릭터만 Assets/Art/Characters/<폴더>/<접두어>_<시트>_<번호>.png (임포트 설정은 PrototypeSceneBuilder)
#
# 사용법:  powershell -ExecutionPolicy Bypass -File Tools\sprites\process_sheets.ps1 [-Only hero,changgwi]
# 원본: ArtSource/<src>/<시트>.png|webp  (Codex 요청서 image_request.md 참고)

param([string[]]$Only)
# powershell -File 로 넘기면 'a,b'가 문자열 하나로 들어오므로 쉼표로 나눈다
if ($Only) { $Only = @($Only | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ }) }

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, System.Drawing
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
Add-Type -ReferencedAssemblies System.Drawing -Path (Join-Path $PSScriptRoot 'SheetTool.cs')

# ── 캐릭터별 설정 ──
# defaults = SheetOptions 기본값 덮어쓰기 (SheetTool.cs 참고), sheets[].o = 그 시트만 덮어쓰기
# scale 'hat' 캐릭터는 첫 시트가 크기 기준(TargetBrimToFeet) → 나머지 시트는 같은 갓 폭으로 맞춘다.
# view = 1080p 화면에서 늘려 보이는 배율. 새 콘셉트는 1920×1080 그대로(1배, 90px/유닛, 주인공 키 ≈128px),
#        옛 전사는 960×540을 2배로 늘려 보이던 규격(45px/유닛).
$characters = @(
  @{
    name = 'hero'; src = 'hero'; game = $false; folder = 'Hero'; prefix = 'hero'; view = 1
    defaults = @{ Bg = 'key'; Color = 'color'; CellW = 384; CellH = 224; AnchorX = 192; FootY = 208; AlignX = 'hat'; AlignY = 'ground'; ScaleMode = 'hat'; TargetBrimToFeet = 120 }
    sheets = @(
      @{ name = 'staff_idle';          label = '봉 · 대기';         fps = 6;  o = @{ Frames = 4 } },
      @{ name = 'staff_run';           label = '봉 · 달리기';       fps = 12; o = @{ Frames = 6 } },
      @{ name = 'staff_parry';         label = '봉 · 패링';         fps = 12; o = @{ Frames = 4 } },
      @{ name = 'staff_counter';       label = '봉 · 반격';         fps = 14; o = @{ Frames = 4 } },
      @{ name = 'staff_skill_sweep';   label = '봉 · 휩쓸기';       fps = 14; o = @{ Frames = 5 } },
      @{ name = 'staff_skill_slam';    label = '봉 · 내려찍기';     fps = 12; o = @{ Frames = 5 } },
      @{ name = 'swap';                label = '무기 교체';         fps = 12; o = @{ Frames = 4 } },
      @{ name = 'talisman_idle';       label = '부적 · 대기';       fps = 6;  o = @{ Frames = 4 } },
      @{ name = 'talisman_run';        label = '부적 · 달리기';     fps = 12; o = @{ Frames = 6 } },
      @{ name = 'talisman_parry';      label = '부적 · 패링';       fps = 12; o = @{ Frames = 4 } },
      @{ name = 'talisman_counter';    label = '부적 · 반격';       fps = 14; o = @{ Frames = 4 } },
      @{ name = 'talisman_skill_fan';  label = '부적 · 부채꼴';     fps = 14; o = @{ Frames = 5 } },
      @{ name = 'talisman_skill_seal'; label = '부적 · 봉인';       fps = 10; o = @{ Frames = 5 } },
      @{ name = 'jump';                label = '점프';             fps = 6;  o = @{ Frames = 5; AlignY = 'feet' } },
      @{ name = 'dash';                label = '대시 (0.15초)';    fps = 20; o = @{ Frames = 3 } },
      @{ name = 'hit';                 label = '피격';             fps = 12; o = @{ Frames = 3 } },
      @{ name = 'death';               label = '사망';             fps = 6;  o = @{ Frames = 5; AlignX = 'mass'; ScaleMode = 'frame'; RefFrame = 0; TargetH = 124 } },
      @{ name = 'wake';                label = '기상';             fps = 6;  o = @{ Frames = 4; AlignX = 'mass'; ScaleMode = 'frame'; RefFrame = 3; TargetH = 128 } }
    )
  },
  @{
    name = 'changgwi'; src = 'yokai/changgwi'; game = $false; folder = 'Changgwi'; prefix = 'changgwi'; view = 1
    defaults = @{ Bg = 'key'; Color = 'color'; CellW = 384; CellH = 224; AnchorX = 192; FootY = 208; AlignX = 'feet'; AlignY = 'ground'; ScaleMode = 'median'; TargetH = 140 }
    sheets = @(
      @{ name = 'idle';          label = '창귀 · 대기';        fps = 6;  o = @{ Frames = 4 } },
      @{ name = 'move';          label = '창귀 · 이동';        fps = 8;  o = @{ Frames = 6 } },
      @{ name = 'attack_normal'; label = '창귀 · 일반 공격';   fps = 10; o = @{ Frames = 6 } },
      @{ name = 'heavy_windup';  label = '창귀 · 강공격 준비'; fps = 6;  o = @{ Frames = 4; ScaleMode = 'frame'; RefFrame = 0 } },
      @{ name = 'heavy_attack';  label = '창귀 · 강공격';      fps = 12; o = @{ Frames = 4; ScaleMode = 'frame'; RefFrame = 3 } },
      @{ name = 'hurt';          label = '창귀 · 피격/기절';   fps = 8;  o = @{ Frames = 5; ScaleMode = 'frame'; RefFrame = 3 } },
      @{ name = 'death';         label = '창귀 · 사망';        fps = 8;  o = @{ Frames = 5; AlignX = 'mass'; ScaleMode = 'frame'; RefFrame = 0 } }
    )
  },
  @{
    name = 'dokkaebibul'; src = 'yokai/dokkaebibul'; game = $false; folder = 'Dokkaebibul'; prefix = 'dokkaebibul'; view = 1
    # 떠 있는 불: 무게중심을 판정 네모 가운데(바닥에서 58px 위)에 둔다
    defaults = @{ Bg = 'key'; Color = 'color'; CellW = 384; CellH = 224; AnchorX = 192; FootY = 208; AlignX = 'mass'; AlignY = 'mass'; MassY = 150; ScaleMode = 'area'; TargetArea = 6000 }
    sheets = @(
      @{ name = 'idle';          label = '도깨비불 · 대기';        fps = 8;  o = @{ Frames = 4 } },
      @{ name = 'move';          label = '도깨비불 · 이동';        fps = 10; o = @{ Frames = 6 } },
      @{ name = 'attack_normal'; label = '도깨비불 · 일반 공격';   fps = 10; o = @{ Frames = 6 } },
      @{ name = 'heavy_windup';  label = '도깨비불 · 강공격 준비'; fps = 6;  o = @{ Frames = 4; ScaleMode = 'areaFrame'; RefFrame = 0 } },
      @{ name = 'heavy_attack';  label = '도깨비불 · 강공격';      fps = 12; o = @{ Frames = 4 } },
      @{ name = 'hurt';          label = '도깨비불 · 피격/기절';   fps = 8;  o = @{ Frames = 5 } },
      @{ name = 'death';         label = '도깨비불 · 사망';        fps = 8;  o = @{ Frames = 5 } },
      @{ name = 'projectile';    label = '도깨비불 · 불덩이 (청록 4 + 빨강 4)'; fps = 12; o = @{ Frames = 8; CellW = 128; CellH = 80; AnchorX = 64; MassY = 40; ScaleMode = 'areaFrame'; RefFrame = 0; TargetArea = 480 } }
    )
  },
  @{
    # 덩치 큰 근접: 키 192px. 강공격 준비는 팔을 머리 위로 들어 키가 커지므로 첫 프레임(평소 자세) 기준
    name = 'dueoksini'; src = 'yokai/dueoksini'; game = $false; folder = 'Dueoksini'; prefix = 'dueoksini'; view = 1
    defaults = @{ Bg = 'key'; Color = 'color'; CellW = 448; CellH = 384; AnchorX = 224; FootY = 368; AlignX = 'feet'; AlignY = 'ground'; ScaleMode = 'median'; TargetH = 192 }
    sheets = @(
      @{ name = 'idle';          label = '두억시니 · 대기';        fps = 6;  o = @{ Frames = 4 } },
      @{ name = 'move';          label = '두억시니 · 이동';        fps = 8;  o = @{ Frames = 6 } },
      @{ name = 'attack_normal'; label = '두억시니 · 일반 공격';   fps = 10; o = @{ Frames = 6 } },
      @{ name = 'heavy_windup';  label = '두억시니 · 강공격 준비'; fps = 6;  o = @{ Frames = 4; ScaleMode = 'frame'; RefFrame = 0 } },
      @{ name = 'heavy_attack';  label = '두억시니 · 강공격';      fps = 12; o = @{ Frames = 4; ScaleMode = 'frame'; RefFrame = 3 } },
      @{ name = 'hurt';          label = '두억시니 · 피격/기절';   fps = 8;  o = @{ Frames = 5; ScaleMode = 'frame'; RefFrame = 3 } },
      @{ name = 'death';         label = '두억시니 · 사망';        fps = 8;  o = @{ Frames = 5; AlignX = 'mass'; ScaleMode = 'frame'; RefFrame = 0 } }
    )
  },
  @{
    # 네발 짐승: 키 112 · 폭 200px. 가로로 길어 칸도 넓게, 앞뒤 발 가운데(무게중심)로 정렬
    name = 'jangsanbeom'; src = 'yokai/jangsanbeom'; game = $false; folder = 'Jangsanbeom'; prefix = 'jangsanbeom'; view = 1
    defaults = @{ Bg = 'key'; Color = 'color'; CellW = 512; CellH = 256; AnchorX = 256; FootY = 240; AlignX = 'mass'; AlignY = 'ground'; ScaleMode = 'median'; TargetH = 112 }
    sheets = @(
      @{ name = 'idle';          label = '장산범 · 대기';        fps = 6;  o = @{ Frames = 4 } },
      @{ name = 'move';          label = '장산범 · 이동';        fps = 12; o = @{ Frames = 6 } },
      @{ name = 'attack_normal'; label = '장산범 · 일반 공격';   fps = 10; o = @{ Frames = 6 } },
      @{ name = 'heavy_windup';  label = '장산범 · 강공격 준비'; fps = 6;  o = @{ Frames = 4; ScaleMode = 'frame'; RefFrame = 0 } },
      @{ name = 'heavy_attack';  label = '장산범 · 덮치기';      fps = 12; o = @{ Frames = 4; ScaleMode = 'frame'; RefFrame = 0 } },
      @{ name = 'hurt';          label = '장산범 · 피격/기절';   fps = 8;  o = @{ Frames = 5; ScaleMode = 'frame'; RefFrame = 2 } },
      @{ name = 'death';         label = '장산범 · 사망';        fps = 8;  o = @{ Frames = 5; ScaleMode = 'frame'; RefFrame = 0 } }
    )
  },
  @{
    # 그림자 덩어리: 키 128px(게임에서 키움). 보라색이라 초록 배경
    name = 'eodukssini'; src = 'yokai/eodukssini'; game = $false; folder = 'Eodukssini'; prefix = 'eodukssini'; view = 1
    defaults = @{ Bg = 'key'; KeyR = 0; KeyG = 255; KeyB = 0; Color = 'color'; CellW = 512; CellH = 256; AnchorX = 256; FootY = 240; AlignX = 'mass'; AlignY = 'ground'; ScaleMode = 'median'; TargetH = 128 }
    sheets = @(
      @{ name = 'idle';          label = '어둑시니 · 대기';        fps = 6;  o = @{ Frames = 4 } },
      @{ name = 'move';          label = '어둑시니 · 이동';        fps = 8;  o = @{ Frames = 6 } },
      @{ name = 'attack_normal'; label = '어둑시니 · 일반 공격';   fps = 10; o = @{ Frames = 6 } },
      @{ name = 'heavy_windup';  label = '어둑시니 · 강공격 준비'; fps = 6;  o = @{ Frames = 4; ScaleMode = 'frame'; RefFrame = 0 } },
      @{ name = 'heavy_attack';  label = '어둑시니 · 강공격';      fps = 12; o = @{ Frames = 4; ScaleMode = 'frame'; RefFrame = 3 } },
      @{ name = 'hurt';          label = '어둑시니 · 피격/기절';   fps = 8;  o = @{ Frames = 5; ScaleMode = 'frame'; RefFrame = 1 } },
      @{ name = 'death';         label = '어둑시니 · 사망';        fps = 8;  o = @{ Frames = 5; ScaleMode = 'frame'; RefFrame = 0 } }
    )
  },
  @{
    # 사당 NPC: 키 120px. 축복은 방울을 머리 위로 들어 키가 커지므로 첫 프레임 기준
    name = 'mentor'; src = 'mentor'; game = $false; folder = 'Mentor'; prefix = 'mentor'; view = 1
    defaults = @{ Bg = 'key'; Color = 'color'; CellW = 384; CellH = 224; AnchorX = 192; FootY = 208; AlignX = 'feet'; AlignY = 'ground'; ScaleMode = 'median'; TargetH = 120 }
    sheets = @(
      @{ name = 'idle';  label = '만신 · 대기'; fps = 5;  o = @{ Frames = 4 } },
      @{ name = 'talk';  label = '만신 · 대화'; fps = 6;  o = @{ Frames = 4 } },
      @{ name = 'bless'; label = '만신 · 축복'; fps = 8;  o = @{ Frames = 6; ScaleMode = 'frame'; RefFrame = 0 } }
    )
  },
  @{
    # 보스: 1페이즈 키 320px, 2페이즈 480px(철을 먹고 커짐). 칸 하나로 통일, 몸 무게중심 정렬.
    # 피격 시트는 1·2 페이즈 프레임이 섞여 있어 1페이즈 크기 기준으로만 맞춘다(2페이즈 프레임은 원본 비율대로).
    name = 'bulgasari'; src = 'boss/bulgasari'; game = $false; folder = 'Bulgasari'; prefix = 'bulgasari'; view = 1
    defaults = @{ Bg = 'key'; Color = 'color'; CellW = 1024; CellH = 640; AnchorX = 512; FootY = 620; AlignX = 'mass'; AlignY = 'ground'; ScaleMode = 'median'; TargetH = 320 }
    sheets = @(
      @{ name = 'p1_idle';          label = '불가살이 1 · 대기';        fps = 5;  o = @{ Frames = 4 } },
      @{ name = 'p1_walk';          label = '불가살이 1 · 걷기';        fps = 6;  o = @{ Frames = 4 } },
      @{ name = 'p1_combo';         label = '불가살이 1 · 3연타';       fps = 10; o = @{ Frames = 6; ScaleMode = 'frame'; RefFrame = 0 } },
      @{ name = 'p1_heavy_windup';  label = '불가살이 1 · 강공격 준비'; fps = 5;  o = @{ Frames = 4; ScaleMode = 'frame'; RefFrame = 0 } },
      @{ name = 'p1_heavy_attack';  label = '불가살이 1 · 강공격';      fps = 10; o = @{ Frames = 4; ScaleMode = 'frame'; RefFrame = 3 } },
      @{ name = 'transform';        label = '불가살이 · 변신';          fps = 5;  o = @{ Frames = 6; ScaleMode = 'frame'; RefFrame = 0 } },
      @{ name = 'hurt';             label = '불가살이 · 피격 (1·1·2·2)'; fps = 8;  o = @{ Frames = 4; ScaleMode = 'frame'; RefFrame = 0 } },
      @{ name = 'p2_idle';          label = '불가살이 2 · 대기';        fps = 5;  o = @{ Frames = 4; TargetH = 480 } },
      @{ name = 'p2_walk';          label = '불가살이 2 · 걷기';        fps = 5;  o = @{ Frames = 4; TargetH = 480 } },
      @{ name = 'p2_attack_normal'; label = '불가살이 2 · 코 휘두르기'; fps = 10; o = @{ Frames = 6; ScaleMode = 'frame'; RefFrame = 0; TargetH = 480 } },
      @{ name = 'p2_heavy_windup';  label = '불가살이 2 · 강공격 준비'; fps = 5;  o = @{ Frames = 4; ScaleMode = 'frame'; RefFrame = 0; TargetH = 560 } },
      @{ name = 'p2_heavy_attack';  label = '불가살이 2 · 강공격';      fps = 10; o = @{ Frames = 4; ScaleMode = 'frame'; RefFrame = 3; TargetH = 480 } },
      @{ name = 'death';            label = '불가살이 · 사망';          fps = 6;  o = @{ Frames = 6; ScaleMode = 'frame'; RefFrame = 0; TargetH = 480 } }
    )
  },
  @{
    name = 'fx'; src = 'fx'; game = $false; folder = 'Fx'; prefix = 'fx'; view = 1
    # 검정 배경 = 빛나는 이펙트. 칸 중심 정렬(프레임마다 위치가 변하는 것도 그대로), 모든 이펙트 같은 배율.
    defaults = @{ Bg = 'black'; Color = 'color'; CellW = 256; CellH = 256; AnchorX = 128; MassY = 128; AlignX = 'cell'; AlignY = 'cell'; ScaleMode = 'fixed'; Fixed = 1.6; Coverage = 0.2 }
    sheets = @(
      @{ name = 'staff_slash';         label = '봉 궤적';       fps = 20; o = @{ Frames = 4 } },
      @{ name = 'staff_thrust';        label = '봉 찌르기';     fps = 20; o = @{ Frames = 3 } },
      @{ name = 'staff_slam';          label = '봉 내려찍기';   fps = 16; o = @{ Frames = 5 } },
      @{ name = 'parry_staff';         label = '패링 (봉)';     fps = 20; o = @{ Frames = 4 } },
      @{ name = 'parry_talisman';      label = '패링 (부적)';   fps = 16; o = @{ Frames = 4 } },
      @{ name = 'talisman_projectile'; label = '부적 날아가기'; fps = 16; o = @{ Frames = 4 } },
      @{ name = 'talisman_burst';      label = '부적 폭발';     fps = 16; o = @{ Frames = 5 } },
      @{ name = 'talisman_seal';       label = '봉인진';        fps = 12; o = @{ Frames = 6 } },
      @{ name = 'yogi_absorb';         label = '요기 흡수';     fps = 14; o = @{ Frames = 6 } },
      @{ name = 'hit_spark';           label = '타격';          fps = 20; o = @{ Frames = 3 } },
      @{ name = 'yokai_dissolve';      label = '요괴 소멸';     fps = 12; o = @{ Frames = 6 } },
      @{ name = 'heavy_warning';       label = '강공격 경고';   fps = 12; o = @{ Frames = 4 } },
      @{ name = 'dust_land';           label = '착지 먼지';     fps = 14; o = @{ Frames = 3 } },
      @{ name = 'gate_portal';         label = '귀문 소용돌이'; fps = 10; o = @{ Frames = 6 } }
    )
  },
  @{
    # 폐기된 옛 콘셉트(수묵 전사). 지금 게임에 들어가 있는 프레임을 다시 만들 수 있게 남겨 둠.
    name = 'warrior'; src = 'warrior'; game = $true; folder = 'Warrior'; prefix = 'warrior'; view = 2
    defaults = @{ Bg = 'white'; Color = 'grey'; CellW = 160; CellH = 88; AnchorX = 92; FootY = 84; AlignX = 'hat'; AlignY = 'ground'; ScaleMode = 'hat'; TargetBrimToFeet = 56; Coverage = 0.5; Despill = $false }
    sheets = @(
      @{ name = 'idle';    label = '대기';            fps = 6;  o = @{ Frames = 4 } },
      @{ name = 'run';     label = '이동 (달리기)';    fps = 12; o = @{ Frames = 6 } },
      @{ name = 'parry';   label = '패링';            fps = 12; o = @{ Frames = 4 } },
      @{ name = 'dash';    label = '대시 (0.15초)';   fps = 20; o = @{ Frames = 3 } },
      @{ name = 'counter'; label = '반격 베기';        fps = 14; o = @{ Frames = 4 } },
      @{ name = 'attack';  label = '공격 스킬';        fps = 14; o = @{ Frames = 5 } },
      @{ name = 'jump';    label = '점프';            fps = 6;  o = @{ Frames = 5; AlignY = 'feet' } },
      @{ name = 'hit';     label = '피격';            fps = 12; o = @{ Frames = 3 } },
      @{ name = 'death';   label = '사망';            fps = 6;  o = @{ Frames = 5; AlignX = 'mass'; ScaleMode = 'frame'; RefFrame = 0; TargetH = 58 } },
      @{ name = 'enter';   label = '교대 등장';        fps = 12; o = @{ Frames = 3; AlignY = 'feet' } },
      @{ name = 'walk';    label = '걷기 (보관용)';    fps = 10; o = @{ Frames = 6 }; preview = $true }
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

function New-Options($defaults, $over) {
  $o = New-Object SheetOptions
  foreach ($h in @($defaults, $over)) {
    if ($null -eq $h) { continue }
    foreach ($k in $h.Keys) { $o.$k = $h[$k] }
  }
  return $o
}

$tmp = Join-Path $env:TEMP 'ParryRL_sprites'
New-Item -ItemType Directory -Force $tmp | Out-Null
$previewDir = Join-Path $root 'Captures\art_test'
New-Item -ItemType Directory -Force $previewDir | Out-Null

foreach ($chr in $characters) {
  if ($Only -and ($Only -notcontains $chr.name)) { continue }
  $outDir = Join-Path $root "Assets\Art\Characters\$($chr.folder)"
  if ($chr.game) { New-Item -ItemType Directory -Force $outDir | Out-Null }
  $hat = 0.0
  $anim2 = ''; $anim6 = ''; $strips = ''; $meta = @()
  # 미리보기 배율: 게임 실제 크기 · 그 2배 · 낱장 (1배 캐릭터는 2배, 2배 캐릭터는 3배)
  $z1 = $chr.view; $z2 = $chr.view * 2; $z3 = $chr.view + 1

  foreach ($sh in $chr.sheets) {
    $srcFile = Get-ChildItem (Join-Path $root "ArtSource\$($chr.src)") -Filter "$($sh.name).*" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $srcFile) { Write-Output "$($chr.name)/$($sh.name): 원본 없음 — 건너뜀"; continue }
    $png = Join-Path $tmp "$($chr.name)_$($sh.name).png"
    Convert-ToPng $srcFile.FullName $png

    $o = New-Options $chr.defaults $sh.o
    if ($o.ScaleMode -eq 'hat') { $o.TargetHat = $hat }
    $o.Bg = [string]$o.Bg

    $c = $o.CellW; $r = $o.CellH
    $stripPath = Join-Path $previewDir "$($chr.name)_$($sh.name)_strip.png"
    $inGame = $chr.game -and -not $sh.preview
    $framePrefix = if ($inGame) { Join-Path $outDir "$($chr.prefix)_$($sh.name)" } else { $null }
    $info = [SheetTool]::Run($png, $stripPath, $framePrefix, $o)
    Write-Output "$($chr.name)/$($sh.name): $info"
    if ($o.ScaleMode -eq 'hat' -and $hat -eq 0 -and $info -match '\(=([\d.]+)px\)') { $hat = [double]$Matches[1] }

    $img = [Drawing.Image]::FromFile($stripPath); $n = [int]($img.Width / $c); $img.Dispose()

    # 프레임 수가 줄었으면 남은 옛 프레임(+meta) 정리
    if ($inGame) {
      Get-ChildItem $outDir -Filter "$($chr.prefix)_$($sh.name)_*.png" | Where-Object {
        [int]($_.BaseName -replace '.*_', '') -ge $n } | ForEach-Object {
          Remove-Item $_.FullName; if (Test-Path "$($_.FullName).meta") { Remove-Item "$($_.FullName).meta" } }
    }

    # 미리보기 (CSS steps 애니메이션)
    $b64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($stripPath))
    $dur = [Math]::Round($n / $sh.fps, 3)
    foreach ($z in $z1, $z2) {
      $w = $c * $z; $h = $r * $z; $end = - $c * $n * $z
      $div = "<div class='cell'><b>$($sh.label)</b><span>$n" + "f / $($sh.fps)fps</span><div class='anim' style=""width:${w}px;height:${h}px;background-image:url(data:image/png;base64,$b64);background-size:$($c*$n*$z)px ${h}px;--end:${end}px;animation:play ${dur}s steps($n) infinite""></div><div class='ground' style='width:${w}px'></div></div>"
      if ($z -eq $z1) { $anim2 += $div } else { $anim6 += $div }
    }
    $strips += "<div class='cell' style='margin-bottom:16px'><b>$($sh.label)</b><img class='strip' width='$($c*$n*$z3)' height='$($r*$z3)' src='data:image/png;base64,$b64'></div>"

    # 정리 파일용: 기준점 = 땅에 닿는 점(ground/feet) 또는 무게중심(mass/cell — 떠 있는 것·이펙트)
    $py = if ($o.AlignY -eq 'ground' -or $o.AlignY -eq 'feet') { $o.FootY } else { $o.MassY }
    $meta += [ordered]@{
      sheet = $sh.name; label = $sh.label; file = "$($chr.name)_$($sh.name)_strip.png"
      frames = $n; fps = $sh.fps; cellW = $c; cellH = $r
      pivotPx = @($o.AnchorX, $py); pivotKind = $(if ($py -eq $o.FootY) { 'ground' } else { 'center' })
      unityPivot = @([Math]::Round($o.AnchorX / $c, 4), [Math]::Round(1 - $py / $r, 4))
    }
  }

  $manifest = [ordered]@{
    character = $chr.name; pixelsPerUnit = $(if ($chr.view -eq 1) { 90 } else { 45 })
    note = '가로 한 줄 스트립. 칸(cellW×cellH)을 왼쪽부터 frames개로 자르면 프레임. 그림은 오른쪽을 봄(왼쪽은 좌우 반전). pivotPx는 칸 왼쪽 위 기준 px, unityPivot은 Unity 스프라이트 피벗(0~1, 아래가 0).'
    sheets = $meta
  }
  [IO.File]::WriteAllText((Join-Path $previewDir "$($chr.name)_manifest.json"), ($manifest | ConvertTo-Json -Depth 5), (New-Object Text.UTF8Encoding $false))

  $tpl = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'preview_template.html'), [Text.Encoding]::UTF8)
  $html = $tpl.Replace('%TITLE%', $chr.name).Replace('%ANIM2%', $anim2).Replace('%ANIM6%', $anim6).Replace('%STRIPS%', $strips).Replace('%Z1%', $z1).Replace('%Z2%', $z2).Replace('%Z3%', $z3)
  [IO.File]::WriteAllText((Join-Path $previewDir "$($chr.name)_anim_preview.html"), $html, (New-Object Text.UTF8Encoding $false))
}
