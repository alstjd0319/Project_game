using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ParryRL.Tests
{
    /// <summary>
    /// 화면 확인용 캡처. 프로젝트 루트의 Captures/*.png 로 저장된다 (Assets 밖이라 임포트되지 않음).
    /// UI·연출을 바꾸면 여기에 장면을 추가해 눈으로 확인한다.
    /// </summary>
    public class CaptureTests
    {
        private const int Width = 1920;
        private const int Height = 1080;

        private static string CaptureDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures"));

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScene.LoadPrototype();
            foreach (var e in Object.FindObjectsByType<EnemyController>()) e.enabled = false;
        }

        [UnityTest]
        public IEnumerator 런_방_진행_웨이브와_문_고르기()
        {
            yield return TestScene.LoadRun();
            GameTuning.Current.godMode = true;
            var player = Object.FindAnyObjectByType<PlayerDefense>();
            var room = Object.FindAnyObjectByType<RoomController>();
            void Put(float x)
            {
                var pos = new Vector3(x, player.transform.position.y, 0f);
                player.transform.position = pos;
                player.GetComponent<Rigidbody2D>().position = pos;
                player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                Physics2D.SyncTransforms();
            }
            yield return new WaitForSeconds(2.2f);
            yield return Capture("run_wave");                 // 방에 들어오면 가장 왼쪽에서 시작                 // 1파도: 상단 "방 1 / 6 · 일반"
            for (int i = 0; i < 2; i++)
            {
                foreach (var e in Object.FindObjectsByType<EnemyController>()) if (!e.IsDead) e.TakeDamage(99999, 1f, false);
                yield return new WaitForSeconds(2.4f);
            }
            Put(room.Gates[1].transform.position.x - 1.5f);
            yield return new WaitForSeconds(1.2f);
            yield return Capture("run_gate_right");           // 안쪽 문 앞: 종류 표시 + 안내 + 일렁이는 소용돌이 (두 문 모두 오른쪽 끝)
            Put(room.Gates[0].transform.position.x - 1.5f);
            yield return new WaitForSeconds(1.2f);
            yield return Capture("run_gate_left");            // 바깥 문 앞

            // 포탈 연출: 들어가며 흩어짐 → 다음 방에서 빛으로 나타남
            PortalTransition.PlayEffects = true;
            try
            {
                Put(room.Gates[0].transform.position.x);
                yield return new WaitForSeconds(0.1f);
                room.Gates[0].Enter();
                yield return new WaitForSeconds(0.17f);
                yield return Capture("portal_enter");             // 몸이 보라 빛으로 흩어지는 중
                float w = 0f;
                while (RunState.RoomNumber < 2 && w < 3f) { w += Time.deltaTime; yield return null; }
                yield return new WaitForSeconds(0.25f);
                yield return Capture("portal_exit");              // 새 방 왼쪽 끝에서 빛이 감겨 몸이 되는 중
            }
            finally
            {
                PortalTransition.PlayEffects = false;
            }
        }

        [UnityTest]
        public IEnumerator 런_포탈_종류별_틀과_색()
        {
            yield return TestScene.LoadRun(RoomType.Reward); // 싸움 없는 평지 방
            GameTuning.Current.godMode = true;
            var room = Object.FindAnyObjectByType<RoomController>();
            var player = Object.FindAnyObjectByType<PlayerDefense>();
            yield return new WaitForSeconds(0.3f);
            float cx = Camera.main.transform.position.x;
            var kinds = new (RoomType type, bool exit)[] { (RoomType.Normal, false), (RoomType.Elite, false), (RoomType.Shop, false), (RoomType.Reward, false), (RoomType.Boss, false), (RoomType.Normal, true) };
            for (int i = 0; i < kinds.Length; i++)
                RoomGate.Create(new Vector3(cx - 9f + i * 3.6f, 0f, 0f), kinds[i].type, kinds[i].exit, null, room.PortalArt);
            var pos = new Vector3(cx - 11.5f, player.transform.position.y, 0f);
            player.transform.position = pos;
            player.GetComponent<Rigidbody2D>().position = pos;
            yield return new WaitForSeconds(1.2f);
            yield return Capture("portal_frames_all");     // 일반 · 엘리트 · 상점 · 보상 · 보스 · 사당 출구
        }

        [UnityTest]
        public IEnumerator 런_방_종류별_제단_상점_보상_엘리트()
        {
            void Put(float x)
            {
                var player = Object.FindAnyObjectByType<PlayerDefense>();
                var pos = new Vector3(x, player.transform.position.y, 0f);
                player.transform.position = pos;
                player.GetComponent<Rigidbody2D>().position = pos;
                player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                Physics2D.SyncTransforms();
            }
            // 상점: 엽전 100, 아이템 두 개를 가진 상태
            yield return TestScene.LoadRun(RoomType.Shop);
            RunState.AddGold(100);
            RunState.AddSoulStones(2);
            RunState.AddItem(RunItemCatalog.All[0]);
            RunState.AddItem(RunItemCatalog.All[2]);
            yield return new WaitForSeconds(2.0f);
            Put(-3.25f);
            yield return new WaitForSeconds(0.6f);
            yield return Capture("run_shop");

            // 보상방
            yield return TestScene.LoadRun(RoomType.Reward);
            yield return new WaitForSeconds(2.0f);
            Put(0f);
            yield return new WaitForSeconds(0.6f);
            yield return Capture("run_reward");
            var altars = Object.FindObjectsByType<RoomAltar>();
            var soulAltar = System.Linq.Enumerable.FirstOrDefault(altars, a => a.Offer.Kind == OfferKind.Soul) ?? altars[0];
            Put(soulAltar.transform.position.x - 1.2f);
            soulAltar.TryTake();
            yield return new WaitForSeconds(0.3f);
            yield return Capture("run_reward_open");          // 상자가 열리며 영혼석이 튀어 오른다
            yield return new WaitForSeconds(0.6f);
            yield return Capture("run_reward_opened");

            // 엘리트방: 깬 뒤 영혼석/아이템 제단
            yield return TestScene.LoadRun(RoomType.Elite);
            GameTuning.Current.godMode = true;
            yield return new WaitForSeconds(2.2f);
            Capture("run_elite_fight");
            yield return new WaitForSeconds(0.1f);
            for (int i = 0; i < 4 && RoomController.Instance.CurrentPhase != RoomController.Phase.Cleared; i++)
            {
                foreach (var e in Object.FindObjectsByType<EnemyController>()) if (!e.IsDead) e.TakeDamage(99999, 1f, false);
                yield return new WaitForSeconds(2.0f);
            }
            Put(-3.25f);
            yield return new WaitForSeconds(0.8f);
            yield return Capture("run_elite_reward");
        }

        [UnityTest]
        public IEnumerator 런_방_지형_레이아웃_모음()
        {
            var names = new[] { "basic", "stairs", "zigzag", "tower", "roof", "flat" };
            for (int index = 0; index < names.Length; index++)
            {
                RoomLayouts.ForcedIndex = index;
                yield return TestScene.LoadRun(RoomType.Normal);
                GameTuning.Current.godMode = true;
                GameTuning.Current.showHitboxes = true; // 배경의 떠 있는 섬과 헷갈리지 않게 발판 판정 네모를 함께 보인다
                foreach (var e in Object.FindObjectsByType<EnemyController>()) e.enabled = false;
                var cam = Camera.main;
                cam.orthographicSize = 9.5f; // 넓게 — 발판 배치 전체가 한 장에 보이도록
                var rig = cam.GetComponent<CameraRig>();
                if (rig != null) rig.enabled = false;
                cam.transform.position = new Vector3(0f, 4.2f, -10f);
                yield return new WaitForSeconds(0.5f);
                yield return Capture("layout_" + names[index]);
            }
            RoomLayouts.ForcedIndex = -1;
        }

        [UnityTest]
        public IEnumerator 레벨디자인_구간별_전투_장면()
        {
            // 방 번호 → 구간(배경·바닥) + 구간에 맞는 장면: 1구간 시간차(창귀만) · 2구간 엄호 사격(높이 뜬 도깨비불) · 3구간 포위
            var shots = new[] { ("stagger", RoomLayouts.Stairs, 1, "encounter_zone1_stagger"), ("cover", RoomLayouts.Tower, 3, "encounter_zone2_cover"), ("siege", RoomLayouts.Roof, 4, "encounter_zone3_siege") };
            foreach (var (id, layout, entered, name) in shots)
            {
                EncounterCatalog.ForcedId = id;
                RoomLayouts.ForcedIndex = layout;
                var rooms = new RoomType[entered];
                for (int i = 0; i < entered; i++) rooms[i] = RoomType.Normal;
                yield return TestScene.LoadRun(rooms);
                GameTuning.Current.godMode = true;
                GameTuning.Current.enemyPassive = true;
                var player = Object.FindAnyObjectByType<PlayerDefense>();
                var pos = new Vector3(-6f, 0.8f, 0f); // 뒤에서 들어올 자리가 있게 방 가운데 쪽으로
                player.transform.position = pos;
                player.GetComponent<Rigidbody2D>().position = pos;
                yield return new WaitUntil(() => RoomController.Instance.CurrentPhase == RoomController.Phase.Fighting);
                yield return new WaitForSeconds(2.8f); // 늦게 들어오는 몬스터까지
                yield return Capture(name);
            }
            EncounterCatalog.ForcedId = null;
            RoomLayouts.ForcedIndex = -1;
        }

        [UnityTest]
        public IEnumerator 런_바닥_구멍과_추락_복귀()
        {
            RoomLayouts.ForcedIndex = RoomLayouts.Zigzag;
            yield return TestScene.LoadRun(RoomType.Normal);
            GameTuning.Current.enemyPassive = true;
            foreach (var e in Object.FindObjectsByType<EnemyController>()) e.enabled = false;
            var player = Object.FindAnyObjectByType<PlayerDefense>();
            var pit = RoomLayouts.All[RoomLayouts.Zigzag].Pits[0];
            void Put(float x, float y)
            {
                var pos = new Vector3(x, y, 0f);
                player.transform.position = pos;
                player.GetComponent<Rigidbody2D>().position = pos;
                player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                Physics2D.SyncTransforms();
            }
            Put(pit.Left - 2.2f, 0.8f);
            yield return new WaitForSeconds(1.0f);
            yield return Capture("run_pit");                  // 바닥에 뚫린 구멍 앞
            Put(pit.X, 0.8f);
            yield return new WaitForSeconds(0.45f);           // 빠지는 중
            yield return Capture("run_fall_in");
            yield return new WaitForSeconds(1.0f);            // 되돌아온 뒤: 추락 피해 · 깜빡이는 무적
            yield return Capture("run_fall_back");
            RoomLayouts.ForcedIndex = -1;
        }

        [UnityTest]
        public IEnumerator 보스_불가살이_1페이즈_변신_2페이즈()
        {
            var rooms = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Append(System.Linq.Enumerable.Repeat(RoomType.Normal, RunState.TotalRooms - 2), RoomType.Boss));
            yield return TestScene.LoadRun(rooms);
            GameTuning.Current.godMode = true;
            var player = Object.FindAnyObjectByType<PlayerDefense>();
            float t = 0f;
            while (BossController.Instance == null && t < 5f) { t += Time.deltaTime; yield return null; }
            var boss = BossController.Instance;
            // 보스 앞(왼쪽)으로 플레이어를 옮겨 한 화면에
            var pos = new Vector3(boss.transform.position.x - 6f, player.transform.position.y, 0f);
            player.transform.position = pos;
            player.GetComponent<Rigidbody2D>().position = pos;
            Physics2D.SyncTransforms();
            if (CameraRig.Instance != null) CameraRig.Instance.SnapToTarget();
            GameTuning.Current.enemyPassive = false;
            boss.Enemy.HeavyChanceOverride = 0f;
            boss.Enemy.ComboChance = 1f;
            bool announced = false;
            boss.Enemy.ComboPreparing += () => announced = true;
            for (float w = 0f; w < 5f && !announced; w += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(0.3f);
            yield return Capture("boss_combo_announce");      // "불가살이가 연속 공격을 준비합니다!" + 웅크림
            for (float w = 0f; w < 6f && boss.Enemy.ComboStrikeCount < 1; w += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(0.08f);
            yield return Capture("boss_claw");               // 첫 타격의 할퀴기 궤적
            for (float w = 0f; w < 6f && boss.Enemy.ComboStrikeCount < 2; w += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(0.05f);
            yield return Capture("boss_combo");               // 1페이즈 3연타 중 (둘째 타격 뒤 셋째 느낌표)
            boss.Enemy.HeavyChanceOverride = 0.35f;
            GameTuning.Current.enemyPassive = true;
            yield return new WaitForSeconds(1.2f);
            boss.Enemy.TakeDamage(boss.Enemy.MaxHp / 5, 1f, false, null, HitStagger.Light);
            yield return new WaitForSeconds(0.6f);
            yield return Capture("boss_phase1");              // 1페이즈 + 상단 체력바(80%)
            boss.Enemy.TakeDamage(boss.Enemy.MaxHp / 3, 1f, false, null, HitStagger.Light);
            yield return new WaitForSeconds(0.9f);
            yield return Capture("boss_transform");           // 변신 중
            while (boss.IsTransforming) yield return null;
            yield return new WaitForSeconds(0.8f);
            yield return Capture("boss_phase2");              // 2페이즈 (더 큰 모습, 체력바 "2페이즈")
            GameTuning.Current.enemyPassive = false;
            GameTuning.Current.heavyOnly = true;
            for (float w = 0f; w < 4f && !boss.Enemy.IsWindingUp; w += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(0.45f);
            yield return Capture("boss_phase2_heavy");        // 2페이즈 강공격 예비 모션
            GameTuning.Current.heavyOnly = false;
        }

        [UnityTest]
        public IEnumerator 보스_등장_연출()
        {
            var rooms = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Append(System.Linq.Enumerable.Repeat(RoomType.Normal, RunState.TotalRooms - 2), RoomType.Boss));
            yield return TestScene.LoadRun(true, rooms);
            GameTuning.Current.godMode = true;
            GameTuning.Current.enemyPassive = true;
            yield return new WaitForSeconds(1.5f);
            var player = Object.FindAnyObjectByType<PlayerDefense>();
            var pos = player.transform.position + Vector3.right * (RoomController.BossTriggerAdvance + 1f);
            player.transform.position = pos;
            player.GetComponent<Rigidbody2D>().position = pos;
            Physics2D.SyncTransforms();
            while (BossController.Instance == null) yield return null;
            yield return new WaitForSeconds(0.45f);
            yield return Capture("boss_entrance_warn");       // 내려앉을 땅이 달아오름
            yield return new WaitForSeconds(0.45f);
            yield return Capture("boss_entrance_fall");       // 떨어지는 중
            yield return new WaitForSeconds(0.25f);
            yield return Capture("boss_entrance_land");       // 착지 충격 + 이름·체력바
        }

        [UnityTest]
        public IEnumerator 보스_특수_패턴_가시_땅찍기_돌진()
        {
            var rooms = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Append(System.Linq.Enumerable.Repeat(RoomType.Normal, RunState.TotalRooms - 2), RoomType.Boss));
            IEnumerator Setup(float dx)
            {
                yield return TestScene.LoadRun(rooms);
                GameTuning.Current.godMode = true;
                GameTuning.Current.enemyPassive = true;
                float w = 0f;
                while (BossController.Instance == null && w < 5f) { w += Time.deltaTime; yield return null; }
                var player = Object.FindAnyObjectByType<PlayerDefense>();
                var pos = new Vector3(BossController.Instance.transform.position.x + dx, player.transform.position.y, 0f);
                player.transform.position = pos;
                player.GetComponent<Rigidbody2D>().position = pos;
                Physics2D.SyncTransforms();
                if (CameraRig.Instance != null) CameraRig.Instance.SnapToTarget();
                yield return new WaitForSeconds(0.5f);
            }
            yield return Setup(-8f);
            BossController.Instance.StartPattern(BossPattern.SpineVolley);
            yield return new WaitForSeconds(0.75f);
            yield return Capture("boss_pattern_windup");      // 예비 동작 + 느낌표
            yield return new WaitForSeconds(0.45f);
            yield return Capture("boss_pattern_spines");      // 가시 발사

            yield return Setup(-8f);
            BossController.Instance.StartPattern(BossPattern.GroundSlam);
            yield return new WaitForSeconds(1.15f);
            yield return Capture("boss_pattern_slam");        // 땅 찍기 충격파

            yield return Setup(-12f);
            BossController.Instance.StartPattern(BossPattern.Dash);
            yield return new WaitForSeconds(1.15f);
            yield return Capture("boss_pattern_dash_p1");     // 1페이즈 돌진 (멀리 있으면)

            yield return Setup(-11f);
            BossController.Instance.ForcePhase2();
            while (BossController.Instance.IsTransforming) yield return null;
            BossController.Instance.StartPattern(BossPattern.Dash);
            yield return new WaitForSeconds(1.05f);
            yield return Capture("boss_pattern_dash");        // 2페이즈 돌진
        }

        [UnityTest]
        public IEnumerator 보스_연속_패링_기절()
        {
            var rooms = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Append(System.Linq.Enumerable.Repeat(RoomType.Normal, RunState.TotalRooms - 2), RoomType.Boss));
            yield return TestScene.LoadRun(rooms);
            GameTuning.Current.godMode = true;
            GameTuning.Current.enemyPassive = true;
            float w = 0f;
            while (BossController.Instance == null && w < 5f) { w += Time.deltaTime; yield return null; }
            var boss = BossController.Instance;
            var defense = Object.FindAnyObjectByType<PlayerDefense>();
            var pos = new Vector3(boss.transform.position.x - 5f, defense.transform.position.y, 0f);
            defense.transform.position = pos;
            defense.GetComponent<Rigidbody2D>().position = pos;
            Physics2D.SyncTransforms();
            if (CameraRig.Instance != null) CameraRig.Instance.SnapToTarget();
            for (int i = 0; i < 5; i++)
            {
                yield return new WaitForSeconds(1.1f);
                defense.TryActivate();
                Vector2 p = defense.transform.position;
                EnemyAttack.Spawn(boss.Enemy, AttackType.Normal, new Vector2(p.x + 0.72f, p.y), Vector2.left, new Vector2(0.6f, 0.6f), 10f, 6f, Color.red, 10);
                yield return new WaitForSeconds(0.25f);
                if (i == 3) yield return Capture("boss_parry_streak");   // 체력바 "연속 패링 4 / 5"
            }
            yield return new WaitForSeconds(0.25f);
            yield return Capture("boss_parry_stun");                  // 5연속 패링 → 기절
        }

        [UnityTest]
        public IEnumerator 스킬_D_F_연출()
        {
            var defense = Object.FindAnyObjectByType<PlayerDefense>();
            var combat = defense.GetComponent<PlayerCombat>();
            var gauge = defense.GetComponent<SkillGauge>();
            var enemies = Object.FindObjectsByType<EnemyController>();
            void Put(float dx)
            {
                foreach (var e in enemies)
                {
                    var pos = new Vector3(defense.transform.position.x + dx, e.transform.localScale.y * 0.5f, 0f);
                    e.transform.position = pos;
                    e.GetComponent<Rigidbody2D>().position = pos;
                    dx += 1.3f;
                }
            }
            GameTuning.Current.enemyInvincible = true; // 여러 번 맞아도 살아 있게
            gauge.Add(20);
            Put(2.2f);
            yield return new WaitForSeconds(0.3f);

            combat.TrySkill(1);                         // 봉 휩쓸기
            yield return new WaitForSeconds(0.1f);
            yield return Capture("skill_staff_sweep");

            yield return new WaitForSeconds(2.9f);
            Put(2.2f);
            combat.TrySkill(2);                         // 봉 내려찍기
            yield return new WaitForSeconds(0.2f);
            yield return Capture("skill_staff_slam");

            yield return new WaitForSeconds(0.5f);
            combat.TrySwitchWeapon();
            yield return new WaitForSeconds(0.6f);
            Put(3.0f);
            combat.TrySkill(1);                         // 부적 부채꼴
            yield return new WaitForSeconds(0.12f);
            yield return Capture("skill_talisman_fan");

            yield return new WaitForSeconds(5.2f);      // 내려찍기 쿨타임(6초)이 지나도록
            Put(3.0f);
            combat.TrySkill(2);                         // 부적 봉인진
            yield return new WaitForSeconds(0.9f);
            yield return Capture("skill_talisman_seal");
        }

        [UnityTest]
        public IEnumerator 사당_포탈_NPC_스킬트리()
        {
            yield return TestScene.LoadHub();
            var interactor = Object.FindAnyObjectByType<PlayerInteractor>();
            var body = interactor.GetComponent<Rigidbody2D>();
            void MoveTo(float x)
            {
                var pos = new Vector3(x, interactor.transform.position.y, 0f);
                interactor.transform.position = pos;
                body.position = pos;
                body.linearVelocity = Vector2.zero;
                Physics2D.SyncTransforms();
            }
            var portal = Object.FindAnyObjectByType<ShrinePortal>();
            var npc = Object.FindAnyObjectByType<ShrineNpc>();

            yield return new WaitForSeconds(1.2f);
            yield return Capture("hub_start");

            MoveTo(npc.transform.position.x - 0.5f);
            yield return new WaitForSeconds(1.2f);
            yield return Capture("hub_npc_prompt");

            interactor.TryInteract();
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Capture("hub_skilltree_panel");
            SkillTreePanel.Close();

            // 레벨 6(포인트 5)로 몇 칸 익힌 모습
            Object.FindAnyObjectByType<PlayerLevel>().Restore(6, 0);
            SkillTreeState.TryBuy(SkillTreeState.IndexOf(SkillBranch.Health, 0), 6);
            SkillTreeState.TryBuy(SkillTreeState.IndexOf(SkillBranch.Health, 0), 6);
            SkillTreeState.TryBuy(SkillTreeState.IndexOf(SkillBranch.MeleeAttack, 0), 6);
            SkillTreeState.TryBuy(SkillTreeState.IndexOf(SkillBranch.Resource, 0), 6);
            interactor.TryInteract();
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Capture("hub_skilltree_learned");
            SkillTreePanel.Close();
            SkillTreeState.Reset();
            Object.FindAnyObjectByType<PlayerLevel>().Restore(1, 0);

            MoveTo(portal.transform.position.x - 7f);   // 멀리서 본 문 (은은하게)
            yield return new WaitForSeconds(1.5f);
            yield return Capture("hub_portal_far");

            MoveTo(portal.transform.position.x + 0.6f);
            yield return new WaitForSeconds(1.5f);
            yield return Capture("hub_portal_prompt");
        }

        [UnityTest]
        public IEnumerator 바닥_구간별_타일()
        {
            string[] names = { "shrine", "zone1", "zone2", "zone3" };
            for (int z = 0; z < names.Length; z++)
            {
                GameTuning.Current.backgroundZone = z;
                yield return new WaitForSeconds(0.5f);
                yield return Capture("floor_" + names[z]);
            }
        }

        [UnityTest]
        public IEnumerator 화면_끝에서_벽_없이_막힌다()
        {
            foreach (var e in Object.FindObjectsByType<EnemyController>()) e.gameObject.SetActive(false);
            Camera.main.aspect = 16f / 9f; // 배치모드의 작은 창 비율이 아니라 실제 1920×1080 화면 기준으로 카메라가 멈추게
            var player = Object.FindAnyObjectByType<PlayerMotor>();
            var body = player.GetComponent<Rigidbody2D>();
            // 오른쪽 벽에 몸이 닿는 자리(벽 안쪽 면 23 − 몸 반폭 0.4)에 세워 화면 끝에서 막히는 모습을 본다
            var pos = new Vector3(22.55f, 0.7f, 0f);
            body.position = pos;
            player.transform.position = pos;
            body.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1.2f);
            Debug.Log("[edge] player x=" + player.transform.position.x + " camera x=" + Camera.main.transform.position.x);
            Assert.AreEqual(22.55f, player.transform.position.x, 0.1f, "벽 안쪽 면에 닿아 더 가지 못한다");
            yield return Capture("floor_wall_edge_right");
        }

        [UnityTest]
        public IEnumerator 도력_구슬_중간_상태()
        {
            var defense = Object.FindAnyObjectByType<PlayerDefense>();
            defense.GetComponent<SkillGauge>().Add(8);
            defense.GetComponent<PlayerParty>().TakeDamage(110); // 체력도 일부 닳은 상태
            yield return new WaitForSecondsRealtime(0.15f);
            yield return Capture("hud_dorok_gained");   // 막 얻은 순간: 새로 찬 구슬만 반짝인다
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Capture("hud_dorok_partial");  // 시간이 지나면 갖고 있는 구슬은 모두 같은 모양
        }

        [UnityTest]
        public IEnumerator 근접_일반공격_느낌표()
        {
            GameTuning.Current.heavyChance = 0f; // 일반 공격만
            var defense = Object.FindAnyObjectByType<PlayerDefense>();
            EnemyController melee = null;
            foreach (var e in Object.FindObjectsByType<EnemyController>())
            {
                if (e.Kind == EnemyKind.Melee) { melee = e; e.enabled = true; } else e.gameObject.SetActive(false); // SetUp이 적 AI를 꺼 두므로 근접만 켠다
            }
            var pos = new Vector3(defense.transform.position.x + 2.2f, melee.transform.localScale.y * 0.5f, 0f);
            melee.transform.position = pos;
            melee.GetComponent<Rigidbody2D>().position = pos;
            // 예비 모션이 시작되면 노란 느낌표 → 베기 직전 빨간 느낌표
            float t = 0f;
            while (Object.FindAnyObjectByType<PressCueIcon>() == null && t < 3f) { t += Time.deltaTime; yield return null; }
            yield return new WaitForSecondsRealtime(0.25f);
            yield return Capture("melee_cue_yellow");
            t = 0f;
            while (melee.PressCueCount == 0 && t < 3f) { t += Time.deltaTime; yield return null; }
            yield return new WaitForSecondsRealtime(0.1f);
            yield return Capture("melee_press_cue");
        }

        [UnityTest]
        public IEnumerator 근접_베기_대시로_피해도_끊기지_않는다()
        {
            var defense = Object.FindAnyObjectByType<PlayerDefense>();
            var motor = defense.GetComponent<PlayerMotor>();
            EnemyController melee = null;
            foreach (var e in Object.FindObjectsByType<EnemyController>())
            {
                if (e.Kind == EnemyKind.Melee) melee = e; else e.gameObject.SetActive(false);
            }
            var pos = new Vector3(defense.transform.position.x + 2.0f, melee.transform.localScale.y * 0.5f, 0f);
            melee.transform.position = pos;
            melee.GetComponent<Rigidbody2D>().position = pos;
            melee.enabled = false;
            yield return new WaitForSeconds(0.3f);
            motor.Dash(-1);   // 베기 순간 뒤로 대시 (무적)
            var slash = EnemyAttack.SpawnSlash(melee, AttackType.Normal, new Vector2(melee.transform.position.x - melee.HalfWidth, defense.transform.position.y), -1f, 1.2f, 45f, 2.6f, Color.red, 10);
            yield return new WaitForSecondsRealtime(0.07f);
            yield return Capture("melee_slash_through_dash");
        }

        [UnityTest]
        public IEnumerator 봉_일반공격_단계별()
        {
            GameTuning.Current.enemyInvincible = true; // 여러 번 맞아도 살아 있게
            TestScene.SetAttackSkillFree(true);
            var defense = Object.FindAnyObjectByType<PlayerDefense>();
            var combat = defense.GetComponent<PlayerCombat>();
            var enemies = Object.FindObjectsByType<EnemyController>();
            void Put()
            {
                float dx = 1.6f;
                foreach (var e in enemies)
                {
                    var pos = new Vector3(defense.transform.position.x + dx, e.transform.localScale.y * 0.5f, 0f);
                    e.transform.position = pos;
                    e.GetComponent<Rigidbody2D>().position = pos;
                    dx += 1.2f;
                }
            }
            yield return new WaitForSeconds(0.3f);
            for (int stage = 1; stage <= 3; stage++)
            {
                Put();
                combat.TryAttackSkill();
                yield return new WaitForSecondsRealtime(0.17f);   // 베기가 가장 크게 펼쳐지는 중간 장면
                yield return Capture("combo_stage" + stage);
                yield return new WaitForSeconds(0.45f);
            }
        }

        [UnityTest]
        public IEnumerator 봉_베기_이동하며_공격해도_이펙트가_따라간다()
        {
            TestScene.SetAttackSkillFree(true);
            foreach (var e in Object.FindObjectsByType<EnemyController>()) e.gameObject.SetActive(false);
            var defense = Object.FindAnyObjectByType<PlayerDefense>();
            var combat = defense.GetComponent<PlayerCombat>();
            var body = defense.GetComponent<Rigidbody2D>();
            yield return new WaitForSeconds(0.3f);
            combat.TryAttackSkill();
            // 오른쪽으로 달리며 베기 (달리는 중에 공격)
            for (float t = 0f; t < 0.17f; t += Time.deltaTime)
            {
                body.linearVelocity = new Vector2(9f, body.linearVelocity.y);
                yield return null;
            }
            yield return Capture("combo_while_running");
        }

        [UnityTest]
        public IEnumerator Hud_전투중()
        {
            var defense = Object.FindAnyObjectByType<PlayerDefense>();
            var combat = defense.GetComponent<PlayerCombat>();
            var gauge = defense.GetComponent<SkillGauge>();
            var level = defense.GetComponent<PlayerLevel>();

            // 경험치 일부 (레벨 3)
            level.AddXp(20 + 30 + 18);

            gauge.Add(5);            // 이동·공격 가능
            combat.TryAttackSkill(); // 공격 쿨타임
            defense.TryActivate();   // 헛스윙 쿨타임
            yield return new WaitForSeconds(0.55f);

            yield return Capture("hud_combat");
        }

        [UnityTest]
        public IEnumerator 원거리_무기_스킬창_변환()
        {
            var defense = Object.FindAnyObjectByType<PlayerDefense>();
            var combat = defense.GetComponent<PlayerCombat>();
            TestScene.SetAttackSkillFree(true);
            var melee = Object.FindObjectsByType<EnemyController>()[0];
            melee.transform.position = new Vector3(defense.transform.position.x + 6f, melee.transform.localScale.y * 0.5f, 0f);
            melee.GetComponent<Rigidbody2D>().position = melee.transform.position;
            yield return new WaitForSeconds(0.3f);
            yield return Capture("weapon_melee_skillbar");

            combat.TrySwitchWeapon();
            yield return new WaitForSeconds(0.15f);
            combat.TryAttackSkill();
            yield return new WaitForSeconds(0.12f);
            yield return Capture("weapon_ranged_skillbar");
        }

        [UnityTest]
        public IEnumerator 배경_구간별()
        {
            var defense = Object.FindAnyObjectByType<PlayerDefense>();
            string[] names = { "shrine", "zone1", "zone2", "zone3" };
            for (int i = 0; i < names.Length; i++)
            {
                GameTuning.Current.backgroundZone = i;
                yield return new WaitForSeconds(0.25f);
                yield return Capture("bg_" + names[i]);
            }
            GameTuning.Current.backgroundZone = 1;
        }

        [UnityTest]
        public IEnumerator 플레이어_피격_방어패시브와_캐릭터탭()
        {
            var defense = Object.FindAnyObjectByType<PlayerDefense>();
            TuningPanel.Instance.SetVisible(true);
            TuningPanel.Instance.SelectTab(2);
            yield return new WaitForSeconds(0.3f); // 착지

            SpawnAttackAtPlayer(defense, 1.0f); // 방어 없이 맞음 → "-7 (방어 3)"
            yield return new WaitForSecondsRealtime(0.25f);
            yield return Capture("warrior_passive");
        }

        [UnityTest]
        public IEnumerator 패링_순간()
        {
            var defense = Object.FindAnyObjectByType<PlayerDefense>();
            yield return new WaitForSeconds(0.3f);
            defense.TryActivate();
            var attack = SpawnAttack(defense, 1.4f, heavy: false);
            float t = 0f;
            while (attack != null && t < 1f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return new WaitForSecondsRealtime(0.04f);
            yield return Capture("parry_moment");
        }

        // ───────────── 몬스터 ─────────────

        [UnityTest]
        public IEnumerator 근접몬스터_베기와_원거리_투사체()
        {
            var defense = Object.FindAnyObjectByType<PlayerDefense>();
            GameTuning.Current.godMode = true;
            GameTuning.Current.heavyChance = 0f;
            var enemies = Object.FindObjectsByType<EnemyController>();
            var melee = System.Linq.Enumerable.First(enemies, e => e.Kind == EnemyKind.Melee);
            var ranged = System.Linq.Enumerable.First(enemies, e => e.Kind == EnemyKind.Ranged);
            PlaceEnemy(melee, defense, 2.1f);
            PlaceEnemy(ranged, defense, -6f);
            yield return new WaitForSeconds(0.3f);

            // 근접: 일반 베기가 한 번에 뻗은 직후 + 날아오는 원거리 투사체 (둘의 모양 차이)
            melee.enabled = true;
            Vector2 p = defense.transform.position;
            EnemyAttack.Spawn(ranged, AttackType.Normal, (Vector2)ranged.transform.position, p - (Vector2)ranged.transform.position,
                new Vector2(0.6f, 0.6f), 8f, 16f, new Color(1f, 0.7f, 0.45f, 0.55f), 10);
            EnemyAttack slash = null;
            float t = 0f;
            while ((slash == null || !slash.IsSlash) && t < 5f)
            {
                slash = System.Linq.Enumerable.FirstOrDefault(Object.FindObjectsByType<EnemyAttack>(), a => a.IsSlash);
                t += Time.deltaTime;
                yield return null;
            }
            yield return new WaitForSeconds(0.07f);
            yield return Capture("enemy_melee_slash");

            // 강공격: 긴 예비 모션(몸이 크게 부풀고 빨개짐, "!!") → 두껍고 진한 베기
            yield return new WaitForSeconds(1.5f);
            foreach (var a in Object.FindObjectsByType<EnemyAttack>()) Object.Destroy(a.gameObject);
            GameTuning.Current.heavyOnly = true;
            PlaceEnemy(melee, defense, 2.1f);
            t = 0f;
            while (melee.transform.localScale.x <= 1.001f && t < 5f) { t += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(0.6f);
            yield return Capture("enemy_melee_heavy_windup");
            slash = null;
            t = 0f;
            while (slash == null && t < 3f) { slash = Object.FindAnyObjectByType<EnemyAttack>(); t += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(0.07f);
            yield return Capture("enemy_melee_slash_heavy");
        }


        private static void PlaceEnemy(EnemyController enemy, PlayerDefense defense, float dx)
        {
            var pos = new Vector3(defense.transform.position.x + dx, enemy.transform.localScale.y * 0.5f, 0f);
            enemy.transform.position = pos;
            enemy.GetComponent<Rigidbody2D>().position = pos;
            Physics2D.SyncTransforms();
        }

        private static EnemyAttack SpawnAttack(PlayerDefense defense, float fromDx, bool heavy)
        {
            Vector2 p = defense.transform.position;
            return EnemyAttack.Spawn(null, heavy ? AttackType.Heavy : AttackType.Normal, new Vector2(p.x + fromDx, p.y), Vector2.left,
                new Vector2(0.6f, 0.6f), 8f, 6f, new Color(1f, 0.7f, 0.45f, 0.55f), 10);
        }
        [UnityTest]
        public IEnumerator 튜닝패널_타격감탭()
        {
            TuningPanel.Instance.SetVisible(true);
            TuningPanel.Instance.SelectTab(0);
            yield return null;
            yield return Capture("tuning_hitfeel");
        }

        [UnityTest]
        public IEnumerator 튜닝패널_연습탭_몬스터끄기()
        {
            GameTuning.Current.hideRanged = true;
            TuningPanel.Instance.SetVisible(true);
            TuningPanel.Instance.SelectTab(4);
            yield return null;
            yield return null;
            yield return Capture("tuning_practice");
        }

        [UnityTest]
        public IEnumerator 튜닝패널_판정탭_타이밍기록()
        {
            var defense = Object.FindAnyObjectByType<PlayerDefense>();
            TuningPanel.Instance.SetVisible(true);
            TuningPanel.Instance.SelectTab(1);
            yield return new WaitForSeconds(0.3f); // 착지

            // 1) 제때 막음 → "누르고 0.xx초 뒤 성공"
            defense.TryActivate();
            SpawnAttackAtPlayer(defense, 2f);
            yield return new WaitForSecondsRealtime(0.5f);

            // 2) 너무 일찍 눌러 헛스윙 → 쿨타임 중 피격 → "너무 일찍"
            defense.TryActivate();
            yield return new WaitForSeconds(0.45f);
            SpawnAttackAtPlayer(defense, 1.2f);
            yield return new WaitForSecondsRealtime(0.25f);

            yield return Capture("tuning_judge");
        }

        private static void SpawnAttackAtPlayer(PlayerDefense defense, float fromDx)
        {
            Vector2 p = defense.transform.position;
            EnemyAttack.Spawn(null, AttackType.Normal, new Vector2(p.x + fromDx, p.y), Vector2.left,
                new Vector2(0.6f, 0.6f), 10f, 6f, new Color(1f, 0.7f, 0.45f, 0.55f), 10);
        }

        /// <summary>오버레이 캔버스를 카메라 렌더로 바꿔 PNG로 저장.</summary>
        public static IEnumerator Capture(string name, int width = Width, int height = Height)
        {
            foreach (var canvas in Object.FindObjectsByType<Canvas>())
            {
                if (!canvas.isRootCanvas) continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = Camera.main;
                canvas.planeDistance = 1f;
            }

            var cam = Camera.main;
            var rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;
            // 캔버스 배율이 바뀐 뒤 UI가 다시 배치될 시간 (배율에 따라 글자 폭이 달라짐)
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;

            Directory.CreateDirectory(CaptureDir);
            string path = Path.Combine(CaptureDir, name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
            Debug.Log($"[Capture] {path}");
            Assert.IsTrue(File.Exists(path));
        }
    }
}
