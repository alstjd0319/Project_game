# AI로 뽑은 배경(ArtSource/bg/<장소>/)을 게임용으로 가공한다.
#   - 레이어(먼·중간·가까운 배경): 마젠타 지우기, 좌우 이음새 맞추기(가장 덜 티 나는 세로선으로 잘라 이어 붙임),
#     필요하면 원 영역의 밝은 픽셀 지우기(먼 배경과 겹치는 달)
#   - 소품·타일: 마젠타 지우기(타일은 AI가 그린 격자선도 메움) → 떨어진 조각마다 PNG 하나
# 결과: Captures/art_test/bg/<장소>/ + 시차 스크롤 미리보기 Captures/art_test/bg/<장소>_preview.html
#
# 사용법:  powershell -ExecutionPolicy Bypass -File Tools\sprites\process_bg.ps1 [-Only shrine,zone1]

param([string[]]$Only)
if ($Only) { $Only = @($Only | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ }) }

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
Add-Type -ReferencedAssemblies System.Drawing -Path (Join-Path $PSScriptRoot 'BgTool.cs')

# seam = 이음새를 찾을 겹침 폭(px). 그만큼 그림 폭이 줄어든다. speed = 미리보기 스크롤 속도(px/초, 1080p 기준)
# front = 주인공 앞에 그릴 레이어. 지금 "가까운 배경"들은 땅 높이의 건물·울타리까지 들어 있어서 주인공을 가리므로
#         주인공 뒤(세 번째 배경)로 둔다.
# erase = 'cx,cy,r,최소밝기' — 원 안에서 이 밝기 이상인 픽셀을 지운다
$places = @(
  @{
    name = 'shrine'; label = '사당 (귀문 밖)'
    layers = @(
      @{ file = 'far';  keyed = $false; seam = 160; speed = 15 },
      @{ file = 'mid';  keyed = $true;  seam = 256; speed = 40 },
      @{ file = 'near'; keyed = $true;  seam = 320; speed = 70 }
    )
    cuts = @( @{ file = 'props'; gap = 4; minArea = 600; grid = $false } )
    ground = @{ piece = 'props_06' }
  },
  @{
    name = 'zone1'; label = '1구간 (귀문 안 입구)'
    layers = @(
      @{ file = 'far';  keyed = $false; seam = 160; speed = 15 },
      @{ file = 'mid';  keyed = $true;  seam = 256; speed = 40; erase = '1137,460,138,135' },
      @{ file = 'near'; keyed = $true;  seam = 320; speed = 70 }
    )
    cuts = @(
      @{ file = 'props'; gap = 4; minArea = 600; grid = $false },
      @{ file = 'tiles'; gap = 3;  minArea = 300; grid = $true }
    )
    ground = @{ piece = 'tiles_00' }
  },
  @{
    name = 'zone2'; label = '2구간 (황천길)'
    layers = @(
      @{ file = 'far';  keyed = $false; seam = 160; speed = 15 },
      @{ file = 'mid';  keyed = $true;  seam = 256; speed = 40 },
      @{ file = 'near'; keyed = $true;  seam = 320; speed = 140; front = $true }
    )
    cuts = @(
      @{ file = 'props'; gap = 4; minArea = 600; grid = $false },
      @{ file = 'tiles'; gap = 3; minArea = 300; grid = $true }
    )
    ground = @{ piece = 'tiles_02' }
  },
  @{
    name = 'zone3'; label = '3구간 (저승 문턱)'
    layers = @(
      @{ file = 'far';  keyed = $false; seam = 160; speed = 15 },
      @{ file = 'mid';  keyed = $true;  seam = 256; speed = 40 },
      @{ file = 'near'; keyed = $true;  seam = 320; speed = 140; front = $true }
    )
    cuts = @(
      @{ file = 'props'; gap = 4; minArea = 600; grid = $false },
      @{ file = 'tiles'; gap = 3; minArea = 300; grid = $true }
    )
    ground = @{ piece = 'tiles_01' }
  },
  @{
    name = 'boss'; label = '보스방 (저승 문)'
    layers = @(
      @{ file = 'far'; keyed = $false; seam = 160; speed = 10 },
      @{ file = 'mid'; keyed = $true;  seam = 256; speed = 30 }
    )
    cuts = @(
      @{ file = 'props'; gap = 4; minArea = 600; grid = $false },
      @{ file = 'tiles'; gap = 3; minArea = 300; grid = $true }
    )
    ground = @{ piece = 'tiles_05' }
  },
  @{
    # 배경은 아니지만 같은 "조각 자르기"로 대화창 초상 4장을 나눈다 (원본 크기 그대로, 축소는 UI 규격이 정해지면)
    name = 'mentor_portraits'; dir = 'mentor'; layers = @()
    cuts = @( @{ file = 'portraits'; gap = 4; minArea = 2000; grid = $false } )
  },
  @{
    # UI 시트(HUD·아이콘·대화창·스킬 트리)도 조각마다 PNG 하나로
    name = 'ui'; dir = 'ui'; layers = @()
    cuts = @(
      @{ file = 'hud';         gap = 4; minArea = 150; grid = $false },
      @{ file = 'icons';       gap = 4; minArea = 150; grid = $false },
      @{ file = 'dialog_menu'; gap = 4; minArea = 150; grid = $false },
      @{ file = 'skilltree';   gap = 4; minArea = 150; grid = $false }
    )
  }
)

