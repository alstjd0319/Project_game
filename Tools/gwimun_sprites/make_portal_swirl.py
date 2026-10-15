"""사당 문 안쪽 소용돌이 프레임 생성: ArtSource/portal_swirl_strip.png(8장)를 한 줄씩 좌우로 물결치게 밀어
16장으로 만든다 (원본 8장은 서로 거의 같아서 그대로는 일렁임이 약하다). 실행: python3 -I Tools/gwimun_sprites/make_portal_swirl.py
결과: Assets/Art/Fx/fx_portal_swirl_0~15.png (192×224, 가운데 피벗은 PrototypeSceneBuilder가 맞춘다)."""
import math
from PIL import Image

SRC = 'ArtSource/portal_swirl_strip.png'
OUT = 'Assets/Art/Fx/fx_portal_swirl_{}.png'
CELL_W, CELL_H, SRC_FRAMES, OUT_FRAMES = 192, 224, 8, 16
AMPLITUDE = 7      # 한 줄이 좌우로 밀리는 최대 픽셀
PERIOD = 60        # 물결 한 번의 세로 길이(픽셀) — 내용(181px)에 약 3번

strip = Image.open(SRC).convert('RGBA')
for k in range(OUT_FRAMES):
    src = strip.crop(((k % SRC_FRAMES) * CELL_W, 0, (k % SRC_FRAMES + 1) * CELL_W, CELL_H))
    phase = 2 * math.pi * k / OUT_FRAMES       # 한 바퀴 돌면 처음으로 → 끊김 없이 반복
    out = Image.new('RGBA', (CELL_W, CELL_H), (0, 0, 0, 0))
    for y in range(CELL_H):
        dx = round(AMPLITUDE * math.sin(2 * math.pi * y / PERIOD - phase))
        row = src.crop((0, y, CELL_W, y + 1))
        out.paste(row, (dx, y))
    out.save(OUT.format(k))
print('만든 프레임:', OUT_FRAMES)
