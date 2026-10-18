"""불가살이 특수 패턴 이펙트를 보스 디자인(쇠 가시 · 녹슨 철판 · 용암 균열)에 맞춰 코드로 그린다.
게임 규격 그대로 90px = 1유닛(빌더가 PPU 90 · 점 필터 · 가운데 피벗으로 임포트)이라 코드에서 배율 1이 원래 크기다.
옆에서 보는 게임이라 땅 이펙트는 모두 바닥선을 기준으로 옆모습으로 그린다. 그림은 오른쪽을 본다.

  bossfx_spine_<n>     날아가는 쇠 가시 (반복)          128×48,  가시 가운데 = 그림 가운데
  bossfx_wave_<n>      바닥을 타는 가시 파도 (반복)     128×144, 바닥선 = 아래에서 27px (판정 상자 가운데에서 0.5유닛 아래)
  bossfx_slam_<n>      땅 찍기 충격 (한 번)             384×256, 바닥선 = 위에서 200px
  bossfx_dash_<n>      돌진 앞 압력파 (반복)            160×256
  bossfx_dashtrail_<n> 돌진 잔상·불티·흙먼지 (한 번)    320×256, 바닥선 = 위에서 248px

실행: python3 -I Tools/gwimun_sprites/make_bossfx.py
"""
import math
import os
import random

from PIL import Image, ImageDraw

OUT = 'Assets/Art/Fx'

# 보스 그림에서 고른 색
OUTLINE = (24, 19, 18, 255)
STEEL_D = (62, 56, 56, 255)
STEEL_M = (104, 97, 96, 255)
STEEL_L = (160, 153, 148, 255)
STEEL_H = (222, 216, 206, 255)
RUST_D = (92, 44, 24, 255)
RUST_M = (150, 78, 38, 255)
LAVA_D = (128, 26, 6)
LAVA_M = (214, 66, 12)
LAVA_O = (255, 128, 22)
LAVA_Y = (255, 196, 70)
LAVA_W = (255, 240, 170)
ROCK_D = (40, 30, 26, 255)
ROCK_M = (78, 60, 48, 255)
ROCK_L = (118, 94, 74, 255)
DUST = (104, 86, 72)


def new(w, h):
    return Image.new('RGBA', (w, h), (0, 0, 0, 0))


def over(img, layer):
    img.alpha_composite(layer)


def glow(img, cx, cy, rx, ry, color, alpha=200, steps=4):
    """계단식(도트 느낌) 빛무리: 바깥일수록 옅다."""
    layer = new(*img.size)
    d = ImageDraw.Draw(layer)
    for i in range(steps):
        k = 1 - i / steps
        a = int(alpha * (i + 1) / steps)
        d.ellipse((cx - rx * k, cy - ry * k, cx + rx * k, cy + ry * k), fill=color + (a,))
    over(img, layer)


def rot(pts, cx, cy, ang):
    c, s = math.cos(ang), math.sin(ang)
    return [(cx + x * c - y * s, cy + x * s + y * c) for x, y in pts]


def quill(img, cx, cy, length, width, ang, hot=0.0):
    """보스 등의 쇠 가시 하나 (잎 모양 칼날). ang 0 = 오른쪽, hot = 뿌리 쪽 달군 정도(0~1)."""
    L, W = length, width
    shape = [(-0.5 * L, 0), (-0.38 * L, -0.3 * W), (-0.12 * L, -0.5 * W), (0.18 * L, -0.34 * W), (0.5 * L, 0),
             (0.18 * L, 0.34 * W), (-0.12 * L, 0.5 * W), (-0.38 * L, 0.3 * W)]
    d = ImageDraw.Draw(img)
    d.polygon(rot(shape, cx, cy, ang), fill=OUTLINE)
    inner = [(x * 0.9, y * 0.72) for x, y in shape]
    d.polygon(rot(inner, cx, cy, ang), fill=STEEL_M)
    top = [(x, min(y, 0)) for x, y in inner]          # 위쪽 반 = 빛 받는 면
    d.polygon(rot(top, cx, cy, ang), fill=STEEL_L)
    under = [(x * 0.85, max(y, 0) * 0.7 + 0.12 * W) for x, y in inner if y >= 0]
    if len(under) > 2:
        d.polygon(rot(under, cx, cy, ang), fill=STEEL_D)
    ridge = rot([(-0.3 * L, -0.06 * W), (0.42 * L, -0.02 * W)], cx, cy, ang)
    d.line(ridge, fill=STEEL_H, width=1)
    if hot > 0:
        # 뿌리만 달궈진 쇠: 보스 2페이즈 가시처럼 갈라진 틈으로 용암빛이 비친다
        hx, hy = rot([(-0.4 * L, 0)], cx, cy, ang)[0]
        glow(img, hx, hy, L * 0.14 * hot + 2, W * 0.4 * hot + 2, LAVA_O, int(130 * hot), 3)
        d = ImageDraw.Draw(img)
        root = [(x, y * 0.8) for x, y in inner if x <= -0.3 * L]
        if len(root) > 2:
            d.polygon(rot(root, cx, cy, ang), fill=RUST_D)
        d.line(rot([(-0.44 * L, 0), (-0.22 * L, -0.04 * W), (-0.08 * L, 0.06 * W)], cx, cy, ang), fill=LAVA_M + (255,), width=1)
        d.line(rot([(-0.42 * L, 0), (-0.3 * L, -0.02 * W)], cx, cy, ang), fill=LAVA_Y + (255,), width=1)


