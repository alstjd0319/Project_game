using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ParryRL.Tests
{
    /// <summary>도트 그림: 판정 네모와의 관계(숨김·겹쳐 보기·발끝 정렬)와 동작별 그림 전환.</summary>
    public class ArtTests
    {
        private PlayerSprite _sprite;
        private PlayerDefense _defense;
        private PlayerCombat _combat;
        private PlayerParty _party;
        private SpriteRenderer _box;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScene.LoadPrototype();
            foreach (var e in Object.FindObjectsByType<EnemyController>()) e.enabled = false;
            _sprite = Object.FindAnyObjectByType<PlayerSprite>();
            _defense = _sprite.GetComponent<PlayerDefense>();
            _combat = _sprite.GetComponent<PlayerCombat>();
            _party = _sprite.GetComponent<PlayerParty>();
            _box = _sprite.GetComponent<SpriteRenderer>();
            yield return new WaitForSeconds(0.3f); // 착지
        }

        [UnityTest]
        public IEnumerator 전사는_그림으로_보이고_판정네모는_숨는다()
        {
            Assert.IsTrue(_sprite.IsShowingArt, "전사는 도트 그림");
            Assert.IsFalse(_box.enabled, "판정 네모는 숨김");
            Assert.AreEqual("idle", _sprite.Clip);

            GameTuning.Current.showHitboxes = true;
            yield return null;
            Assert.IsTrue(_box.enabled, "연습 스위치: 판정 네모를 그림 위에 겹쳐 보기");
            Assert.Less(_box.color.a, 0.5f, "겹쳐 볼 때는 반투명");
            Assert.Greater(_box.sortingOrder, _sprite.ArtRenderer.sortingOrder, "그림보다 위에");
            GameTuning.Current.showHitboxes = false;
        }

        [UnityTest]
        public IEnumerator 그림_발끝은_판정네모_바닥이고_도트_설정이다()
        {
            yield return null;
            var col = _sprite.GetComponent<BoxCollider2D>();
            var art = _sprite.ArtRenderer;
            Assert.AreEqual(col.bounds.min.y, art.transform.position.y, 0.01f, "그림 피벗(발끝) = 네모 바닥");
            Assert.AreEqual(col.bounds.center.x, art.transform.position.x, 0.01f, "그림 피벗(삿갓 중심) = 네모 가운데");
            Assert.AreEqual(45f, art.sprite.pixelsPerUnit, "960×540 기준: 1유닛 = 45px");
            Assert.AreEqual(FilterMode.Point, art.sprite.texture.filterMode, "도트는 흐리게 늘리지 않는다");
            Assert.AreEqual(1f, art.transform.lossyScale.x, 0.001f, "부모 네모의 늘림을 상쇄 (픽셀 비율 유지)");
            Assert.AreEqual(1f, art.transform.lossyScale.y, 0.001f);
        }

        [UnityTest]
        public IEnumerator 패링_그림은_방어_활성시간에_맞춰_끝까지_재생된다()
        {
            _defense.TryActivate();
            yield return null;
            Assert.AreEqual("parry", _sprite.Clip);
            Assert.AreEqual(0, _sprite.Frame);

            int maxFrame = 0;
            while (_defense.State == DefenseState.Active)
            {
                maxFrame = Mathf.Max(maxFrame, _sprite.Frame);
                yield return null;
            }
            Assert.AreEqual(3, maxFrame, "활성이 끝나기 전에 마지막(4번째) 프레임까지 간다");

            yield return null;
            Assert.AreEqual("idle", _sprite.Clip, "헛스윙 뒤 대기");
        }

        [UnityTest]
        public IEnumerator 패링에_성공하면_반격_그림()
        {
            _defense.TryActivate();
            Vector2 p = _defense.transform.position;
            var attack = EnemyAttack.Spawn(null, AttackType.Normal, new Vector2(p.x + 1.2f, p.y), Vector2.left,
                new Vector2(0.6f, 0.6f), 10f, 6f, Color.white, 10);
            float t = 0f;
            while (attack != null && t < 1f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return null;
            Assert.AreEqual("counter", _sprite.Clip, "막자마자 반격 베기");

            // 히트스탑·슬로우모션이 끼어도 반격 4장이 끝나면 대기로
            t = 0f;
            while (_sprite.Clip == "counter" && t < 3f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.AreEqual("idle", _sprite.Clip, "한 번 재생하고 대기로");
        }

        [UnityTest]
        public IEnumerator 공격스킬_피격은_한번_재생()
        {
            _sprite.GetComponent<SkillGauge>().Add(3);
            Assert.IsTrue(_combat.TryAttackSkill());
            yield return null;
            Assert.AreEqual("attack", _sprite.Clip);
            Assert.AreEqual(_sprite.ArtFor(CharacterKind.Warrior).attackHitFrame, _sprite.Frame,
                "판정이 누르는 순간 생기므로 칼을 뻗은 장면부터 (판정-시각 일치)");

            _party.TakeDamage(1);
            yield return null;
            Assert.AreEqual("hit", _sprite.Clip, "새 동작이 들어오면 덮어쓴다");

            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual("idle", _sprite.Clip);
        }

        [UnityTest]
        public IEnumerator 사망_그림은_게임오버로_시간이_멈춰도_끝까지()
        {
            _party.TakeDamage(10000);
            yield return null;
            Assert.IsTrue(_party.IsDead);
            Assert.AreEqual("death", _sprite.Clip);
            Assert.AreEqual(0f, Time.timeScale, "게임오버");

            yield return new WaitForSecondsRealtime(1.2f);
            var art = _sprite.ArtFor(CharacterKind.Warrior);
            Assert.AreEqual(art.death.frames.Length - 1, _sprite.Frame, "쓰러진 장면에서 멈춤");
        }

        [UnityTest]
        public IEnumerator 대시_공중_착지_그림()
        {
            _sprite.GetComponent<SkillGauge>().Add(5);
            Assert.IsTrue(_combat.TryMoveSkill());
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual("dash", _sprite.Clip);
            Assert.IsTrue(Object.FindAnyObjectByType<FxPiece>() != null, "대시 잔상");

            yield return new WaitForSeconds(0.4f);
            _sprite.transform.position += Vector3.up * 3f; // 정점에서 떨어지기 시작
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual("air", _sprite.Clip);
            Assert.AreEqual(2, _sprite.Frame, "세로 속도가 거의 0 → 정점");

            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(3, _sprite.Frame, "떨어지는 중 → 낙하");

            var motor = _sprite.GetComponent<PlayerMotor>();
            float t = 0f;
            while (!motor.IsGrounded && t < 2f) { t += Time.deltaTime; yield return null; }
            yield return null;
            Assert.AreEqual("land", _sprite.Clip, "착지 순간 잠깐");
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual("idle", _sprite.Clip);
        }

        // ───────────── 공격 이펙트 ─────────────

        private static void AssertSameRect(Bounds expected, Bounds actual, string what)
        {
            Assert.AreEqual(expected.center.x, actual.center.x, 0.01f, what + " 가운데 x");
            Assert.AreEqual(expected.center.y, actual.center.y, 0.01f, what + " 가운데 y");
            Assert.AreEqual(expected.size.x, actual.size.x, 0.01f, what + " 가로");
            Assert.AreEqual(expected.size.y, actual.size.y, 0.01f, what + " 세로");
        }

        [UnityTest]
        public IEnumerator 몬스터_베기는_그림이고_그림은_판정박스와_같은_크기_판정이_꺼지면_부서진다()
        {
            var fx = GameAssets.AttackFx;
            Assert.IsNotNull(fx, "씬에 공격 이펙트 그림 연결");
            Vector2 p = _sprite.transform.position;
            // 플레이어에게 닿지 않는 곳에서 왼쪽으로 베기
            var slash = EnemyAttack.SpawnSlash(null, AttackType.Normal, new Vector2(p.x + 10f, 0.7f), -1f, 1.0f, 45f, 2.4f,
                new Color(1f, 0.7f, 0.45f, 0.55f), 10);
            var box = slash.GetComponent<SpriteRenderer>();
            var col = slash.GetComponent<BoxCollider2D>();
            var art = slash.transform.Find("Art").GetComponent<SpriteRenderer>();

            for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate(); // 다 뻗음 (0.05초)
            yield return null;
            Assert.AreEqual(2.4f, slash.transform.localScale.x, 0.001f);
            Assert.IsFalse(box.enabled, "판정 네모는 숨김");
            Assert.AreSame(fx.enemySlash.frames[0], art.sprite, "판정이 살아 있는 동안은 꽉 찬 첫 장");
            Assert.IsTrue(art.flipX, "왼쪽으로 베면 뒤집기");
            Physics2D.SyncTransforms();
            AssertSameRect(col.bounds, art.bounds, "그림 = 판정");

            GameTuning.Current.showHitboxes = true;
            yield return null;
            Assert.IsTrue(box.enabled, "판정 상자 보기");
            Assert.Greater(box.sortingOrder, art.sortingOrder);
            GameTuning.Current.showHitboxes = false;

            Bounds last = col.bounds;
            float t = 0f;
            while (slash != null && t < 1f) { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(slash == null, "판정이 끝나면 공격은 사라짐");
            var breakup = Object.FindAnyObjectByType<FxClip>();
            Assert.IsNotNull(breakup, "부서지는 그림은 판정 없이 마저 재생");
            Assert.GreaterOrEqual(breakup.Frame, 1);
            Assert.IsNull(breakup.GetComponent<Collider2D>());
            AssertSameRect(last, breakup.Renderer.bounds, "부서지는 그림도 같은 자리");
        }

        [UnityTest]
        public IEnumerator 몬스터_강공격_베기는_강공격_그림()
        {
            Vector2 p = _sprite.transform.position;
            var slash = EnemyAttack.SpawnSlash(null, AttackType.Heavy, new Vector2(p.x + 10f, 0.9f), 1f, 1.4f, 45f, 2.4f,
                new Color(1f, 0.1f, 0.3f, 1f), 20);
            yield return new WaitForFixedUpdate();
            var art = slash.transform.Find("Art").GetComponent<SpriteRenderer>();
            Assert.AreSame(GameAssets.AttackFx.enemyHeavySlash.frames[0], art.sprite);
            Assert.IsFalse(art.flipX);
        }

        [UnityTest]
        public IEnumerator 전사_공격스킬과_반격은_그림이고_판정박스와_같은_크기()
        {
            _sprite.GetComponent<SkillGauge>().Add(3);
            Assert.IsTrue(_combat.TryAttackSkill());
            var clip = Object.FindAnyObjectByType<FxClip>();
            Assert.IsNotNull(clip, "공격 스킬 그림");
            Assert.AreSame(GameAssets.AttackFx.warriorAttack.frames[0], clip.Renderer.sprite);
            Assert.IsNull(GameObject.Find("MeleeStrike"), "판정 네모는 안 보임");
            Vector2 p = _sprite.transform.position;
            int facing = _sprite.GetComponent<PlayerMotor>().Facing;
            Assert.AreEqual(facing < 0, clip.Renderer.flipX, "바라보는 쪽으로 베기");
            var expected = new Bounds(new Vector3(p.x + facing * 1.2f, p.y, 0f), new Vector3(2.4f, 1.6f, 0f));
            AssertSameRect(expected, clip.Renderer.bounds, "그림 = 공격 스킬 판정 (2.4×1.6)");
            Object.Destroy(clip.gameObject);
            yield return null;

            // 반격: 왼쪽에서 온 공격을 막으면 왼쪽으로 베기 그림
            _defense.TryActivate();
            EnemyAttack.Spawn(null, AttackType.Normal, new Vector2(p.x - 1.2f, p.y), Vector2.right,
                new Vector2(0.6f, 0.6f), 10f, 6f, Color.white, 10);
            FxClip counter = null;
            float t = 0f;
            while (counter == null && t < 1f) { counter = Object.FindAnyObjectByType<FxClip>(); t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsNotNull(counter, "반격 그림");
            Assert.AreSame(GameAssets.AttackFx.warriorCounter.frames[0], counter.Renderer.sprite);
            Assert.IsTrue(counter.Renderer.flipX, "왼쪽으로 반격");
            Assert.AreEqual(1.8f, counter.Renderer.bounds.size.x, 0.01f, "반격 판정 가로 1.8");
            Assert.AreEqual(1.6f, counter.Renderer.bounds.size.y, 0.01f);
        }

        // ───────────── 근접 몬스터 ─────────────

        private static EnemyController FindEnemy(EnemyKind kind) =>
            System.Linq.Enumerable.First(Object.FindObjectsByType<EnemyController>(), e => e.Kind == kind);

        private void PlaceNearPlayer(EnemyController enemy, float dx)
        {
            var pos = new Vector3(_sprite.transform.position.x + dx, enemy.transform.localScale.y * 0.5f, 0f);
            enemy.transform.position = pos;
            enemy.GetComponent<Rigidbody2D>().position = pos;
            Physics2D.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator 근접몬스터만_그림이고_발끝은_판정네모_바닥()
        {
            var melee = FindEnemy(EnemyKind.Melee);
            var art = melee.GetComponent<EnemySprite>();
            yield return null;
            Assert.IsNotNull(art, "근접 몬스터에 그림");
            Assert.IsTrue(art.IsShowingArt);
            Assert.IsFalse(melee.GetComponent<SpriteRenderer>().enabled, "판정 네모는 숨김");
            Assert.AreEqual("idle", art.Clip);

            var col = melee.GetComponent<BoxCollider2D>();
            Assert.AreEqual(col.bounds.min.y, art.ArtRenderer.transform.position.y, 0.01f, "그림 피벗(발끝) = 네모 바닥");
            Assert.AreEqual(col.bounds.center.x, art.ArtRenderer.transform.position.x, 0.01f, "그림 피벗(삿갓 중심) = 네모 가운데");
            Assert.AreEqual(45f, art.ArtRenderer.sprite.pixelsPerUnit);
            Assert.AreEqual(FilterMode.Point, art.ArtRenderer.sprite.texture.filterMode);
            Assert.AreEqual(1f, art.ArtRenderer.transform.lossyScale.x, 0.001f, "부모 네모의 늘림을 상쇄");
            Assert.AreEqual(1f, art.ArtRenderer.transform.lossyScale.y, 0.001f);

            GameTuning.Current.showHitboxes = true;
            yield return null;
            var box = melee.GetComponent<SpriteRenderer>();
            Assert.IsTrue(box.enabled, "판정 상자 보기: 네모를 그림 위에 겹침");
            Assert.Greater(box.sortingOrder, art.ArtRenderer.sortingOrder);
            GameTuning.Current.showHitboxes = false;

            var ranged = FindEnemy(EnemyKind.Ranged);
            Assert.IsNull(ranged.GetComponent<EnemySprite>(), "원거리 몬스터는 아직 네모");
            Assert.IsTrue(ranged.GetComponent<SpriteRenderer>().enabled);
        }

        [UnityTest]
        public IEnumerator 근접몬스터_예비모션은_반응시간에_맞춰_끝까지_재생되고_베기는_칼을_뻗은_장면부터()
        {
            GameTuning.Current.godMode = true;
            GameTuning.Current.heavyChance = 0f;
            var melee = FindEnemy(EnemyKind.Melee);
            var art = melee.GetComponent<EnemySprite>();
            PlaceNearPlayer(melee, -2f); // 플레이어 왼쪽 → 오른쪽을 본다
            melee.enabled = true;

            float t = 0f;
            while (!melee.IsWindingUp && t < 5f) { t += Time.deltaTime; yield return null; }
            yield return null;
            Assert.AreEqual("windup", art.Clip);
            Assert.IsFalse(art.ArtRenderer.flipX, "플레이어 쪽(오른쪽)을 본다");

            int maxFrame = 0;
            while (melee.IsWindingUp)
            {
                maxFrame = Mathf.Max(maxFrame, art.Frame);
                yield return null;
            }
            Assert.AreEqual(art.Art.windup.frames.Length - 1, maxFrame, "베기가 나가기 전에 마지막 예비 모션 장면까지");

            yield return null;
            Assert.AreEqual("slash", art.Clip, "베기 박스가 나가는 순간 베는 그림");
            Assert.LessOrEqual(art.Art.slashHitFrame, art.Frame, "칼을 뻗은 장면부터 (판정-시각 일치)");

            while (melee.IsRecovering) yield return null;
            yield return null;
            Assert.That(art.Clip == "idle" || art.Clip == "walk", $"회복이 끝나면 대기/걷기 (지금 {art.Clip})");
        }

        [UnityTest]
        public IEnumerator 근접몬스터_강공격은_강공격_그림()
        {
            GameTuning.Current.godMode = true;
            GameTuning.Current.heavyOnly = true;
            var melee = FindEnemy(EnemyKind.Melee);
            var art = melee.GetComponent<EnemySprite>();
            PlaceNearPlayer(melee, 2f);
            melee.enabled = true;

            float t = 0f;
            while (!melee.IsWindingUp && t < 5f) { t += Time.deltaTime; yield return null; }
            yield return null;
            Assert.AreEqual("heavyWindup", art.Clip);
            Assert.IsTrue(art.ArtRenderer.flipX, "플레이어 쪽(왼쪽)을 본다");

            while (melee.IsWindingUp) yield return null;
            yield return null;
            Assert.AreEqual("heavySlash", art.Clip);
            Assert.AreEqual(art.Art.heavySlashHitFrame, art.Frame, "준비 자세는 건너뛰고 내려찍는 장면부터");
        }

        [UnityTest]
        public IEnumerator 근접몬스터_피격_기절_사망_그림()
        {
            var melee = FindEnemy(EnemyKind.Melee);
            var art = melee.GetComponent<EnemySprite>();

            melee.TakeDamage(1, 1f, false);
            yield return null;
            Assert.AreEqual("hit", art.Clip, "경직 동안 피격 그림");
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual("idle", art.Clip);

            melee.Stun(0.5f);
            yield return null;
            Assert.AreEqual("stun", art.Clip);
            yield return new WaitForSeconds(0.7f);
            Assert.AreEqual("idle", art.Clip);

            Vector3 at = art.ArtRenderer.transform.position;
            var deathClip = art.Art.death;
            melee.TakeDamage(100000, 1f, true);
            yield return null;
            Assert.IsTrue(melee == null, "몬스터 오브젝트는 바로 사라짐");
            var corpse = Object.FindAnyObjectByType<EnemyCorpse>();
            Assert.IsNotNull(corpse, "쓰러지는 그림은 따로 남는다");
            Assert.AreEqual(at.x, corpse.transform.position.x, 0.01f);
            Assert.IsNull(corpse.GetComponent<Collider2D>(), "판정 없음");

            yield return new WaitForSeconds(0.75f);
            Assert.AreEqual(deathClip.frames.Length - 1, corpse.Frame, "마지막 장면(잉크 웅덩이)까지");
            yield return new WaitForSeconds(1f);
            Assert.IsTrue(corpse == null, "흐려지며 사라짐");
        }
    }
}
