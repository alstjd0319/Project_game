# 이미지 생성 요청서 (Codex용)

> 작성: Claude · 2026-10-13
> 대상: **패링 로그라이크 (가제)** — 조선 배경 한국 민속 공포, 2D 횡스크롤 도트 액션. 주인공 퇴마사가 귀문(저승으로 통하는 문) 안으로 들어가 요괴를 받아치며(패링) 가장 깊은 곳의 문을 닫는다.
> 이 파일은 **Codex가 읽고 이미지를 생성**하기 위한 지시서다. 사람이 읽을 설명은 한국어, 이미지 프롬프트는 영어.

---

## 0. Codex 작업 규칙 (먼저 끝까지 읽을 것)

1. **할 일**: 아래 각 표의 한 줄 = 이미지 한 장. 지정된 경로에 PNG로 저장한다. **다른 파일은 만들거나 고치지 않는다** (이 md, 코드, 기존 `ArtSource/warrior/` 등 전부 건드리지 말 것).
2. **프롬프트 조립**: 각 이미지의 최종 프롬프트 = `[STYLE]` + 해당 섹션의 `[캐릭터/대상 블록]` + 해당 섹션의 `[시트 틀]`에 표의 값을 채운 것. 블록 안의 `{중괄호}`를 표의 값으로 바꿔 넣는다.
3. **참고 이미지**: 이미지 도구가 참고 이미지(reference image) 입력을 지원하면, 표의 "참고" 칸에 적힌 파일을 함께 넣는다. 지원하지 않으면 텍스트만으로 생성한다.
   - 캐릭터마다 **기준 그림(base)을 가장 먼저** 만들고, 그 캐릭터의 나머지 시트는 전부 그 기준 그림을 참고로 넣는다 (디자인이 시트마다 달라지는 것을 막기 위함).