def clip_below(img, y):
    """바닥선 아래는 지운다 — 게임에선 바닥 타일이 그 자리를 덮지 않으므로 땅속에 빛이 번지면 어색하다."""
    ImageDraw.Draw(img).rectangle((0, y, img.width, img.height), fill=(0, 0, 0, 0))


def rock(img, cx, cy, r, rng, hot=False):
    d = ImageDraw.Draw(img)
    n = 5
    pts = [(cx + math.cos(a) * r * rng.uniform(0.7, 1.1), cy + math.sin(a) * r * rng.uniform(0.6, 1.0))
           for a in [i * 2 * math.pi / n + rng.uniform(-0.3, 0.3) for i in range(n)]]
    d.polygon(pts, fill=ROCK_D)
    inner = [(cx + (x - cx) * 0.65, cy + (y - cy) * 0.65 - 1) for x, y in pts]
    d.polygon(inner, fill=RUST_M if hot else ROCK_M)
    d.point((cx - r * 0.2, cy - r * 0.4), fill=LAVA_Y + (255,) if hot else ROCK_L)


def ember(img, x, y, size, heat):
    col = LAVA_W if heat > 0.75 else LAVA_Y if heat > 0.45 else LAVA_O if heat > 0.2 else LAVA_M
    d = ImageDraw.Draw(img)
    d.rectangle((x, y, x + size - 1, y + size - 1), fill=col + (255,))


def crack(img, x0, y, length, direction, heat, rng, thickness=3):
    """바닥선을 따라 갈라지는 용암 균열. heat 1 = 갓 터짐(노랑), 0 = 식음(검붉음)."""
    if length < 2 or heat <= 0:
        return
    pts, x, yy = [], x0, y
    while abs(x - x0) < length:
        pts.append((x, yy))
        x += direction * rng.randint(5, 10)
        yy = y + rng.randint(-2, 3)
    pts.append((x0 + direction * length, y + 1))
    outer = LAVA_D if heat < 0.5 else LAVA_M
    mid = LAVA_M if heat < 0.4 else LAVA_O
    core = LAVA_O if heat < 0.4 else LAVA_Y if heat < 0.85 else LAVA_W
    a = int(255 * min(1, heat * 1.6))
    d = ImageDraw.Draw(img)
    d.line(pts, fill=ROCK_D, width=thickness + 2)
    d.line(pts, fill=outer + (a,), width=thickness + 1)
    d.line(pts, fill=mid + (a,), width=max(1, thickness - 1))
    d.line(pts, fill=core + (a,), width=1)
    for i in range(1, len(pts) - 1, 2):   # 잔가지
        px, py = pts[i]
        bx = px + direction * rng.randint(3, 8)
        by = py + rng.choice([-1, 1]) * rng.randint(2, 5)
        d.line([(px, py), (bx, by)], fill=mid + (a,), width=1)


def dust(img, cx, cy, r, alpha):
    if alpha <= 0 or r <= 0:
        return
    layer = new(*img.size)
    d = ImageDraw.Draw(layer)
    d.ellipse((cx - r, cy - r * 0.75, cx + r, cy + r * 0.75), fill=DUST + (int(alpha * 0.55),))
    d.ellipse((cx - r * 0.7, cy - r * 0.75, cx + r * 0.5, cy + r * 0.3), fill=(132, 112, 94, int(alpha * 0.6)))
    over(img, layer)


