"""상점·보상 상품 그림: 상품마다 어울리는 메달 아이콘 + 상점 진열대.
- status_icons.png (1017 추가분 원본/ui, 마젠타, 6×3 메달 — 안내_3차_추가분.md 순서: 공격↑·방어↑·속도↑·재생·무적·폭주 / 청룡·백호·주작·현무·기린·기절 / 둔화·저주·독·화상·공포·속박)
- upgyeong_icons.png (1016 원본/ui, 마젠타, 3×5 업경 아이콘)
- shop_props.png (1016 원본/bg/shop/props.png, 마젠타) 아래 줄 둘째 = 천 덮인 나무 진열대
→ Assets/Art/Rewards/offer_<id>.png (높이 96px) · shop_table.png (폭 180px). 실행: python3 -I Tools/gwimun_sprites/cut_offer_icons.py
상품 ↔ 아이콘 (그림이 상품 그대로는 없어서 효과가 비슷한 메달을 고름):
  bell_red(근접 공격력) = 공격↑(엇갈린 봉) · bell_ink(원거리 공격력) = 업경 부적 할퀴기 · amber(최대 체력) = 업경 초록 심장 ·
  rosary(패링 도력) = 업경 보라 구슬 · scroll(경험치) = 업경 금빛 구름 엽전? 아니라 재화 혼불 → offer_scroll은 status 재생 대신 혼불 아이콘 ·
  heal(체력 회복) = 재생(연꽃) · gauge(도력 가득) = 무적(빛 고리)
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from ui_cutter import remove_magenta  # noqa: E402

SRC = 'ArtSource/gwimun/reward'
OUT = 'Assets/Art/Rewards'
ICON_H = 96


def grid(sheet, cols, rows):
    a = np.asarray(sheet.getchannel('A')) > 0
    ys, xs = np.nonzero(a)
    x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
    cw, ch = (x1 - x0) / cols, (y1 - y0) / rows
    cells = {}
    for r in range(rows):
        for c in range(cols):
            box = (int(x0 + c * cw), int(y0 + r * ch), int(x0 + (c + 1) * cw), int(y0 + (r + 1) * ch))
            im = sheet.crop(box)
            cells[(r, c)] = im.crop(im.getbbox())
    return cells


def save(im, name, height=None, width=None):
    k = height / im.height if height else width / im.width
    im = im.resize((max(1, round(im.width * k)), max(1, round(im.height * k))), Image.LANCZOS)
    al = np.asarray(im.getchannel('A'))
    im.putalpha(Image.fromarray(np.where(al > 110, 255, 0).astype(np.uint8)))
    im.save(f'{OUT}/{name}.png')


def main():
    status = grid(remove_magenta(Image.open(f'{SRC}/status_icons.png')), 6, 3)
    upg = grid(remove_magenta(Image.open(f'{SRC}/upgyeong_icons.png')), 3, 5)
    picks = {
        'bell_red': status[(0, 0)],   # 공격↑
        'bell_ink': upg[(0, 1)],      # 부적 할퀴기
        'amber': upg[(2, 1)],         # 초록 심장
        'rosary': upg[(4, 1)],        # 보라 구슬
        'heal': status[(0, 3)],       # 재생 연꽃
        'gauge': status[(0, 4)],      # 빛 고리
    }
    for name, im in picks.items():
        save(im, f'offer_{name}', height=ICON_H)
    props = remove_magenta(Image.open(f'{SRC}/shop_props.png'))
    a = np.asarray(props.getchannel('A')) > 0
    # 아래 줄 둘째 덩어리 (x 약 0.27~0.52, y 0.6 아래)
    w, h = props.size
    region = props.crop((int(w * 0.26), int(h * 0.58), int(w * 0.53), h))
    save(region.crop(region.getbbox()), 'shop_table', width=180)
    print(sorted(picks), 'table')


if __name__ == '__main__':
    main()