$outRoot = Join-Path $root 'Captures\art_test\bg'
New-Item -ItemType Directory -Force $outRoot | Out-Null
$K = @(255, 0, 255, 120)   # 마젠타 키, 허용 오차

foreach ($pl in $places) {
  if ($Only -and ($Only -notcontains $pl.name)) { continue }
  $src = if ($pl.dir) { Join-Path $root "ArtSource\$($pl.dir)" } else { Join-Path $root "ArtSource\bg\$($pl.name)" }
  $out = Join-Path $outRoot $pl.name
  if (Test-Path $out) { Remove-Item $out -Recurse -Force }
  New-Item -ItemType Directory -Force $out | Out-Null

  foreach ($ly in $pl.layers) {
    $info = [BgTool]::Layer("$src\$($ly.file).png", "$out\$($ly.file).png", $ly.keyed, $K[0], $K[1], $K[2], $K[3], $ly.seam, [string]$ly.erase)
    Write-Output "$($pl.name)/$($ly.file): $info"
  }
  foreach ($ct in $pl.cuts) {
    $info = [BgTool]::Cut("$src\$($ct.file).png", "$out\$($ct.file)", $K[0], $K[1], $K[2], $K[3], $ct.gap, $ct.minArea, $ct.grid)
    Write-Output "$($pl.name)/$($ct.file): $info"
  }
  if ($pl.layers.Count -eq 0) { continue }

  # ── 미리보기: 1920×1080 화면에 레이어를 1080 높이로 맞춰 겹치고, 속도를 달리해 옆으로 흘린다 ──
  $s = 1080 / 1024
  $layersHtml = ''
  $z = 1
  foreach ($ly in $pl.layers) {
    if ($ly.front) { continue }
    $img = [Drawing.Image]::FromFile("$out\$($ly.file).png"); $w = [Math]::Round($img.Width * $s); $img.Dispose()
    $dur = [Math]::Round($w / $ly.speed, 2)
    $layersHtml += "<div class='layer' style='z-index:$z;background-image:url($($pl.name)/$($ly.file).png);background-size:${w}px 1080px;--w:-${w}px;animation-duration:${dur}s'></div>`n"
    $z++
  }
  # 땅 한 줄: 윗면을 화면 아래에서 200px(약 2유닛) 위에 둔다. 조각이 더 길면 아래가 화면 밖으로 잘린다.
  $groundY = 880
  $gp = "$out\$($pl.ground.piece).png"
  if (Test-Path $gp) {
    $img = [Drawing.Image]::FromFile($gp); $gw = [Math]::Round($img.Width * $s); $gh = [Math]::Round($img.Height * $s); $img.Dispose()
    $gdur = [Math]::Round($gw / 100, 2)
    $layersHtml += "<div class='layer' style='z-index:$z;top:${groundY}px;height:${gh}px;background-image:url($($pl.name)/$($pl.ground.piece).png);background-size:${gw}px ${gh}px;--w:-${gw}px;animation-duration:${gdur}s'></div>`n"
    $z++
  }
  $groundTop = $groundY + [Math]::Round(10 * $s)
  # 크기 비교용 주인공 (128px 규격 대기 동작 첫 프레임: 칸 384×224, 발끝 208)
  $hero = Join-Path $root 'Captures\art_test\hero_staff_idle_strip.png'
  if (Test-Path $hero) {
    Copy-Item $hero "$outRoot\hero_idle.png" -Force
    $layersHtml += "<div class='hero' style='z-index:$z;top:$($groundTop - 208)px'></div>`n"
    $z++
  }
  foreach ($ly in $pl.layers) {
    if (-not $ly.front) { continue }
    $img = [Drawing.Image]::FromFile("$out\$($ly.file).png"); $w = [Math]::Round($img.Width * $s); $img.Dispose()
    $dur = [Math]::Round($w / $ly.speed, 2)
    $layersHtml += "<div class='layer' style='z-index:$z;background-image:url($($pl.name)/$($ly.file).png);background-size:${w}px 1080px;--w:-${w}px;animation-duration:${dur}s'></div>`n"
    $z++
  }

  $pieces = ''
  Get-ChildItem $out -Filter '*.png' | Where-Object { $_.BaseName -match '_\d\d$' } | Sort-Object Name | ForEach-Object {
    $pieces += "<figure><img src='$($pl.name)/$($_.Name)'><figcaption>$($_.BaseName)</figcaption></figure>"
  }
  $layerImgs = ''
  foreach ($ly in $pl.layers) {
    $layerImgs += "<figure class='wide'><div class='tile2' style='background-image:url($($pl.name)/$($ly.file).png)'></div><figcaption>$($ly.file) — 같은 그림 2장을 이어 붙인 것 (가운데가 이음새)</figcaption></figure>"
  }

  $html = @"
<!doctype html><html lang="ko"><head><meta charset="utf-8"><title>$($pl.label) 배경 미리보기</title>
<style>
body{background:#1e1f24;color:#ddd;font-family:'Malgun Gothic',sans-serif;margin:16px}
h2{margin:24px 0 8px;font-size:18px}
.note{color:#aaa;font-size:13px}
.frame{width:calc(100vw - 48px);aspect-ratio:16/9;overflow:hidden;position:relative;background:#000}
.view{width:1920px;height:1080px;position:absolute;left:0;top:0;transform-origin:0 0}
.layer{position:absolute;left:0;top:0;width:100%;height:1080px;background-repeat:repeat-x;image-rendering:pixelated;animation:scroll linear infinite}
.paused .layer{animation-play-state:paused}
@keyframes scroll{from{background-position-x:0}to{background-position-x:var(--w)}}
.hero{position:absolute;left:768px;width:384px;height:224px;background:url(hero_idle.png) 0 0/1536px 224px;image-rendering:pixelated;animation:idle .67s steps(4) infinite}
@keyframes idle{to{background-position-x:-1536px}}
figure{display:inline-block;margin:6px;vertical-align:top;background:#2b2d34;padding:6px}
figure img{image-rendering:pixelated;max-height:260px;background:repeating-conic-gradient(#3a3c44 0 25%,#33353c 0 50%) 0 0/16px 16px}
figcaption{font-size:12px;color:#bbb}
.tile2{height:300px;width:calc(100vw - 80px);background-repeat:repeat-x;background-size:auto 300px;background-position:center;image-rendering:pixelated;background-color:#555}
button{font-size:14px;padding:4px 10px}
</style></head><body>
<h2>$($pl.label) — 1080p 화면, 시차 스크롤 <button onclick="document.querySelector('.view').classList.toggle('paused')">멈춤/재생</button></h2>
<p class="note">먼 배경 · 중간 배경 · 가까운 배경 · 땅 · 주인공(128px 규격) 순으로 겹침. 레이어마다 속도가 달라 이음새가 계속 지나간다. 좌우 이음새·달 위치 확인용.</p>
<div class="frame"><div class="view">
$layersHtml</div></div>
<h2>레이어 이음새</h2>
$layerImgs
<h2>잘라낸 조각</h2>
$pieces
<script>
function fit(){const f=document.querySelector('.frame'),v=document.querySelector('.view');v.style.transform='scale('+(f.clientWidth/1920)+')'}
addEventListener('resize',fit);fit();
</script>
</body></html>
"@
  [IO.File]::WriteAllText((Join-Path $outRoot "$($pl.name)_preview.html"), $html, (New-Object Text.UTF8Encoding $false))
}