# ---------------------------------------------------------------- 가시

def make_spine():
    frames = []
    W, H, cx, cy = 128, 48, 72, 24
    for f in range(6):
        rng = random.Random(100 + f)
        img = new(W, H)
        tail = 46 + (f % 3) * 6
        back = cx - 30
        # 뒤로 끌리는 열기
        glow(img, back - tail * 0.45, cy, tail * 0.55, 7, LAVA_D, 120, 3)
        glow(img, back - tail * 0.3, cy, tail * 0.36, 5, LAVA_M, 170, 3)
        glow(img, back - 6, cy, 14, 4, LAVA_Y, 210, 2)
        for i in range(5):
            ex = back - rng.randint(8, tail)
            ember(img, ex, cy + rng.randint(-8, 7), rng.choice([1, 2]), 1 - (back - ex) / tail)
        quill(img, cx, cy, 64, 15, 0.0, hot=0.9 if f % 2 == 0 else 0.7)
        frames.append(img)
    return frames


# ---------------------------------------------------------------- 바닥 가시 파도

def make_wave():
    frames = []
    W, H, gy = 128, 144, 117
    n = 6
    spikes = [(78, 0.0, 70, 0.32), (60, 0.33, 58, 0.55), (96, 0.6, 50, 0.15), (44, 0.75, 40, 0.75)]  # x, 위상, 길이, 기울기
    for f in range(n):
        rng = random.Random(200 + f)
        img = new(W, H)
        # 뒤쪽 흙먼지
        dust(img, 26 - f * 2, gy - 10, 16 + f, 150 - f * 15)
        dust(img, 40, gy - 6, 12, 120)
        # 바닥 아래 열기
        glow(img, 72, gy, 54, 12, LAVA_M, 150, 3)
        glow(img, 84, gy, 24, 7, LAVA_Y, 200, 2)
        # 흙더미
        d = ImageDraw.Draw(img)
        mound = [(14, gy + 1), (34, gy - 6), (58, gy - 12), (88, gy - 14), (112, gy - 6), (124, gy + 1)]
        d.polygon(mound, fill=ROCK_D)
        d.polygon([(22, gy + 1), (40, gy - 4), (62, gy - 9), (86, gy - 11), (108, gy - 4), (118, gy + 1)], fill=ROCK_M)
        # 솟는 쇠 가시 (위상에 따라 솟았다 들어간다)
        for x, ph, L, tilt in spikes:
            t = (f / n + ph) % 1.0
            rise = math.sin(t * math.pi)
            if rise < 0.08:
                continue
            ang = -math.pi / 2 + tilt
            length = L * (0.45 + 0.55 * rise)
            cx = x + math.cos(ang) * length * 0.5
            cy = gy - 6 + math.sin(ang) * length * 0.5
            quill(img, cx, cy, length, 14 + L * 0.06, ang, hot=0.8)
        clip_below(img, gy + 2)
        # 바닥선 균열
        crack(img, 16, gy + 1, 104, 1, 0.9, rng, 2)
        for i in range(6):
            ember(img, rng.randint(30, 116), gy - rng.randint(10, 100), rng.choice([1, 2]), rng.random())
        for i in range(3):
            rock(img, rng.randint(30, 110), gy - rng.randint(20, 70), rng.randint(3, 5), rng, hot=rng.random() < 0.4)
        frames.append(img)
    return frames


# ---------------------------------------------------------------- 땅 찍기

