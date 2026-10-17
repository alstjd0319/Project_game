using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ParryRL.Tests
{
    /// <summary>보스 불가살이: 마지막 방(평지)에서 등장 · 상단 퍼센트 체력바 · 50% 아래 변신 → 2페이즈(더 크고 빠름).</summary>
    public class BossTests
    {
        private static RoomController Room => Object.FindAnyObjectByType<RoomController>();

        private static IEnumerator LoadBossRoom()
        {
            var rooms = Enumerable.Repeat(RoomType.Normal, RunState.TotalRooms - 2).Append(RoomType.Boss).ToArray();
            yield return TestScene.LoadRun(rooms);
            GameTuning.Current.godMode = true;
            GameTuning.Current.enemyPassive = true;
            float t = 0f;
            while (BossController.Instance == null && t < 5f) { t += Time.deltaTime; yield return null; }
            Assert.IsNotNull(BossController.Instance, "보스가 나왔다");
            yield return null;
        }

        private static void Hit(EnemyController boss, int damage, bool heavy = false, HitStagger stagger = HitStagger.Full) =>
            boss.TakeDamage(damage, 1f, heavy, null, stagger);

        [UnityTest]
        public IEnumerator 마지막_방은_평지의_보스방이고_불가살이와_상단_체력바가_뜬다()
        {
            yield return LoadBossRoom();
            Assert.IsTrue(RunState.IsFinalRoom);
            Assert.AreEqual(RoomType.Boss, RunState.Current);
            Assert.AreEqual(RoomLayouts.Flat, Room.LayoutIndex, "보스방 지형은 평지");
            var boss = BossController.Instance;
            Assert.AreEqual(1, boss.Phase);
            Assert.IsTrue(Hud.BossBarVisible, "화면 상단 보스 체력바");
            StringAssert.Contains("불가살이", Hud.BossBarText);
            StringAssert.Contains("100%", Hud.BossBarText);
            Assert.AreEqual(0, Object.FindObjectsByType<WorldBar>().Length, "머리 위 체력바는 없다");

            Hit(boss.Enemy, boss.Enemy.MaxHp / 4);
            yield return null;
            StringAssert.Contains("75%", Hud.BossBarText, "퍼센트로 표시");
        }

        [UnityTest]
        public IEnumerator 체력이_50퍼센트_아래로_가면_변신해_2페이즈가_된다()
        {
            yield return LoadBossRoom();
            var boss = BossController.Instance;
            var enemy = boss.Enemy;
            var sprite = enemy.GetComponent<EnemySprite>();
            var p1Art = sprite.Art;
            Vector3 p1Scale = enemy.transform.localScale;

            Hit(enemy, enemy.MaxHp / 2 + 10);
            yield return null;
            Assert.IsTrue(boss.IsTransforming, "변신 시작");
            Assert.IsTrue(enemy.Invulnerable, "변신 중엔 무적");
            float hp = enemy.HpRatio;
            Hit(enemy, 100);
            Assert.AreEqual(hp, enemy.HpRatio, 1e-4f, "변신 중 피해 없음");
            yield return null; // 그림은 LateUpdate에서 바뀐다
            Assert.AreEqual("once", sprite.Clip, "변신 동작 재생");

            float t = 0f;
            while (boss.IsTransforming && t < 5f) { t += Time.deltaTime; yield return null; }
            Assert.AreEqual(2, boss.Phase);
            Assert.IsFalse(enemy.Invulnerable);
            Assert.Less(enemy.Tempo, 1f, "2페이즈는 공격 박자가 빠르다");
            Assert.Greater(enemy.transform.localScale.x, p1Scale.x, "모습(판정)이 커졌다");
            Assert.AreNotEqual(p1Art, sprite.Art, "2페이즈 그림");
            StringAssert.Contains("2페이즈", Hud.BossBarText);
            float feet = enemy.transform.position.y - enemy.transform.localScale.y * 0.5f;
            Assert.AreEqual(0f, feet, 0.05f, "커져도 발은 땅에");
        }

        [UnityTest]
        public IEnumerator 일페이즈_일반_공격은_3연타이고_타격마다_느낌표가_뜬다()
        {
            yield return LoadBossRoom();
            var enemy = BossController.Instance.Enemy;
            enemy.HeavyChanceOverride = 0f; // 일반 공격만
            Assert.AreEqual(3, enemy.ComboHits);
            // 플레이어를 보스 바로 앞에 두고 공격하게 한다
            var player = Object.FindAnyObjectByType<PlayerDefense>();
            var pos = new Vector3(enemy.transform.position.x - 3f, player.transform.position.y, 0f);
            player.transform.position = pos;
            player.GetComponent<Rigidbody2D>().position = pos;
            Physics2D.SyncTransforms();
            GameTuning.Current.enemyPassive = false;
            int cues0 = enemy.PressCueCount;
            float t = 0f;
            while (enemy.ComboStrikeCount < 3 && t < 6f) { t += Time.deltaTime; yield return null; }
            Assert.AreEqual(3, enemy.ComboStrikeCount, "한 번 공격에 세 번 친다");
            Assert.GreaterOrEqual(enemy.PressCueCount - cues0, 3, "타격마다 빨간 느낌표");
            Assert.IsTrue(enemy.IsComboAttack);
        }

        [UnityTest]
        public IEnumerator 이페이즈는_코_휘두르기_한_번이고_보스방_전용_배경이_깔린다()
        {
            yield return LoadBossRoom();
            var bg = Object.FindAnyObjectByType<ParallaxBackground>();
            Assert.IsTrue(bg.IsBossBackground, "불가살이 보스방 배경");
            var boss = BossController.Instance;
            boss.ForcePhase2();
            float t = 0f;
            while (boss.IsTransforming && t < 5f) { t += Time.deltaTime; yield return null; }
            Assert.AreEqual(1, boss.Enemy.ComboHits, "2페이즈 일반 공격은 한 번");
            // 일반 방은 보스 배경이 아니다
            yield return TestScene.LoadRun();
            yield return null;
            Assert.IsFalse(Object.FindAnyObjectByType<ParallaxBackground>().IsBossBackground);
        }

        [UnityTest]
        public IEnumerator 이페이즈에서_맞거나_기절해도_1페이즈_모습으로_돌아가지_않는다()
        {
            yield return LoadBossRoom();
            var boss = BossController.Instance;
            boss.ForcePhase2();
            float t = 0f;
            while (boss.IsTransforming && t < 5f) { t += Time.deltaTime; yield return null; }
            var sprite = boss.Enemy.GetComponent<EnemySprite>();
            bool p1Look = false;
            void Check()
            {
                var name = sprite.ArtRenderer.sprite != null ? sprite.ArtRenderer.sprite.name : "";
                if (name.StartsWith("bulgasari_p1") || name.StartsWith("bulgasari_hurt")) p1Look = true;
            }
            Hit(boss.Enemy, 5, heavy: true); // 강공격 반격 (보스는 경직은 안 걸리지만 맞은 그림 확인)
            for (float w = 0f; w < 0.4f; w += Time.deltaTime) { yield return null; Check(); }
            boss.Enemy.Stun(1.5f);
            for (float w = 0f; w < 0.8f; w += Time.deltaTime) { yield return null; Check(); }
            Assert.IsFalse(p1Look, "2페이즈 경직·기절 그림은 2페이즈 모습 (피격 시트는 1페이즈 크기라 안 쓴다)");
        }

        private static IEnumerator ParryOnce(EnemyController boss, PlayerDefense defense)
        {
            yield return new WaitForSeconds(1.1f); // 패링 쿨타임·판정 창이 끝나도록
            Assert.IsTrue(defense.TryActivate());
            Vector2 p = defense.transform.position;
            EnemyAttack.Spawn(boss, AttackType.Normal, new Vector2(p.x + 0.72f, p.y), Vector2.left, new Vector2(0.6f, 0.6f), 10f, 6f, Color.red, 10);
            yield return new WaitForSeconds(0.2f);
        }

        [UnityTest]
        public IEnumerator 보스_공격을_연속_5번_패링하면_기절하고_맞으면_처음부터()
        {
            yield return LoadBossRoom();
            GameTuning.Current.godMode = false;
            var boss = BossController.Instance;
            var defense = Object.FindAnyObjectByType<PlayerDefense>();
            var party = defense.GetComponent<PlayerParty>();
            for (int i = 0; i < 3; i++) yield return ParryOnce(boss.Enemy, defense);
            Assert.AreEqual(3, boss.ParryStreak);
            StringAssert.Contains("연속 패링 <b>3</b> / 5", Hud.BossBarText, "체력바에 연속 패링 표시");

            party.ClearInvulnerability();
            party.TakeDamage(5); // 맞으면 처음부터
            Assert.AreEqual(0, boss.ParryStreak);

            for (int i = 0; i < 4; i++) yield return ParryOnce(boss.Enemy, defense);
            Assert.IsFalse(boss.Enemy.IsStunned);
            yield return ParryOnce(boss.Enemy, defense);
            Assert.IsTrue(boss.Enemy.IsStunned, "5연속 패링 → 기절");
            Assert.AreEqual(1, boss.ParryStunCount);
            Assert.AreEqual(0, boss.ParryStreak, "기절하면 다시 0부터");
            yield return new WaitForSeconds(2.0f);
            Assert.IsTrue(boss.Enemy.IsStunned, "기절 저항 무시 — 2초 넘게 간다");
            yield return new WaitForSeconds(0.8f);
            Assert.IsFalse(boss.Enemy.IsStunned, "2.5초 뒤 풀린다");
        }

        [UnityTest]
        public IEnumerator 한_방에_큰_피해를_입어도_1페이즈에선_죽지_않고_변신한다()
        {
            yield return LoadBossRoom();
            var boss = BossController.Instance;
            Hit(boss.Enemy, 99999);
            yield return null;
            Assert.IsFalse(boss.Enemy.IsDead, "1페이즈에선 체력 1에서 멈춘다");
            Assert.IsTrue(boss.IsTransforming);
        }

        [UnityTest]
        public IEnumerator 보스는_경직에_걸리지_않는다_강공격_반격에도()
        {
            yield return LoadBossRoom();
            var enemy = BossController.Instance.Enemy;
            int staggers = 0;
            enemy.Staggered += () => staggers++;
            Hit(enemy, 5, heavy: false, stagger: HitStagger.Full);
            Hit(enemy, 5, heavy: true, stagger: HitStagger.Full);
            Assert.AreEqual(0, staggers, "반격·스킬·강공격 반격 모두 경직 없음");
            Assert.IsFalse(enemy.IsStaggered);
            // 하던 공격도 끊기지 않는다
            GameTuning.Current.enemyPassive = false;
            var player = Object.FindAnyObjectByType<PlayerDefense>();
            var pos = new Vector3(enemy.transform.position.x - 3f, player.transform.position.y, 0f);
            player.transform.position = pos;
            player.GetComponent<Rigidbody2D>().position = pos;
            Physics2D.SyncTransforms();
            float t = 0f;
            while (!enemy.IsWindingUp && t < 6f) { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(enemy.IsWindingUp);
            Hit(enemy, 5, heavy: true, stagger: HitStagger.Full);
            yield return null;
            Assert.IsTrue(enemy.IsWindingUp || enemy.IsRecovering, "예비 모션 중 맞아도 공격이 그대로 나간다");
        }

        [UnityTest]
        public IEnumerator 보스를_쓰러뜨리면_체력바가_사라지고_사당으로_나가는_문이_열린다()
        {
            yield return LoadBossRoom();
            var boss = BossController.Instance;
            boss.ForcePhase2();
            float t = 0f;
            while (boss.IsTransforming && t < 5f) { t += Time.deltaTime; yield return null; }
            Hit(boss.Enemy, 99999);
            t = 0f;
            while (Room.CurrentPhase != RoomController.Phase.Cleared && t < 5f) { t += Time.deltaTime; yield return null; }
            Assert.AreEqual(RoomController.Phase.Cleared, Room.CurrentPhase);
            Assert.IsFalse(Hud.BossBarVisible);
            Assert.AreEqual(1, Room.Gates.Count);
            Assert.IsTrue(Room.Gates[0].IsExit);
        }
    }
}
