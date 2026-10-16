"""바닥 타일 시트(ArtSource/gwimun/floor/floor_tiles_<구간>_clean.png, 8×8칸·칸 약 156.75px·배경 투명)에서
게임 바닥에 쓸 칸만 오려 Assets/Art/Floor/<구간>_<역할>_<번호>.png 로 저장한다. (사당은 shrine_props.png의 "땅 한 줄" 조각을 쓴다. 흙바닥 타일 shrine_dirt_albedo.png는 받아 두었지만 사용자가 원래 그림으로 되돌려 쓰지 않는다.)

실행: python3 -I Tools/gwimun_sprites/cut_floor.py

역할 이름 (PrototypeSceneBuilder.CreateFloorSets가 같은 이름으로 읽는다):
  top   땅 윗면 가운데 타일 (이어 붙임)      topL / topR  윗면 왼쪽·오른쪽 끝
  fill  땅 속 채움 타일 (아래로 반복)          fillL / fillR 속 채움 왼쪽·오른쪽 끝
  platL / platM / platR  떠 있는 발판 조각 (윗면 + 아래로 늘어진 바위가 한 장)

칸 경계에 보라색 가는 선이 있어서, 오릴 때 내용 상자를 2px 안쪽으로 줄여 이음새가 안 보이게 한다.
같은 역할의 가운데 타일은 폭(과 속 채움은 높이)을 같게 맞춘다(가운데를 자름).
결과 크기·"윗면에서 땅 표면까지의 높이"를 floor_meta.json에 적어 두면 빌더가 읽어 1유닛 = 몸통 두께로 PPU를 정한다.
"""
import json
import os
import numpy as np
from PIL import Image

SRC = 'ArtSource/gwimun/floor'
OUT = 'Assets/Art/Floor'
CW = 1254 / 8
INSET = 2

# 구간별: 역할 → 칸 목록 [(열, 행), ...]. platL/M/R 항목은 (윗칸, 아랫칸) 두 칸을 위아래로 이어 한 장으로 만든다.
SPECS = {
    'zone1': {
        'top': [(1, 0), (2, 0)], 'topL': [(0, 0)], 'topR': [(3, 0)],
        'fill': [(1, 1), (2, 1)], 'fillL': [(0, 1)], 'fillR': [(3, 1)],
        'platL': [(0, 3)], 'platM': [(1, 3), (2, 3)], 'platR': [(3, 3)],   # 아랫칸(길게 늘어진 바위)은 너무 길어 쓰지 않는다
    },
    'zone2': {
        'top': [(1, 2), (2, 2), (3, 2), (4, 2), (5, 2), (6, 2), (7, 2), (0, 2)],
        'fill': [(0, 3), (1, 3), (2, 3), (3, 3), (4, 3), (5, 3), (6, 3), (7, 3)],
        'platL': [(0, 1)], 'platM': [(1, 1), (2, 1)], 'platR': [(2, 1)],
    },
    'zone3': {
        'top': [(1, 1), (2, 1)], 'topL': [(0, 1)], 'topR': [(3, 1)],
        'fill': [(0, 3), (1, 3), (2, 3), (3, 3)],
        'platL': [(3, 0)], 'platM': [(4, 0)], 'platR': [(5, 0)],
    },
}


def cell_box(c, r):
    return int(c * CW), int(r * CW), int((c + 1) * CW) + 1, int((r + 1) * CW) + 1


def content_bbox(img, box):
    """칸 안에서 불투명한 부분의 상자(칸 좌표 → 시트 좌표)."""
    x0, y0, x1, y1 = box
    a = np.asarray(img.getchannel('A'))[y0:y1, x0:x1] > 128
    ys, xs = np.nonzero(a)
    return x0 + xs.min(), y0 + ys.min(), x0 + xs.max() + 1, y0 + ys.max() + 1


def crop_tile(img, cell):
    box = cell_box(*cell)
    l, t, r, b = content_bbox(img, box)
    return img.crop((l + INSET, t + INSET if t > box[1] + 4 else t, r - INSET, b - INSET))


def crop_stack(img, top_cell, hang_cell=None):
    """떠 있는 발판: 윗칸 + (있으면) 아랫칸의 늘어진 부분을 칸 x 좌표를 맞춰 위아래로 이어 한 장으로."""
    tb = cell_box(*top_cell)
    l, t, r, b = content_bbox(img, tb)
    top = img.crop((tb[0], t, tb[2], tb[3] - INSET))
    if hang_cell is None:
        return top.crop((l - tb[0] + INSET, 0, r - tb[0] - INSET, top.height))
    hb = cell_box(*hang_cell)
    hl, ht, hr, hbot = content_bbox(img, hb)
    hang = img.crop((hb[0], hb[1] + INSET, hb[2], hbot))
    canvas = Image.new('RGBA', (tb[2] - tb[0], top.height + hang.height), (0, 0, 0, 0))
    canvas.paste(top, (0, 0))
    canvas.alpha_composite(hang, (0, top.height))
    bb = canvas.getchannel('A').point(lambda v: 255 if v > 128 else 0).getbbox()
    return canvas.crop((bb[0] + INSET, 0, bb[2] - INSET, bb[3]))