def make_slam():
    frames = []
    W, H, cx, gy = 384, 256, 192, 200
    n = 10
    base = random.Random(7)
    debris = []
    for i in range(16):
        side = -1 if i % 2 else 1
        debris.append(dict(vx=side * base.uniform(4, 15), vy=-base.uniform(9, 19), r=base.randint(3, 7),
                           q=base.random() < 0.35, ang=base.uniform(-1, 1), hot=base.random() < 0.5))
    for f in range(n):
        rng = random.Random(300 + f)
        img = new(W, H)
        t = f / (n - 1)
        heat = max(0.0, 1.0 - t * 1.05)
        # 흙먼지 (뒤에서 피어나 퍼진다)
        if f >= 1:
            for side in (-1, 1):
                for k in range(3):
                    r = 14 + f * 4 + k * 6
                    dust(img, cx + side * (30 + f * 9 + k * 22), gy - 10 - k * 4, r, max(0, 190 - f * 18 - k * 20))
        # 충격 빛 (처음 몇 장)
        if f <= 3:
            k = [1.0, 0.85, 0.55, 0.3][f]
            glow(img, cx, gy - 10, 46 + f * 26, 30 + f * 12, LAVA_M, int(170 * k), 4)
            glow(img, cx, gy - 6, 28 + f * 14, 18 + f * 6, LAVA_Y, int(220 * k), 3)
            if f <= 1:
                glow(img, cx, gy - 4, 18, 12, LAVA_W, 240, 2)
        # 반원 충격파 (옆에서 본 돔)
        if 1 <= f <= 5:
            d = ImageDraw.Draw(img)
            rr = 40 + f * 28
            a = int(230 * (1 - (f - 1) / 5))
            box = (cx - rr, gy - rr * 0.62, cx + rr, gy + rr * 0.62)
            d.arc(box, 180, 360, fill=LAVA_O + (a,), width=4)
            d.arc((box[0] + 3, box[1] + 3, box[2] - 3, box[3] - 3), 190, 350, fill=LAVA_Y + (a,), width=1)
        clip_below(img, gy + 2)
        # 바닥 균열 (양옆으로 뻗고 식는다)
        reach = min(1.0, (f + 1) / 4)
        crng = random.Random(77)
        for side in (-1, 1):
            crack(img, cx, gy, int(170 * reach), side, heat, random.Random(78 + side), 4)
            for b in (40, 95):
                if 170 * reach > b:
                    bx = cx + side * b
                    crack(img, bx, gy + 1, int(30 * reach), side, heat * 0.8, crng, 2)
        # 솟구친 바위·쇠 가시 조각
        for p in debris:
            tt = f + 0.5
            x = cx + p['vx'] * tt
            y = gy - 8 + p['vy'] * tt + 1.6 * tt * tt
            if y > gy - 2 or x < 4 or x > W - 4:
                continue
            if p['q']:
                quill(img, x, y, 22, 7, math.atan2(p['vy'] + 3.2 * tt, p['vx']), hot=0.6 if heat > 0.3 else 0)
            else:
                rock(img, x, y, p['r'], random.Random(id(p) % 1000 + f), hot=p['hot'] and heat > 0.3)
        # 땅에서 튀어나온 큰 가시 둘 (찍은 자리)
        if 1 <= f <= 7:
            rise = math.sin(min(1.0, f / 3) * math.pi / 2) * (1 if f < 6 else 1 - (f - 5) / 3)
            for side, L in ((1, 60), (-1, 46)):
                ang = -math.pi / 2 + side * 0.42
                length = L * rise
                if length > 8:
                    quill(img, cx + side * 14 + math.cos(ang) * length * 0.5, gy - 2 + math.sin(ang) * length * 0.5,
                          length, 16, ang, hot=heat)
        # 불티
        for i in range(int(16 * heat) + 2):
            ex = cx + rng.randint(-120, 120)
            ey = gy - rng.randint(8, int(30 + 110 * (1 - t * 0.5)))
            ember(img, ex, ey, rng.choice([1, 2, 2]), heat * rng.random() + 0.1)
        frames.append(img)
    return frames


# ---------------------------------------------------------------- 돌진: 앞 압력파

def streak(img, x_back, x_front, y, thick, heat, alpha):
    """앞(오른쪽)이 두껍고 뒤로 가늘어지는 불꼬리 한 줄."""
    if alpha <= 0 or x_front - x_back < 3:
        return
    d = ImageDraw.Draw(img)
    h = thick / 2
    outer = LAVA_D if heat < 0.5 else LAVA_M
    d.polygon([(x_back, y), (x_front - 2, y - h), (x_front, y), (x_front - 2, y + h)], fill=outer + (alpha,))
    core_back = x_front - (x_front - x_back) * 0.55
    d.polygon([(core_back, y), (x_front - 2, y - max(0.5, h - 1)), (x_front, y), (x_front - 2, y + max(0.5, h - 1))],
              fill=(LAVA_O if heat < 0.8 else LAVA_Y) + (alpha,))
    d.line([(x_front - (x_front - x_back) * 0.25, y), (x_front, y)], fill=LAVA_W + (alpha,), width=1)


