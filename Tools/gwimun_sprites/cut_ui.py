"""귀문 UI 시트(원본/ui/hud.png · icons.png)를 Assets/Art/Ui/ui_*.png 로 오린다.
실행: python3 -I Tools/gwimun_sprites/cut_ui.py [원본/ui 폴더 경로]   (기본: ArtSource/gwimun/ui)
HUD 시트는 조각이 서로 붙어 자동 구분이 어려워서 일부(용 장식 틀·부적 메달)는 상자를 직접 적었다."""
import os
import sys
sys.path.insert(0, os.path.dirname(__file__))
from PIL import Image
from ui_cutter import remove_magenta, components

SRC = sys.argv[1] if len(sys.argv) > 1 else 'ArtSource/gwimun/ui'
OUT = 'Assets/Art/Ui'
os.makedirs(OUT, exist_ok=True)

# ── HUD 시트: 이름 → 상자(좌, 위, 오른쪽, 아래) ──
HUD = {
    'hp_frame': (45, 93, 969, 200),            # 옥 장식 모서리의 체력 틀 (가운데가 비어 있다)
    'hp_fill': (108, 233, 902, 294),           # 초록 채움 바
    'bar_empty': (108, 321, 902, 384),         # 구리 테두리 빈 바
    'orb_empty': (1055, 169, 1137, 253),       # 도력 구슬: 빈 것
    'orb_full': (1207, 169, 1292, 255),        # 찬 것
    'orb_spark': (1336, 146, 1480, 285),       # 가득/방금 찬 것 (반짝)
    'orb_string': (1091, 292, 1399, 392),      # 구슬을 꿰는 매듭 줄
    'slot_parchment': (53, 450, 247, 645),     # 스킬 칸 틀: 평소
    'slot_active': (296, 448, 495, 645),       # 선택·사용 가능 강조
    'slot_locked': (550, 449, 742, 645),       # 잠김(회색)
    'cooldown_disc': (783, 464, 950, 634),     # 쿨타임 원판
    'weapon_staff': (989, 422, 1205, 713),     # 봉 메달
    'weapon_talisman': (1265, 422, 1490, 713), # 부적 메달
    'dragon_frame': (42, 716, 1494, 940),      # 용 장식 큰 틀 (보스 체력 등 큰 바용)
    'purple_fill': (188, 901, 1348, 960),      # 보라 채움 바
}

hud = remove_magenta(Image.open(os.path.join(SRC, 'hud.png')))
# 용 장식 틀 아래쪽에 보라 채움 바가 붙어 있어 틀을 오리기 전에 그 자리를 지운다
ERASE = {'dragon_frame': [(188, 896, 1350, 965)]}
for name, box in HUD.items():
    img = hud.copy() if name in ERASE else hud
    for e in ERASE.get(name, []):
        img.paste((0, 0, 0, 0), e)
    img.crop(box).save(f'{OUT}/ui_{name}.png')

# ── 아이콘 시트: 18조각을 위→아래, 왼→오른쪽 순서로 ──
icons = remove_magenta(Image.open(os.path.join(SRC, 'icons.png')))
boxes = components(icons)
boxes.sort(key=lambda b: ((b[1] + b[3]) // 2 // 150, b[0]))
assert len(boxes) == 18, f'아이콘 조각 {len(boxes)}개 (18개여야 한다)'
names = ['icon_heart', 'icon_lantern', 'icon_staff', 'icon_talisman', 'icon_coin', 'icon_key'] + [f'skill_{i:02d}' for i in range(12)]
for name, box in zip(names, boxes):
    icons.crop(box).save(f'{OUT}/ui_{name}.png')
print('오린 조각:', len(HUD) + len(names))
