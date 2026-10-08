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
        public IEnumerator Hud_전투중()
        {
            var defense = Object.FindAnyObjectByType<PlayerDefense>();
            var combat = defense.GetComponent<PlayerCombat>();
            var gauge = defense.GetComponent<SkillGauge>();
            var level = defense.GetComponent<PlayerLevel>();
            var augments = defense.GetComponent<AugmentManager>();

            // 증강 2개 획득 + 경험치 일부
            level.AddXp(20 + 30);
            yield return new WaitForSecondsRealtime(0.7f);
            while (augments.IsChoosing) augments.Choose(0);
            level.AddXp(18);

            gauge.Add(5);            // 이동·공격 가능, 스왑(10) 부족
            combat.TryAttackSkill(); // 공격 쿨타임
            defense.TryActivate();   // 헛스윙 쿨타임
            yield return new WaitForSeconds(0.55f);

            yield return Capture("hud_combat");
        }

        [UnityTest]
        public IEnumerator 증강_선택창()
        {
            var level = Object.FindAnyObjectByType<PlayerLevel>();
            level.AddXp(20 + 30); // 남은 선택 2 표시
            yield return new WaitForSecondsRealtime(1.2f); // 열림 + 카드 등장 애니메이션

            yield return Capture("augment_choice");
        }

        [UnityTest]
        public IEnumerator 증강목록창()
        {
            var level = Object.FindAnyObjectByType<PlayerLevel>();
            var augments = level.GetComponent<AugmentManager>();

            // 6레벨: 이미 가진 중첩형 증강이 후보에 있으면 그걸 골라 "×2"가 보이게
            Random.InitState(7);
            level.AddXp(20 + 30 + 40 + 50 + 60 + 70);
            yield return new WaitForSecondsRealtime(0.7f);
            while (augments.IsChoosing)
            {
                int pick = 0;
                for (int i = 0; i < augments.Options.Count; i++)
                {
                    var o = augments.Options[i];
                    if (o.MaxStacks > 1 && augments.StacksOf(o) > 0) { pick = i; break; }
                    if (o.MaxStacks > 1) pick = i;
                }
                augments.Choose(pick);
            }

            AugmentListPanel.Instance.SetVisible(true);
            yield return null;
            yield return Capture("augment_list");

            // 작은 게임 뷰에서는 글자가 상대적으로 넓어져 줄바꿈이 생긴다 (에디터 Game 탭 크기 재현)
            yield return Capture("augment_list_960x540", 960, 540);
            yield return Capture("augment_list_640x360", 640, 360);
        }

        [UnityTest]
        public IEnumerator 증강목록창_전부_모음()
        {
            var level = Object.FindAnyObjectByType<PlayerLevel>();
            var augments = level.GetComponent<AugmentManager>();
            level.AddXp(5000);
            yield return new WaitForSecondsRealtime(0.7f);
            while (augments.IsChoosing) augments.Choose(0);

            AugmentListPanel.Instance.SetVisible(true);
            yield return null;
            yield return Capture("augment_list_full");
        }

        [UnityTest]
        public IEnumerator 전사_피격_방어패시브와_캐릭터탭()
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
        public IEnumerator 궁수_원거리_반격()
        {
            var defense = Object.FindAnyObjectByType<PlayerDefense>();
            var party = defense.GetComponent<PlayerParty>();
            defense.GetComponent<SkillGauge>().Add(10);
            party.TryManualSwap();
            yield return new WaitForSeconds(0.4f);

            // 근접 몬스터를 9유닛 밖에 세우고, 그 몬스터가 보낸 공격을 회피 → 궁수 반격이 멀리 날아감
            var melee = System.Linq.Enumerable.First(Object.FindObjectsByType<EnemyController>(), e => e.Kind == EnemyKind.Melee);
            var pos = new Vector3(defense.transform.position.x + 9f, melee.transform.localScale.y * 0.5f, 0f);
            melee.transform.position = pos;
            melee.GetComponent<Rigidbody2D>().position = pos;
            Physics2D.SyncTransforms();

            defense.TryActivate();
            Vector2 p = defense.transform.position;
            EnemyAttack.Spawn(melee, AttackType.Normal, new Vector2(p.x + 1.2f, p.y), Vector2.left,
                new Vector2(0.6f, 0.6f), 10f, 6f, new Color(1f, 0.7f, 0.45f, 0.55f), 10);

            float t = 0f;
            while (melee.HpRatio >= 1f && t < 3f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return new WaitForSecondsRealtime(0.12f);
            yield return Capture("archer_range");
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

        [UnityTest]
        public IEnumerator 회피_순간()
        {
            var defense = Object.FindAnyObjectByType<PlayerDefense>();
            defense.GetComponent<SkillGauge>().Add(10);
            defense.GetComponent<PlayerParty>().TryManualSwap();
            yield return new WaitForSeconds(0.45f);
            defense.TryActivate();
            var attack = SpawnAttack(defense, 1.4f, heavy: false);
            float t = 0f;
            while (attack != null && !attack.IsGhost && t < 1f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return new WaitForSecondsRealtime(0.07f);
            yield return Capture("dodge_moment");
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

        // ───────────── 증강 ─────────────

        private static void PlaceEnemy(EnemyController enemy, PlayerDefense defense, float dx)
        {
            var pos = new Vector3(defense.transform.position.x + dx, enemy.transform.localScale.y * 0.5f, 0f);
            enemy.transform.position = pos;
            enemy.GetComponent<Rigidbody2D>().position = pos;
            Physics2D.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator 증강_등장충격파_기절()
        {
            var defense = Object.FindAnyObjectByType<PlayerDefense>();
            var party = defense.GetComponent<PlayerParty>();
            var gauge = defense.GetComponent<SkillGauge>();
            defense.GetComponent<AugmentManager>().Grant(AugmentCatalog.Get("w_shockwave"));
            gauge.Add(20);
            party.TryManualSwap(); // 궁수
            yield return new WaitForSeconds(0.45f);

            // 기절 색이 보이도록 AI는 켜 두되 공격은 끈다
            GameTuning.Current.enemyPassive = true;
            var enemies = Object.FindObjectsByType<EnemyController>();
            foreach (var e in enemies) e.enabled = true;
            PlaceEnemy(System.Linq.Enumerable.First(enemies, e => e.Kind == EnemyKind.Melee), defense, 2f);
            PlaceEnemy(System.Linq.Enumerable.First(enemies, e => e.Kind == EnemyKind.Ranged), defense, -2.2f);

            party.TryManualSwap(); // 전사 등장 → 충격파
            yield return new WaitForSecondsRealtime(0.1f);
            yield return Capture("aug_shockwave");
        }

        [UnityTest]
        public IEnumerator 증강_등장화살비()
        {
            var defense = Object.FindAnyObjectByType<PlayerDefense>();
            var party = defense.GetComponent<PlayerParty>();
            defense.GetComponent<AugmentManager>().Grant(AugmentCatalog.Get("a_arrow_rain"));
            var enemies = Object.FindObjectsByType<EnemyController>();
            PlaceEnemy(System.Linq.Enumerable.First(enemies, e => e.Kind == EnemyKind.Melee), defense, 5f);
            PlaceEnemy(System.Linq.Enumerable.First(enemies, e => e.Kind == EnemyKind.Ranged), defense, 9f);
            yield return new WaitForSeconds(0.3f);

            defense.GetComponent<SkillGauge>().Add(10);
            party.TryManualSwap(); // 궁수 등장 → 화살비
            yield return new WaitForSeconds(0.28f);
            yield return Capture("aug_arrow_rain");
        }

        [UnityTest]
        public IEnumerator 증강_갈래화살_교대공명_데미지설명()
        {
            var defense = Object.FindAnyObjectByType<PlayerDefense>();
            var augments = defense.GetComponent<AugmentManager>();
            augments.Grant(AugmentCatalog.Get("a_split_arrow"));
            augments.Grant(AugmentCatalog.Get("swap_resonance"));
            var enemies = Object.FindObjectsByType<EnemyController>();
            var melee = System.Linq.Enumerable.First(enemies, e => e.Kind == EnemyKind.Melee);
            PlaceEnemy(melee, defense, 6f);
            PlaceEnemy(System.Linq.Enumerable.First(enemies, e => e.Kind == EnemyKind.Ranged), defense, -7f);
            yield return new WaitForSeconds(0.3f);

            // 전사가 강공격 패링 → 궁수 등장(공명 시작) → 반격 + 갈래 화살 2발
            defense.TryActivate();
            Vector2 p = defense.transform.position;
            EnemyAttack.Spawn(melee, AttackType.Heavy, new Vector2(p.x + 1.2f, p.y), Vector2.left,
                new Vector2(0.9f, 0.9f), 8f, 6f, new Color(1f, 0.1f, 0.3f, 1f), 20);

            float t = 0f;
            while (melee.HpRatio >= 1f && t < 3f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return new WaitForSecondsRealtime(0.15f);
            yield return Capture("aug_split_resonance");
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
        public IEnumerator 튜닝패널_증강탭_골라받기와_게이지무한()
        {
            var panel = TuningPanel.Instance;
            GameTuning.Current.infiniteGauge = true;
            Object.FindAnyObjectByType<SkillGauge>().Add(20);
            panel.SetVisible(true);
            panel.SelectTab(TuningPanel.AugmentTab);
            panel.GrantFromPanel(AugmentCatalog.Get("wide_window"));
            panel.GrantFromPanel(AugmentCatalog.Get("wide_window"));
            panel.GrantFromPanel(AugmentCatalog.Get("w_blood_parry"));
            panel.GrantFromPanel(AugmentCatalog.Get("w_blood_parry"));
            panel.GrantFromPanel(AugmentCatalog.Get("a_split_arrow"));
            yield return null;
            yield return Capture("tuning_augments");
            yield return Capture("tuning_augments_640x360", 640, 360);
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