def equalize_width(tiles):
    w = min(t.width for t in tiles)
    out = []
    for t in tiles:
        x = (t.width - w) // 2
        out.append(t.crop((x, 0, x + w, t.height)))
    return out


def equalize_height_bottom(tiles):
    """속 채움은 위·아래 이음새가 맞도록 높이를 같게 (가운데를 자른다)."""
    h = min(t.height for t in tiles)
    out = []
    for t in tiles:
        y = (t.height - h) // 2
        out.append(t.crop((0, y, t.width, y + h)))
    return out


def surface_row(tile):
    """윗면 타일에서 '풀 끝이 아니라 흙·돌 몸통이 시작되는' 줄: 폭의 80% 이상이 불투명한 첫 줄."""
    a = np.asarray(tile.getchannel('A')) > 128
    cover = a.mean(axis=1)
    for y, v in enumerate(cover):
        if v >= 0.8:
            return y
    return 0


def main():
    os.makedirs(OUT, exist_ok=True)
    for f in os.listdir(OUT):
        if f.endswith('.png') and (f.startswith('zone') or f.startswith('shrine')):
            os.remove(os.path.join(OUT, f))
    meta = {}
    for zone, spec in SPECS.items():
        img = Image.open(f'{SRC}/floor_tiles_{zone}_clean.png').convert('RGBA')
        tiles = {}
        for role, cells in spec.items():
            if role.startswith('plat'):
                tiles[role] = [crop_stack(img, c[0], c[1]) if isinstance(c[0], tuple) else crop_stack(img, c) for c in cells]
            else:
                tiles[role] = [crop_tile(img, c) for c in cells]
        tiles['top'] = equalize_width(tiles['top'])
        tiles['fill'] = equalize_height_bottom(equalize_width(tiles['fill']))
        tiles['platM'] = equalize_width(tiles['platM'])
        # 윗면 타일: 표면 줄과 몸통 두께 (맨 아래 − 표면)
        top_h = [t.height for t in tiles['top']]
        surf = int(np.median([surface_row(t) for t in tiles['top']]))
        body = int(np.median([t.height - surface_row(t) for t in tiles['top']]))
        plat_surf = int(np.median([surface_row(t) for t in tiles['platM']])) if 'platM' in tiles else 0
        files = []
        for role, lst in tiles.items():
            for i, t in enumerate(lst):
                name = f'{zone}_{role}_{i}.png'
                t.save(f'{OUT}/{name}')
                # surface = 땅 표면 줄(윗면·발판은 풀 끝이 아닌 몸통이 시작되는 줄), 속 채움은 맨 위
                files.append({'name': name, 'height': t.height, 'surface': surface_row(t) if role in ('top', 'topL', 'topR') or role.startswith('plat') else 0})
        meta[zone] = {
            'files': files,
            'bodyPx': body,                       # 윗면 타일의 표면→아래 끝 높이(px). 1유닛(바닥 판정 두께)이 되도록 PPU로 쓴다
            'platSurfacePx': plat_surf,           # 발판 조각의 맨 위 → 윗면(표면)까지(px)
            'tileWidthPx': tiles['top'][0].width,
            'roles': {r: len(l) for r, l in tiles.items()},
        }
        print(zone, meta[zone], 'top tile heights', top_h)

    # 사당: 에셋 지형 타일 세트(shrine_tiles.json) — 윗면 = ground_14(위만 풀, 좌우 이웃 있음), 속 채움 = ground_15 + 무늬 변형 4장.
    # 그림은 칸(90px)보다 사방 30px 크므로 불투명 영역만 잘라(약 90px) 이어 붙인다.
    TD = 'ArtSource/gwimun/floor/shrine_tiles'
    def trim(name):
        im = Image.open(f'{TD}/{name}.png').convert('RGBA')
        return im.crop(im.getchannel('A').point(lambda v: 255 if v > 128 else 0).getbbox())
    top = trim('ground_14')
    top.save(f'{OUT}/shrine_top_0.png')
    fills = [trim(n) for n in ('ground_15', 'ground_15_var0', 'ground_15_var1', 'ground_15_var2', 'ground_15_var3')]
    w = min(f.width for f in fills); h = min(f.height for f in fills)
    files = [{'name': 'shrine_top_0.png', 'height': top.height, 'surface': surface_row(top)}]
    for i, f in enumerate(fills):
        f.crop((0, 0, w, h)).save(f'{OUT}/shrine_fill_{i}.png')
        files.append({'name': f'shrine_fill_{i}.png', 'height': h, 'surface': 0})
    sr = surface_row(top)
    meta['shrine'] = {'files': files, 'bodyPx': top.height - sr, 'platSurfacePx': 0, 'tileWidthPx': top.width, 'roles': {'top': 1, 'fill': len(fills)}}
    print('shrine', meta['shrine'])
    # Unity JsonUtility는 사전을 못 읽으므로 {"zones":[{zone, bodyPx, files:[...]}]} 목록으로 쓴다
    zones = [{'zone': z, 'bodyPx': v['bodyPx'], 'files': v['files']} for z, v in meta.items()]
    json.dump({'zones': zones}, open(f'{OUT}/floor_meta.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)


if __name__ == '__main__':
    main()
