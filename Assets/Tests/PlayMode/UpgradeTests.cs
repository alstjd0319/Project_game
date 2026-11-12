using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ParryRL.Tests
{
    /// <summary>웨이브 강화 3택 (docs/기획_웨이브모드.md 7-2장): 뽑기 규칙 · 고르기 창 · 효과 8종.</summary>
    public class UpgradeTests
    {
        private PlayerDefense _defense;
        private PlayerParty _party;
        private PlayerCombat _combat;
        private SkillGauge _gauge;
        private WaveUpgradeEffects _fx;
        private EnemyController[] _enemies;

        private Vector2 PlayerPos => _defense.transform.position;

        private IEnumerator Setup()
        {
            yield return TestScene.LoadPrototype();
            WaveUpgrades.Reset();
            _defense = Object.FindAnyObjectByType<PlayerDefense>();
            _party = _defense.GetComponent<PlayerParty>();
            _combat = _defense.GetComponent<PlayerCombat>();
            _gauge = _defense.GetComponent<SkillGauge>();
            _fx = _defense.gameObject.AddComponent<WaveUpgradeEffects>();
            _enemies = Object.FindObjectsByType<EnemyController>().Where(e => !e.IsDead).ToArray();
            foreach (var e in _enemies) { e.enabled = false; Place(e, 20f); }
            yield return new WaitForSeconds(0.3f);
        }

        private void Place(EnemyController enemy, float dx)
        {
            var pos = new Vector3(PlayerPos.x + dx, enemy.transform.localScale.y * 0.5f, 0f);
            enemy.transform.position = pos;
            enemy.GetComponent<Rigidbody2D>().position = pos;
            Physics2D.SyncTransforms();
        }

        private EnemyAttack Shoot(AttackType type, EnemyController source, float fromDx)
        {
            var origin = new Vector2(PlayerPos.x + fromDx, PlayerPos.y);
            return EnemyAttack.Spawn(source, type, origin, new Vector2(-Mathf.Sign(fromDx), 0f), new Vector2(0.6f, 0.6f),
                10f, 6f, Color.red, type == AttackType.Heavy ? 20 : 10);
        }

        private IEnumerator Parry(AttackType type, EnemyController source, float fromDx = 1.5f)
        {
            Assert.IsTrue(_defense.TryActivate());
            Shoot(type, source, fromDx);
            yield return new WaitForSeconds(0.3f);
        }

        [TearDown]
        public void TearDown()
        {
            WaveUpgrades.Reset();
            RoomController.UseWaveMap = false;
            WaveController.RestTime = 3f;
            WaveController.OfferUpgrades = false;
            Time.timeScale = 1f;
        }

        [Test]
        public void 뽑기는_서로_다른_3개이고_패링_강화가_하나_이상_최대_단계는_빠진다()
        {
            WaveUpgrades.Reset();
            for (int seed = 0; seed < 50; seed++)
            {
                var picks = WaveUpgrades.Roll(3, new System.Random(seed));
                Assert.AreEqual(3, picks.Distinct().Count());
                Assert.IsTrue(picks.Any(id => WaveUpgrades.Def(id).Category == UpgradeCategory.Parry));
            }
            for (int i = 0; i < 3; i++) WaveUpgrades.Add(UpgradeId.SoulDrain);
            Assert.IsFalse(WaveUpgrades.Add(UpgradeId.SoulDrain), "3단계가 최대");
            for (int seed = 0; seed < 50; seed++)
                Assert.IsFalse(WaveUpgrades.Roll(3, new System.Random(seed)).Contains(UpgradeId.SoulDrain));
            Assert.AreEqual(8, WaveUpgrades.All.Length);
        }

        [UnityTest]
        public IEnumerator 웨이브를_깨면_게임이_멈추고_3장_중_하나를_고른다()
        {
            RoomController.UseWaveMap = true;
            WaveController.RestTime = 0.2f;
            yield return TestScene.LoadRun();
            WaveController.OfferUpgrades = true;
            GameTuning.Current.godMode = true;
            GameTuning.Current.enemyPassive = true;
            for (float t = 0f; t < 10f && (WaveController.Instance == null || WaveController.Instance.Wave != 1); t += Time.deltaTime) yield return null;
            var waves = WaveController.Instance;
            Assert.AreEqual(1, waves.Wave);
            for (float t = 0f; t < 40f && !UpgradeChoicePanel.IsOpen; t += 0.1f)
            {
                foreach (var e in Object.FindObjectsByType<EnemyController>()) if (!e.IsDead) e.TakeDamage(99999, 1f, false);
                yield return new WaitForSecondsRealtime(0.1f);
            }
            Assert.IsTrue(UpgradeChoicePanel.IsOpen, "웨이브를 깨면 강화 창");
            Assert.AreEqual(3, UpgradeChoicePanel.Options.Count);
            Assert.AreEqual(0f, Time.timeScale, "고르는 동안 게임이 멈춘다");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(1, waves.Wave, "고르기 전엔 다음 웨이브가 시작되지 않는다");

            var chosen = UpgradeChoicePanel.Options[1];
            Assert.IsTrue(UpgradeChoicePanel.Choose(1));
            Assert.AreEqual(1, WaveUpgrades.Level(chosen));
            Assert.IsFalse(UpgradeChoicePanel.IsOpen);
            Assert.AreEqual(1f, Time.timeScale);
            StringAssert.Contains(WaveUpgrades.Def(chosen).Name, Hud.UpgradesText, "HUD에 가진 강화");
            for (float t = 0f; t < 3f && waves.Wave != 2; t += Time.deltaTime) yield return null;
            Assert.AreEqual(2, waves.Wave, "고르면 쉬는 시간 뒤 다음 웨이브");
        }

        [UnityTest]
        public IEnumerator 흡혼과_도력_넘침_패링하면_체력과_도력이_더_찬다()
        {
            yield return Setup();
            _party.TakeDamage(30, true, true);
            _party.ClearInvulnerability();
            int hp = _party.Hp;
            WaveUpgrades.Add(UpgradeId.SoulDrain);
            WaveUpgrades.Add(UpgradeId.SoulDrain);
            WaveUpgrades.Add(UpgradeId.GaugeOverflow);
            int gauge = _gauge.Value;
            yield return Parry(AttackType.Normal, _enemies[0]);
            Assert.AreEqual(hp + 2, _party.Hp, "흡혼 2단계 = +2");
            Assert.AreEqual(gauge + 1 + 1, _gauge.Value, "일반 패링 1 + 도력 넘침 1");
        }

        [UnityTest]
        public IEnumerator 되받아치기_막은_투사체가_쏜_몬스터에게_돌아간다()
        {
            yield return Setup();
            var shooter = _enemies[0];
            Place(shooter, 6f); // 근접 반격(1.8)은 닿지 않는 거리
            WaveUpgrades.Add(UpgradeId.Reflect);
            yield return Parry(AttackType.Normal, shooter);
            Assert.AreEqual(1, _fx.ReflectCount);
            yield return new WaitForSeconds(0.8f);
            Assert.Less(shooter.HpRatio, 1f, "되돌아간 투사체에 맞음");
        }

        [UnityTest]
        public IEnumerator 근접_베기는_되받아치지_않는다()
        {
            yield return Setup();
            WaveUpgrades.Add(UpgradeId.Reflect);
            Assert.IsTrue(_defense.TryActivate());
            EnemyAttack.SpawnSlash(_enemies[0], AttackType.Normal, PlayerPos + Vector2.right * 1.2f, -1f, 1f, 60f, 1.5f, Color.red, 10);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(0, _fx.ReflectCount);
        }

        [UnityTest]
        public IEnumerator 연쇄_반격_반격이_다른_적에게도_튄다()
        {
            yield return Setup();
            Assume.That(_enemies.Length >= 2);
            Place(_enemies[0], 1.2f);
            Place(_enemies[1], -4f);
            WaveUpgrades.Add(UpgradeId.ChainCounter);
            yield return Parry(AttackType.Normal, _enemies[0], 1.0f);
            Assert.AreEqual(1, _fx.ChainCount);
            yield return new WaitForSeconds(1f);
            Assert.Less(_enemies[1].HpRatio, 1f, "뒤에 있던 적도 맞음");
        }

        [UnityTest]
        public IEnumerator 반격_파동_강공격을_막으면_뒤쪽_적도_맞는다()
        {
            yield return Setup();
            var behind = _enemies[0];
            Place(behind, -1.6f);
            WaveUpgrades.Add(UpgradeId.CounterBurst);
            yield return Parry(AttackType.Normal, null, 3f); // 0.2초 뒤 닿음 = 퍼펙트(0.08초) 아님
            Assert.AreEqual(0, _fx.BurstCount, "일반 패링(퍼펙트 아님)엔 안 터진다");
            yield return new WaitForSeconds(0.5f);
            yield return Parry(AttackType.Heavy, null);
            Assert.AreEqual(1, _fx.BurstCount);
            Assert.Less(behind.HpRatio, 1f, "파동은 사방으로");
        }

        [UnityTest]
        public IEnumerator 숙련은_D_F_쿨타임을_줄인다()
        {
            yield return Setup();
            float d = _combat.Skill1Cooldown, f = _combat.Skill2Cooldown;
            WaveUpgrades.Add(UpgradeId.Mastery);
            WaveUpgrades.Add(UpgradeId.Mastery);
            Assert.AreEqual(d * 0.7f, _combat.Skill1Cooldown, 1e-4f);
            Assert.AreEqual(f * 0.7f, _combat.Skill2Cooldown, 1e-4f);
            GameTuning.Current.infiniteGauge = true;
            Assert.IsTrue(_combat.TrySkill(1));
            Assert.AreEqual(d * 0.7f, _combat.Skill1CooldownLeft, 0.05f);
        }

        [UnityTest]
        public IEnumerator 꿰뚫는_부적은_앞의_적을_뚫고_뒤의_적도_맞힌다()
        {
            yield return Setup();
            Assume.That(_enemies.Length >= 2);
            TestScene.SetAttackSkillFree(true);
            Assert.IsTrue(_combat.TrySwitchWeapon());
            int facing = _defense.GetComponent<PlayerMotor>().Facing;
            Place(_enemies[0], 3f * facing);
            Place(_enemies[1], 5f * facing);
            WaveUpgrades.Add(UpgradeId.PiercingTalisman);
            Assert.IsTrue(_combat.TryAttackSkill());
            yield return new WaitForSeconds(0.8f);
            Assert.Less(_enemies[0].HpRatio, 1f);
            Assert.Less(_enemies[1].HpRatio, 1f, "1마리를 뚫고 뒤의 적도");
        }

        [UnityTest]
        public IEnumerator 꿰뚫는_부적이_없으면_첫_적에서_멈춘다()
        {
            yield return Setup();
            Assume.That(_enemies.Length >= 2);
            TestScene.SetAttackSkillFree(true);
            Assert.IsTrue(_combat.TrySwitchWeapon());
            int facing = _defense.GetComponent<PlayerMotor>().Facing;
            Place(_enemies[0], 3f * facing);
            Place(_enemies[1], 5f * facing);
            Assert.IsTrue(_combat.TryAttackSkill());
            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(1f, _enemies[1].HpRatio);
        }

        [UnityTest]
        public IEnumerator 수호_부적은_몸_주위를_돌며_닿은_적을_때린다()
        {
            yield return Setup();
            var target = _enemies[0];
            Place(target, 1.6f);
            WaveUpgrades.Add(UpgradeId.GuardTalismans);
            var guards = _defense.GetComponent<GuardTalismans>();
            Assert.IsNotNull(guards);
            Assert.AreEqual(2, guards.Count);
            WaveUpgrades.Add(UpgradeId.GuardTalismans);
            Assert.AreEqual(3, guards.Count, "단계가 오르면 장수가 는다");
            yield return new WaitForSeconds(2f);
            Assert.Greater(guards.HitCount, 0);
            Assert.Less(target.HpRatio, 1f);
            WaveUpgrades.Reset();
            Assert.AreEqual(0, guards.Count, "판이 끝나면 사라진다");
        }
    }
}
