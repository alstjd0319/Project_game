"""불가살이 할퀴기 궤적: 1017 추가분 P9 `fxenemy_changgwi_claw_strip.png`(창귀 할퀴기, 4장, 칸 384×512, 남색)를
장면 합집합 상자로 자르고 불가살이 색(녹슨 쇠 → 용암 주황 → 달군 노랑)으로 다시 칠해 Assets/Art/Fx/bossfx_claw_<n>.png 로 저장한다.
그림은 오른쪽 위로 긋는 발톱 세 줄 (게임에서 보스가 왼쪽을 보면 좌우 반전).
실행: python3 -I Tools/gwimun_sprites/cut_bossclaw.py   (원본 사본: ArtSource/gwimun/fxenemy/)
"""
import os

import numpy as np
from PIL import Image

SRC = 'ArtSource/gwimun/fxenemy/fxenemy_changgwi_claw_strip.png'
OUT = 'Assets/Art/Fx'
N = 4
# 밝기(0~1) → 색: 어두운 곳은 녹슨 쇠, 중간은 용암 주황, 가장 밝은 곳은 달군 노랑
RAMP = [(0.0, (60, 22, 10)), (0.35, (150, 52, 14)), (0.6, (235, 110, 25)), (0.85, (255, 190, 70)), (1.0, (255, 245, 200))]


def ramp(v):
    out = np.zeros(v.shape + (3,), np.float32)
    for (a, ca), (b, cb) in zip(RAMP, RAMP[1:]):
        m = (v >= a) & (v <= b)
        k = ((v - a) / (b - a))[..., None]
        out[m] = (np.array(ca) * (1 - k) + np.array(cb) * k)[m]
    return out


def main():
    for f in os.listdir(OUT):
        if f.startswith('bossfx_claw_'):
            os.remove(os.path.join(OUT, f))
    strip = Image.open(SRC).convert('RGBA')
    cw = strip.width // N
    cells = [strip.crop((i * cw, 0, (i + 1) * cw, strip.height)) for i in range(N)]
    bbs = [c.getbbox() for c in cells if c.getbbox()]
    box = (min(b[0] for b in bbs), min(b[1] for b in bbs), max(b[2] for b in bbs), max(b[3] for b in bbs))
    for i, c in enumerate(cells):
        a = np.asarray(c.crop(box)).astype(np.float32)
        v = a[..., :3].max(axis=2) / 255.0
        lit = (a[..., 3] > 0)
        if lit.any():
            v = np.clip(v / max(1e-3, np.percentile(v[lit], 98)), 0, 1)  # 가장 밝은 곳이 달군 노랑이 되게 늘린다
        rgb = ramp(v)
        out = np.dstack([rgb, a[..., 3]]).astype(np.uint8)
        Image.fromarray(out, 'RGBA').save(f'{OUT}/bossfx_claw_{i}.png')
    print('claw', box, (box[2] - box[0], box[3] - box[1]))


if __name__ == '__main__':
    main()
