"""UI 시트(마젠타 배경, 1536×1024)를 조각으로 오린다. 미리보기 폴더의 조각은 대충 잘려 있어(점선이 점으로 흩어짐 등) 원본에서 다시 오린다.
함수: remove_magenta(img) → RGBA, components(alpha, grow) → 조각 상자 목록."""
from collections import deque
import numpy as np
from PIL import Image


def remove_magenta(img):
    a = np.asarray(img.convert('RGB')).astype(int)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    mag = (r > 190) & (b > 190) & (g < 110) & ((r - g) > 110) & ((b - g) > 110)
    # 가장자리 번짐: 배경과 닿은 마젠타 기운 픽셀도 지운다 (두 번)
    for _ in range(2):
        near = np.zeros_like(mag)
        near[1:] |= mag[:-1]; near[:-1] |= mag[1:]; near[:, 1:] |= mag[:, :-1]; near[:, :-1] |= mag[:, 1:]
        fringe = near & ~mag & (r > 150) & (b > 140) & (g < 140) & ((r - g) > 50) & ((b - g) > 40)
        mag = mag | fringe
    alpha = np.where(mag, 0, 255).astype(np.uint8)
    return Image.fromarray(np.dstack([a.astype(np.uint8), alpha]), 'RGBA')


def components(alpha_img, grow=8, min_area=400, scale=4):
    """투명하지 않은 덩어리를 grow 픽셀 키워 하나로 묶고, 원래 크기의 상자(좌, 위, 오른쪽, 아래)를 돌려준다.
    속도를 위해 scale분의 1로 줄여 덩어리를 찾은 뒤, 원래 해상도의 알파로 상자를 다듬는다."""
    al = np.asarray(alpha_img.getchannel('A')) > 0
    h, w = al.shape
    hs, ws = h // scale, w // scale
    small = al[:hs * scale, :ws * scale].reshape(hs, scale, ws, scale).any(axis=(1, 3))
    big = small.copy()
    for _ in range(max(1, grow // scale + 1)):
        n = big.copy()
        n[1:] |= big[:-1]; n[:-1] |= big[1:]; n[:, 1:] |= big[:, :-1]; n[:, :-1] |= big[:, 1:]
        big = n
    seen = np.zeros_like(big)
    boxes = []
    for y0 in range(hs):
        for x0 in np.nonzero(big[y0] & ~seen[y0])[0]:
            if seen[y0, x0]:
                continue
            dq = deque([(y0, x0)])
            seen[y0, x0] = True
            xs, ys = [x0], [y0]
            while dq:
                y, x = dq.popleft()
                for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < hs and 0 <= nx < ws and big[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        dq.append((ny, nx))
                        xs.append(nx); ys.append(ny)
            x1, y1, x2, y2 = min(xs) * scale, min(ys) * scale, (max(xs) + 1) * scale, (max(ys) + 1) * scale
            sub = al[y1:y2, x1:x2]
            if sub.sum() < min_area:
                continue
            yy, xx = np.nonzero(sub)
            boxes.append((int(x1 + xx.min()), int(y1 + yy.min()), int(x1 + xx.max() + 1), int(y1 + yy.max() + 1)))
    return boxes
