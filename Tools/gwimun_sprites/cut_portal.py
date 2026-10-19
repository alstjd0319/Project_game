"""방 포탈 그림을 오린다 (실행: python3 -I Tools/gwimun_sprites/cut_portal.py, 원본: ArtSource/gwimun/portal/).

- portal_frames.png (1016 원본/bg/rooms, 마젠타 배경, 방 종류별 포탈 틀 7개 — 안내.md 2절 순서:
  일반 · 엘리트 · 상점 · 보상 · 휴식 · 이벤트 · 보스) → Assets/Art/Props/portal_frame_<n>.png
  게임 크기(높이 FRAME_H px = 90px/유닛 기준)로 줄여 저장하고, 틀 안쪽 뚫린 부분(소용돌이 자리)을 찾아 portal_meta.json에 적는다.
  틀마다 안쪽 구멍 모양 그대로의 가림막(마스크) portal_hole_<n>.png도 만든다 — 소용돌이를 구멍 모양대로만 보이게 (틀과 같은 크기·피벗).
- fx_gate_portal_<n>.png (에셋 귀문 소용돌이, 이미 잘린 6장) → fx_gate_portal_gray_<n>.png: 밝기만 남긴 회색판 — 게임에서 방 종류 색을 곱해 그 색 소용돌이로 쓴다.
- fxstatus_portal_enter/exit_strip.png (1017 추가분 미리보기, 투명) → Assets/Art/Fx/fxstatus_portal_<enter|exit>_<n>.png
  모든 장면의 불투명 영역을 합친 상자로 자르고(장면끼리 위치가 같게), 사람 모양의 발끝 위치를 meta에 적는다.
"""
import json
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from ui_cutter import remove_magenta  # noqa: E402

SRC = 'ArtSource/gwimun/portal'
PROPS = 'Assets/Art/Props'
FX = 'Assets/Art/Fx'
FRAME_H = 342          # 포탈 틀 높이 = 3.8유닛 (주인공 키 약 1.4유닛)
FX_FIGURE_H = 150      # 포탈 들어가기/나오기의 사람 모양 높이 = 주인공 그림(약 128px)보다 조금 크게


def hole_mask(alpha):
    """틀 안쪽 구멍: 바깥 테두리와 이어지지 않은 투명 픽셀 (True = 구멍)."""
    clear = alpha == 0
    h, w = clear.shape
    outside = np.zeros_like(clear)
    stack = [(y, x) for y in range(h) for x in (0, w - 1) if clear[y, x]] + [(y, x) for x in range(w) for y in (0, h - 1) if clear[y, x]]
    for y, x in stack:
        outside[y, x] = True
    while stack:
        y, x = stack.pop()
        for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
            if 0 <= ny < h and 0 <= nx < w and clear[ny, nx] and not outside[ny, nx]:
                outside[ny, nx] = True
                stack.append((ny, nx))
    hole = clear & ~outside
    # 장식 사이 작은 틈(엽전 줄 사이·천 아래 등)도 "구멍"으로 잡히므로 가장 큰 덩어리(문 안쪽)만 남긴다
    labels = np.zeros(hole.shape, np.int32)
    best, best_n, label = 0, 0, 0
    for y0, x0 in zip(*np.nonzero(hole)):
        if labels[y0, x0]:
            continue
        label += 1
        labels[y0, x0] = label
        stack, n = [(y0, x0)], 0
        while stack:
            y, x = stack.pop()
            n += 1
            for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                if 0 <= ny < h and 0 <= nx < w and hole[ny, nx] and not labels[ny, nx]:
                    labels[ny, nx] = label
                    stack.append((ny, nx))
        if n > best_n:
            best, best_n = label, n
    return labels == best


def box_of(mask):
    ys, xs = np.nonzero(mask)
    if len(xs) == 0:
        return None
    return int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1


def grow(mask, n):
    m = mask.copy()
    for _ in range(n):
        g = m.copy()
        g[1:] |= m[:-1]; g[:-1] |= m[1:]; g[:, 1:] |= m[:, :-1]; g[:, :-1] |= m[:, 1:]
        m = g
    return m


