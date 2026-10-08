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
        private const string SquarePath = "Assets/Art/Square.png";
        private const string NoFrictionPath = "Assets/Art/NoFriction.physicsMaterial2D";
        private const string MeleePrefabPath = "Assets/Prefabs/Enemy_Melee.prefab";
        private const string RangedPrefabPath = "Assets/Prefabs/Enemy_Ranged.prefab";

        private const string SpriteDir = "Assets/Sprites/ParryPrototype/";
        private const string AudioDir = "Assets/Audio/ParryPrototype/";

        private const float ArenaHalfWidth = 22f;

        // 도트 그림 규격 — Tools/sprites/process_sheets.ps1의 칸 규격과 같아야 한다.
        // 960×540 기준(1080p에서 2배) · 화면 높이 12유닛 → 1유닛 = 45px.
        private const float PixelsPerUnit = 45f;
        private const int FrameW = 160, FrameH = 88, AnchorX = 92, FootY = 84;
        private const string WarriorArtDir = "Assets/Art/Characters/Warrior";
        private const string WarriorArtPath = WarriorArtDir + "/WarriorArt.asset";
        // 근접 몬스터는 키가 더 크고 강공격 때 칼을 머리 위로 치켜들어 칸이 크다 (process_sheets.ps1의 melee cell과 같아야 한다).
        private const int MeleeFrameW = 176, MeleeFrameH = 120, MeleeAnchorX = 80, MeleeFootY = 116;
        private const string MeleeArtDir = "Assets/Art/Enemies/Melee";
        private const string MeleeArtPath = MeleeArtDir + "/MeleeArt.asset";
        // 공격 이펙트 — 프레임이 이미 판정 박스 크기(45px/유닛)라 피벗 가운데, 게임에서 박스 크기로 늘려 그린다.
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
            var meleeArt = CreateEnemyArt(MeleeArtDir, "melee", MeleeArtPath);
            CreateEnemyPrefab(MeleePrefabPath, "Enemy_Melee", EnemyKind.Melee,
                new Vector2(1f, 1.6f), new Color(0.58f, 0.46f, 0.78f), meleeArt);
            CreateEnemyPrefab(RangedPrefabPath, "Enemy_Ranged", EnemyKind.Ranged,
                new Vector2(0.9f, 1.3f), new Color(0.35f, 0.62f, 0.8f), null);
            CreateCharacterArt(CharacterKind.Warrior, WarriorArtDir, "warrior", WarriorArtPath);
            CreateAttackFxArt();
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene이 사용되지 않는 에셋을 언로드하므로, 씬에 넣을 에셋은 씬 생성 후에 다시 로드한다.
            _square = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
            var noFriction = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(NoFrictionPath);
            var warriorArt = AssetDatabase.LoadAssetAtPath<CharacterArt>(WarriorArtPath);
            var attackFx = AssetDatabase.LoadAssetAtPath<AttackFxArt>(FxArtPath);
            var meleePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MeleePrefabPath).GetComponent<EnemyController>();
            var rangedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RangedPrefabPath).GetComponent<EnemyController>();

            // ── 시스템 ──
            var systems = new GameObject("Systems");
            systems.AddComponent<GameManager>();
            var gameAssets = systems.AddComponent<GameAssets>();
            Set(gameAssets, "square", _square);
            if (attackFx != null) Set(gameAssets, "attackFx", attackFx);
            systems.AddComponent<HitFeel>();
            systems.AddComponent<AudioSource>();
            var sfx = systems.AddComponent<Sfx>();
            SetOptional(sfx, "parryClip", LoadAsset<AudioClip>(AudioDir + "parry_normal_success.wav"));
            SetOptional(sfx, "perfectParryClip", LoadAsset<AudioClip>(AudioDir + "parry_success.wav"));
            SetOptional(sfx, "dodgeClip", LoadAsset<AudioClip>(AudioDir + "dodge_success.wav"));
            SetOptional(sfx, "perfectDodgeClip", LoadAsset<AudioClip>(AudioDir + "perfect_dodge_success.wav"));
            systems.AddComponent<Hud>();

            // ── 레벨 ──
            var level = new GameObject("Level").transform;
            CreateSolid("Ground", new Vector2(0f, -0.5f), new Vector2(ArenaHalfWidth * 2f + 2f, 1f), new Color(0.3f, 0.32f, 0.38f), level);
            CreateSolid("Wall_L", new Vector2(-ArenaHalfWidth - 0.5f, 5f), new Vector2(1f, 12f), new Color(0.3f, 0.32f, 0.38f), level);
            CreateSolid("Wall_R", new Vector2(ArenaHalfWidth + 0.5f, 5f), new Vector2(1f, 12f), new Color(0.3f, 0.32f, 0.38f), level);
            CreateOneWay("Platform_L", new Vector2(-7f, 2.4f), new Vector2(4f, 0.4f), new Color(0.36f, 0.38f, 0.45f), level);
            CreateOneWay("Platform_R", new Vector2(7f, 2.4f), new Vector2(4f, 0.4f), new Color(0.36f, 0.38f, 0.45f), level);
            CreateOneWay("Platform_Top", new Vector2(0f, 4.4f), new Vector2(3f, 0.4f), new Color(0.36f, 0.38f, 0.45f), level);

            // 배경 기둥 (판정 없음, 이동감 표시용)
            var bg = new GameObject("Background").transform;
            for (float x = -ArenaHalfWidth; x <= ArenaHalfWidth; x += 4f)
                CreateVisual("Pillar", new Vector2(x, 4f), new Vector2(0.5f, 8f), new Color(1f, 1f, 1f, 0.035f), -10, bg);

            // ── 플레이어 ──
            var player = CreateVisual("Player", new Vector2(-3f, 0.7f), new Vector2(0.8f, 1.4f), new Color(0.95f, 0.38f, 0.32f), 10, null);
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
            var party = player.AddComponent<PlayerParty>();
            SetOptional(party, "warriorSprite", LoadAsset<Sprite>(SpriteDir + "warrior.png"));
            SetOptional(party, "warriorParrySprite", LoadAsset<Sprite>(SpriteDir + "warrior_parry.png"));
            SetOptional(party, "archerSprite", LoadAsset<Sprite>(SpriteDir + "archer.png"));
            SetOptional(party, "archerDodgeSprite", LoadAsset<Sprite>(SpriteDir + "archer_dodge.png"));
            player.AddComponent<PlayerCombat>();
            player.AddComponent<PlayerDefense>();
            player.AddComponent<PlayerLevel>();
            player.AddComponent<RunModifiers>(); // 이번 판 증강 보정 층 (획득한 증강 효과 컴포넌트가 여기 붙는다)
            player.AddComponent<AugmentManager>();

            // PlayerSprite는 PlayerDefense를 RequireComponent하므로 반드시 PlayerDefense를 붙인 뒤에 추가한다 (아니면 방어 컴포넌트가 둘이 된다)
            // 도트 애니메이션 (판정 없음). 발끝 = 네모 바닥, 부모의 늘림(0.8×1.4)을 상쇄해 픽셀 비율 유지.
            // 그림이 없는 캐릭터(궁수)는 PlayerParty의 스프라이트가 대신 보인다.
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
            list.arraySize = warriorArt != null ? 1 : 0;
            if (warriorArt != null) list.GetArrayElementAtIndex(0).objectReferenceValue = warriorArt;
            artsProp.ApplyModifiedPropertiesWithoutUndo();

            // ── 적 스포너 ──
            var spawner = new GameObject("EnemySpawner").AddComponent<EnemySpawner>();
            Set(spawner, "meleePrefab", meleePrefab);
            Set(spawner, "rangedPrefab", rangedPrefab);
            SetFloat(spawner, "arenaMinX", -ArenaHalfWidth);
            SetFloat(spawner, "arenaMaxX", ArenaHalfWidth);

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
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[ParryRL] 프로토타입 씬 생성 완료: {ScenePath}");
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
        /// 캐릭터 프레임(&lt;prefix&gt;_&lt;동작&gt;_&lt;번호&gt;.png)을 도트 설정으로 임포트하고 CharacterArt 에셋으로 묶는다.
        /// 폴더에 그림이 없으면 아무것도 하지 않는다 (그 캐릭터는 네모로 표시).
        /// </summary>
        private static void CreateCharacterArt(CharacterKind kind, string dir, string prefix, string assetPath)
        {
            if (!AssetDatabase.IsValidFolder(dir)) return;
            ImportFrames(dir, FrameW, FrameH, AnchorX, FootY);

            var art = AssetDatabase.LoadAssetAtPath<CharacterArt>(assetPath);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<CharacterArt>();
                AssetDatabase.CreateAsset(art, assetPath);
            }
            art.kind = kind;
            art.idle = LoadClip(dir, prefix, "idle", 6f);
            art.run = LoadClip(dir, prefix, "run", 12f);
            art.parry = LoadClip(dir, prefix, "parry", 12f);
            art.dash = LoadClip(dir, prefix, "dash", 20f); // 대시 0.15초 = 3프레임
            art.counter = LoadClip(dir, prefix, "counter", 14f); // 4장 ≈ 0.29초
            art.attack = LoadClip(dir, prefix, "attack", 14f);   // 5장 ≈ 0.36초 (공격 스킬 쿨타임 1초 안)
            art.jump = LoadClip(dir, prefix, "jump", 6f);        // 공중은 세로 속도로 고르므로 fps는 안 쓴다
            art.hit = LoadClip(dir, prefix, "hit", 12f);
            art.death = LoadClip(dir, prefix, "death", 6f);
            art.enter = LoadClip(dir, prefix, "enter", 12f);
            EditorUtility.SetDirty(art);
        }

        /// <summary>몬스터 프레임을 임포트하고 EnemyArt 에셋으로 묶는다. 그림이 없으면 null (네모로 표시).</summary>
        private static EnemyArt CreateEnemyArt(string dir, string prefix, string assetPath)
        {
            if (!AssetDatabase.IsValidFolder(dir)) return null;
            ImportFrames(dir, MeleeFrameW, MeleeFrameH, MeleeAnchorX, MeleeFootY);

            var art = AssetDatabase.LoadAssetAtPath<EnemyArt>(assetPath);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<EnemyArt>();
                AssetDatabase.CreateAsset(art, assetPath);
            }
            art.idle = LoadClip(dir, prefix, "idle", 6f);
            art.walk = LoadClip(dir, prefix, "walk", 8f);
            art.windup = LoadClip(dir, prefix, "windup", 8f);            // 예비 모션 길이에 맞추므로 fps는 안 쓴다
            art.slash = LoadClip(dir, prefix, "slash", 12f);             // 3장 = 0.25초 (회복 0.35초 안)
            art.heavyWindup = LoadClip(dir, prefix, "heavywindup", 8f);
            art.heavySlash = LoadClip(dir, prefix, "heavyslash", 12f);
            art.stun = LoadClip(dir, prefix, "stun", 6f);
            art.hit = LoadClip(dir, prefix, "hit", 8f);                  // 2장 = 0.25초 (경직 0.35초 안)
            art.death = LoadClip(dir, prefix, "death", 8f);
            EditorUtility.SetDirty(art);
            return art;
        }

        /// <summary>공격 이펙트 프레임을 임포트하고 AttackFxArt로 묶는다. 그림이 없으면 아무것도 안 함 (공격은 네모).</summary>
        private static void CreateAttackFxArt()
        {
            if (!AssetDatabase.IsValidFolder(FxDir)) return;
            // 피벗 가운데: 프레임마다 크기가 달라도(효과마다 판정 크기가 다름) 같은 식으로 맞춘다
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { FxDir }))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                settings.spritePixelsPerUnit = PixelsPerUnit;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.filterMode = FilterMode.Point;
                settings.mipmapEnabled = false;
                settings.alphaIsTransparency = true;
                importer.SetTextureSettings(settings);
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            var fx = AssetDatabase.LoadAssetAtPath<AttackFxArt>(FxArtPath);
            if (fx == null)
            {
                fx = ScriptableObject.CreateInstance<AttackFxArt>();
                AssetDatabase.CreateAsset(fx, FxArtPath);
            }
            fx.enemySlash = LoadClip(FxDir, "enemy", "slash", 20f);           // 부서지는 3장 = 0.15초
            fx.enemyHeavySlash = LoadClip(FxDir, "enemy", "heavyslash", 20f);
            fx.warriorCounter = LoadClip(FxDir, "warrior", "counter", 14f);  // 4장 ≈ 0.29초 (반격 그림과 비슷한 길이)
            fx.warriorAttack = LoadClip(FxDir, "warrior", "attack", 14f);
            EditorUtility.SetDirty(fx);
        }

        /// <summary>도트 프레임 임포트 설정: 점 필터 · 45px/유닛 · 피벗 = 칸의 기준점(삿갓 중심 · 발끝).</summary>
        private static void ImportFrames(string dir, int frameW, int frameH, int anchorX, int footY)
        {
            var pivot = new Vector2((float)anchorX / frameW, (float)(frameH - footY - 1) / frameH);

            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { dir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
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

        private static SpriteClip LoadClip(string dir, string prefix, string clip, float fps)
        {
            var frames = new System.Collections.Generic.List<Sprite>();
            for (int i = 0; ; i++)
            {
                var s = AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{prefix}_{clip}_{i}.png");
                if (s == null) break;
                frames.Add(s);
            }
            return new SpriteClip { frames = frames.ToArray(), fps = fps };
        }

        private static void CreateEnemyPrefab(string path, string name, EnemyKind kind, Vector2 size, Color color, EnemyArt art)
        {
            var go = CreateVisual(name, Vector2.zero, size, color, 8, null);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            var enemy = go.AddComponent<EnemyController>();
            var so = new SerializedObject(enemy);
            so.FindProperty("kind").enumValueIndex = (int)kind;
            if (art == null)
            {
                // 도트 애니메이션이 없는 몬스터(원거리)는 Project_Game 스프라이트 3장
                so.FindProperty("idleSprite").objectReferenceValue = LoadAsset<Sprite>(SpriteDir + "enemy_idle.png");
                so.FindProperty("windupSprite").objectReferenceValue = LoadAsset<Sprite>(SpriteDir + "enemy_windup.png");
                so.FindProperty("attackSprite").objectReferenceValue = LoadAsset<Sprite>(SpriteDir + "enemy_attack.png");
            }
            if (kind == EnemyKind.Ranged)
            {
                so.FindProperty("maxHp").intValue = 60;
                so.FindProperty("moveSpeed").floatValue = 2.5f;
                so.FindProperty("xpReward").intValue = 15; // 원거리는 스왑을 강요하는 까다로운 적
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            if (art != null)
            {
                // 도트 그림 (판정 없음). 발끝 = 네모 바닥, 부모의 늘림을 상쇄해 픽셀 비율 유지.
                var artGo = new GameObject("Art");
                artGo.transform.SetParent(go.transform, false);
                artGo.transform.localPosition = new Vector3(0f, -0.5f, 0f);
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
        private static void CreateSolid(string name, Vector2 pos, Vector2 size, Color color, Transform parent)
        {
            var go = CreateVisual(name, pos, size, color, 0, parent);
            go.AddComponent<BoxCollider2D>();
        }

        /// <summary>아래에서 통과해 올라가고 위에서만 밟히는 얇은 발판 (↓ + 점프로 내려감). 콜라이더 size는 기본값 유지.</summary>
        private static void CreateOneWay(string name, Vector2 pos, Vector2 size, Color color, Transform parent)
        {
            var go = CreateVisual(name, pos, size, color, 0, parent);
            var col = go.AddComponent<BoxCollider2D>();
            col.usedByEffector = true;
            var effector = go.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.useSideFriction = false;
            effector.useSideBounce = false;
            effector.surfaceArc = 160f;
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
