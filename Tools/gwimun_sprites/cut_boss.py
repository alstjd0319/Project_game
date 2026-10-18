"""불가살이(3구간 보스) 스트립(ArtSource/gwimun/boss/bulgasari/bulgasari_<시트>_strip.png, 칸 1024×640)을
Assets/Art/Enemies/Bulgasari/bulgasari_<시트>_<번호>.png 프레임으로 자른다. 원본은 에셋 폴더 1016의 미리보기 스트립(가공 완료본).
피벗은 manifest의 (0.5, 0.0312) = 발끝(칸 아래에서 20px 위)이고 PPU 90 — 빌더(PrototypeSceneBuilder.CreateBossArt)가 같은 값으로 임포트한다.
실행: python3 -I Tools/gwimun_sprites/cut_boss.py
"""
import json
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from ui_cutter import remove_magenta  # noqa: E402

SRC = 'ArtSource/gwimun/boss/bulgasari'
OUT = 'Assets/Art/Enemies/Bulgasari'


def main():
    os.makedirs(OUT, exist_ok=True)
    for f in os.listdir(OUT):
        if f.endswith('.png'):
            os.remove(os.path.join(OUT, f))
    manifest = json.load(open(f'{SRC}/bulgasari_manifest.json', encoding='utf-8'))
    total = 0
    for sheet in manifest['sheets']:
        strip = Image.open(f"{SRC}/{sheet['file']}").convert('RGBA')
        w, h = sheet['cellW'], sheet['cellH']
        for i in range(sheet['frames']):
            strip.crop((i * w, 0, (i + 1) * w, h)).save(f"{OUT}/bulgasari_{sheet['sheet']}_{i}.png", optimize=True)
            total += 1
    print('프레임', total)
    fix_tall_p2_heavy_windup()


# 2페이즈 강공격 준비(코를 높이 치켜드는 동작)는 미리보기 스트립에서 칸(640) 위로 넘쳐 코끝이 잘려 있다.
# 원본(1016 원본/boss/bulgasari/p2_heavy_windup.png, 마젠타 배경, 4장 가로)에서 다시 그려 칸을 위로 늘린다.
# 크기·가로 위치는 잘리지 않은 기존 프레임에 맞추고(배율 = 첫 장 폭 비율), 발끝은 칸 아래에서 20px 위 그대로.
TALL_H = 832  # 칸 높이 (빌더 피벗 y = 20 / TALL_H)


def fix_tall_p2_heavy_windup():
    src = f'{SRC}/original_p2_heavy_windup.png'
    if not os.path.exists(src):
        return
    orig = remove_magenta(Image.open(src))
    a = np.asarray(orig.getchannel('A')) > 0
    col = a.sum(0)
    runs, start = [], None
    for x, v in enumerate(col):
        if v > 2 and start is None:
            start = x
        if v <= 2 and start is not None:
            runs.append((start, x))
            start = None
    if start is not None:
        runs.append((start, len(col)))
    runs = [r for r in runs if r[1] - r[0] > 30]
    assert len(runs) == 4, runs
    boxes = []
    for x0, x1 in runs:
        ys = np.nonzero(a[:, x0:x1].any(axis=1))[0]
        boxes.append((x0, int(ys.min()), x1, int(ys.max()) + 1))
    # 잘린 미리보기 프레임에서 가로 위치(왼쪽 끝)와 배율을 가져온다 (잘린 건 위쪽뿐이라 가로·아래는 맞다)
    old = [np.asarray(Image.open(f'{OUT}/bulgasari_p2_heavy_windup_{i}.png').getchannel('A')) > 0 for i in range(4)]
    old_x0 = [int(np.nonzero(o.any(axis=0))[0].min()) for o in old]
    o0 = old[0]
    k = (np.nonzero(o0.any(axis=0))[0].max() + 1 - old_x0[0]) / (boxes[0][2] - boxes[0][0])
    feet = TALL_H - 20
    for i, (x0, y0, x1, y1) in enumerate(boxes):
        fr = orig.crop((x0, y0, x1, y1))
        fr = fr.resize((round(fr.width * k), round(fr.height * k)), Image.LANCZOS)
        al = np.asarray(fr.getchannel('A'))
        fr.putalpha(Image.fromarray(np.where(al > 110, 255, 0).astype(np.uint8)))
        cell = Image.new('RGBA', (1024, TALL_H), (0, 0, 0, 0))
        cell.alpha_composite(fr, (old_x0[i], feet - fr.height))
        cell.save(f'{OUT}/bulgasari_p2_heavy_windup_{i}.png', optimize=True)
    print('2페이즈 강공격 준비 다시 그림: 배율', round(float(k), 3), '칸', (1024, TALL_H))


if __name__ == '__main__':
    main()