def make_dash():
    frames = []
    W, H, cy = 160, 256, 128
    for f in range(4):
        rng = random.Random(400 + f)
        img = new(W, H)
        bulge = 10 + (f % 2) * 3
        def front_x(yy):
            k = (yy - cy) / 112
            return 118 + bulge * math.cos(k * math.pi / 2) - abs(k) * 14
        # 몸 앞 열기 (압력파 바로 뒤에만 얇게)
        glow(img, 104, cy, 22, 100, LAVA_M, 80, 3)
        # 뒤로 흐르는 불꼬리 (길이·끝이 제각각)
        for i in range(11):
            y = cy - 100 + i * 20 + rng.randint(-5, 5)
            fx = front_x(y) - rng.randint(4, 16)
            length = rng.randint(24, 70)
            streak(img, fx - length, fx, y, rng.choice([3, 4, 5]), rng.random(), rng.randint(150, 230))
        # 앞 압력파 ")" — 가운데가 두껍고 위아래 끝은 가늘다
        d = ImageDraw.Draw(img)
        for k in range(-12, 12):
            y0, y1 = cy + k * 9.4, cy + (k + 1) * 9.4
            mid = 1 - abs((k + 0.5) / 12)
            for w, col, a in ((int(3 + 6 * mid), LAVA_D, 220), (int(2 + 4 * mid), LAVA_O, 240), (max(1, int(2 * mid)), LAVA_Y, 255)):
                d.line([(front_x(y0), y0), (front_x(y1), y1)], fill=col + (a,), width=w)
        for k in range(-6, 6):   # 하얗게 달군 가운데
            y0, y1 = cy + k * 9.4, cy + (k + 1) * 9.4
            d.line([(front_x(y0) + 1, y0), (front_x(y1) + 1, y1)], fill=LAVA_W + (255,), width=1)
        # 앞에서 튀는 불티 · 뒤로 날리는 쇠 부스러기
        for i in range(12):
            y = cy + rng.randint(-104, 104)
            ember(img, int(front_x(y)) + rng.randint(2, 18), y, rng.choice([1, 2]), rng.random())
        for i in range(2):
            quill(img, rng.randint(60, 96), cy + rng.randint(-80, 80), 18, 6, math.pi + rng.uniform(-0.2, 0.2), hot=0.5)
        frames.append(img)
    return frames


# ---------------------------------------------------------------- 돌진: 잔상

def make_dashtrail():
    frames = []
    W, H, gy = 320, 256, 248
    n = 6
    base = random.Random(9)
    streaks = [(base.randint(60, 230), base.randint(200, 316), base.randint(50, 150), base.choice([3, 4, 6]), base.random())
               for _ in range(14)]
    shards = [(base.randint(120, 300), base.randint(60, 220), base.uniform(-0.3, 0.3)) for _ in range(6)]
    for f in range(n):
        rng = random.Random(500 + f)
        img = new(W, H)
        t = f / (n - 1)
        fade = 1 - t
        # 땅에 차인 흙먼지
        for k in range(3):
            dust(img, 250 - k * 50 - f * 10, gy - 14 - k * 3, 16 + k * 5 + f * 4, int(200 * fade) - k * 30)
        # 녹은 쇠 잔상: 뒤로 밀리며 짧아지고 식는다
        for y, front, length, thick, heat in streaks:
            fx = front - f * 12
            streak(img, fx - length * (1 - t * 0.55), fx, y, thick, heat * fade, int(220 * fade))
        for x, y, a0 in shards:
            if fade < 0.2:
                continue
            quill(img, x - f * 16, y + f * f * 1.5, 20, 6, math.pi + a0, hot=fade * 0.7)
        for i in range(int(14 * fade) + 1):
            ember(img, rng.randint(40, 310), rng.randint(40, gy - 6), rng.choice([1, 2]), fade * rng.random())
        clip_below(img, gy + 2)
        frames.append(img)
    return frames


def main():
    clips = {'spine': make_spine(), 'wave': make_wave(), 'slam': make_slam(), 'dash': make_dash(), 'dashtrail': make_dashtrail()}
    for f in os.listdir(OUT):  # 이 스크립트가 그리는 것만 지운다 (bossfx_claw_* 는 cut_bossclaw.py 몫)
        if any(f.startswith(f'bossfx_{name}_') for name in clips) and not f.startswith('bossfx_claw_'):
            os.remove(os.path.join(OUT, f))
    for name, frames in clips.items():
        for i, im in enumerate(frames):
            im.save(f'{OUT}/bossfx_{name}_{i}.png')
    print({k: len(v) for k, v in clips.items()})


if __name__ == '__main__':
    main()
