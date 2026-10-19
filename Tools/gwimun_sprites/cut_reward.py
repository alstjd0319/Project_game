"""보상 관련 그림 (1016 에셋, 사본 ArtSource/gwimun/reward/)을 게임 폴더로 자른다.
실행: python3 -I Tools/gwimun_sprites/cut_reward.py

- roomobj_chest_open_strip.png (보상 상자 열기, 6장, 칸 256×192, 발끝 (0.5, 0.0833)) → Assets/Art/Rewards/reward_chest_<n>.png
- items_<coin_spin|soulstone_float|exp_orb|pickup_coin|pickup_soul|pickup_exp>_strip.png → Assets/Art/Rewards/reward_<시트>_<n>.png (칸 그대로, 피벗은 manifest)
- currency_set.png (마젠타 배경, 3×3) 아래 줄 작은 아이콘 3개(엽전 · 영혼석 · 혼불) → Assets/Art/Rewards/reward_icon_<coin|soul|exp>.png (HUD용)
- bg_far.png / bg_mid.png (보상방 배경, 높이 1024) → Assets/Art/Backgrounds/reward/far.png · mid.png
빌더(PrototypeSceneBuilder.CreateRewardArt / CreateRoomBackgroundSet)가 임포트한다.
"""
import json
import os
import shutil
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from ui_cutter import remove_magenta, components  # noqa: E402

SRC = 'ArtSource/gwimun/reward'
OUT = 'Assets/Art/Rewards'
BG = 'Assets/Art/Backgrounds/reward'


def cut_strip(file, name, frames, cw, ch):
    strip = Image.open(f'{SRC}/{file}').convert('RGBA')
    for i in range(frames):
        strip.crop((i * cw, 0, (i + 1) * cw, ch)).save(f'{OUT}/reward_{name}_{i}.png')


def main():
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(BG, exist_ok=True)
    for f in os.listdir(OUT):
        if f.endswith('.png'):
            os.remove(os.path.join(OUT, f))
    pivots = {}
    for mf in ('items_manifest.json', 'roomobj_manifest.json'):
        for s in json.load(open(f'{SRC}/{mf}', encoding='utf-8'))['sheets']:
            if s['sheet'] == 'hearth_fire':
                continue
            name = 'chest' if s['sheet'] == 'chest_open' else s['sheet']
            cut_strip(s['file'], name, s['frames'], s['cellW'], s['cellH'])
            pivots[name] = s['unityPivot']
    # 재화 아이콘: 아래 줄(세로 2/3 아래)의 덩어리 3개를 왼쪽부터 엽전 · 영혼석 · 혼불
    sheet = remove_magenta(Image.open(f'{SRC}/currency_set.png'))
    boxes = [b for b in components(sheet, grow=10) if b[1] > sheet.height * 0.6]
    boxes.sort(key=lambda b: b[0])
    assert len(boxes) == 3, boxes
    for name, b in zip(('coin', 'soul', 'exp'), boxes):
        im = sheet.crop(b)
        k = 64 / max(im.size)
        im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS).save(f'{OUT}/reward_icon_{name}.png')
    json.dump(pivots, open(f'{OUT}/reward_pivots.json', 'w'), indent=1)
    shutil.copy(f'{SRC}/bg_far.png', f'{BG}/far.png')
    shutil.copy(f'{SRC}/bg_mid.png', f'{BG}/mid.png')
    print(sorted(pivots), 'icons', boxes)


if __name__ == '__main__':
    main()