4. **캔버스 크기**: 표에 적힌 크기 (`1536x1024` 가로 / `1024x1024` 정사각 / `1024x1536` 세로). 표에 없으면 `1536x1024`.
5. **배경색 (중요)**: 나중에 스크립트로 배경을 지운다. 그래서 배경은 **완전히 평평한 단색**이어야 한다 — 그라데이션·그림자·질감·바닥선 금지.
   - 기본: **#FF00FF (마젠타)**
   - 예외는 각 섹션에 적어 둠 (어둑시니·보라 이펙트 = #00FF00 초록, 이펙트 = #000000 검정, 원경 배경 = 배경 없이 꽉 채움).
6. **글자 금지**: 어떤 이미지에도 글자·숫자·라벨·제목·서명·워터마크를 넣지 않는다 (부적의 붓글씨 무늬는 예외 — 읽히지 않는 장식이면 됨).
7. **실패 시 다시 생성**: 아래 중 하나라도 해당하면 같은 프롬프트로 최대 3번까지 다시 만든다.
   - 프레임 수가 틀림
   - 프레임끼리 겹치거나 붙음
   - 캐릭터가 왼쪽을 봄 (오른쪽을 봐야 함)
   - 배경이 단색이 아님
   - 글자가 들어감
   - 기준 그림과 디자인이 확연히 다름
8. **기록**: 다 끝나면 `ArtSource/_generation_log.md`에 표로 남긴다: 파일 경로 · 성공/실패 · 다시 만든 횟수 · 문제점 한 줄.
9. **순서**: 우선순위 `P1 → P2 → P3`. 사용자가 "P1만" 같이 범위를 말하면 그 범위만 한다.
   - P1: 주인공 전부, 창귀, 도깨비불, 전투 이펙트, HUD
   - P2: 나머지 요괴, 스승, 대화·메뉴 UI, 1구간 배경, 사당 배경
   - P3: 보스, 2·3구간·보스 배경, 스킬 트리 UI

---

## 1. 공통 스타일 `[STYLE]` — 모든 프롬프트 맨 앞에 붙인다

```text
2D side-scrolling action game asset in polished PAINTED PIXEL ART style (like modern indie pixel games: clean hard-edged pixels, limited palette, readable silhouettes, subtle dithering, no blur, no anti-aliased soft edges).
Setting: Joseon-era Korea, Korean folklore horror. Strictly KOREAN design — not Japanese (no torii, no shimenawa, no samurai/ninja look), not Chinese.
Mood: night, moonlight, eerie but beautiful.
Color rule: bright RED is reserved for enemy attack warnings. Use red only where explicitly requested.
No text, no letters, no numbers, no labels, no signatures, no watermarks.
```

스타일 기준 그림 (참고 이미지로 쓸 수 있으면 넣을 것): `ArtSource/concept/bg_1_entrance.png` — 배경 컨셉이 이미 원하는 "채색 도트" 그림체다.

---

## 2. 캐릭터 시트 공통 틀 `[SHEET]`

주인공·스승·요괴·보스의 동작 시트는 전부 이 틀을 쓴다. 가공 스크립트(`Tools/sprites/process_sheets.ps1`)가 이 규칙을 전제로 자른다.

```text
Sprite animation sheet of the character described above (use the attached base sprite as the exact design reference: same outfit, colors, proportions and size).
Action: {action}.
Exactly {frames} frames in ONE horizontal row, in animation order from left to right, evenly spaced.
- Every frame is a side view facing RIGHT.
- The character is the same size in every frame and fills about 60% of the canvas height.
- Feet on the same baseline in every frame, except frames that are explicitly airborne.
- Generous empty space between frames. No frame, weapon, hair or cloth may touch or overlap another frame.
- Background: perfectly flat solid {bg} — no gradient, no shadow, no ground line, no texture.
- No slash trails, sparks or effects outside the body unless the action says so (effects are separate assets).
```

`{bg}` 기본값 = `#FF00FF`. 기준 그림(base)은 아래 `[BASE]` 틀을 쓴다.

```text
Single base sprite of the character described above: idle standing pose, full body, side view facing RIGHT, centered on the canvas, filling about 60% of the canvas height.
This image is the master design reference for every later animation sheet, so keep the design clean and simple with large readable shapes (the in-game sprite is only about {game_px} pixels tall).
Background: perfectly flat solid {bg}.
```

---

## 3. 주인공 — 떠돌이 도사 퇴마사 (P1)

컨셉: `ArtSource/concept/exorcist_1_dosa.png` (팀 선택 1안). 무기 둘을 자유롭게 바꿔 든다 — **봉(근거리)** / **부적(원거리)**. 방어는 패링 하나뿐.

### 캐릭터 블록 `[HERO]`

```text
Character: a young male Korean Taoist exorcist (dosa), early 20s, in the spirit of the folk hero Jeon Woo-chi. Playful, confident.
- Wide-brimmed black Korean gat hat with chin strings that flutter. Messy black hair under it.
- Pale jade-green dopo robe with long, wide sleeves (drawn as a few LARGE cloth shapes, not many thin strips). Black sash with a small talisman pouch. Dark trousers with wrapped shins, black shoes.
- Weapon 1 (STAFF): a worn wooden staff, about as tall as he is, with a small brass bell and a knotted cord near the top.
- Weapon 2 (TALISMANS): small yellow paper talismans with tiny red brush marks (the only red allowed on him).
- When he uses one weapon, the other is put away: in staff mode the talismans stay in the pouch; in talisman mode the staff is strapped diagonally across his back.
- Main colors: jade green, black, warm wood brown, talisman yellow.
```

### 시트 목록 — 저장 위치 `ArtSource/hero/`

| 파일 | 틀 | 프레임 | 동작 `{action}` | 참고 |
|---|---|---|---|---|
| `base.png` | BASE (`{game_px}`=128) | 1 | — (staff mode: staff held upright in his right hand) | concept/exorcist_1_dosa.png |
| `staff_idle.png` | SHEET | 4 | idle breathing loop in STAFF mode, staff held ready diagonally in both hands, sleeves and hat strings gently moving | base.png |
| `staff_run.png` | SHEET | 6 | fast run cycle in STAFF mode, body leaning forward, staff held low in both hands, one frame with both feet off the ground (airborne) | base.png |
| `staff_parry.png` | SHEET | 4 | PARRY in STAFF mode, feet planted wide and still: 1 bring the staff up, 2 staff held horizontally in front with both hands bracing to block, 3 slight recoil from an impact, 4 return to ready | base.png |
| `staff_counter.png` | SHEET | 4 | quick counterattack in STAFF mode right after a parry: 1 staff drawn back, 2 fast forward thrust with the staff fully extended, 3 follow-through, 4 back to ready | base.png |
| `staff_skill_sweep.png` | SHEET | 5 | powerful wide horizontal sweep with the staff: 1 deep wind-up, 2 step forward, 3 full sweep with the staff fully extended forward, 4 follow-through behind him, 5 recover | base.png |
| `staff_skill_slam.png` | SHEET | 5 | jumping overhead slam with the staff: 1 crouch, 2 leap up with the staff raised overhead (airborne), 3 peak (airborne), 4 smash the staff down onto the ground in front, 5 recover | base.png |
| `talisman_idle.png` | SHEET | 4 | idle breathing loop in TALISMAN mode: staff strapped on his back, 3 talismans fanned between the fingers of his raised right hand | base.png |
| `talisman_run.png` | SHEET | 6 | fast run cycle in TALISMAN mode, staff on his back, talismans held in one hand, one frame airborne | base.png |
| `talisman_parry.png` | SHEET | 4 | PARRY in TALISMAN mode, feet planted: 1 snap one talisman up in front of him, 2 the talisman held up flat like a shield with a faint golden glow, 3 slight recoil from an impact, 4 return to ready | base.png |
| `talisman_counter.png` | SHEET | 4 | quick counterattack in TALISMAN mode right after a parry: 1 hand pulled back, 2 sharp flick throwing one talisman forward (arm fully extended, the talisman just leaving the fingers), 3 follow-through, 4 back to ready | base.png |
| `talisman_skill_fan.png` | SHEET | 5 | throw a fan of three talismans: 1 wind-up with talismans fanned, 2 step forward, 3 wide throwing motion with arm fully extended (talismans leaving the hand), 4 follow-through, 5 recover | base.png |
| `talisman_skill_seal.png` | SHEET | 5 | place a sealing talisman: 1 kneel, 2 slap a talisman onto the ground in front, 3 hold with two fingers raised in a prayer seal, 4 stand up, 5 ready | base.png |
| `jump.png` | SHEET | 5 | jump sequence, staff strapped on his back: 1 crouch, 2 rising (airborne), 3 apex with sleeves floating (airborne), 4 falling with sleeves blown upward (airborne), 5 landing crouch | base.png |
| `dash.png` | SHEET | 3 | very fast dash forward, staff on his back: 1 push off low, 2 full horizontal lunge with sleeves and hat strings stretched straight back, 3 recover upright | base.png |
| `swap.png` | SHEET | 4 | weapon swap from STAFF to TALISMANS: 1 staff in hands, 2 swing the staff onto his back, 3 hand reaching into the pouch, 4 talismans fanned in hand | base.png |
| `hit.png` | SHEET | 3 | hit reaction: 1 sudden recoil backward (to the LEFT), hat tilting, 2 staggering off balance, 3 regaining stance | base.png |
| `death.png` | SHEET | 5 | death: 1 hit hard, 2 dropping to one knee, the staff falling from his hand, 3 collapsing forward, 4 lying face down with the gat hat fallen beside him, 5 same as 4, completely still | base.png |
| `wake.png` | SHEET | 4 | waking up after being spat out of the gate: 1 lying face down, 2 pushing himself up, 3 kneeling and putting the gat back on, 4 standing | base.png |
| `portraits.png` | (아래 초상 틀) | 4 | — | base.png, concept/exorcist_1_dosa.png |

**초상 틀 (대화창 얼굴)** — `portraits.png`, 캔버스 `1536x1024`

```text
Four dialogue portraits of the character described above, bust shots (head and shoulders), all with the same framing and size, in ONE horizontal row with space between them, three-quarter view facing RIGHT.
Expressions: 1 confident smirk, 2 surprised, 3 serious/determined, 4 tired/hurt.
Painted pixel art, readable at 96x96 pixels. Background: perfectly flat solid #FF00FF.
```

---

## 4. 요괴 (P1: 창귀·도깨비불 / P2: 나머지)

공통 동작 세트 — 패링 게임이라 **일반 공격과 강공격의 준비 자세가 한눈에 구분되는 것**이 가장 중요하다.

- 일반 준비: 짧고 작게.
- 강공격 준비: 크고 과장되게 + **빨간 빛** (이게 유일하게 빨강이 허락되는 곳).
- 준비 자세의 실제 길이(반응 시간)는 게임 코드가 정한다. 그림은 자세만.

| 파일 (요괴 폴더 안) | 틀 | 프레임 | 동작 `{action}` |
|---|---|---|---|
| `base.png` | BASE | 1 | — |
| `idle.png` | SHEET | 4 | idle loop (요괴별 설명 참고) |
| `move.png` | SHEET | 6 | moving forward (요괴별 설명 참고) |
| `attack_normal.png` | SHEET | 6 | NORMAL attack: frames 1-3 short, subtle wind-up (요괴별 설명), frames 4-6 the attack itself and follow-through |
| `heavy_windup.png` | SHEET | 4 | HEAVY attack wind-up: big exaggerated pose building up over 4 frames, with a growing RED glow around the body (the danger signal) |
| `heavy_attack.png` | SHEET | 4 | HEAVY attack release and follow-through, the red glow fading |
| `hurt.png` | SHEET | 5 | frames 1-2 hit recoil backward (to the LEFT); frames 3-5 stunned loop (dizzy, wobbling) |
| `death.png` | SHEET | 5 | death, dissolving (요괴별 설명 참고) |

`base.png`의 참고 = 해당 요괴 컨셉 이미지. 나머지 시트의 참고 = 그 요괴의 `base.png`.

### 4-1. 창귀 — 기본 근접 · `ArtSource/yokai/changgwi/` · P1 · 게임 키 128px

참고: `concept/yokai_changgwi.png`

```text
Character: Changgwi, the ghost of a person eaten by a tiger, now the tiger's servant.
- Gaunt, pale grey human figure in torn Joseon peasant clothes (a few LARGE torn shapes, not many thin strips), hunched, swaying like a puppet.
- Long thin arms with sharp claw-like fingers. Faint dark tiger-stripe marks on the skin. Hollow white eyes, mouth hanging open. No blood.
- Colors: pale grey, dark navy stripes, dirty off-white cloth.
```

동작 보충: idle = 흐느적 흔들림 · move = 앞으로 고꾸라질 듯 휘청이며 걷기 · 일반 준비 = 한 손을 살짝 뒤로 젖힘 → 할퀴기 · 강공격 준비 = 두 팔을 머리 위로 높이 치켜듦 → 두 손으로 내리 할퀴기 · death = 무너지며 회색 연기로 흩어짐.

### 4-2. 도깨비불 — 기본 원거리 · `ArtSource/yokai/dokkaebibul/` · P1 · 게임 키 64px

참고: `concept/yokai_dokkaebibul.png`

```text
Character: Dokkaebi-bul, a floating goblin fire.
- A ghostly blue-green (teal) flame with a mischievous face (two glowing eyes, a jagged grin) and a flickering tail. No body, it floats.
- Colors: teal and pale cyan. It turns RED only in the heavy-attack sheets.
```

동작 보충: 떠 있으므로 "Feet on the same baseline" 대신 **모든 프레임에서 몸 중심 높이를 같게**. idle = 위아래로 둥실 · move = 꼬리를 끌며 떠감 · 일반 준비 = 움츠렸다가 부풀기 → 입에서 불덩이를 뱉기 · 강공격 준비 = 붉게 변하며 커짐 → 큰 불덩이 뱉기 · death = 펑 꺼지며 연기.

추가 시트: `projectile.png` — SHEET 틀 대신 아래 프롬프트, `1536x1024`

```text
Projectile sprites for the goblin fire above, in ONE horizontal row with space between them:
frames 1-4 a small teal fireball flying RIGHT (4-frame flicker loop, flame tail trailing left);
frames 5-8 a larger RED fireball flying RIGHT (4-frame flicker loop) for the heavy attack.
Background: perfectly flat solid #FF00FF.
```

### 4-3. 두억시니 — 묵직한 강공격 · `ArtSource/yokai/dueoksini/` · P2 · 게임 키 192px

참고: `concept/yokai_dueoksini.png`

```text
Character: Dueoksini, a malicious demon that crushes people's heads.
- Huge, hulking, hunched body with a tiny head. Enormous oversized hands. Muddy dark-brown skin, a crude rope belt, a few torn yellow talismans stuck to its body (exorcists failed against it before).
- Long wet black hair over a small face, white eyes. No blood.
- Colors: dark brown, rope tan, talisman yellow.
```

동작 보충: idle = 거친 숨 · move = 쿵쿵 느린 걸음 · 일반 준비 = 한 손을 옆으로 크게 빼기(대기 자세와 확실히 다르게) → 한 손 후려치기 · 강공격 준비 = 두 손을 머리 위로 깍지 끼고 치켜듦 → 두 손 내려찍기(앞 땅에 충격) · death = 앞으로 쓰러지며 진흙처럼 무너짐.

### 4-4. 장산범 — 빠른 기습 · `ArtSource/yokai/jangsanbeom/` · P2 · 게임 크기 112x200px (네발)

참고: `concept/yokai_jangsanbeom.png`
배경색 주의: 흰 털이라 **마젠타 #FF00FF 유지** (흰 배경 금지).

```text
Character: Jangsanbeom, the legendary white beast of Mount Jangsan that lures people by imitating human voices.
- A four-legged tiger-like beast covered in long flowing white hair, drawn as a few LARGE hair locks (not many thin strands). Black claws.
- Under the hair, an eerie almost-human pale face with a faint smile.
- Colors: off-white, pale grey shadows, black claws.
```

동작 보충: 네발짐승이라 "가로로 긴" 캐릭터 — 캔버스 `1536x1024`, 4~6프레임이 가로로 넉넉히 들어가게. idle = 낮게 엎드려 고개만 까딱 · move = 미끄러지듯 낮은 질주 · 일반 준비 = 앞발 하나를 들어 올림 → 짧게 앞발 할퀴기 · 강공격 준비 = 몸을 바닥에 붙여 웅크리고 털이 곤두섬 → 길게 덮치기(제자리에서 몸을 쭉 뻗는 자세로, 실제 이동은 게임이 함) · death = 털이 흩날리며 사라짐.

### 4-5. 어둑시니 — 시간 압박 · `ArtSource/yokai/eodukssini/` · P2 · 게임 키 128px (게임에서 키움)

참고: `concept/yokai_eodukssini.png`
배경색 주의: 보라색 캐릭터라 **#00FF00 (초록)** 사용.

```text
Character: Eodukssini, a shadow spirit that grows bigger the longer you look at it.
- A shapeless mass of dark violet-black shadow with smoky edges, two glowing pale eyes. Give it a faint lighter violet rim along its edges so it stays visible on dark backgrounds.
- Colors: violet-black, lighter violet rim, pale glowing eyes.
```

동작 보충: 크기 변화는 게임에서 처리하므로 **모든 시트를 같은 크기 하나로** 그린다. idle = 연기처럼 일렁임 · move = 바닥을 미끄러지듯 흐름 · 일반 준비 = 몸을 웅크림 → 그림자 팔을 앞으로 길게 뻗기(팔이 몸 길이의 2배까지 뻗음, 프레임 간격을 넓게) · 강공격 준비 = 몸이 부풀며 붉은 빛 → 거대한 그림자 팔로 휩쓸기 · death = 빛 속으로 흩어짐.

---

## 5. 보스 — 불가살이 (P3) · `ArtSource/boss/bulgasari/`

참고: `concept/boss_bulgasari.png` (단, 뒤의 문은 그리지 말 것 — 문은 배경 에셋)

```text
Character: Bulgasari, the legendary unkillable monster that eats iron and grows larger.
- A huge chimera: bear-like body, short elephant-like trunk with two small tusks, small rhinoceros eyes, ox tail, tiger-striped legs. Fur made of sharp iron needles. Rusted iron plates fused into its skin.
- Phase 1 colors: dark iron grey, rust brown, tiger-yellow legs.
- Phase 2 (after eating iron): much larger, with glowing MOLTEN ORANGE-GOLD cracks across the body (orange-gold, NOT red — red is reserved for attack warnings).
```

보스 공통: 크기가 커서 **한 시트 최대 4프레임** (아래 6프레임 시트는 `1536x1024`에 프레임 사이 간격을 좁게라도 지킬 것). 강공격 준비는 1페이즈 = 붉은 빛, **2페이즈 = 흰 섬광**(몸이 이미 주황이라 붉은 빛이 안 보이므로).

| 파일 | 틀 | 프레임 | 동작 `{action}` | 참고 |
|---|---|---|---|---|
| `p1_base.png` | BASE (`{game_px}`=320) | 1 | phase 1 | concept/boss_bulgasari.png |
| `p1_idle.png` | SHEET | 4 | phase 1 idle, heavy breathing, iron needles bristling | p1_base.png |
| `p1_walk.png` | SHEET | 4 | phase 1 heavy walk forward | p1_base.png |
| `p1_combo.png` | SHEET | 6 | phase 1 NORMAL attack combo: 1-2 short wind-up raising a front paw, 3 claw swipe, 4 second swipe, 5 third swipe, 6 recover | p1_base.png |
| `p1_heavy_windup.png` | SHEET | 4 | phase 1 HEAVY wind-up: rearing up on hind legs with a growing RED glow | p1_base.png |
| `p1_heavy_attack.png` | SHEET | 4 | phase 1 HEAVY attack: slamming both front paws down, red glow fading | p1_base.png |
| `transform.png` | SHEET | 6 | phase change: 1 staggered, 2 grabbing rusted iron from the ground, 3 eating it, 4 body swelling, 5 molten orange cracks spreading, 6 phase 2 roar (the last frame matches phase 2 size) | p1_base.png |
| `p2_base.png` | BASE (`{game_px}`=480) | 1 | phase 2 | p1_base.png |
| `p2_idle.png` | SHEET | 4 | phase 2 idle, molten cracks pulsing | p2_base.png |
| `p2_walk.png` | SHEET | 4 | phase 2 slow stomping walk | p2_base.png |
| `p2_attack_normal.png` | SHEET | 6 | phase 2 NORMAL attack: 1-3 short wind-up pulling the trunk back, 4-6 trunk whip forward and follow-through | p2_base.png |
| `p2_heavy_windup.png` | SHEET | 4 | phase 2 HEAVY wind-up: trunk raised high overhead, the whole body flashing WHITE (not red) | p2_base.png |
| `p2_heavy_attack.png` | SHEET | 4 | phase 2 HEAVY attack: huge iron-trunk slam into the ground | p2_base.png |
| `hurt.png` | SHEET | 4 | frames 1-2 phase 1 hit recoil, frames 3-4 phase 2 hit recoil | p2_base.png |
| `death.png` | SHEET | 6 | phase 2 death: collapsing, the molten cracks cooling to dark grey, crumbling into a pile of rusted iron | p2_base.png |

---

## 6. 스승 — 만신 (P2) · `ArtSource/mentor/`

참고: `concept/mentor_1_mansin.png` (팀 선택 1안). **색 변경**: 컨셉의 옥색 쾌자는 주인공 옥색 도포와 겹치므로 **짙은 남보라(deep indigo)** 로 바꾼다.

```text
Character: the hero's mentor, a great shaman (mansin) guarding a mountain shrine. A woman who looks in her 30s. Warm and calm, but her eyes are sharp.
- Black hair in a low bun with a simple silver binyeo hairpin.
- White sobok (plain white hanbok) under a DEEP INDIGO sleeveless kwaeja overcoat (not green).
- Holds a set of small brass shaman bells in one hand and a folded paper fan in the other.
- Colors: white, deep indigo, brass.
- She is an NPC who stands in the shrine and faces LEFT toward the hero — but draw every sprite facing RIGHT anyway; the game flips it.
```

| 파일 | 틀 | 프레임 | 동작 `{action}` | 참고 |
|---|---|---|---|---|
| `base.png` | BASE (`{game_px}`=120) | 1 | — | concept/mentor_1_mansin.png |
| `idle.png` | SHEET | 4 | standing idle, bells gently swaying, sleeves moving slightly | base.png |
| `talk.png` | SHEET | 4 | talking: small gestures with the fan, head tilting | base.png |
| `bless.png` | SHEET | 6 | teaching a skill: 1 raise the bells, 2-4 ring them in a circle with a soft golden glow, 5 point the fan forward, 6 back to idle | base.png |
| `portraits.png` | 초상 틀 (3장) | 4 | expressions: 1 warm smile, 2 worried, 3 stern/frightening, 4 teasing laugh | base.png, concept/mentor_1_mansin.png |

---

## 7. 이펙트 (P1) · `ArtSource/fx/`

이펙트는 게임에서 **더하기 합성(빛나게)** 으로 쓴다 → 배경 **#000000 (검정)**, 캐릭터 없이 이펙트만.

이펙트 틀 `[FX]`:

```text
Visual effect sprite sheet for a 2D pixel art action game. Effect only, no characters.
Effect: {effect}.
Exactly {frames} frames in ONE horizontal row, left to right in animation order, evenly spaced, each frame centered in its own cell, nothing touching between frames.
The effect is oriented to the RIGHT where it has a direction.
Painted pixel art with glow drawn as bright pixels (no soft blur). Background: perfectly flat solid #000000.
```

| 파일 | 프레임 | `{effect}` |
|---|---|---|
| `staff_slash.png` | 4 | a wide crescent arc trail of a wooden staff swing, pale jade-white light, from thin to full to fading |
| `staff_thrust.png` | 3 | a short straight burst of pale jade-white wind lines from a forward staff thrust |
| `staff_slam.png` | 5 | a ground impact: jade-white shockwave ring spreading along the ground with dust and small rock chips |
| `parry_staff.png` | 4 | parry clash spark: a bright white-gold star flash with sharp sparks bursting outward, then fading |
| `parry_talisman.png` | 4 | parry by talisman: a glowing golden hexagonal seal pattern flashing in front, then shattering into light |
| `talisman_projectile.png` | 4 | a spinning yellow paper talisman flying RIGHT with a short golden light trail (4-frame spin loop) |
| `talisman_burst.png` | 5 | a talisman exploding into golden light and paper shreds |
| `talisman_seal.png` | 6 | a golden circular seal glyph appearing on the ground, pulsing, then fading |
| `yogi_absorb.png` | 6 | pale violet-white wisps of spirit energy swirling inward toward the center and vanishing (energy being absorbed) |
| `hit_spark.png` | 3 | a small white impact burst for hitting an enemy |
| `yokai_dissolve.png` | 6 | dark grey smoke dissolving upward into nothing |
| `heavy_warning.png` | 4 | a RED danger glyph (a sharp red exclamation-like flare made of brush strokes, no letters) flashing above an enemy — the warning signal for a heavy attack |
| `dust_land.png` | 3 | small grey dust puffs on both sides, for landing |
| `gate_portal.png` | 6 | a swirling violet vortex door of mist (loop), seen from the side as a tall oval |

---

## 8. UI (P1: HUD / P2: 대화·메뉴 / P3: 스킬 트리)

컨셉: `concept/ui_1_hud.png`, `concept/ui_2_dialog_menu.png`, `concept/ui_3_skilltree.png` — 이미 좋은 방향이니 **같은 디자인을 정돈한 버전**으로 만든다. 저장 위치 `ArtSource/ui/`.

UI 공통 `[UI]`:

```text
Pixel art game UI sprite sheet, Korean traditional style: hanji paper, dancheong patterns (teal, green, gold), maedeup knots, dark carved wood, jade beads, brush-ink accents. Same design language as the attached concept.
Draw each listed element separately, evenly spaced in a clean grid, nothing touching.
Window frames and bars: ornaments ONLY at the corners and ends, plain uniform middle, so they can be stretched in-engine (9-slice).
No text, no letters, no numbers. Avoid red.
Background: perfectly flat solid #FF00FF.
Elements:
{elements}
```

| 파일 | 참고 | `{elements}` |
|---|---|---|
| `hud.png` (P1) | concept/ui_1_hud.png | 1) health bar frame (wood with dancheong corner ornaments, no hanging tassels) 2) health bar fill strip (jade green) 3) health bar empty strip 4) ONE single spirit bead in three separate states: empty (dark), filled (glowing pale violet-white), just-filled (bright flash) — single beads only, not a row 5) the cord segment that links two beads 6) skill slot frame in 3 states: normal, highlighted (gold edge), disabled (grey) 7) a dark round cooldown overlay 8) weapon indicator round frame with a STAFF icon 9) the same frame with a TALISMAN icon 10) boss health bar frame with iron chains at both ends 11) boss health bar fill (deep violet) |
| `dialog_menu.png` (P2) | concept/ui_2_dialog_menu.png | 1) dialogue box (hanji panel, dark wood frame, corner ornaments) 2) portrait frame (square, wood with dancheong) 3) name tag plaque 4) menu panel with carved wooden lattice (changsal) corners 5) button normal 6) button hover (gold edge) 7) button pressed 8) selection cursor shaped like a small hanging maedeup knot 9) divider (thin brush-ink stroke with a knot in the middle) |
| `skilltree.png` (P3) | concept/ui_3_skilltree.png | 1) skill node frame in 4 states: locked (dark stone), available (faint glow), learned (bright jade-gold), selected (gold outline) 2) connection line pieces: straight and corner, each in unlearned (faint ink dots) and learned (glowing brush line) 3) STAFF branch emblem (crossed wooden staffs) 4) TALISMAN branch emblem (a fan of yellow talismans) 5) skill tree background as an unrolled hanji scroll with wooden rollers on the left and right ends |
| `icons.png` (P1) | concept/ui_1_hud.png, concept/ui_3_skilltree.png | small icons, each on its own: 1) health (jade heart) 2) spirit bead 3) staff 4) talisman 5) Joseon coin (sangpyeong tongbo) 6) key 7) skill icons, 12 provisional: staff sweep, staff thrust, staff slam, spinning staff, single talisman throw, triple talisman fan, sealing talisman on the ground, talisman barrier, paper clone, spirit chain, gauge boost, life talisman |

