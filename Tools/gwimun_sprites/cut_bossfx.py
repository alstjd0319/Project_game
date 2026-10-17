"""불가살이 특수 패턴 이펙트 시트(ArtSource/gwimun/bossfx/bulgasari_<dash|spine_volley|ground_slam>_fx.png, 96×96 × 8장 가로, 투명 PNG —
사용자가 준 bulgasari-vfx 패키지의 Textures)를 Assets/Art/Fx/bossfx_<dash|spine|slam>_<n>.png 프레임으로 자른다. 그림은 오른쪽을 본다.
빌더(CreateAttackFxArt)가 다른 이펙트처럼 가운데 피벗으로 임포트해 AttackFxArt.bossDash/bossSpine/bossSlam에 묶는다.
실행: python3 -I Tools/gwimun_sprites/cut_bossfx.py
"""
import os
from PIL import Image

SRC = 'ArtSource/gwimun/bossfx'
OUT = 'Assets/Art/Fx'
SHEETS = {'dash': 'bulgasari_dash_fx.png', 'spine': 'bulgasari_spine_volley_fx.png', 'slam': 'bulgasari_ground_slam_fx.png'}
CELL = 96

for f in os.listdir(OUT):
    if f.startswith('bossfx_') and f.endswith('.png'):
        os.remove(os.path.join(OUT, f))
for name, file in SHEETS.items():
    sheet = Image.open(f'{SRC}/{file}').convert('RGBA')
    for i in range(sheet.width // CELL):
        sheet.crop((i * CELL, 0, (i + 1) * CELL, CELL)).save(f'{OUT}/bossfx_{name}_{i}.png')
print('done')