def cut_frames(meta):
    for f in os.listdir(PROPS):
        if (f.startswith('portal_frame_') or f.startswith('portal_hole_')) and f.endswith('.png'):
            os.remove(os.path.join(PROPS, f))
    sheet = remove_magenta(Image.open(f'{SRC}/portal_frames.png'))
    # 틀끼리 거의 붙어 있어 덩어리로는 안 나뉜다 → 불투명 픽셀이 (거의) 없는 세로 줄을 경계로 나눈다
    a = np.asarray(sheet.getchannel('A')) > 0
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
    runs = [r for r in runs if r[1] - r[0] > 20]
    assert len(runs) == 7, f'포탈 틀 7개를 찾지 못했다: {len(runs)}'
    boxes = []
    for x0, x1 in runs:
        ys = np.nonzero(a[:, x0:x1].any(axis=1))[0]
        boxes.append((x0, int(ys.min()), x1, int(ys.max()) + 1))
    meta['frames'] = []
    for i, box in enumerate(boxes):
        im = sheet.crop(box)
        k = FRAME_H / im.height
        im = im.resize((max(1, round(im.width * k)), FRAME_H), Image.LANCZOS)
        a = np.asarray(im.getchannel('A')).copy()
        a = np.where(a > 110, 255, 0).astype(np.uint8)  # 줄인 뒤 반투명 가장자리를 정리 (점 필터로 그려도 테두리가 번지지 않게)
        im.putalpha(Image.fromarray(a))
        hole = hole_mask(a)
        hb = box_of(hole)
        im.save(f'{PROPS}/portal_frame_{i}.png')
        # 가림막: 구멍을 2px 넓혀(틀 안쪽 가장자리 밑으로 들어가게) 흰색으로. 소용돌이는 틀 뒤에 그려지므로 넓힌 부분은 틀에 덮인다
        m = grow(hole, 2)
        mask = np.zeros((im.height, im.width, 4), np.uint8)
        mask[m] = (255, 255, 255, 255)
        Image.fromarray(mask, 'RGBA').save(f'{PROPS}/portal_hole_{i}.png')
        meta['frames'].append({'w': im.width, 'h': im.height, 'hole': hb})
        print('frame', i, im.size, 'hole', hb)


def cut_fx(meta, name):
    for f in os.listdir(FX):
        if f.startswith(f'fxstatus_portal_{name}_'):
            os.remove(os.path.join(FX, f))
    strip = Image.open(f'{SRC}/fxstatus_portal_{name}_strip.png').convert('RGBA')
    n = 6
    cw = strip.width // n
    cells = [strip.crop((i * cw, 0, (i + 1) * cw, strip.height)) for i in range(n)]
    bbs = [c.getbbox() for c in cells if c.getbbox()]
    union = (min(b[0] for b in bbs), min(b[1] for b in bbs), max(b[2] for b in bbs), max(b[3] for b in bbs))
    # 사람 모양 높이: 사람이 또렷한 장면(들어가기 첫 장 / 나오기 마지막 장)의 높이
    ref = cells[0] if name == 'enter' else cells[-1]
    rb = ref.getbbox()
    k = FX_FIGURE_H / (rb[3] - rb[1])
    for i, c in enumerate(cells):
        im = c.crop(union)
        im = im.resize((max(1, round(im.width * k)), max(1, round(im.height * k))), Image.LANCZOS)
        im.save(f'{FX}/fxstatus_portal_{name}_{i}.png')
    w, h = round((union[2] - union[0]) * k), round((union[3] - union[1]) * k)
    feet = round((rb[3] - union[1]) * k)  # 위에서 발끝까지(px)
    meta[name] = {'w': w, 'h': h, 'feet': feet}
    print(name, (w, h), 'feet', feet)


def gray_vortex():
    """귀문 소용돌이 회색판: 각 픽셀의 가장 밝은 채널(명도)만 남긴다 → 게임에서 곱하는 색이 그대로 소용돌이 색이 된다."""
    for i in range(6):
        src = f'{FX}/fx_gate_portal_{i}.png'
        if not os.path.exists(src):
            return
        a = np.asarray(Image.open(src).convert('RGBA')).astype(np.float32)
        v = a[..., :3].max(axis=2)
        v = np.clip(v / max(1.0, float(v[a[..., 3] > 0].max() if (a[..., 3] > 0).any() else 255)) * 255, 0, 255)  # 가장 밝은 곳 = 흰색
        out = np.dstack([v, v, v, a[..., 3]]).astype(np.uint8)
        Image.fromarray(out, 'RGBA').save(f'{FX}/fx_gate_portal_gray_{i}.png')


MARK_H = 130  # 방 표식 높이(px) = 약 1.45유닛 (문 틀 위에 매다는 장식)


def cut_marks():
    """방 표식 7개 (1016 원본/ui/room_marks.png, 마젠타 배경, 문 틀과 같은 순서) → Assets/Art/Props/portal_mark_<n>.png."""
    for f in os.listdir(PROPS):
        if f.startswith('portal_mark_'):
            os.remove(os.path.join(PROPS, f))
    sheet = remove_magenta(Image.open(f'{SRC}/room_marks.png'))
    a = np.asarray(sheet.getchannel('A')) > 0
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
    runs = [r for r in runs if r[1] - r[0] > 40]
    assert len(runs) == 7, f'방 표식 7개를 찾지 못했다: {runs}'
    for i, (x0, x1) in enumerate(runs):
        ys = np.nonzero(a[:, x0:x1].any(axis=1))[0]
        im = sheet.crop((x0, int(ys.min()), x1, int(ys.max()) + 1))
        k = MARK_H / im.height
        im = im.resize((max(1, round(im.width * k)), MARK_H), Image.LANCZOS)
        al = np.asarray(im.getchannel('A'))
        im.putalpha(Image.fromarray(np.where(al > 110, 255, 0).astype(np.uint8)))
        im.save(f'{PROPS}/portal_mark_{i}.png')
    print('marks', len(runs))


def main():
    meta = {}
    cut_marks()
    gray_vortex()
    cut_frames(meta)
    cut_fx(meta, 'enter')
    cut_fx(meta, 'exit')
    json.dump(meta, open(f'{PROPS}/portal_meta.json', 'w'), indent=1)


if __name__ == '__main__':
    main()