---

## 9. 배경 (P2: 사당·1구간 / P3: 2·3구간·보스)

컨셉: `concept/bg_*.png` — 그림체와 색은 그대로 쓰되, 게임에 바로 쓸 수 있게 **레이어별로 따로** 만든다. 게임 해상도 1920x1080, 화면이 좌우로 스크롤된다. 저장 위치 `ArtSource/bg/<장소>/`.

레이어 틀:

- **원경 `far.png`** — `1536x1024`, 배경색 없이 화면을 꽉 채움.

  ```text
  FAR background parallax layer of {place}: sky and distant scenery only, no ground and no foreground objects. Low contrast, muted, so characters stand out. The left and right edges must tile seamlessly when repeated horizontally. Fill the whole canvas.
  ```

- **중경 `mid.png`** — `1536x1024`, 배경 **#FF00FF**.

  ```text
  MID background parallax layer of {place}: middle-distance buildings, trees and silhouettes, sitting on the lower part of the canvas. Slightly more contrast than the far layer but still muted. Left and right edges tile seamlessly. Everything that is not part of the layer is perfectly flat solid #FF00FF.
  ```

- **근경 `near.png`** — `1536x1024`, 배경 **#FF00FF**.

  ```text
  NEAR foreground parallax layer of {place}: a few dark foreground elements (grass, branches, posts) along the very bottom and edges only, leaving the middle of the screen empty. Left and right edges tile seamlessly. Everything else is perfectly flat solid #FF00FF.
  ```

