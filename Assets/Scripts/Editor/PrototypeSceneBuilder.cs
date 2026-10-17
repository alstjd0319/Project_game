using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ParryRL.EditorTools
{
    /// <summary>
    /// 화이트박스 프로토타입 씬을 코드로 통째로 생성한다.
    /// 메뉴: Tools → ParryRL → 프로토타입 씬 생성
    /// 배치모드: -executeMethod ParryRL.EditorTools.PrototypeSceneBuilder.Build
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Prototype.unity";
        public const string StartScenePath = "Assets/Scenes/Start.unity"; // 첫 화면 (게임 시작 · 종료하기)
        public const string HubScenePath = "Assets/Scenes/Hub.unity";     // 사당: 시작 화면과 플레이 씬 사이 대기 씬 (포탈 · 만신)
        private const string MentorArtDir = "Assets/Art/Characters/Mentor";
        private const string MentorArtPath = MentorArtDir + "/MentorArt.asset";
        private const float HubHalfWidth = 16f;
        // 사당 소품(포탈 문): 원본 시트(원본/bg/shrine/props.png)에서 오려 바깥 마젠타만 투명하게 한 그림. 90px/유닛, 피벗 = 아래 가운데(바닥)
        private const string PropsDir = "Assets/Art/Props";
        private const string GateSpritePath = PropsDir + "/shrine_gate.png";
        private static readonly Vector2 BottomPivot = new(0.5f, 0f);
        private const string SquarePath = "Assets/Art/Square.png";
        private const string NoFrictionPath = "Assets/Art/NoFriction.physicsMaterial2D";
        private const string MeleePrefabPath = "Assets/Prefabs/Enemy_Melee.prefab";
        private const string RangedPrefabPath = "Assets/Prefabs/Enemy_Ranged.prefab";

        private const string AudioDir = "Assets/Audio/ParryPrototype/";

        private const float ArenaHalfWidth = 22f;
        // 옆 벽: 카메라가 멈추는 끝(반폭 + 1)에 벽 안쪽 면이 딱 닿게 둬서, 벽이 화면 밖에 있고 플레이어는 화면 끝에서 막힌다
        private const float WallInset = 1f;

        // 도트 그림 규격 (귀문 에셋 묶음 안내.md): 화면 1920×1080을 배율 1로, 화면 높이 12유닛 → 1유닛 = 90px.
        // 칸 안의 기준점(피벗)은 시트 manifest 값. 땅 캐릭터는 발끝(0.5, 0.0714), 도깨비불은 몸 중심(0.5, 0.3304).
        private const float PixelsPerUnit = 90f;
        private static readonly Vector2 GroundPivot = new(0.5f, 0.0714f);
        private static readonly Vector2 FloatPivot = new(0.5f, 0.3304f);
        private static readonly Vector2 CenterPivot = new(0.5f, 0.5f);
        private const string HeroArtDir = "Assets/Art/Characters/Hero";
        private const string StaffArtPath = HeroArtDir + "/StaffArt.asset";
        private const string TalismanArtPath = HeroArtDir + "/TalismanArt.asset";
        private const string ChanggwiArtDir = "Assets/Art/Enemies/Changgwi";
        private const string ChanggwiArtPath = ChanggwiArtDir + "/ChanggwiArt.asset";
        private const string DokkaebibulArtDir = "Assets/Art/Enemies/Dokkaebibul";
        private const string DokkaebibulArtPath = DokkaebibulArtDir + "/DokkaebibulArt.asset";
        // 전투 이펙트 — 칸 256×256, 피벗 가운데. 게임에서 판정 박스(또는 원본 크기)에 맞춰 그린다.
        // 배경 — 원본 1536×1024를 세로 12유닛(화면 높이)에 맞춘다: 1024px = 12유닛 → PPU 85.33. 좌우로 이어 붙여 반복한다.
        private const string BgDir = "Assets/Art/Backgrounds";
        private static readonly string[] BgZones = { "shrine", "zone1", "zone2", "zone3" }; // 사당 · 1구간 · 2구간 황천길 · 3구간 저승 문턱 (보스방은 보류)
        private static readonly string[] BgZoneLabels = { "사당", "1구간", "2구간 황천길", "3구간 저승 문턱" };
        private static readonly bool[] BgNearInFront = { false, false, true, true }; // 안내.md: 사당·1구간은 near를 뒤에, 2·3구간은 앞에
        private const string UiDir = "Assets/Art/Ui";                 // Tools/gwimun_sprites/cut_ui.py가 오린 UI 조각
        private const string UiArtPath = UiDir + "/UiArt.asset";
        // 바닥 타일: Tools/gwimun_sprites/cut_floor.py가 오린 조각(floor_meta.json 포함). 구간 순서는 배경과 같다 (사당·1·2·3구간)
        private const string FloorDir = "Assets/Art/Floor";
        private static readonly string[] FloorZones = { "shrine", "zone1", "zone2", "zone3" };
        private const string FxDir = "Assets/Art/Fx";
        private const string FxArtPath = FxDir + "/AttackFx.asset";

        private static Sprite _square;

        [MenuItem("Tools/ParryRL/프로토타입 씬 생성")]
        public static void Build()
        {
            EnsureFolder("Assets/Art");
            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Scenes");

            _square = CreateSquareSprite();
            CreateNoFrictionMaterial();
            var changgwiArt = CreateEnemyArt(ChanggwiArtDir, "changgwi", ChanggwiArtPath, FloatPivotFor: false);
            var dokkaebibulArt = CreateEnemyArt(DokkaebibulArtDir, "dokkaebibul", DokkaebibulArtPath, FloatPivotFor: true);
            // 근접 = 창귀, 원거리 = 도깨비불 (두억시니·장산범·어둑시니·불가살이·만신은 게임에 아직 없어 보류)
            CreateEnemyPrefab(MeleePrefabPath, "Enemy_Melee", EnemyKind.Melee,
                new Vector2(1f, 1.6f), new Color(0.58f, 0.46f, 0.78f), changgwiArt, floating: false);
            CreateEnemyPrefab(RangedPrefabPath, "Enemy_Ranged", EnemyKind.Ranged,
                new Vector2(0.9f, 1.3f), new Color(0.35f, 0.62f, 0.8f), dokkaebibulArt, floating: true);
            CreateHeroArt(WeaponMode.Melee, StaffArtPath, "staff");
            CreateHeroArt(WeaponMode.Ranged, TalismanArtPath, "talisman");
            CreateAttackFxArt();
            CreateMentorArt();
            CreateUiArt();
            CreateFloorSets();
            if (AssetDatabase.IsValidFolder(PropsDir)) ImportFrames(PropsDir, _ => BottomPivot);
            var bgSets = CreateBackgroundSets();
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene이 사용되지 않는 에셋을 언로드하므로, 씬에 넣을 에셋은 씬 생성 후에 다시 로드한다.
            _square = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
            var noFriction = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(NoFrictionPath);
            // 배경 묶음도 NewScene 뒤에 다시 로드한다 (안 그러면 씬에 null이 들어간다)
            bgSets = LoadBackgroundSets();
            var floorSets = LoadFloorSets();
            var staffArt = AssetDatabase.LoadAssetAtPath<CharacterArt>(StaffArtPath);
            var talismanArt = AssetDatabase.LoadAssetAtPath<CharacterArt>(TalismanArtPath);
            var attackFx = AssetDatabase.LoadAssetAtPath<AttackFxArt>(FxArtPath);
            var uiArt = AssetDatabase.LoadAssetAtPath<UiArt>(UiArtPath);
            var meleePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MeleePrefabPath).GetComponent<EnemyController>();
            var rangedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RangedPrefabPath).GetComponent<EnemyController>();

            // ── 시스템 ──
            var systems = new GameObject("Systems");
            systems.AddComponent<GameManager>();
            var gameAssets = systems.AddComponent<GameAssets>();
            Set(gameAssets, "square", _square);
            if (attackFx != null) Set(gameAssets, "attackFx", attackFx);
            if (uiArt != null) Set(gameAssets, "uiArt", uiArt);
            systems.AddComponent<HitFeel>();
            systems.AddComponent<AudioSource>();
            var sfx = systems.AddComponent<Sfx>();
            SetOptional(sfx, "parryClip", LoadAsset<AudioClip>(AudioDir + "parry_normal_success.wav"));
            SetOptional(sfx, "perfectParryClip", LoadAsset<AudioClip>(AudioDir + "parry_success.wav"));
            systems.AddComponent<Hud>();

            // ── 레벨 ──
            var level = new GameObject("Level").transform;
            var ground = CreateSolid("Ground", new Vector2(0f, -0.5f), new Vector2((ArenaHalfWidth + WallInset + 2f) * 2f, 1f), new Color(0.3f, 0.32f, 0.38f, 0.55f), level);
            var wallL = CreateSolid("Wall_L", new Vector2(-(ArenaHalfWidth + WallInset) - 0.5f, 5f), new Vector2(1f, 12f), new Color(0.3f, 0.32f, 0.38f, 0.55f), level);
            var wallR = CreateSolid("Wall_R", new Vector2(ArenaHalfWidth + WallInset + 0.5f, 5f), new Vector2(1f, 12f), new Color(0.3f, 0.32f, 0.38f, 0.55f), level);
            var platL = CreateOneWay("Platform_L", new Vector2(-7f, 2.4f), new Vector2(4f, 0.4f), new Color(0.36f, 0.38f, 0.45f, 0.55f), level);
            var platR = CreateOneWay("Platform_R", new Vector2(7f, 2.4f), new Vector2(4f, 0.4f), new Color(0.36f, 0.38f, 0.45f, 0.55f), level);
            var platTop = CreateOneWay("Platform_Top", new Vector2(0f, 4.4f), new Vector2(3f, 0.4f), new Color(0.36f, 0.38f, 0.45f, 0.55f), level);
            // 방 지형(RoomLayout)이 더 많은 발판을 쓸 수 있게 여분을 만들어 둔다 — 쓰지 않는 건 RoomController가 레이아웃을 적용할 때 꺼진다
            var platExtra = new Transform[RoomLayouts.MaxPlatforms - 3];
            for (int i = 0; i < platExtra.Length; i++)
                platExtra[i] = CreateOneWay($"Platform_{i + 4}", new Vector2(0f, -20f), new Vector2(3f, 0.4f), new Color(0.36f, 0.38f, 0.45f, 0.55f), level);
            // 바닥 그림: 판정 네모 위에 구간 타일을 깐다 (구간은 F1 연습 탭의 배경 구간과 함께 바뀐다)
            var levelSegments = new List<(Transform solid, FloorDecorator.Kind kind)>
            {
                (ground, FloorDecorator.Kind.Ground), (wallL, FloorDecorator.Kind.Wall), (wallR, FloorDecorator.Kind.Wall),
                (platL, FloorDecorator.Kind.Platform), (platR, FloorDecorator.Kind.Platform), (platTop, FloorDecorator.Kind.Platform),
            };
            foreach (var extra in platExtra) levelSegments.Add((extra, FloorDecorator.Kind.Platform));
            AddFloorDecorator(level, floorSets, -1, levelSegments.ToArray());

            // 배경 (판정 없음): 먼·중간·가까운 3겹 시차 스크롤. 구간은 F1 연습 탭에서 바꾼다.
            var background = new GameObject("Background").AddComponent<ParallaxBackground>();
            var bgSerialized = new SerializedObject(background);
            var bgList = bgSerialized.FindProperty("sets");
            bgList.arraySize = bgSets.Length;
            for (int b = 0; b < bgSets.Length; b++) bgList.GetArrayElementAtIndex(b).objectReferenceValue = bgSets[b];
            bgSerialized.ApplyModifiedPropertiesWithoutUndo();

            // ── 플레이어 ──
            var player = CreatePlayer(new Vector2(-3f, 0.7f), noFriction, staffArt, talismanArt);

            // ── 적 스포너 ──
            var spawner = new GameObject("EnemySpawner").AddComponent<EnemySpawner>();
            Set(spawner, "meleePrefab", meleePrefab);
            Set(spawner, "rangedPrefab", rangedPrefab);
            SetFloat(spawner, "arenaMinX", -ArenaHalfWidth);
            SetFloat(spawner, "arenaMaxX", ArenaHalfWidth);

            // ── 방 진행 (런 중에만 동작 — RunState.Active가 아니면 예전 연습 모드 그대로) ──
            var roomController = new GameObject("RoomController").AddComponent<RoomController>();
            var runGateSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GateSpritePath);
            if (runGateSprite != null) Set(roomController, "gateSprite", runGateSprite);

            // ── 카메라 ──
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = new Vector3(0f, 3.5f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.1f, 0.13f);
            camGo.AddComponent<AudioListener>();
            var rig = camGo.AddComponent<CameraRig>();
            Set(rig, "target", player.transform);
            SetFloat(rig, "arenaMinX", -ArenaHalfWidth - 1f);
            SetFloat(rig, "arenaMaxX", ArenaHalfWidth + 1f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            BuildHubScene();
            BuildStartScene();
            AssetDatabase.SaveAssets();
            Debug.Log($"[ParryRL] 프로토타입 씬 생성 완료: {ScenePath}");
        }

        /// <summary>플레이어 한 명 (사당·플레이 씬 공용): 몸 네모·물리·전투 컴포넌트·도트 그림.</summary>
        private static GameObject CreatePlayer(Vector2 position, PhysicsMaterial2D noFriction, CharacterArt staffArt, CharacterArt talismanArt)
        {
            var player = CreateVisual("Player", position, new Vector2(0.8f, 1.4f), new Color(0.95f, 0.38f, 0.32f), 10, null);
            var rb = player.AddComponent<Rigidbody2D>();
            rb.gravityScale = 1.6f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var playerCol = player.AddComponent<BoxCollider2D>();
            playerCol.sharedMaterial = noFriction;

            // 바라보는 방향 표시 눈 (몸 안쪽, 판정 없음)
            var eye = CreateVisual("Eye", Vector2.zero, Vector2.one, new Color(0.08f, 0.08f, 0.1f), 11, player.transform);
            eye.transform.localPosition = new Vector3(0.22f, 0.25f, 0f);
            eye.transform.localScale = new Vector3(0.16f / 0.8f, 0.16f / 1.4f, 1f);

            player.AddComponent<SkillGauge>();
            var motor = player.AddComponent<PlayerMotor>();
            Set(motor, "eye", eye.transform);
            player.AddComponent<PlayerParty>();
            player.AddComponent<PlayerCombat>();
            player.AddComponent<PlayerDefense>();
            player.AddComponent<PlayerLevel>();

            // PlayerSprite는 PlayerDefense를 RequireComponent하므로 반드시 PlayerDefense를 붙인 뒤에 추가한다 (아니면 방어 컴포넌트가 둘이 된다)
            // 도트 애니메이션 (판정 없음). 발끝 = 네모 바닥, 부모의 늘림(0.8×1.4)을 상쇄해 픽셀 비율 유지.
            var art = new GameObject("Art");
            art.transform.SetParent(player.transform, false);
            art.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            art.transform.localScale = new Vector3(1f / 0.8f, 1f / 1.4f, 1f);
            var artSr = art.AddComponent<SpriteRenderer>();
            artSr.sortingOrder = 12;
            var playerSprite = player.AddComponent<PlayerSprite>();
            Set(playerSprite, "artRenderer", artSr);
            Set(playerSprite, "eye", eye.transform);
            var artsProp = new SerializedObject(playerSprite);
            var list = artsProp.FindProperty("arts");
            var heroArts = new System.Collections.Generic.List<CharacterArt>();
            if (staffArt != null) heroArts.Add(staffArt);
            if (talismanArt != null) heroArts.Add(talismanArt);
            list.arraySize = heroArts.Count;
            for (int a = 0; a < heroArts.Count; a++) list.GetArrayElementAtIndex(a).objectReferenceValue = heroArts[a];
            artsProp.ApplyModifiedPropertiesWithoutUndo();
            player.AddComponent<PlayerPersistence>(); // 사당 ↔ 플레이 씬을 넘어 체력·레벨·도력·무기를 이어받는다
            player.AddComponent<PlayerInteractor>(); // 사당의 포탈·NPC 상호작용 (E). 다른 씬엔 대상이 없어 아무 일도 안 한다
            return player;
        }

        /// <summary>
        /// 시작 화면 씬 (빌드 첫 번째 씬). 사당 배경이 천천히 흐르고, 메뉴는 StartMenu가 코드로 만든다.
        /// 메뉴만 따로 다시 만들 때: Tools → ParryRL → 시작 씬 생성 (게임 씬·그림 에셋이 이미 있어야 한다).
        /// </summary>
        [MenuItem("Tools/ParryRL/시작 씬 생성")]
        public static void BuildStartScene()
        {
            EnsureFolder("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene 뒤에 에셋을 로드한다 (안 그러면 씬에 null이 들어간다)
            var bgSets = LoadBackgroundSets();
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = new Vector3(0f, 3.5f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.1f, 0.13f);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<MenuCameraPan>();

            var systems = new GameObject("Systems");
            systems.AddComponent<AudioSource>();
            systems.AddComponent<Sfx>(); // 메뉴 선택·확인 소리

            var background = new GameObject("Background").AddComponent<ParallaxBackground>();
            var bgSerialized = new SerializedObject(background);
            var bgList = bgSerialized.FindProperty("sets");
            bgList.arraySize = bgSets.Length;
            for (int b = 0; b < bgSets.Length; b++) bgList.GetArrayElementAtIndex(b).objectReferenceValue = bgSets[b];
            bgSerialized.FindProperty("forcedZone").intValue = 0; // 사당
            bgSerialized.ApplyModifiedPropertiesWithoutUndo();

            // 시작 화면에는 주인공을 세우지 않는다 (StartMenu.heroArt를 채우면 대기 모션으로 서 있게 할 수 있다)
            new GameObject("StartMenu").AddComponent<StartMenu>();

            EditorSceneManager.SaveScene(scene, StartScenePath);
            SetBuildScenes();
            Debug.Log($"[ParryRL] 시작 씬 생성 완료: {StartScenePath}");
        }

        /// <summary>빌드 순서: 0 시작 화면 → 1 사당(대기) → 2 플레이. 아직 없는 씬은 건너뛴다.</summary>
        private static void SetBuildScenes()
        {
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (string path in new[] { StartScenePath, HubScenePath, ScenePath })
                if (File.Exists(path)) list.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        /// <summary>
        /// 사당(대기) 씬: 시작 화면과 플레이 씬 사이. 플레이어가 서 있고, 오른쪽 끝 포탈(E)로 플레이 씬에 들어가며,
        /// NPC 만신(E)이 스킬 트리 창을 연다 (창의 틀만 — 스킬 트리 내용은 아직 없음). 적은 없다.
        /// 메뉴: Tools → ParryRL → 사당 씬 생성 (그림 에셋이 이미 있어야 한다).
        /// </summary>
        [MenuItem("Tools/ParryRL/사당 씬 생성")]
        public static void BuildHubScene()
        {
            EnsureFolder("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene 뒤에 에셋을 로드한다 (안 그러면 씬에 null이 들어간다)
            _square = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
            var noFriction = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(NoFrictionPath);
            var bgSets = LoadBackgroundSets();
            var staffArt = AssetDatabase.LoadAssetAtPath<CharacterArt>(StaffArtPath);
            var talismanArt = AssetDatabase.LoadAssetAtPath<CharacterArt>(TalismanArtPath);
            var attackFx = AssetDatabase.LoadAssetAtPath<AttackFxArt>(FxArtPath);
            var mentorArt = AssetDatabase.LoadAssetAtPath<NpcArt>(MentorArtPath);
            var gateSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GateSpritePath);
            var uiArt = AssetDatabase.LoadAssetAtPath<UiArt>(UiArtPath);

            var systems = new GameObject("Systems");
            systems.AddComponent<GameManager>();
            var gameAssets = systems.AddComponent<GameAssets>();
            Set(gameAssets, "square", _square);
            if (attackFx != null) Set(gameAssets, "attackFx", attackFx);
            if (uiArt != null) Set(gameAssets, "uiArt", uiArt);
            systems.AddComponent<HitFeel>();
            systems.AddComponent<AudioSource>();
            var sfx = systems.AddComponent<Sfx>();
            SetOptional(sfx, "parryClip", LoadAsset<AudioClip>(AudioDir + "parry_normal_success.wav"));
            SetOptional(sfx, "perfectParryClip", LoadAsset<AudioClip>(AudioDir + "parry_success.wav"));
            systems.AddComponent<Hud>();

            var level = new GameObject("Level").transform;
            var solid = new Color(0.3f, 0.32f, 0.38f, 0.55f);
            var hubGround = CreateSolid("Ground", new Vector2(0f, -0.5f), new Vector2((HubHalfWidth + WallInset + 2f) * 2f, 1f), solid, level);
            var hubWallL = CreateSolid("Wall_L", new Vector2(-(HubHalfWidth + WallInset) - 0.5f, 5f), new Vector2(1f, 12f), solid, level);
            var hubWallR = CreateSolid("Wall_R", new Vector2(HubHalfWidth + WallInset + 0.5f, 5f), new Vector2(1f, 12f), solid, level);
            // 사당 바닥은 흙바닥 타일 (구간 0 고정)
            AddFloorDecorator(level, LoadFloorSets(), 0, (hubGround, FloorDecorator.Kind.Ground), (hubWallL, FloorDecorator.Kind.Wall), (hubWallR, FloorDecorator.Kind.Wall));

            // 사당 배경 고정
            var background = new GameObject("Background").AddComponent<ParallaxBackground>();
            var bgSerialized = new SerializedObject(background);
            var bgList = bgSerialized.FindProperty("sets");
            bgList.arraySize = bgSets.Length;
            for (int b = 0; b < bgSets.Length; b++) bgList.GetArrayElementAtIndex(b).objectReferenceValue = bgSets[b];
            bgSerialized.FindProperty("forcedZone").intValue = 0;
            bgSerialized.ApplyModifiedPropertiesWithoutUndo();

            var player = CreatePlayer(new Vector2(-HubHalfWidth + 6f, 0.7f), noFriction, staffArt, talismanArt); // 왼쪽에서 시작 → 오른쪽 끝 포탈

            // 오른쪽 끝 포탈(문): 상호작용 판정 2.6×3.2
            var portal = CreateVisual("ShrinePortal", new Vector2(HubHalfWidth - 2f, 1.6f), new Vector2(2.6f, 3.2f), new Color(0.6f, 0.45f, 1f, 0.3f), 9, null);
            portal.GetComponent<SpriteRenderer>().enabled = false;
            var shrinePortal = portal.AddComponent<ShrinePortal>(); // Interactable·트리거 콜라이더가 함께 붙는다
            portal.GetComponent<BoxCollider2D>().isTrigger = true;
            if (gateSprite != null)
            {
                // 문 그림(정지 그림): 바닥(판정 네모 아래)에 세운다. 판정 네모(2.6×3.2)는 문 안쪽 소용돌이 자리에 맞춘다
                var gateArt = CreateArtChild(portal, Vector2.one * 1.25f, -0.5f, 9); // 플레이어가 문 안으로 들어서 보이게 ×1.25
                var gateRenderer = gateArt.GetComponent<SpriteRenderer>();
                gateRenderer.sprite = gateSprite;
                Set(shrinePortal, "gateArt", gateRenderer); // 소용돌이·빛무리 연출은 ShrinePortal이 시작할 때 문 안쪽에 겹쳐 그린다
            }
            else
            {
                // 그림이 없으면 귀문 소용돌이 이펙트를 반복 재생
                var portalArt = CreateArtChild(portal, new Vector2(1.5f, 1.5f), 0.1875f, 9);
                Set(shrinePortal, "artPlayer", portalArt);
            }

            // 스승 만신: 상호작용 판정 2.8×2.2, 발이 땅에 오도록 그림을 내린다
            var npc = CreateVisual("Npc_Mentor", new Vector2(-2f, 1.1f), new Vector2(2.8f, 2.2f), new Color(0.3f, 0.4f, 0.8f, 0.3f), 9, null);
            npc.GetComponent<SpriteRenderer>().enabled = false;
            var npcArt = CreateArtChild(npc, Vector2.one, -0.5f, 11);
            var shrineNpc = npc.AddComponent<ShrineNpc>();
            npc.GetComponent<BoxCollider2D>().isTrigger = true;
            if (mentorArt != null) Set(shrineNpc, "art", mentorArt);
            Set(shrineNpc, "artPlayer", npcArt);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = new Vector3(0f, 3.5f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.1f, 0.13f);
            camGo.AddComponent<AudioListener>();
            var rig = camGo.AddComponent<CameraRig>();
            Set(rig, "target", player.transform);
            SetFloat(rig, "arenaMinX", -HubHalfWidth - 1f);
            SetFloat(rig, "arenaMaxX", HubHalfWidth + 1f);

            EditorSceneManager.SaveScene(scene, HubScenePath);
            SetBuildScenes();
            Debug.Log($"[ParryRL] 사당 씬 생성 완료: {HubScenePath}");
        }

        /// <summary>
        /// 판정 네모(부모) 안에 그림 자식을 만든다: 부모의 늘림을 상쇄해 픽셀 비율을 유지하고(× <paramref name="scale"/>),
        /// 세로 위치는 부모 로컬 y(<paramref name="localY"/>)로 둔다. 반환값은 그림을 반복 재생할 ClipPlayer.
        /// </summary>
        private static ClipPlayer CreateArtChild(GameObject parent, Vector2 scale, float localY, int order)
        {
            var art = new GameObject("Art");
            art.transform.SetParent(parent.transform, false);
            var ps = parent.transform.localScale;
            art.transform.localPosition = new Vector3(0f, localY, 0f);
            art.transform.localScale = new Vector3(scale.x / ps.x, scale.y / ps.y, 1f);
            var sr = art.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            return art.AddComponent<ClipPlayer>();
        }

        private static BackgroundSet[] LoadBackgroundSets()
        {
            var list = new System.Collections.Generic.List<BackgroundSet>();
            foreach (string zone in BgZones)
            {
                var set = AssetDatabase.LoadAssetAtPath<BackgroundSet>($"{BgDir}/{zone}/{zone}Set.asset");
                if (set != null) list.Add(set);
            }
            return list.ToArray();
        }

        // ───────────── 에셋 ─────────────

        private static Sprite CreateSquareSprite()
        {
            if (!File.Exists(SquarePath))
            {
                var tex = new Texture2D(4, 4);
                var pixels = new Color32[16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(pixels);
                tex.Apply();
                File.WriteAllBytes(SquarePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(SquarePath, ImportAssetOptions.ForceSynchronousImport);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(SquarePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 4f; // 4px = 1유닛 → 스프라이트 1×1 유닛
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
        }

        private static PhysicsMaterial2D CreateNoFrictionMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(NoFrictionPath);
            if (mat != null) return mat;
            mat = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };
            AssetDatabase.CreateAsset(mat, NoFrictionPath);
            return mat;
        }

        /// <summary>
        /// 퇴마사 프레임(hero_&lt;시트&gt;_&lt;번호&gt;.png)을 도트 설정으로 임포트하고, 무기별 CharacterArt로 묶는다.
        /// 봉(근접)·부적(원거리)이 각각 대기·달리기·패링·반격·일반공격 그림을 따로 갖고, 점프·대시·피격·사망은 공용이다.
        /// 스킬 시트 중 휩쓸기(봉)·부채꼴(부적)을 일반공격에 쓴다 — 내려찍기·봉인 등 나머지 스킬 시트는 스킬이 생기면 쓴다.
        /// </summary>
        private static void CreateHeroArt(WeaponMode weapon, string assetPath, string w)
        {
            if (!AssetDatabase.IsValidFolder(HeroArtDir)) return;
            ImportFrames(HeroArtDir, _ => GroundPivot);

            var art = AssetDatabase.LoadAssetAtPath<CharacterArt>(assetPath);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<CharacterArt>();
                AssetDatabase.CreateAsset(art, assetPath);
            }
            string d = HeroArtDir;
            art.kind = CharacterKind.Player;
            art.weapon = weapon;
            art.idle = LoadClip(d, "hero", w + "_idle", 6f);
            art.run = LoadClip(d, "hero", w + "_run", 12f);
            art.parry = LoadClip(d, "hero", w + "_parry", 12f);
            art.counter = LoadClip(d, "hero", w + "_counter", 14f);
            art.attack = LoadClip(d, "hero", w == "staff" ? "staff_skill_sweep" : "talisman_skill_fan", 14f);
            art.skill1 = LoadClip(d, "hero", w == "staff" ? "staff_skill_sweep" : "talisman_skill_fan", 14f);
            art.skill2 = LoadClip(d, "hero", w == "staff" ? "staff_skill_slam" : "talisman_skill_seal", w == "staff" ? 12f : 10f);
            art.dash = LoadClip(d, "hero", "dash", 20f);       // 대시 0.15초 = 3프레임
            art.jump = LoadClip(d, "hero", "jump", 6f);        // 공중은 세로 속도로 고르므로 fps는 안 쓴다
            art.hit = LoadClip(d, "hero", "hit", 12f);
            art.death = LoadClip(d, "hero", "death", 6f);
            art.enter = LoadClip(d, "hero", "swap", 12f);      // 무기를 바꿀 때
            art.counterHitFrame = 1;  // 반격 시트: 0 준비 · 1 뻗음 · 2 거둠
            art.attackHitFrame = 2;   // 일반공격 시트: 0 준비 · 1 웅크림 · 2 뻗음 · 3 거둠
            // 스킬 시트: 휩쓸기/부채꼴은 2 = 뻗음·던짐, 내려찍기는 2 = 공중 정점 → 3에서 내리꽂음, 봉인은 2 = 부적을 바닥에 놓음
            art.skill1StartFrame = 2;
            art.skill2StartFrame = 2;
            art.parryImpactFrame = 1; // 패링 시트: 0 올림 · 1 막음
            EditorUtility.SetDirty(art);
        }

        /// <summary>
        /// 몬스터 프레임(&lt;이름&gt;_&lt;시트&gt;_&lt;번호&gt;.png)을 임포트하고 EnemyArt로 묶는다.
        /// 일반 공격 시트(6장)는 앞 3장 = 예비 모션, 뒤 3장 = 베기/발사로 나눠 쓴다. 피격 시트는 앞 2장 = 경직, 뒤 3장 = 기절.
        /// </summary>
        private static EnemyArt CreateEnemyArt(string dir, string prefix, string assetPath, bool FloatPivotFor)
        {
            if (!AssetDatabase.IsValidFolder(dir)) return null;
            ImportFrames(dir, file => file.Contains("_projectile_") ? CenterPivot : FloatPivotFor ? FloatPivot : GroundPivot);

            var art = AssetDatabase.LoadAssetAtPath<EnemyArt>(assetPath);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<EnemyArt>();
                AssetDatabase.CreateAsset(art, assetPath);
            }
            art.idle = LoadClip(dir, prefix, "idle", 6f);
            art.walk = LoadClip(dir, prefix, "move", 8f);
            art.windup = LoadClipRange(dir, prefix, "attack_normal", 0, 3, 8f);   // 예비 모션 길이에 맞추므로 fps는 안 쓴다
            art.slash = LoadClipRange(dir, prefix, "attack_normal", 3, 3, 12f);   // 3장 = 0.25초 (회복 0.35초 안)
            art.heavyWindup = LoadClip(dir, prefix, "heavy_windup", 8f);
            art.heavySlash = LoadClip(dir, prefix, "heavy_attack", 12f);
            art.stun = LoadClipRange(dir, prefix, "hurt", 2, 3, 6f);
            art.hit = LoadClipRange(dir, prefix, "hurt", 0, 2, 12f);
            art.death = LoadClip(dir, prefix, "death", 8f);
            art.slashHitFrame = 0;       // 베기 시트는 뻗은 장면부터
            art.heavySlashHitFrame = 1;  // 강공격 시트: 0 내리치기 시작 · 1 맞는 순간
            EditorUtility.SetDirty(art);
            return art;
        }

        /// <summary>만신(사당 NPC) 프레임(mentor_&lt;시트&gt;_&lt;번호&gt;.png)을 임포트하고 NpcArt로 묶는다.</summary>
        private static void CreateMentorArt()
        {
            if (!AssetDatabase.IsValidFolder(MentorArtDir)) return;
            ImportFrames(MentorArtDir, _ => GroundPivot);
            var art = AssetDatabase.LoadAssetAtPath<NpcArt>(MentorArtPath);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<NpcArt>();
                AssetDatabase.CreateAsset(art, MentorArtPath);
            }
            art.idle = LoadClip(MentorArtDir, "mentor", "idle", 5f);
            art.talk = LoadClip(MentorArtDir, "mentor", "talk", 6f);
            art.bless = LoadClip(MentorArtDir, "mentor", "bless", 8f);
            EditorUtility.SetDirty(art);
        }

        [System.Serializable]
        private class FloorMetaFile { public string name; public int height; public int surface; }
        [System.Serializable]
        private class FloorMetaZone { public string zone; public FloorMetaFile[] files; public int bodyPx; }
        [System.Serializable]
        private class FloorMeta { public FloorMetaZone[] zones; }

        /// <summary>바닥 타일 조각(floor_meta.json)을 임포트하고 구간별 FloorSet으로 묶는다. 윗면·발판은 표면 줄, 속 채움은 위 가운데가 피벗.</summary>
        private static void CreateFloorSets()
        {
            if (!AssetDatabase.IsValidFolder(FloorDir) || !File.Exists($"{FloorDir}/floor_meta.json")) return;
            var parsed = JsonUtility.FromJson<FloorMeta>(File.ReadAllText($"{FloorDir}/floor_meta.json"));
            var meta = new System.Collections.Generic.Dictionary<string, FloorMetaZone>();
            foreach (var z in parsed.zones) meta[z.zone] = z;
            var pivots = new System.Collections.Generic.Dictionary<string, Vector2>();
            foreach (var z in meta)
                foreach (var f in z.Value.files)
                    pivots[f.name] = f.name.Contains("_fill_") ? new Vector2(0.5f, 1f)
                        : new Vector2(0.5f, 1f - (float)f.surface / Mathf.Max(1, f.height));
            ImportFrames(FloorDir, file => pivots.TryGetValue(file, out var p) ? p : CenterPivot);

            foreach (string zone in FloorZones)
            {
                if (!meta.TryGetValue(zone, out var zm)) continue;
                string path = $"{FloorDir}/{zone}FloorSet.asset";
                var set = AssetDatabase.LoadAssetAtPath<FloorSet>(path);
                if (set == null)
                {
                    set = ScriptableObject.CreateInstance<FloorSet>();
                    AssetDatabase.CreateAsset(set, path);
                }
                Sprite[] Role(string role)
                {
                    var list = new System.Collections.Generic.List<Sprite>();
                    for (int i = 0; ; i++)
                    {
                        var sp = AssetDatabase.LoadAssetAtPath<Sprite>($"{FloorDir}/{zone}_{role}_{i}.png");
                        if (sp == null) break;
                        list.Add(sp);
                    }
                    return list.ToArray();
                }
                set.zoneName = zone;
                set.top = Role("top"); set.fill = Role("fill");
                set.platL = Role("platL"); set.platM = Role("platM"); set.platR = Role("platR");
                set.unitScale = PixelsPerUnit / Mathf.Max(1, zm.bodyPx); // 윗면 타일의 몸통 높이 = 판정 두께 1유닛
                set.fillDarken = 0f;
                EditorUtility.SetDirty(set);
            }
        }

        private static FloorSet[] LoadFloorSets()
        {
            var list = new System.Collections.Generic.List<FloorSet>();
            foreach (string zone in FloorZones)
            {
                var set = AssetDatabase.LoadAssetAtPath<FloorSet>($"{FloorDir}/{zone}FloorSet.asset");
                list.Add(set); // 없으면 null — FloorDecorator가 예전 네모로 둔다
            }
            return list.ToArray();
        }

        private static void AddFloorDecorator(Transform level, FloorSet[] floorSets, int forcedZone, params (Transform solid, FloorDecorator.Kind kind)[] segments)
        {
            var deco = level.gameObject.AddComponent<FloorDecorator>();
            var so = new SerializedObject(deco);
            var setsProp = so.FindProperty("sets");
            setsProp.arraySize = floorSets.Length;
            for (int i = 0; i < floorSets.Length; i++) setsProp.GetArrayElementAtIndex(i).objectReferenceValue = floorSets[i];
            var segProp = so.FindProperty("segments");
            segProp.arraySize = segments.Length;
            for (int i = 0; i < segments.Length; i++)
            {
                var e = segProp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("solid").objectReferenceValue = segments[i].solid;
                e.FindPropertyRelative("kind").enumValueIndex = (int)segments[i].kind;
            }
            so.FindProperty("forcedZone").intValue = forcedZone;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>UI 조각(ui_*.png)을 도트 설정으로 임포트하고 UiArt로 묶는다. 피벗은 가운데.</summary>
        private static void CreateUiArt()
        {
            if (!AssetDatabase.IsValidFolder(UiDir)) return;
            ImportFrames(UiDir, _ => CenterPivot);
            var art = AssetDatabase.LoadAssetAtPath<UiArt>(UiArtPath);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<UiArt>();
                AssetDatabase.CreateAsset(art, UiArtPath);
            }
            Sprite S(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{UiDir}/ui_{name}.png");
            art.hpFrame = S("hp_frame"); art.hpFill = S("hp_fill"); art.barEmpty = S("bar_empty");
            art.purpleFill = S("purple_fill"); art.dragonFrame = S("dragon_frame");
            art.orbEmpty = S("orb_empty"); art.orbFull = S("orb_full"); art.orbSpark = S("orb_spark"); art.orbString = S("orb_string");
            art.slotParchment = S("slot_parchment"); art.slotActive = S("slot_active"); art.slotLocked = S("slot_locked");
            art.cooldownDisc = S("cooldown_disc"); art.weaponStaff = S("weapon_staff"); art.weaponTalisman = S("weapon_talisman");
            art.iconHeart = S("icon_heart"); art.iconLantern = S("icon_lantern"); art.iconStaff = S("icon_staff"); art.iconTalisman = S("icon_talisman");
            art.skillScroll = S("skilltree_scroll"); art.nodeLocked = S("node_locked"); art.nodeOpen = S("node_open");
            art.nodeOwned = S("node_owned"); art.nodeSelected = S("node_selected"); art.lineDim = S("line_dim"); art.lineGlow = S("line_glow");
            art.skillIcons = new Sprite[12];
            for (int i = 0; i < 12; i++) art.skillIcons[i] = S($"skill_{i:00}");
            EditorUtility.SetDirty(art);
        }

        /// <summary>전투 이펙트 프레임을 임포트하고 AttackFxArt로 묶는다.</summary>
        private static void CreateAttackFxArt()
        {
            if (!AssetDatabase.IsValidFolder(FxDir)) return;
            ImportFrames(FxDir, _ => CenterPivot);
            // 봉인진 그림은 런타임에 빛기둥 층의 아래를 흐리게 만들려고 읽기 가능으로 임포트한다 (SealZone.MakePillars)
            foreach (string guid in AssetDatabase.FindAssets("fxhero_seal_ t:Texture2D", new[] { FxDir }))
            {
                var imp = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if (imp != null && !imp.isReadable) { imp.isReadable = true; imp.SaveAndReimport(); }
            }

            var fx = AssetDatabase.LoadAssetAtPath<AttackFxArt>(FxArtPath);
            if (fx == null)
            {
                fx = ScriptableObject.CreateInstance<AttackFxArt>();
                AssetDatabase.CreateAsset(fx, FxArtPath);
            }
            fx.enemySlash = new SpriteClip();       // 창귀 베기는 몸 그림이 맡는다 (판정 박스만 표시)
            fx.enemyHeavySlash = new SpriteClip();
            fx.meleeCounter = LoadClip(FxDir, "fxhero", "counter_big", 12f);   // 1016 퇴마사 이펙트(Tools/gwimun_sprites/cut_fxhero.py)
            fx.meleeAttack = LoadClip(FxDir, "fxhero", "combo1", 12f);
            fx.meleeAttack2 = LoadClip(FxDir, "fxhero", "combo2", 12f);
            fx.meleeAttack3 = LoadClip(FxDir, "fxhero", "combo3", 12f);
            fx.parryMelee = LoadClip(FxDir, "fxhero", "parry_shockwave", 12f);
            fx.parryRanged = LoadClip(FxDir, "fx", "parry_talisman", 16f);
            fx.talismanProjectile = LoadClip(FxDir, "fx", "talisman_projectile", 16f);
            fx.talismanBurst = LoadClip(FxDir, "fxhero", "talisman_hit", 12f);
            fx.hitSpark = LoadClip(FxDir, "fx", "hit_spark", 20f);
            fx.yokaiDissolve = LoadClip(FxDir, "fx", "yokai_dissolve", 12f);
            fx.heavyWarning = LoadClip(FxDir, "fx", "heavy_warning", 12f);
            fx.dustLand = LoadClip(FxDir, "fx", "dust_land", 14f);
            fx.yogiAbsorb = LoadClip(FxDir, "fxhero", "yogi_gain", 12f);
            fx.gatePortal = LoadClip(FxDir, "fx", "gate_portal", 10f);
            fx.portalSwirl = LoadClip(FxDir, "fx", "portal_swirl", 14f); // 16장 (Tools/gwimun_sprites/make_portal_swirl.py), 한 바퀴 약 1.1초
            fx.staffSlam = LoadClip(FxDir, "fxhero", "quake_wave", 12f);
            fx.sealStart = LoadClip(FxDir, "fxhero", "seal_start", 12f);
            fx.sealLoop = LoadClip(FxDir, "fxhero", "seal_loop", 12f);
            fx.sealEnd = LoadClip(FxDir, "fxhero", "seal_end", 12f);
            fx.perfectParry = LoadClip(FxDir, "fxhero", "perfect_parry", 12f);
            fx.dokkaebiProjectile = LoadClip(DokkaebibulArtDir, "dokkaebibul", "projectile", 12f);
            EditorUtility.SetDirty(fx);
        }

        /// <summary>구간별 배경 3겹(far·mid·near)을 임포트하고 BackgroundSet 에셋으로 묶는다. 그림이 없는 구간은 건너뛴다.</summary>
        private static BackgroundSet[] CreateBackgroundSets()
        {
            var sets = new System.Collections.Generic.List<BackgroundSet>();
            for (int i = 0; i < BgZones.Length; i++)
            {
                string dir = $"{BgDir}/{BgZones[i]}";
                if (!AssetDatabase.IsValidFolder(dir)) continue;
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { dir }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    settings.spriteAlignment = (int)SpriteAlignment.Center;
                    settings.spritePixelsPerUnit = 1024f / 12f;
                    settings.spriteMeshType = SpriteMeshType.FullRect;
                    settings.filterMode = FilterMode.Point;
                    settings.mipmapEnabled = false;
                    settings.alphaIsTransparency = true;
                    importer.SetTextureSettings(settings);
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }

                string assetPath = $"{dir}/{BgZones[i]}Set.asset";
                var set = AssetDatabase.LoadAssetAtPath<BackgroundSet>(assetPath);
                if (set == null)
                {
                    set = ScriptableObject.CreateInstance<BackgroundSet>();
                    AssetDatabase.CreateAsset(set, assetPath);
                }
                set.zoneName = BgZoneLabels[i];
                set.far = AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/far.png");
                set.mid = AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/mid.png");
                set.near = AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/near.png");
                set.nearInFront = BgNearInFront[i];
                EditorUtility.SetDirty(set);
                sets.Add(set);
            }
            return sets.ToArray();
        }

        /// <summary>도트 프레임 임포트 설정: 점 필터 · 90px/유닛 · 압축 없음 · 피벗은 파일마다 지정.</summary>
        private static void ImportFrames(string dir, System.Func<string, Vector2> pivotFor)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { dir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivotFor(Path.GetFileName(path));
                settings.spritePixelsPerUnit = PixelsPerUnit;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.filterMode = FilterMode.Point;
                settings.mipmapEnabled = false;
                settings.alphaIsTransparency = true;
                importer.SetTextureSettings(settings);
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        private static SpriteClip LoadClip(string dir, string prefix, string clip, float fps) =>
            LoadClipRange(dir, prefix, clip, 0, int.MaxValue, fps);

        /// <summary>시트의 start번째 프레임부터 count장.</summary>
        private static SpriteClip LoadClipRange(string dir, string prefix, string clip, int start, int count, float fps)
        {
            var frames = new System.Collections.Generic.List<Sprite>();
            for (int i = start; frames.Count < count; i++)
            {
                var s = AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{prefix}_{clip}_{i}.png");
                if (s == null) break;
                frames.Add(s);
            }
            return new SpriteClip { frames = frames.ToArray(), fps = fps };
        }

        private static void CreateEnemyPrefab(string path, string name, EnemyKind kind, Vector2 size, Color color, EnemyArt art, bool floating)
        {
            var go = CreateVisual(name, Vector2.zero, size, color, 8, null);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            var enemy = go.AddComponent<EnemyController>();
            var so = new SerializedObject(enemy);
            so.FindProperty("kind").enumValueIndex = (int)kind;
            if (kind == EnemyKind.Ranged)
            {
                so.FindProperty("maxHp").intValue = 60;
                so.FindProperty("moveSpeed").floatValue = 2.5f;
                so.FindProperty("xpReward").intValue = 15; // 원거리는 까다로운 적
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            if (art != null)
            {
                // 도트 그림 (판정 없음). 땅 몬스터는 발끝 = 네모 바닥, 떠 있는 도깨비불은 몸 중심 = 네모 가운데.
                // 부모의 늘림을 상쇄해 픽셀 비율 유지.
                var artGo = new GameObject("Art");
                artGo.transform.SetParent(go.transform, false);
                artGo.transform.localPosition = new Vector3(0f, floating ? 0f : -0.5f, 0f);
                artGo.transform.localScale = new Vector3(1f / size.x, 1f / size.y, 1f);
                var artSr = artGo.AddComponent<SpriteRenderer>();
                artSr.sortingOrder = 8;
                var sprite = go.AddComponent<EnemySprite>();
                Set(sprite, "artRenderer", artSr);
                Set(sprite, "art", art);
            }

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        // ───────────── 헬퍼 ─────────────

        /// <summary>스프라이트만 있는 네모 (판정 없음).</summary>
        private static GameObject CreateVisual(string name, Vector2 pos, Vector2 size, Color color, int order, Transform parent)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _square;
            sr.color = color;
            sr.sortingOrder = order;
            return go;
        }

        /// <summary>충돌 지형. 콜라이더 size는 기본값(1,1) 그대로 → 보이는 네모 = 충돌 범위.</summary>
        private static Transform CreateSolid(string name, Vector2 pos, Vector2 size, Color color, Transform parent)
        {
            var go = CreateVisual(name, pos, size, color, 0, parent);
            go.AddComponent<BoxCollider2D>();
            return go.transform;
        }

        /// <summary>아래에서 통과해 올라가고 위에서만 밟히는 얇은 발판 (↓ + 점프로 내려감). 콜라이더 size는 기본값 유지.</summary>
        private static Transform CreateOneWay(string name, Vector2 pos, Vector2 size, Color color, Transform parent)
        {
            var go = CreateVisual(name, pos, size, color, 0, parent);
            var col = go.AddComponent<BoxCollider2D>();
            col.usedByEffector = true;
            var effector = go.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.useSideFriction = false;
            effector.useSideBounce = false;
            effector.surfaceArc = 160f;
            return go.transform;
        }

        private static T LoadAsset<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

        /// <summary>에셋이 없어도(null) 오류 없이 넘어간다 — 비워두면 흰 네모/합성음으로 동작.</summary>
        private static void SetOptional(Object target, string property, Object value)
        {
            if (value == null) return;
            var so = new SerializedObject(target);
            so.FindProperty(property).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, string property, Object value)
        {
            if (value == null) Debug.LogError($"[ParryRL] {target.name}.{property}에 넣을 값이 null");
            var so = new SerializedObject(target);
            so.FindProperty(property).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string property, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
