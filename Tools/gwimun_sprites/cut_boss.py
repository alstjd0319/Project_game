"""불가살이(3구간 보스) 스트립(ArtSource/gwimun/boss/bulgasari/bulgasari_<시트>_strip.png, 칸 1024×640)을
Assets/Art/Enemies/Bulgasari/bulgasari_<시트>_<번호>.png 프레임으로 자른다. 원본은 에셋 폴더 1016의 미리보기 스트립(가공 완료본).
피벗은 manifest의 (0.5, 0.0312) = 발끝(칸 아래에서 20px 위)이고 PPU 90 — 빌더(PrototypeSceneBuilder.CreateBossArt)가 같은 값으로 임포트한다.
실행: python3 -I Tools/gwimun_sprites/cut_boss.py
"""
import json
import os
from PIL import Image

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


if __name__ == '__main__':
    main()
