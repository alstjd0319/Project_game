# 패링 로그라이크 (가제) — Claude Code 가이드

> ⚠️ **전역 `CLAUDE.md`(`C:\Users\cogks\.claude\CLAUDE.md`)의 규칙은 이 프로젝트에 적용하지 않는다.**
> 그 파일은 Flutter 앱(UniTask)용(Dart 문법, Riverpod, go_router, 폴더 구조 등)이다. 이 프로젝트는 Unity(C#)이며, 아래 규칙과 [`기획서.md`](기획서.md)를 따른다.

## 지금 어디까지 왔나 (세션을 새로 시작하면 먼저 확인)

- **졸업 프로젝트** (2인 팀). 2026-10-01 교수님 중간 점검. **약 10주 안에 완성**해야 함.
- **위치·버전 관리**: `C:\Users\cogks\Documents\ParryRoguelike`. 2026-10-01 이전 이름은 `게임제작테스트2`라 옛 기록에 그 이름이 나온다.
  - GitHub 비공개 저장소 [JTW16/ParryRoguelike](https://github.com/JTW16/ParryRoguelike), 브랜치 `main`. 이 PC의 Git은 JTW16 계정으로 로그인돼 있다.
  - 커밋·푸시는 사용자가 요청할 때 한다.
  - `Recordings/`(녹화 영상)와 `Library/` 등 캐시는 `.gitignore`로 제외.
  - 에셋을 넣을 때는 라이선스(재배포 금지 여부)를 확인한다. 나중에 공개로 바꿀 수 있기 때문이다.
- **진행 상황·다음 단계**: 노션 개발 로그 맨 위 "한눈에 보기" (아래 링크) — 만든 것 표, 개발 로그 요약(현재 25장까지), 다음 단계
- **다른 노션 페이지** (둘 다 개인 페이지, 공유·이동은 사용자가 결정)
  - [기능 가이드](https://app.notion.com/p/3eb29ac9bdf08155b519c273bb847302): 교수님 설명용. 한 줄 소개 · 차별점 · 기능 24개 + 캡처(`GuideCaptureTests`)
  - [스토리 (아이디어)](https://app.notion.com/p/3eb29ac9bdf081b2ad21ee3ab5e34069): 기획서 10장과 같은 내용, 팀원 상의 필요
- **최근 완료 (2026-09-30~10-01)**
  - 증강 12개 효과 구현 + 데미지 공식 확정 (기획서 5.1·5.3)
  - F1 패널: 증강 골라 받기 탭 · 게이지 무한 · 근접/원거리 몬스터 끄기
  - 근접 몬스터 = 예비 모션 → 몸에서 한 번에 베기 (원거리는 투사체)
  - 스토리 초안 (기획서 10장), 기능 가이드 페이지
  - 테스트 83개 통과
- **10주 계획 (사용자 승인, 기획서·노션엔 저장 안 함 — 사용자 요청)**
  - 범위: 3구간 × 방 3~4개(웨이브) · 몬스터 5종 안팎 · 최종 보스 1(2페이즈) · 무료 에셋 · 기본 UI · 그림+글 프롤로그/엔딩
  - 주차
    - 1: 손맛 튜닝·팀 합의·에셋 선정
    - 2: 런 구조 ("네모로 한 판" ⭐)
    - 3: 몬스터 3종 + 다수전
    - 4~5: 에셋 입히기 ("그래픽 입힌 한 판" ⭐)
    - 6: 보스
    - 7: UI·튜토리얼·스토리
    - 8: 사운드·밸런스 (기능 동결 ⭐)
    - 9: 외부 테스트
    - 10: 버그·빌드·발표
  - 밀리면 보스 페이즈·몬스터 수부터 줄인다.
- **미결정 사항** (임의로 확정하지 말 것): 3장 회피 판정 규칙 차별화(B안, 보류) · 다수전 공격 겹침 대책(보류 — 사용자가 "나중에 차차") · 스토리 세부(오누이·결말 톤) · 9장 TODO. 증강 수치는 전부 가안 (사용자가 플레이해 보고 조정 예정)

## ⚠️ 궁수·스왑 시스템 폐기 (2026-10-08)

- 사용자 결정: **궁수와 그에 딸린 시스템을 전부 폐기**하고 전사 단독으로 간다. 지운 것: 궁수 캐릭터, 스왑(강제·수동 D키·무적·등장기), 회피(백스텝·퍼펙트 회피 시간정지·바람개비), 화살 투사체(`PlayerProjectile`), 궁수 전용 증강 4개, 스왑 연계 증강(교대 공명·등장 충격파), 궁수 스프라이트·회피 효과음, 튜닝 패널의 궁수·회피 항목.
- 남은 증강 6개: 공용(넓은 판정·연쇄 방어·강공격 사냥꾼), 전사(지척의 일격·피의 패링·돌진 베기).
- **아래 `기획서.md`는 아직 두 캐릭터 기준 문장이 많다** — 위 폐기 결정이 우선이며, 기획서 갱신은 사용자 확인 후 한다. 코드에서 `CharacterKind`는 `Player` 하나, `PlayerParty`는 체력·피격 표시만 담당한다.
- 지금 규칙: 패링 성공 → 같은 자리에서 근접 반격(강공격이면 더 큰 연출). 스왑이 없으니 "방어 성공 = 항상 스왑" 규칙도 사라졌다.

## ⚠️ 증강 시스템 폐기 (2026-10-13)

- 사용자 결정: **증강 개념을 쓰지 않는다.** 지운 것: `AugmentManager/Catalog/Definition/Effect`, `RunModifiers`, 증강 6개 파일(`Progression/Augments/`), 레벨업 증강 3택 창(`AugmentChoiceUI`), C 증강 목록(`AugmentListPanel`, `Controls.AugmentList`), F1 패널 증강 탭, HUD 증강 개수 표시, 증강 테스트·캡처.
- 남긴 것: `PlayerLevel`(경험치·레벨, `LeveledUp` 이벤트)은 그대로 — 레벨업하면 HUD에 "레벨 업!" 알림과 효과음만 나오고 **게임은 멈추지 않는다**. 스킬 트리 등 새 성장 보상은 팀 결정 뒤에 `LeveledUp`에 연결한다. 데미지 계산은 `DamageCalc`에서 기본 × 캐릭터 배율만 쓴다 (보너스 층이 필요하면 다시 붙인다). `PlayerMotor.Dash(dir, extraDistance)`의 추가 거리 인자는 남아 있다.
- 아래 본문의 증강 관련 설명(5장 등)과 `기획서.md`의 증강 장은 더 이상 유효하지 않다.

## 런처 · 빌드 (2026-10-14)

- `Editor/GameBuilder.cs`: 독립 실행형 빌드. 메뉴 Tools → ParryRL → 게임 빌드 (macOS / Windows), 배치는 `-executeMethod ParryRL.EditorTools.GameBuilder.BuildMac|BuildWindows`. 씬이 없으면 `PrototypeSceneBuilder.Build`로 먼저 만들고, 씬은 Start(0) → Prototype(1). 빌드 때 PlayerSettings를 맞춘다: 제품명만 "귀문"으로 맞춘다 — **해상도·전체화면 모드·창 크기 조절은 코드가 덮어쓰지 않고 에디터의 Project Settings → Player 값(`ProjectSettings.asset`)을 그대로 쓴다** (2026-10-16 이전엔 1600×900 창 모드로 덮어써서 에디터에서 바꾼 설정이 런처 빌드에 안 먹었다). 지금 값은 `fullscreenMode: 2`(Maximized Window). 결과는 `Builds/`(gitignore).
- `Tools/launcher/귀문_실행.command`(macOS) · `귀문_실행.bat` → `귀문_실행.ps1`(Windows, **UTF-8 BOM 필수**): 빌드가 없으면 Unity 배치로 빌드한 뒤 실행. Unity가 프로젝트를 열고 있으면 배치 빌드가 막히니 사본에서 빌드하거나 에디터 메뉴를 쓴다. `바탕화면에_설치.command`(macOS)는 빌드를 `~/Desktop/귀문.app`으로 복사하고, `바탕화면에_설치.bat`(Windows)는 `귀문.lnk` 바로가기를 만든다. Mac 빌드·바탕화면 설치는 이 PC에서 확인됨(14초, 177MB, 앱이 뜨고 예외 없음), **Windows 빌드는 이 PC에 모듈이 없어 확인 못 했다.**
- 빌드에서 튜닝 파일은 `Tuning/tuning.json`이 아니라 `persistentDataPath`(제품명 폴더)의 `tuning.json`을 쓴다 (`GameTuning.FilePath`).

- **막은 직후 공격 (2026-10-14)**: 패링 판정 창이 남아 있어도 **한 번 막은 뒤에는 일반공격(A)이 나간다** (`PlayerCombat.TryAttackSkill`: 막기 전 활성 중에만 공격 불가, `PlayerDefense.SucceededThisActive`). 공격 직후·같은 프레임에 패링(S)을 켜는 건 원래 된다.

## 시작 화면 (2026-10-14)

- `Assets/Scenes/Start.unity`가 빌드 **0번 씬**, 게임(`Prototype.unity`)이 1번. `PrototypeSceneBuilder.Build`가 둘 다 만들고(`BuildStartScene`만 따로: 메뉴 Tools → ParryRL → 시작 씬 생성), 씬 직접 편집 금지(생성물).
- `StartMenu`(UI/StartMenu.cs): 코드로 만든 uGUI. 사당 배경(`ParallaxBackground.forcedZone = 0`)을 `MenuCameraPan`이 천천히 흘린다. 주인공은 세우지 않는다 (`StartMenu.heroArt`를 채우면 `MenuHero`가 대기 모션으로 서 있게 할 수 있다). 귀문 빛 = 보라색, 제목 "귀문" + 부제 "패링 로그라이크 (가제)".
- 메뉴는 `Entries` 목록에서 버튼을 만든다 → **기능 추가는 `StartMenu.Awake`에 `AddEntry("이어하기", 동작, enabled, hint)` 한 줄** (비활성 항목은 회색·선택 건너뜀 — 아직 없는 기능 자리 표시로 쓸 수 있다). 조작: ↑/↓·마우스 올리기 선택, Enter/Space/클릭 확인. 종료는 `StartMenu.QuitHandler`(에디터에선 플레이 종료, 빌드에선 `Application.Quit`; 테스트가 갈아끼움).
- 게임 → 시작 화면: 일시정지(ESC)·게임오버 화면의 "시작 화면 (M)" 버튼 또는 M 키 → `GameManager.GoToStartMenu()` (판은 버려지고 timeScale 1로 복귀). 일시정지 화면 버튼은 계속(ESC) · 다시 시작(R) · 시작 화면(M), 게임오버는 다시 시작 · 시작 화면. 오버레이 버튼은 `Hud.CreateOverlay`에 `(라벨, 동작)`을 더 넘기면 늘어난다.

## 귀문 에셋 적용 (2026-10-13)

- 새 콘셉트 "귀문"(퇴마사 / 봉·부적 / 창귀·도깨비불 …)의 그림 묶음(`ArtSource/gwimun/안내.md`가 원본 안내)을 **게임에 있는 것만** 적용했다. 옛 전사 그림·옛 근접 몬스터 그림·Project_Game 몬스터 스프라이트·옛 공격 이펙트는 삭제.
- **규격 변경**: 화면 1080p 배율 1 → **1유닛 = 90px** (이전 45px). 기준점(피벗)은 manifest 값(땅 캐릭터 (0.5, 0.0714) / 도깨비불 몸 중심 (0.5, 0.3304)). 프레임은 시트를 `Assets/Art/**/<이름>_<시트>_<번호>.png`로 잘라 두었고 `PrototypeSceneBuilder`가 임포트·묶는다.
- **플레이어(퇴마사)**: 무기별 그림 묶음 `StaffArt`(봉=근접) / `TalismanArt`(부적=원거리) — 대기·달리기·패링·반격·일반공격(봉 휩쓸기 / 부적 부채꼴) + 공용 점프·대시·피격·사망 + 무기 교체(`hero_swap`)는 Q로 바꿀 때 재생. `PlayerSprite`가 `PlayerCombat.Weapon`에 맞춰 묶음을 고른다.
- **몬스터**: 근접 = 창귀, 원거리 = 도깨비불(떠 있어 그림 기준점 = 판정 네모 가운데). 일반 공격 시트 6장은 앞 3장 = 예비 모션, 뒤 3장 = 베기/발사로 나눠 쓴다. 도깨비불 불덩이는 투사체에 청록(일반)·빨강(강공격) 반복 재생.
- **이펙트**: 봉 반격(찌르기)·일반공격(궤적)은 판정 박스 안에 **비율 유지**로 맞춤, 패링 성공(봉/부적별)·부적 화살·폭발·타격·요괴 소멸·강공격 경고·착지 먼지는 원본 크기 × 배율(판정 무관).
- **보류(게임에 아직 없음)**: 두억시니·장산범·어둑시니·불가살이(보스)·만신(사당 NPC), 배경(사당·구간·보스방)·UI 조각, 요기(게이지 대체)·스킬 트리(증강 폐지)·사당·스토리, 봉 내려찍기/부적 봉인 스킬 시트, 귀문 소용돌이·요기 흡수·봉인진 이펙트. 효과음·음악 없음. 새 규칙(회피·교대 삭제, 패링만)은 이미 이 프로젝트가 그렇게 되어 있다.
- **대시 게이지 무료 (2026-10-13)**: `PlayerCombat.moveSkillFree`(기본 켜짐)이면 대시(Shift)가 게이지를 쓰지 않는다 — 쿨타임 1초만. 일반공격(`attackSkillFree`)에 이어 둘 다 무료라 게이지는 지금 쓰이는 곳이 없다 (패링으로 차기만 함; 용도는 요기·스킬 트리 결정 뒤). 테스트는 `TestScene.LoadPrototype`이 둘 다 유료로 되돌려 기존 게이지 규칙 테스트를 유지한다.
- **대시 연속 2번 (2026-10-13)**: `PlayerCombat.moveSkillCharges`(기본 2)회까지 연속으로 쓴다. 쓰면 1회분이 줄고 가득 찬 상태에서 쓰기 시작한 때부터 쿨타임(1초)마다 1회분씩 다시 찬다. 스킬바 이름에 "2/2"처럼 남은 횟수 표시, 충전이 0일 때만 쿨타임 회전이 돈다. 테스트는 `LoadPrototype`이 1회로 되돌려 기존 쿨타임 테스트를 유지한다.
- **배경 적용 (2026-10-13)**: 구간별 3겹(`Assets/Art/Backgrounds/<zone>/far·mid·near.png`)을 `ParallaxBackground`가 카메라를 따라 시차 스크롤(먼 0.15 · 중간 0.4 · 가까운 0.7(뒤) / 1.4(앞), 안내.md 속도 비율)한다. 사당·1구간은 near가 플레이어 뒤, 2·3구간은 앞. 그림 높이 1024px = 12유닛(PPU 85.33)로 화면 높이에 맞추고 좌우로 이어 붙여 반복한다 (`RequiredCopies`가 초광폭 32:9까지 빈틈없이 덮는 장수 계산). `BackgroundSet` 에셋은 `PrototypeSceneBuilder`가 만든다(씬 생성 **뒤에** 다시 로드해야 null이 안 된다). 구간 선택은 `GameTuning.backgroundZone`(저장됨, 기본 1구간) — F1 연습 탭 "배경 구간 바꾸기". 바닥·발판 네모는 배경이 비치게 반투명(알파 0.55). 보스방 배경(문 겹침 문제)·props·tiles·UI 조각은 보류.
- 새 콘셉트의 요기·증강 폐지·스킬 트리는 **임의로 구현하지 않는다** — 팀 결정(안내.md 2절)이 정리되면 따로 진행.

## 플레이어 · 무기 전환 (2026-10-08)

- '전사'라는 이름은 폐기하고 **플레이어**로 통일했다 (화면 글자·코드: `CharacterKind.Player`, `AugmentOwner.Player`, `playerAttackMultiplier` 등). (옛 전사 그림 파일은 귀문 에셋 적용으로 삭제됨).
- **Q 키로 근접 ↔ 원거리 무기 전환** (`PlayerCombat.TrySwitchWeapon`, 쿨타임 0.5초, 게이지 소모 없음, 방어 활성 중엔 불가). 대시(Shift)와 패링(S)은 무기와 무관하게 같다.
  - 근접: 일반공격 = 베기(15, 쿨 0.35초, 내딛기), 반격 = 근접 박스(사거리 1.8, 공격 ×1.5).
  - 원거리: 일반공격 = 직진 화살(12, 쿨 0.45초, 속도 16·사거리 12), 반격 = 목표를 따라가는 화살(20, 배율 `rangedAttackMultiplier` 1.0). 원거리 그림은 아직 없어 플레이어 그림(칼) 그대로다 (화면 표시는 스킬창·상태창 색/글자로).
  - 스킬창은 4칸(Shift 이동 / A 일반공격 / S 패링 / Q 무기)이고 A·Q 칸이 무기에 따라 이름·색(근접 붉은색 / 원거리 초록색)이 바뀐다.

## 기준 문서

- **[`기획서.md`](기획서.md)가 게임 규칙의 단일 기준**이다. 구현 전에 관련 장을 읽고, 규칙·수치가 바뀌면 기획서도 함께 갱신한다.
  - 판정 알고리즘·가안 수치표·제작 순서·원칙: 8장
  - 미정 항목: 9장 TODO — 여기 있는 건 임의로 "확정"하지 말고, 임시값으로 구현했다면 가안표에 "임시"로 기록
- 사용자가 세부값을 정하지 않은 튜닝 항목(적 AI, 임시 스킬 수치 등)은 적당한 기본값을 정해 진행하고, 인스펙터 필드로 노출한다. 기획 간 모순이나 구조가 바뀌는 결정만 질문한다.

## 개발 과정 기록 (노션, 포트폴리오용) — 매 작업마다 갱신

- 페이지: [패링 로그라이크 — 개발 과정 기록](https://app.notion.com/p/3eb29ac9bdf0816b9e9cde4395c9a210) (page_id `3eb29ac9-bdf0-816b-9e9c-de4395c9a210`)
- **의미 있는 진행(기능 구현, 기획 변경, 버그 해결, 구조 변경, 도구·환경 변경)이 끝날 때마다** 이 페이지를 갱신한다. 사용자가 따로 말하지 않아도 작업 마무리 단계에서 한다.
- 포트폴리오용이므로 **과정이 드러나게** 쓴다: 어떤 문제가 있었나 → 원인을 어떻게 추적했나(실패한 가설 포함) → 어디를 어떻게 바꿨나 → 결과·검증(테스트 등) → 무엇이 발전했나.
- 페이지 구조: 맨 위 **"한눈에 보기"**(지금까지 만든 것 표 · 개발 로그 요약 표 · 다음 단계) → 프로젝트 개요(차별점 · 두 캐릭터 표 · 핵심 루프 · 용어 정리) → **개발 로그 ①②③…** (단계별 H1 아래에 번호 섹션).
- 새 작업은 마지막 개발 로그 단계 아래에 날짜 멘션을 단 번호 섹션으로 추가한다 (문제는 callout, 결정은 표, 버그는 증상/추적/원인/해결/결과, 화면 변경은 1차·최종 캡처). 성격이 달라지면 새 단계 H1을 연다. **그리고 "한눈에 보기"의 세 곳(만든 것 · 로그 요약 한 줄 · 다음 단계)도 같이 갱신**한다.
- 이미지가 든 블록은 `update_content`의 old_str/new_str에 넣지 않는다 (서명 URL이 만료돼 이미지가 깨질 수 있음). 페이지가 길어 fetch 결과가 파일로 저장되면 PowerShell로 텍스트만 뽑아 읽는다.
- 업데이트는 `notion-update-page`의 `update_content`/`insert_content`로 필요한 부분만 고친다 (`replace_content`로 통째로 덮어쓰지 말 것 — 사용자가 직접 고친 내용이 사라짐). 수정 전에 `notion-fetch`로 현재 내용을 먼저 읽는다.
- 과장하지 않는다. 확인 안 된 것(예: 실제 플레이 감각)은 확인 안 됐다고 쓴다.

## 환경

- **Unity 6000.6.0f1**, Built-in RP, 2D
- 입력: 레거시 Input Manager (`UnityEngine.Input`) — Input System 패키지 없음
- UI: uGUI (`com.unity.ugui`), HUD는 코드로 생성 (`Assets/Scripts/UI/Hud.cs`)
- 테스트: Unity Test Framework (PlayMode)
- 녹화: Unity Recorder `5.1.7` (Window → General → Recorder)

## 구조

```
Assets/
├── Scripts/                  # asmdef: ParryRL (네임스페이스 ParryRL)
│   ├── Core/                 # GameManager, GameTuning(튜닝 값·JSON), CameraRig, Controls(조작키), GameAssets, Box, GameEnums
│   ├── Combat/               # DamageCalc(플레이어 딜 계산 유일한 곳), HitFeel, Fx, Sfx(코드 합성 효과음), EnemyAttack, MeleeStrike, PlayerProjectile, WorldBar
│   ├── Enemy/                # EnemyController, EnemySpawner
│   ├── Player/               # PlayerMotor, PlayerDefense, PlayerCombat, PlayerParty, SkillGauge
│   ├── Progression/          # PlayerLevel(경험치·레벨), AugmentManager(3택 흐름), AugmentCatalog(증강 목록), AugmentDefinition,
│   │                         # RunModifiers("이번 판 보정" 층), AugmentEffect(증강 효과 기반 클래스)
│   │   └── Augments/         # 증강 효과 1개 = 파일 1개 (XxxAugment.cs, 수치는 맨 위 const)
│   ├── UI/                   # Hud, SkillBar, AugmentChoiceUI, AugmentListPanel(C), TuningPanel(F1), UiKit(uGUI 생성 헬퍼)
│   └── Editor/               # asmdef: ParryRL.Editor — PrototypeSceneBuilder
├── Tests/PlayMode/           # asmdef: ParryRL.Tests — CoreLoopTests, ProgressionTests, AugmentTests, EnemyTests, TuningTests, CaptureTests, GuideCaptureTests(노션 기능 가이드용 장면) (+ TestScene 헬퍼)
├── Scenes/Prototype.unity    # ⚠️ 생성물 — 직접 편집하지 말고 빌더를 고칠 것
├── Prefabs/                  # 적 프리팹 (빌더가 생성)
└── Art/                      # Square.png(1×1 유닛 흰 네모), NoFriction 물리 머티리얼 (빌더가 생성)
```

## 코드 규칙

- 모든 스크립트는 `namespace ParryRL` (에디터는 `ParryRL.EditorTools`, 테스트는 `ParryRL.Tests`).
- **플레이 감각 수치는 `GameTuning`(Core/GameTuning.cs)에 둔다** — 타격감·판정 시간·적 텔레그래프/빈도처럼 플레이하며 맞춰야 하는 값. F1 튜닝 패널(UI/TuningPanel.cs)에 슬라이더를 추가하고, 기본값은 기획서 가안표와 맞춘다. 실제 플레이 값은 `Tuning/tuning.json` (사용자가 패널로 바꾼 값) — 가안표를 갱신할 땐 이 파일을 읽어서 반영한다.
- **증강 효과는 `GameTuning`을 직접 바꾸지 않는다** — 튜닝 값은 `tuning.json`에 저장돼 다음 판까지 남는다. 증강은 `RunModifiers`("이번 판 보정" 층)로 따로 계산한다 (최종값 = 튜닝 기본값 + 증강 보정, 기획서 5.2).
  - 증강 추가 = `Progression/Augments/XxxAugment.cs`(`AugmentEffect` 상속) + `AugmentCatalog` 한 줄 + `AugmentTests` 테스트. 필요한 이벤트만 구독하거나(`PlayerCombat.DefenseSucceeded/CounterFired/MoveSkillUsed`, `PlayerParty.SwapEntered/Damaged`, `PlayerDefense.Whiffed`), 보정값(`DefenseWindowBonus`, `DashDistanceBonus`, `PrepareHit`, `DamageBonus`)만 덮어쓴다.
  - **플레이어 딜은 반드시 `DamageCalc`로 계산한다** (기본 × 캐릭터 패시브 × (1 + 증강 보너스 합), 기획서 5.3). 새 공격도 `HitInfo`를 만들어 `MeleeStrike`/`PlayerProjectile`/`DamageCalc.Apply`에 넘길 것 — 직접 `TakeDamage(숫자)` 금지.
- 딜을 넣는 스킬·증강은 기획서 2장 기준(게이지를 소모할 것, 게이지 1칸당 **기본** 딜 약 10~15)을 지킨다. 게이지 쓰는 딜을 추가하면 `AugmentTests`의 1칸당 효율 테스트에도 넣는다.
- 그 외 오브젝트 고유 값(크기, 체력, 색, 사거리 등)은 `[SerializeField] private` 필드 + 기획서 가안값을 기본값으로. 필요하면 `[Header]`/`[Tooltip]`에 한국어 설명.
- 조작키는 `Controls` 상수로만 참조 (기획서 4.5): ←/→ 이동, Space 점프(2단·고정 높이, ↓+Space 발판 내려가기), S 방어, A 일반공격, Q 무기 전환, Shift 이동 스킬, Enter 수락, ESC 일시정지(일시정지·게임오버 중 M 시작 화면), C 증강 목록, F1 튜닝 패널.
- 보조 창(증강 목록·튜닝 패널)은 평소 접혀 있고 키로 토글, 게임을 멈추지 않는다. 좌측(상태 패널 아래)과 우측에 나눠 서로 겹치지 않게 둔다.
- 입력 처리 전 `GameManager.InputBlocked` 확인 (일시정지·게임오버).
- Unity 6 API 사용: `Rigidbody2D.linearVelocity`, `FindAnyObjectByType` / `FindObjectsByType` (구 API 금지).
- 주석·UI 문자열은 한국어. 주석은 "왜"를 설명할 때만.
- 테스트가 입력 없이 동작을 호출할 수 있도록, 플레이어 행동은 `TryXxx()` 공개 메서드로 두고 `Update`의 키 입력은 그걸 호출만 한다.

## 반드시 지킬 원칙 (기획서 8장)

- **판정-시각 일치**: 눈에 보이는 네모 = 판정 범위. 스프라이트 1×1 유닛 + `BoxCollider2D.size` 기본값 (1,1), 크기는 `localScale`로만. 콜라이더 `offset`/`size` 보정 금지. 판정이 있는 오브젝트의 스케일을 연출 목적으로 바꾸지 말 것 (플레이어 연출은 색상으로).
- **판정은 실제 콜라이더 오버랩**: 적 공격 → `OnTriggerEnter2D`, 플레이어 공격 → 보여주는 박스와 같은 크기의 `Physics2D.OverlapBox*` (호출 전 `Physics2D.SyncTransforms()`). 거리 계산(`Vector2.Distance`) 판정 금지.
- **텔레그래프는 고정 속도**: 원거리 투사체는 거리와 무관하게 일정 속도로 이동. 예외 — 근접 베기는 예비 모션 길이가 반응시간이고 베기 자체는 한 번에 나간다 (느리게 뻗으면 어색하다는 피드백, 기획서 3.1).
- **연출은 unscaled time**: 히트스탑이 `Time.timeScale = 0`을 쓰므로, 이펙트·흔들림·UI 애니메이션은 `Time.unscaledDeltaTime` / `WaitForSecondsRealtime`.
- **화이트박스 단계**: 흰 네모 스프라이트 하나만 재사용하고 색상·크기로 구분. 외부 에셋 추가 금지 (5단계 전까지).
  - 예외(2026-10-08, ParryRoguelike-main2 병합): 퇴마사와 창귀·도깨비불은 도트 **애니메이션**(`Assets/Art/Characters/Hero`, `Assets/Art/Enemies/*`)을 쓴다 (위 "귀문 에셋 적용" 참고). 원본 안내는 `ArtSource/gwimun/안내.md`, 가공 도구 사본은 `Tools/gwimun_sprites/`(원 저장소 폴더 구조에서 실행). `PlayerSprite`/`EnemySprite`가 판정 네모 위에 그림을 그리고(판정은 그대로, F1 "판정 상자 보기"로 겹쳐 확인), 그림이 없는 원거리 몬스터는 아래 Project_Game 스프라이트를 쓴다. 공격 이펙트 그림(`Assets/Art/Fx`: 전사 반격·일반공격, 몬스터 베기)은 `AttackFxArt`/`FxClip`이 판정 박스 크기로 늘려 그린다 (그림 = 판정 범위). 전사는 벨 때 몸이 0.45유닛 내딛는다(`PlayerMotor.Lunge`, 무적 아님). 빌더에서 `PlayerSprite`는 `PlayerDefense`를 붙인 **뒤에** 추가해야 한다 (RequireComponent로 방어 컴포넌트가 둘이 되는 버그).
  - 예외(2026-10-01, Project_Game 병합): 플레이어·근접/원거리 몬스터는 `Assets/Sprites/ParryPrototype` 도트 스프라이트, 방어 성공음은 `Assets/Audio/ParryPrototype` wav를 쓴다. 스프라이트는 `SpriteDrawMode.Sliced`+`size=(1,1)`로 몸 크기(스케일)에 맞춰 늘려 그리므로 판정-시각 일치가 유지된다. 스프라이트 필드를 비우면 흰 네모/합성음으로 돌아간다. 퍼펙트 방어(`GameTuning.perfectWindow` 0.08초)는 연출 전용(`SuccessEffects`)이며 판정·게이지는 일반 성공과 같다.

## 작업 흐름

씬·프리팹 구성을 바꾸려면 `Assets/Scripts/Editor/PrototypeSceneBuilder.cs`를 수정한 뒤 다시 생성한다 (에디터 메뉴: **Tools → ParryRL → 프로토타입 씬 생성**).

**씬 생성 + 전체 테스트(캡처 포함)는 `Tools/run_tests.ps1` 하나로 돌린다** (한 번에 4~5분):

```powershell
powershell -ExecutionPolicy Bypass -File Tools\run_tests.ps1                                   # 씬 생성 + 전체 테스트
powershell -ExecutionPolicy Bypass -File Tools\run_tests.ps1 -SkipBuild -Filter ParryRL.Tests.CaptureTests   # 캡처만
```

- 에디터가 이 프로젝트를 열고 있으면 스크립트가 알아서 `%TEMP%\ParryRL_verify` 사본에서 돌리고 캡처를 원본 `Captures/`로 복사한다. 로그는 `%TEMP%\ParryRL_logs\`.
- 씬 구성(빌더)을 바꾸지 않았으면 `-SkipBuild`로 시간을 아낀다.

- 규칙(판정·게이지 등)을 바꾸면 테스트를 추가·수정하고 통과를 확인한다. 새 PlayMode 테스트는 씬을 `yield return TestScene.LoadPrototype();`으로 연다 — 사용자의 `tuning.json`을 무시하고 기본값으로 돌리기 위함 (안 그러면 사용자가 패널로 값을 바꾸는 순간 테스트가 깨진다).
- 로그에서 `error CS` / `warning CS` / 예외가 없는지 확인한다.
- **UI 글자 폭은 해상도(캔버스 배율)에 따라 달라진다.** 작은 Game 뷰에서는 같은 글도 더 넓게 그려져 줄바꿈이 생긴다. 글이 들어가는 칸을 고정 높이로 두지 말고 `Text.preferredHeight`로 잡은 뒤, 캔버스 `scaleFactor`가 바뀌면 다시 배치한다. 목록처럼 길어질 수 있는 창은 높이 상한 + 스크롤. 캡처는 1920×1080 외에 작은 해상도(`Capture(name, 640, 360)`)로도 찍어 확인한다.
- **화면에 보이는 변경(UI·연출)은 반드시 캡처로 확인한다.** `Assets/Tests/PlayMode/CaptureTests.cs`에 장면을 추가하면 테스트 실행 시 프로젝트 루트 `Captures/*.png`로 저장된다 (캔버스를 ScreenSpaceCamera로 바꿔 RenderTexture로 렌더). 이미지를 직접 열어보고 겹침·가림·대비 문제를 고친 뒤 보고한다. 캡처만 다시 찍을 땐 `-testFilter ParryRL.Tests.CaptureTests`.
- 사용자는 보통 에디터를 꺼둔다. 에디터가 열려 있어 배치모드가 프로젝트 잠금으로 실패하면 `Assets`/`Packages`/`ProjectSettings`만 스크래치 폴더(ASCII 경로)에 복사해서 그 사본으로 빌드·테스트한다.
- 한글 경로 주의: 한글 경로가 들어간 `.ps1` 파일은 **UTF-8 BOM**으로 저장해야 Windows PowerShell 5.1이 경로를 제대로 읽는다.
- `EditorSceneManager.NewScene`은 사용되지 않는 에셋을 언로드한다 — 씬에 연결할 에셋은 씬 생성 **후에** `AssetDatabase.LoadAssetAtPath`로 로드할 것.

## 도력 (2026-10-14)
- 화면에 보이는 "게이지"는 전부 **도력**으로 바꿨다 (클래스·변수 이름 `SkillGauge`/`_gauge`는 그대로). 패링/방어 성공 시 충전되는 규칙은 그대로.
- HUD 도력은 네모 20칸 대신 **구슬(염주) 20개**: 줄에 꿰인 둥근 구슬, 찬 구슬은 숨 쉬듯 빛나고(앞 10개 옥빛 · 뒤 10개 귀문 보라) 가장 최근에 찬 구슬이 가장 밝다. 구슬 그림은 `FxSprites.Glow/Ring`(코드 생성)을 UI에서도 쓴다. 스킬바 코스트 배지도 보라색.
- 배치모드 PlayMode 테스트는 `-nographics` 없이 돌릴 것 (스프라이트 렌더에서 네이티브 크래시, 종료 코드 139).

## 런처 자동 갱신 (2026-10-14)
- `귀문_실행.command`는 Assets/Packages/ProjectSettings에 빌드(`Builds/Mac/귀문.app`)보다 새로운 파일이 있으면 자동으로 다시 빌드한다(에디터가 열려 있으면 이전 빌드로 실행).
- `바탕화면에_설치.command`는 이제 빌드 복사본이 아니라 **바로가기 앱**을 만든다(프로젝트 경로 고정, 바뀐 게 있으면 터미널에서 런처 실행). 배포용 복사는 `--copy`. 윈도우(`.bat/.ps1`)는 아직 이전 방식.

## 스킬 D / F (2026-10-14)
- `Controls.Skill1=D`, `Skill2=F`, `PlayerAction.Skill1/Skill2`, `PlayerCombat.TrySkill(1|2)`. 도력 D 3 / F 5 (`TrySpend`라 연습 스위치 "도력 무한"이 적용), 쿨타임 D 3초 / F 6초(무기를 바꿔도 공유). 패링을 막 켠 동안(못 막음)은 못 쓰고, 막은 뒤엔 바로 가능(일반공격과 같은 규칙).
- 봉: **휩쓸기**(`MeleeStrike` 4.0×2.2, 30, 기절 0.5, 그림은 박스에 꽉 차게 `stretchArt`) · **내려찍기**(모션 정점에서 0.083초 뒤 충격파 박스 3.8×1.6, 45, 기절 1.2, `AttackFxArt.staffSlam`).
- 부적: **부채꼴**(`PlayerProjectile` 5발, 48°, 장당 10) · **봉인진**(`SealZone`: 3.2×1.6 박스, 3.5초, 0.2초마다 기절 갱신 + 0.7초마다 8 데미지(밀치지 않음), 그림 3장→반복→끝 2장, `AttackFxArt.talismanSeal`).
- 모션: `CharacterArt.skill1/skill2`(+`skillNStartFrame`) — 빌더가 `staff_skill_sweep/slam`, `talisman_skill_fan/seal` 시트를 넣는다. 일반공격(A)도 같은 휩쓸기/부채꼴 모션을 쓴다(시트가 4개뿐).
- 스킬바는 6칸(Shift·A·D·F·S·Q). 수치는 `PlayerCombat` 인스펙터 필드(F1 패널엔 아직 없음).
- (수정) 바로가기 앱이 프로젝트 폴더를 `find`로 검사할 때 프로젝트가 Desktop/Documents/Downloads 안에 있으면 macOS 개인정보 보호에 막혀 "바뀐 게 없다"고 오판해 옛 빌드를 열었다. → 프로젝트를 `~/ParryRoguelike-main`(홈 바로 아래)으로 옮겼고, 바로가기는 바뀐 게 있을 때만 터미널에서 런처를 실행한다(보호 폴더 안에 있으면 항상 터미널 경유).

## 사당(대기 씬) (2026-10-15)
- 빌드 순서가 **Start(0) → Hub(1) → Prototype(2)** 로 바뀌었다 (`PrototypeSceneBuilder.SetBuildScenes`, 없는 씬은 건너뜀). `StartMenu.GameSceneName`은 이제 `Hub`("게임 시작" → 사당), 플레이 씬 이름은 `GameManager.RunSceneName`, `GameManager.LoadRun()/LoadHub()`. 씬은 빌더가 만든다: `Build()`가 Hub도 만들고(`BuildHubScene`, 메뉴 Tools → ParryRL → 사당 씬 생성), 씬 직접 편집 금지.
- Hub = 폭 ±16, 사당 배경 고정(`forcedZone 0`), 적·스포너 없음, 플레이어(전투 포함)는 플레이 씬과 같은 `CreatePlayer` 헬퍼로 만든다(좌측 x=-10에서 시작해 NPC를 지나 오른쪽으로). 오른쪽 끝 `ShrinePortal`(x=14, 사당 문 그림 `Assets/Art/Props/shrine_gate.png` — 원본 시트 `bg/shrine/props.png` 오른쪽 아래 문을 오려 바깥 마젠타만 투명하게 한 것(미리보기의 `props_04`는 소용돌이 안쪽까지 구멍이 나 있어 안 썼다), ×1.25. 그림이 없으면 `gate_portal` 이펙트 반복 재생으로 대체). **문 연출**(`ShrinePortal`, 판정 없음): 문 안쪽에 **일렁이는 소용돌이**(`AttackFxArt.portalSwirl`: 사용자가 준 8장 스트립 `ArtSource/portal_swirl_strip.png`은 서로 거의 같아서, `Tools/gwimun_sprites/make_portal_swirl.py`가 한 줄씩 좌우로 물결치게 밀어 **16장**(`Assets/Art/Fx/fx_portal_swirl_N.png`)으로 만든다(진폭 7px, 주기 60px, 한 바퀴 돌면 처음으로). 14fps 반복에 코드 흔들림(크기 ±7%·±5%, 좌우 살랑임, 알파 깜빡임, 가까울수록 1.5배)을 더한다. 내용 138×181을 문 열린 부분 1.25×2.1에 맞춰 늘림 — 옛 `gate_portal` 소용돌이는 쓰지 않는다)와 빛무리(`FxSprites.Glow`, 문 뒤)·안쪽 빛을 겹친다 — 플레이어가 가까울수록(7→1.5유닛) 밝아지고, 상호작용 가능한 거리에선 더 환하게 깜빡인다(`Brightness` 0~1) · `Npc_Mentor`(x=-2, 만신 `NpcArt` idle/talk).
- 상호작용(`World/`): `Interactable`(트리거 박스, 기본 크기 1×1 × localScale, 안내 문구) · `PlayerInteractor`(플레이어에 붙음, **몸 콜라이더와 트리거의 실제 오버랩**, E=`Controls.Interact`, 가장 가까운 대상 하나, `Hud.SetPrompt`로 `[E] …` 안내) · `ShrinePortal`(상호작용 → 화면 보라 플래시 + `GameManager.LoadRun`) · `ShrineNpc`(→ `SkillTreePanel.Open`, 항상 플레이어 쪽을 바라봄, 창이 열려 있으면 대화 모션) · `ClipPlayer`(판정 없는 그림 반복 재생).
- `SkillTreePanel`(UI): **창의 틀만** — "스킬 트리는 아직 준비 중입니다" 안내와 닫기. `ContentRoot`가 나중에 스킬 노드를 올릴 자리. 열리면 `GameManager.SetMenuOpen(true)`(입력 차단·시간 정지), E · ESC · 버튼으로 닫는다. **스킬 트리 내용(노드·도력으로 스킬 습득)은 아직 구현하지 않는다 — 사용자 요청.**
- 만신 그림: `귀문_에셋_공유_1013/미리보기/mentor_*_strip.png`를 384×224로 잘라 `Assets/Art/Characters/Mentor/mentor_<시트>_<번호>.png`로 둔다(PIL 한 줄 스크립트로 잘랐다). 초상(`bg/mentor_portraits`)은 아직 안 쓴다.
- 사당으로 돌아오기: 플레이 씬의 일시정지·게임오버 화면에 **"사당으로 (H)"** 버튼/키(`Controls.Hub`, `GameManager.LoadHub`)가 있다. 사당 씬(`GameManager.InHub`)에서는 이 버튼이 없다 (일시정지 버튼 4개 → 사당에서는 3개, 게임오버 3개 → 2개).
- (수정, 2026-10-15) 사당을 넣고도 `GameBuilder`가 씬 목록을 Start·Prototype 두 개로 고정해 둬서 **빌드된 게임에서 "게임 시작"이 안 넘어갔다**(Hub가 빌드에 없어 로드 실패; 에디터 테스트는 빌드 설정을 써서 통과했다). 이제 `GameBuilder.BuildScenes()`가 빌드 설정(`SetBuildScenes`가 Start → Hub → Prototype으로 맞춤)의 켜진 씬을 그대로 쓴다 — 씬을 추가할 땐 `SetBuildScenes`의 목록만 고치면 된다.

## 씬을 넘는 플레이어 데이터 (2026-10-15)
- `PlayerSession`(static, Player/): 체력 · 레벨/경험치 · 도력 · 무기를 `Capture()`로 담고 `Apply()`로 되돌린다. `GameManager.LoadRun/LoadHub`가 떠나기 직전 `Capture`, 새 씬의 플레이어에 붙은 **`PlayerPersistence`**(빌더 `CreatePlayer`가 붙임, `Start`에서 적용)가 `Apply`. 되돌리는 API: `PlayerParty.SetHp`, `PlayerLevel.Restore`, `SkillGauge.SetValue`, `PlayerCombat.SetWeapon`(모두 조용히 — 알림·모션·쿨타임 없음).
- 비우는 곳(`Reset`): `GameManager.Restart`(다시 시작) · `GoToStartMenu` · `StartMenu.StartGame`(새 게임). 쓰러진 채(체력 0) 떠나면 새 씬에서 체력은 가득. **사당 자동 회복**: `PlayerPersistence.Start`가 사당(`GameManager.InHub`)이면 체력을 `Heal`로 가득 채운다("+N" 표시, 도력·레벨·무기는 그대로, 플레이 씬에선 회복 없음). 쿨타임·진행 중인 판 상태(적 등)는 이어지지 않는다.
- 주의: MonoBehaviour는 **파일 이름 = 클래스 이름**이어야 씬에 저장된다 (`PlayerPersistence`를 `PlayerSession.cs`에 같이 두었다가 씬에서 스크립트가 빠지는 문제가 있었다 → 별도 파일).
- 테스트: `TestScene.LoadPrototype/LoadHub`가 `PlayerSession.Reset()`부터 한다. 스킬 트리로 습득한 스킬을 이어갈 땐 `PlayerSession.Data`에 필드를 더하면 된다.

## 런처로 실행이 안 되던 문제 — 스크립트 파일 이름 (2026-10-15)
- 증상: 바탕화면 `귀문.app`/런처로 실행하면 게임이 바로 죽음 (Player.log: `level0 is corrupted! … Position out of bounds`). 원인: `MenuCameraPan`이 `StartMenu.cs` 안에 같이 있어서, 시작 씬(level0)에 스크립트가 GUID가 아닌 임시 번호(`m_Script: {fileID: 1363278542}`)로 저장됨 → 에디터에서는 멀쩡(테스트 통과)하지만 **빌드된 게임은 그 씬을 못 읽는다** (그래서 "게임 시작이 안 넘어감"도 같은 뿌리였을 수 있다). 같은 실수를 `PlayerPersistence`에서도 한 번 했다.
- 규칙: **씬에 올라가는 MonoBehaviour는 반드시 클래스 이름과 같은 파일에 둔다** (런타임에 `AddComponent`만 하는 클래스는 상관없지만 습관적으로 분리). `MenuCameraPan`·`MenuHero`를 별도 파일로 분리했고, `SceneIntegrityTests`가 씬 파일에 GUID 없는 스크립트 참조·Missing 스크립트가 있으면 실패한다.
- 빌드 확인법: 빌드를 실행한 뒤 `~/Library/Logs/DefaultCompany/귀문/Player.log`에 `corrupted`·`Exception`이 없는지 본다.

## 귀문 UI 그림 적용 — 체력 틀·도력 구슬·스킬 아이콘 (2026-10-16)
- 원본 `ArtSource/gwimun/ui/hud.png`·`icons.png`(에셋 폴더 `원본/ui/`의 사본, 마젠타 배경 1536×1024)를 **직접** 오린다: 미리보기 폴더의 `bg/ui/*` 조각은 대충 잘려 있어(안내.md 5절) 안 쓴다. `Tools/gwimun_sprites/cut_ui.py`(+`ui_cutter.py`: 마젠타 제거·덩어리 찾기)가 `Assets/Art/Ui/ui_*.png` 33장을 만든다 (HUD 조각 15장은 상자 좌표를 스크립트에 적어 둠 — 용 장식 틀·부적 메달처럼 붙은 조각은 자동 구분이 안 돼서; 아이콘 18장은 자동). 다시 오리려면 `python3 -I Tools/gwimun_sprites/cut_ui.py` 후 씬 재생성.
- `UiArt`(UI/UiArt.cs, ScriptableObject) → 빌더 `CreateUiArt`가 만들어 `GameAssets.uiArt`에 연결, 코드는 `GameAssets.Ui`로 읽는다(없으면 UI는 예전 네모·글자 모양 — `Hud.BuildStatusPlain`).
- **체력**: 하트 아이콘 + 옥 장식 틀(`hp_frame`) 안쪽 홈 위에 초록 바(`hp_fill`, `Image.Type.Filled`)가 차오른다. 빨강은 적 공격 경고 전용이라 체력도 초록. **도력**: 빈 구슬(`orb_empty`)을 20개 깔고 그 위에 찬 구슬(`orb_full`)을 겹치며 가장 최근에 찬 구슬은 반짝이는 큰 구슬(`orb_spark`). **경험치**: 구리 빈 바 + 보라 채움. 도력 줄 옆엔 등불 아이콘.
- **스킬 아이콘**(`icons.png`의 틀 포함 12종, `UiArt.skillIcons[0..11]`): 대시=3(회오리 봉) · 패링=7(결계 부적) · 일반공격 봉/부적=1(찌르기)/4(부적 바람) · D 봉/부적=0(휩쓸기 초승달)/5(부적 세 장) · F 봉/부적=2(내려찍기)/6(봉인진) · Q 무기=`weaponStaff/weaponTalisman` 메달. 안 쓴 것: 7 결계(패링으로 씀) 외 8 분신 · 9 사슬 여우 · 10 도력 상승 · 11 잎 부적 — 앞으로 생길 스킬용. 못 쓸 땐 아이콘을 어둡게, 아이콘 뒤는 어두운 중립색(메달이 무기 색으로 비치지 않게).
- 아직 안 쓴 조각: `dragon_frame`(큰 바용 — 보스 체력 후보), `purple_fill`은 경험치에 사용 중, `orb_string`·`slot_*`·`cooldown_disc`(아이콘이 틀을 포함해 불필요), 스킬 트리·대화창 시트(`skilltree.png`·`dialog_menu.png`)는 스킬 트리 구현 때.

## 커밋할 때 스크린샷 기록 (2026-10-16, 사용자 요청)
- **화면이 바뀌는 변경(UI·연출·씬·이펙트 등)을 커밋·푸시할 때는 스크린샷 기록도 같은 커밋에 넣는다.** 순서: `CaptureTests`로 최신 캡처(`Captures/`) 확인(새 장면이면 `CaptureTests`에 추가) → 바뀐 장면을 `docs/screenshots/`에 JPG(1280×720)로 저장(전/후가 의미 있으면 "전"은 이전 커밋의 캡처 파일에서 꺼낸다) → `docs/변경기록_스크린샷.md`에 날짜·커밋·전→후 섹션 추가 → 코드와 함께 커밋·푸시. 로직·문서만 바뀌면 스크린샷 없이 글로만 적거나 생략한다. 노션은 정리를 요청받을 때만 갱신한다.
- 보류 중인 과제: 바닥·벽·발판이 반투명 네모(임시) — 에셋 타일(`shrine/props_06` 땅 한 줄, `zone*/tiles_*`)로 그리는 방안(A)을 제안했고 사용자가 "일단 보류".

## 바닥·벽·발판 타일 그림 (2026-10-16)
- 새 시트: `귀문_에셋_공유_1013/원본/bg/<zone1|zone2|zone3|boss>/floor_tiles_clean.png`(1254×1254, 투명, 8×8칸 ≈156.75px)·`floor_tiles_atlas.png`·`원본/bg/floor_tiles_preview.jpg`(원래 위치 `~/Documents/Codex/2026-10-16/new-chat/outputs/`). 안내.md 4절에 설명 추가, 프로젝트 사본은 `ArtSource/gwimun/floor/`. 칸 경계에 보라 선이 있어 **칸 내용 상자를 2px 안쪽으로** 자른다.
- `Tools/gwimun_sprites/cut_floor.py`: 구간별 `SPECS`(역할 → 칸 좌표)대로 `Assets/Art/Floor/<구간>_<역할>_<번호>.png`(top 윗면 · fill 속 채움 · platL/M/R 발판 · zone1/zone3은 topL/R·fillL/R 모서리도 오리지만 아직 안 씀) + `floor_meta.json`(파일별 표면 줄·높이, 구간별 `bodyPx` = 윗면 몸통 높이)을 만든다. 사당은 `shrine_props.png`의 땅 한 줄 조각(`shrine_top_0`). 보스 시트는 아직 안 씀.
- `FloorSet`(Core/, 구간별 ScriptableObject) → 빌더 `CreateFloorSets`(피벗: 윗면·발판 = 표면 줄 가운데, 속 채움 = 위 가운데; `unitScale = 90 ÷ bodyPx`) → **`FloorDecorator`**(World/, `Level` 오브젝트에 붙음): 판정 네모(Ground·Wall·Platform) 위에 타일을 자식으로 깐다. 윗면은 표면을 판정 윗면에 맞춰 번갈아 이어 붙이고, 속 채움은 표면 −0.9부터 3.5유닛 아래까지, 벽은 안쪽 면에서 바깥으로 속 채움을 쌓고, 발판은 L + M×n + R을 판정 폭에 맞춰(가로세로 같은 배율). 구간은 배경과 같은 규칙(`forcedZone`, 아니면 `GameTuning.backgroundZone`)이고 바뀌면 다시 깐다. 그림이 깔린 판정 네모는 숨기고 "판정 상자 보기"일 때만 알파 0.35로 보인다(그림이 없는 건 예전 네모 그대로 — 사당 구간의 발판).
- 판정은 그대로(콜라이더 크기·위치 무변경) — 보이는 땅 윗면 = 판정 윗면. 테스트: `FloorTests`, 캡처 `바닥_구간별_타일`(`Captures/floor_*.png`).
- 사당 구간(구간 0)은 발판 그림이 없어 `FloorDecorator`가 발판 오브젝트를 **끈다**(판정 포함; 다른 구간으로 바꾸면 다시 켠다). 사당 바닥은 에셋의 "땅 한 줄" 조각(사용자가 보낸 흙바닥 타일 `shrine_dirt_*`은 써 보고 원래대로 되돌렸다 — 파일은 `ArtSource/gwimun/floor/`에 보관).
- **옆 벽 위치**: 벽 안쪽 면 = 카메라가 멈추는 끝(`arenaMin/Max` = 반폭 ± 1) — `PrototypeSceneBuilder.WallInset`(1)로 벽을 바깥으로 옮기고 바닥을 그만큼 늘렸다. 벽은 화면 밖이라 안 보이고 플레이어는 화면 끝에서 막힌다. `FloorTests`가 벽 안쪽 면이 카메라 끝과 맞는지 확인한다(캡처 테스트는 배치모드 창 비율이 달라 `Camera.main.aspect`를 16:9로 고정해 찍는다).
- (수정, 2026-10-16) 도력 구슬: 방금 찬 구슬(`orbSpark`)이 커질 때 오른쪽으로 밀리던 문제 — `Hud.BuildStatusWithArt`의 찬 구슬 이미지 기준점이 `UiKit.Place` 기본(왼쪽 위)이라 확대가 모서리 기준이었다 → 기준점을 가운데로 바꾸고 확대 1.9→1.5배. **`UiKit.Place`는 기준점을 앵커와 같게 잡으니, 크기를 키울 UI 요소는 `Place` 뒤에 `pivot`을 (0.5, 0.5)로 바꾸고 위치를 가운데 기준으로 줄 것.**
- (수정) 도력 구슬 반짝임은 **막 얻은 구슬만 0.6초** (`Hud._orbSparkUntil`): 도력이 늘 때 새로 찬 인덱스에만 시각을 기록해 `orbSpark` + 확대(1.5→1배)를 보여 주고, 평소엔 갖고 있는 구슬이 모두 같은 `orbFull`이다 — 가장 최근 구슬이 계속 달라 보이면 몇 개인지 헷갈린다는 사용자 지적.

## 근접 몬스터 "지금 눌러" 느낌표 — 노랑 → 빨강 (2026-10-16)
- 창귀(근접)는 일반·강공격 모두 **느낌표 하나**(`Combat/PressCueIcon`, 코드로 그린 둥근 느낌표 + 테두리 + 빛무리, 판정 없음)로 타이밍을 알려 준다: 예비 모션이 시작되면 `PressCueIcon.Spawn` → **노란색**으로 톡 튀어나와 둥실거린다 → 베기 직전(`defenseActiveTime × pressCueFraction(0.8)`초 전, `EnemyController.ShowPressCue`)에 **빨갛게**(`TurnRed`, `IsRed`) 바뀌며 한 번 커졌다 가라앉고 뒤에서 에셋 `heavyWarning` 불꽃이 퍼진다(`PressCueCount` +1) → 빨간 느낌표가 보이자마자 패링(S)을 누르면 판정 창이 베기 순간을 덮는다. 몬스터가 예비 모션을 끝내거나(`!IsWindingUp`) 끊기면 서서히 사라진다. 빨강은 적 공격 경고 전용 색이라는 규칙과 맞다.
- 옛 방식(에셋 번개 느낌표를 베기 직전에 한 번 재생, 강공격은 시작에도)은 폐기. 원거리(도깨비불) 강공격의 시작 경고(`heavyWarning` 그림)는 그대로.
- 테스트(`EnemyTests`): 일반·강공격 모두 노랑으로 시작 → 같은 아이콘이 빨갛게 → 즉시 패링하면 피해 0 → 베기 뒤 사라짐(하나뿐). 캡처 `근접_일반공격_느낌표`(`melee_cue_yellow.png`, `melee_press_cue.png`; SetUp이 적 AI를 꺼 두므로 근접만 `enabled = true`).
- (조정, 2026-10-16) 창귀 일반 공격 예비 모션(반응시간) 0.65 → **1.0초**(`GameTuning.meleeNormalReaction`, F1 몬스터 탭 슬라이더로도 조절). 강공격 1.2초는 그대로라 둘의 차이는 길이보다 몸이 붉게 크게 부푸는 정도·강공격 연출로 구분된다. 노란 느낌표는 예비 모션 시작에, 빨갛게 바뀌는 시점은 베기 0.24초 전 그대로.

## 창귀 공격 고정·대시 통과 (2026-10-16)
- **방향 고정**: `EnemyController.AttackRoutine` 시작에서 `_attackDir`(플레이어 쪽)을 정해 `Facing`에 넣고, `Update`의 `Facing` 갱신은 `State.Idle`일 때만 한다(예비 모션·회복 중엔 안 돎). `Launch`의 근접 베기는 `DxToPlayer`를 다시 읽지 않고 `_attackDir`로 낸다. 몬스터는 예비 모션 중 원래 안 움직이므로(`Move`는 Idle만) 공격 도중 위치·방향이 모두 고정 → 플레이어가 옆으로 지나가면 헛스윙.
- **대시 통과**: `EnemyAttack.Resolve(PassThrough)`에서 베기(`_slash`)는 회색 ghost로 만들지 않고 `_slashSpent`(판정만 끄고 끝까지 뻗는 모습 유지)로 처리한다. 투사체(도깨비불)는 예전 ghost 그대로. `PlayerDefense.ReceiveAttack`의 무적(`IsDashInvulnerable`) 통과 경로가 이걸 부른다.
- 테스트: `EnemyTests` — 공격 도중 반대편으로 가도 따라 돌지 않음 / 대시 중 베기가 끊기지 않고 끝까지(수정 전엔 실패). 캡처 `근접_베기_대시로_피해도_끊기지_않는다`.

## 퇴마사 이펙트 1016 교체 (2026-10-16)
- **디자인 에셋 기준 폴더가 `~/Downloads/귀문_에셋_공유_1016/`으로 바뀌었다** (1013은 1차). 안내.md 사본을 `ArtSource/gwimun/안내.md`로 갱신했고 필요한 이펙트 시트만 `ArtSource/gwimun/fxhero/`에 복사했다.
- **새 기능 없이 있는 것만 교체**(사용자 요청): `Tools/gwimun_sprites/cut_fxhero.py`가 `Assets/Art/Fx/fxhero_<클립>_<번호>.png`를 만든다(클립별 불투명 영역 합집합으로 자름, a→b 이어 붙임, `fxhero_meta.json`). 연결(`PrototypeSceneBuilder.CreateAttackFxArt`): `meleeAttack`=combo1(A·D) · `meleeCounter`=counter_big · `parryMelee`=parry_shockwave · `yogiAbsorb`=yogi_gain(a+b) · `talismanBurst`=talisman_hit · `staffSlam`=quake_wave(a+b) · `sealStart/Loop/End`=seal_circle · `perfectParry`=perfect_parry(`SuccessEffects.PlayPerfect`에서 재생). 옛 `fx_staff_slash/thrust/parry_staff/yogi_absorb/talisman_burst/staff_slam/talisman_seal`은 삭제(git 기록에 있음). 남긴 것: `fx_parry_talisman`·`fx_talisman_projectile`(대응 없음)·`dust_land`·`hit_spark`·`yokai_dissolve`·`heavy_warning`·`gate_portal`·포탈.
- 코드 변경: `FxClip.PlayFit/FitScale`(그림을 maxSize 안에 비율 유지로 재생 — 크기가 제각각인 이펙트용), `AttackFxArt.talismanSeal` → `sealStart/sealLoop/sealEnd`, `SealZone`이 시작(한 번) → 반복 → 끝(한 번)을 재생(끝은 지속 시간이 다하기 `끝 길이` 전에 시작). 그림이 가로로 긴 탓에 `ArtTests`의 "정사각 비율" 가정을 "그림 비율 유지"로 고쳤다.
- **적용하지 않은 1016 이펙트**(새 기능이 필요): 콤보 2·3, 모아치기·충전, 강화판(`_plus`), 난타·소용돌이·승룡·낙뢰·결계·부적 폭풍·사슬·화염부·번개부·분신·호신부, 궁극기 6종, 대시 흔적·점프/달리기/강한 착지 먼지, 요기 가득 오라·폭발. 지형 타일(오토타일)도 아직 안 씀.

## 봉 일반공격 단계별 공격 (2026-10-16, 사용자 요청: "근접공격도 추가된 이펙트에 맞춰 단계별 공격")
- 봉(근접) A를 이어 누르면 **1 → 2 → 3단**(`PlayerCombat.NextComboStage`, `_comboDone`/`_comboExpire`). 단마다 이펙트 `AttackFxArt.meleeAttack`/`meleeAttack2`/`meleeAttack3`(= 1016 콤보 1·2·3, `cut_fxhero.py`의 combo1~3). 1단 15(박스 2.4×1.6, 쿨 0.35) · 2단 `comboDamage2` 18(박스 `comboBox2` 2.6×1.9) · 3단 `comboDamage3` 28(박스 `comboBox3` 3.4×2.2, **플레이어를 가운데로 양옆** 휩쓸고 `comboStun3` 0.35초 기절, 마무리 쿨 `comboFinisherCooldown` 0.6, 맞으면 히트스탑·약한 흔들림). 한 단을 치면 `_comboExpire = 지금 + 그 단 쿨타임 + comboWindow(0.6)` — 그때까지 다음 A를 안 누르면 1단부터. 3단을 치면 처음으로. `TrySwitchWeapon`이 단계를 0으로 되돌린다. 부적(원거리)은 단계 없음. 스킬바 A 칸 이름 "베기 N단".
- 판정-시각 일치: 단마다 보이는 이펙트 그림을 그 단의 판정 박스에 비율 유지로 맞춘다(`MeleeStrike` 기본 uniform). 3단의 가운데 기준 박스는 그림(몸을 감싸는 회오리)과 같은 범위.
- 테스트(`SkillTests`): 이어 누르면 단이 올라가고 데미지 1<2<3, 3단이 뒤쪽 적도 맞고 기절, 이어치기 시간/무기 전환으로 1단부터, 부적은 단계 없음. 캡처 `봉_일반공격_단계별`(`combo_stage1~3.png`).
- (수정, 2026-10-16) **부적 봉인진 그림을 바닥에 깔린 느낌으로**: 1016 봉인진 그림은 비스듬히 내려다본 둥근 진이라 옆 시점 게임에서 "위에서 보는" 느낌이 났다 → `SealZone`이 한 장면을 런타임 `Sprite.Create`(원본 텍스처의 부분 사각형)로 **진(아래 부분, 세로 `RingFlatten` 0.4배로 납작하게)** 과 **빛기둥(위 부분, 눌리지 않음, `PillarScale`)** 두 층으로 나눠 그린다. 진의 중심선(`RingCenter` 0.706, 그림 세로 비율)이 바닥에 닿는다. 나눈 그림은 `Split` 사전에 캐시. 그림 파일·`AttackFxArt`는 그대로. 캡처 `스킬_D_F_연출`(`skill_talisman_seal.png`).
- (다듬기, 2026-10-16) 봉인진: 빛기둥 층(`SealZone.MakePillars`)은 그림 맨 위 `RingTop`(0.506)까지만 잘라 아래 22%를 알파로 서서히 흐리고(읽기 가능 텍스처 필요 → 빌더가 `fxhero_seal_*`를 `isReadable`로 임포트, 못 읽으면 흐림 없이 자름) 세로 `PillarHeight` 1.5배로 세운다. 진 층은 `RingTop` 아래 전부를 `RingFlatten` 0.4배로 눌러 뒤쪽 호도 함께 눌린다. 진 아래에 `FxSprites.Glow` 납작 타원 빛(`GroundGlow`)이 시작·끝에 서서히 켜지고 꺼진다.
- (수정, 2026-10-16) **이동하며 공격하면 베기 이펙트가 뒤에 처지던 문제**: `MeleeStrike.Spawn`이 베기 그림(`FxClip`)을 월드 한 자리에 만들어 두기만 했다 → `FxClip.Follow(target)`(생길 때의 오프셋을 유지하며 `LateUpdate`에서 target을 따라감)을 `MeleeStrike.Spawn(..., follow: transform)`으로 쓴다. 일반공격 1~3단·반격·D 휩쓸기가 플레이어를 따라가고, 땅 이펙트(F 내려찍기·봉인진)는 따라가지 않는다. 판정은 여전히 공격이 나간 순간 그 자리의 박스 한 번. 테스트 `SkillTests`(수정 전엔 실패), 캡처 `봉_베기_이동하며_공격해도_이펙트가_따라간다`.
- (조정, 2026-10-16) 원거리 몬스터(도깨비불) 사정거리 `EnemyController.fireRange` 11 → **7**, 불덩이 최대 비행 `rangedMaxTravel` 16 → 10. 화면 반폭이 약 10.7이라 11이면 화면 끝에서도 쐈다. 사정거리 안이면 그 자리에서 쏘고 벗어나면 7 안으로 다가오는 규칙은 그대로. 프리팹 값이라 인스펙터에서도 바꾼다(빌더가 프리팹을 새로 만들 때 코드 기본값이 들어간다).

## 사당 바닥 채움 (2026-10-16)
- 사당 바닥은 에셋 지형 타일 세트(`ArtSource/gwimun/floor/shrine_tiles/`, 원본 `1016/미리보기/tiles/shrine/`)를 쓴다: `cut_floor.py`가 윗면 `ground_14` → `shrine_top_0`, 속 `ground_15`+변형 4장 → `shrine_fill_0~4`로 불투명 영역만 잘라 만든다(약 90px = 1유닛, `fillDarken` 0). 사당엔 발판이 없다. 옛 "땅 한 줄" 방식은 폐기.
