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
- 조작키는 `Controls` 상수로만 참조 (기획서 4.5): ←/→ 이동, Space 점프(2단·고정 높이, ↓+Space 발판 내려가기), S 방어, A 일반공격, Shift 이동 스킬, D 수동 스왑, Enter 수락, ESC 일시정지, C 증강 목록, F1 튜닝 패널.
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
  - 예외(2026-10-08, ParryRoguelike-main2 병합): 전사와 근접 몬스터는 도트 **애니메이션**(`Assets/Art/Characters/Warrior`, `Assets/Art/Enemies/Melee`)을 쓴다. 원본 시트는 `ArtSource/`, 프레임 자르기는 `Tools/sprites/process_sheets.ps1`. `PlayerSprite`/`EnemySprite`가 판정 네모 위에 그림을 그리고(판정은 그대로, F1 "판정 상자 보기"로 겹쳐 확인), 그림이 없는 궁수·원거리 몬스터는 아래 Project_Game 스프라이트를 쓴다. 공격 이펙트 그림(`Assets/Art/Fx`: 전사 반격·일반공격, 몬스터 베기)은 `AttackFxArt`/`FxClip`이 판정 박스 크기로 늘려 그린다 (그림 = 판정 범위). 전사는 벨 때 몸이 0.45유닛 내딛는다(`PlayerMotor.Lunge`, 무적 아님). 시간 정지 바람개비는 퍼펙트 회피 중에만 보인다. 빌더에서 `PlayerSprite`는 `PlayerDefense`를 붙인 **뒤에** 추가해야 한다 (RequireComponent로 방어 컴포넌트가 둘이 되는 버그).
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

- 규칙(판정·게이지·스왑 등)을 바꾸면 테스트를 추가·수정하고 통과를 확인한다. 새 PlayMode 테스트는 씬을 `yield return TestScene.LoadPrototype();`으로 연다 — 사용자의 `tuning.json`을 무시하고 기본값으로 돌리기 위함 (안 그러면 사용자가 패널로 값을 바꾸는 순간 테스트가 깨진다).
- 로그에서 `error CS` / `warning CS` / 예외가 없는지 확인한다.
- **UI 글자 폭은 해상도(캔버스 배율)에 따라 달라진다.** 작은 Game 뷰에서는 같은 글도 더 넓게 그려져 줄바꿈이 생긴다. 글이 들어가는 칸을 고정 높이로 두지 말고 `Text.preferredHeight`로 잡은 뒤, 캔버스 `scaleFactor`가 바뀌면 다시 배치한다. 목록처럼 길어질 수 있는 창은 높이 상한 + 스크롤. 캡처는 1920×1080 외에 작은 해상도(`Capture(name, 640, 360)`)로도 찍어 확인한다.
- **화면에 보이는 변경(UI·연출)은 반드시 캡처로 확인한다.** `Assets/Tests/PlayMode/CaptureTests.cs`에 장면을 추가하면 테스트 실행 시 프로젝트 루트 `Captures/*.png`로 저장된다 (캔버스를 ScreenSpaceCamera로 바꿔 RenderTexture로 렌더). 이미지를 직접 열어보고 겹침·가림·대비 문제를 고친 뒤 보고한다. 캡처만 다시 찍을 땐 `-testFilter ParryRL.Tests.CaptureTests`.
- 사용자는 보통 에디터를 꺼둔다. 에디터가 열려 있어 배치모드가 프로젝트 잠금으로 실패하면 `Assets`/`Packages`/`ProjectSettings`만 스크래치 폴더(ASCII 경로)에 복사해서 그 사본으로 빌드·테스트한다.
- 한글 경로 주의: 한글 경로가 들어간 `.ps1` 파일은 **UTF-8 BOM**으로 저장해야 Windows PowerShell 5.1이 경로를 제대로 읽는다.
- `EditorSceneManager.NewScene`은 사용되지 않는 에셋을 언로드한다 — 씬에 연결할 에셋은 씬 생성 **후에** `AssetDatabase.LoadAssetAtPath`로 로드할 것.
