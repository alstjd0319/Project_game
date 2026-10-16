"""1016 퇴마사 이펙트(fxhero_*_strip.png)에서 게임에 쓰는 것만 오려 Assets/Art/Fx/fxhero_<이름>_<번호>.png 로 저장한다.
실행: python3 -I Tools/gwimun_sprites/cut_fxhero.py   (원본: ArtSource/gwimun/fxhero/)

- a/b로 나뉜 이펙트는 a 다음 b로 이어 붙여 한 클립으로 만든다 (안내.md 4절).
- 시트 칸은 그림보다 훨씬 커서(예: 543×362 칸에 초승달이 작게) 그대로 쓰면 판정 박스에 맞춰 늘릴 때 그림이 작아진다 →
  클립의 모든 프레임에서 불투명한 부분의 합집합 상자로 같은 크기로 자른다 (프레임 간 위치는 유지, 그림 = 칸).
- 옛 fx_* 프레임과 이름이 겹치지 않는다. 피벗은 가운데 (빌더가 맞춘다).
"""
import json
import os
from PIL import Image

SRC = 'ArtSource/gwimun/fxhero'
OUT = 'Assets/Art/Fx'
M = {s['sheet']: s for s in json.load(open(f'{SRC}/fxhero_manifest.json'))['sheets']}

# 클립 이름 → 원본 시트 목록 (순서대로 이어 붙임)
CLIPS = {
    'combo1': ['staff_combo1'],                          # A 봉 베기 1단 · D 휩쓸기
    'combo2': ['staff_combo2'],                          # A 봉 베기 2단 (올려 베기)
    'combo3': ['staff_combo3'],                          # A 봉 베기 3단 (회오리 마무리)
    'counter_big': ['counter_slash_big'],                # 근접 반격
    'parry_shockwave': ['parry_shockwave'],              # 패링 성공 (봉)
    'perfect_parry': ['perfect_parry'],                  # 퍼펙트 패링
    'yogi_gain': ['yogi_gain_a', 'yogi_gain_b'],         # 패링 성공 시 요기 흡수
    'talisman_hit': ['talisman_hit'],                    # 부적 적중
    'quake_wave': ['staff_quake_wave_a', 'staff_quake_wave_b'],   # F 봉 내려찍기
    'seal_start': ['seal_circle_start'],                 # F 부적 봉인진 (시작 · 반복 · 끝)
    'seal_loop': ['seal_circle_loop_a', 'seal_circle_loop_b'],
    'seal_end': ['seal_circle_end'],
}


def sheet_frames(name):
    s = M[name]
    im = Image.open(f'{SRC}/fxhero_{name}_strip.png').convert('RGBA')
    cw, ch = s['cellW'], s['cellH']
    return [im.crop((i * cw, 0, (i + 1) * cw, ch)) for i in range(s['frames'])]


def main():
    os.makedirs(OUT, exist_ok=True)
    for f in os.listdir(OUT):
        if f.startswith('fxhero_') and f.endswith('.png'):
            os.remove(os.path.join(OUT, f))
    # 같은 이펙트의 시작·반복·끝은 크기가 달라 보이지 않게 하나의 상자로 함께 자른다
    groups = [['seal_start', 'seal_loop', 'seal_end']]
    grouped = {c for g in groups for c in g}
    all_frames = {}
    for clip, sheets in CLIPS.items():
        fr = []
        for sh in sheets:
            fr += sheet_frames(sh)
        all_frames[clip] = fr

    def union(clips):
        boxes = [f.getchannel('A').point(lambda a: 255 if a > 8 else 0).getbbox() for c in clips for f in all_frames[c]]
        boxes = [x for x in boxes if x]
        return (min(x[0] for x in boxes), min(x[1] for x in boxes), max(x[2] for x in boxes), max(x[3] for x in boxes))

    boxes_for = {}
    for g in groups:
        bx = union(g)
        for c in g:
            boxes_for[c] = bx
    for c in CLIPS:
        if c not in grouped:
            boxes_for[c] = union([c])

    info = {}
    for clip, sheets in CLIPS.items():
        box = boxes_for[clip]
        for i, f in enumerate(all_frames[clip]):
            f.crop(box).save(f'{OUT}/fxhero_{clip}_{i}.png')
        info[clip] = {'frames': len(all_frames[clip]), 'fps': M[sheets[0]]['fps'], 'size': [box[2] - box[0], box[3] - box[1]]}
        print(clip, info[clip])
    json.dump(info, open(f'{OUT}/fxhero_meta.json', 'w'), ensure_ascii=False, indent=1)


if __name__ == '__main__':
    main()