- **타일 `tiles.png`** — `1024x1024`, 배경 **#FF00FF**.

  ```text
  Ground tileset for {place}, arranged on a clean 8x8 grid of equal square cells (each cell is one tile, 128x128 in this image, representing a 64x64 game tile).
  Include: ground top edge (left, middle, right), ground fill, ground side walls (left, right), inner corners, a floating platform (left, middle, right pieces), and 6 small decorations (grass tufts, small rocks).
  Tiles must connect seamlessly. The walkable top surface is clearly readable. Unused cells are flat #FF00FF.
  ```

- **소품 `props.png`** — `1536x1024`, 배경 **#FF00FF**.

  ```text
  Separate prop sprites for {place}, each drawn on its own with space around it, side view: {props}.
  ```

| 폴더 | 우선 | 참고 | `{place}` | 만들 레이어 | `{props}` |
|---|---|---|---|---|---|
| `shrine/` | P2 | concept/bg_0_shrine.png | the shrine hub OUTSIDE the gate: a small Korean mountain shrine at dusk, warm orange lantern light against blue dusk, a seonangdang sacred tree with five-colored cloth strips, onggi crocks, a stone lantern; on a far mountaintop the dark gate to the underworld | far, mid, near, props (타일 없음 — 평평한 땅 한 줄이면 됨, `tiles.png` 대신 props에 "a flat stone-wall ground strip" 추가) | the shrine building front, the sacred tree with cloth strips and stone cairn, onggi crocks, a stone lantern, a Korean wooden gate (iljumun style, NOT a torii), a flat stone-wall ground strip, the gate entrance door glowing violet (to enter the run) |
| `zone1/` | P2 | concept/bg_1_entrance.png | zone 1, just INSIDE the gate: a twisted imitation of the human world — a ruined Korean mountain temple and an abandoned village at night, with floating chunks of ground and buildings, a moon that is too large and pale, thin fog; cold blue-grey | far, mid, near, tiles, props | a weathered Korean stone pagoda (seoktap, Korean style only), two broken jangseung totem poles, a collapsed thatched-roof house, a broken stone wall section, a dead tree |
| `zone2/` | P3 | concept/bg_2_hwangcheon.png | zone 2, the road to the underworld (hwangcheon-gil): a dark slow river with reeds and a long wooden bridge, floating blue cheongsachorong lanterns, sickly teal and yellow-green sky; Korean pavilions only (not Chinese) | far, mid, near, tiles, props | a decorated funeral bier (kkot-sangyeo), a floating BLUE cheongsachorong lantern (keep red parts tiny), reeds, a wooden bridge segment, a broken grave marker |
| `zone3/` | P3 | concept/bg_3_threshold.png | zone 3, the threshold of the underworld: an underground void with enormous twisted roots, floating broken stone stairs, hanging chains with torn yellow talismans, faint ghostly hands in the far background, a sickly violet glow from deeper below. VERY DARK overall, BUT the walkable ground band must stay readable with a subtle cool rim light | far, mid, near, tiles, props | a twisted root column, a hanging chain with torn talismans, a broken stone stairway piece, a small stone shrine lantern, a ghostly hand reaching from a wall |
| `boss/` | P3 | concept/bg_boss_door.png | the boss arena at the deepest point: a colossal ancient Korean stone door carved with dokkaebi demon faces (gwimyeonwa style), forced open with violet light and mist pouring in, thick iron chains and torn talismans hanging from it; a wide flat stone floor, NO floating platforms; keep the middle height of the screen fairly clear so the boss's poses are readable | far, mid, tiles, props (근경 없음) | a broken iron chain pile, a pile of rusted iron scraps (the boss eats iron), a carved dokkaebi-face stone pillar, a torn talisman banner |

---

## 10. 다 만든 뒤 (Claude가 할 일 — Codex는 하지 않음)

- 배경색 빼기·프레임 자르기·크기 맞추기·도트 보정은 Claude가 `Tools/sprites/` 스크립트로 처리한다 (마젠타·초록·검정 배경 지원 추가 예정).
- 그 결과를 게임(Unity)에 넣고 캡처로 확인한다.
