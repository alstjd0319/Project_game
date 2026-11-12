using System.Collections.Generic;
using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 웨이브 강화(<see cref="WaveUpgrades"/>) 중 패링·반격에 붙는 효과 (WaveController가 플레이어에 붙인다).
    /// 반격 파동 · 연쇄 반격 · 되받아치기 · 흡혼 · 도력 넘침 · 수호 부적(개수 맞추기). 숙련·꿰뚫는 부적은 PlayerCombat이 직접 읽는다.
    /// </summary>
    [RequireComponent(typeof(PlayerCombat))]
    public class WaveUpgradeEffects : MonoBehaviour
    {
        private PlayerCombat _combat;
        private PlayerParty _party;
        private SkillGauge _gauge;
        private GuardTalismans _guards;
        private int _lastReflectVolley;
        private EnemyController _lastReflectSource;

        // 테스트용 횟수
        public int BurstCount { get; private set; }
        public int ChainCount { get; private set; }
        public int ReflectCount { get; private set; }

        private static readonly Color ChainColor = new(0.55f, 0.9f, 1f);
        private static readonly Color ReflectColor = new(1f, 0.85f, 0.45f);

        private void Awake()
        {
            _combat = GetComponent<PlayerCombat>();
            _party = GetComponent<PlayerParty>();
            _gauge = GetComponent<SkillGauge>();
            _combat.DefenseSucceeded += OnDefense;
            _combat.CounterFired += OnCounter;
            WaveUpgrades.Changed += Sync;
            Sync();
        }

        private void OnDestroy()
        {
            if (_combat != null)
            {
                _combat.DefenseSucceeded -= OnDefense;
                _combat.CounterFired -= OnCounter;
            }
            WaveUpgrades.Changed -= Sync;
        }

        private void Sync()
        {
            int n = WaveUpgrades.Guards;
            if (n > 0 && _guards == null) _guards = gameObject.AddComponent<GuardTalismans>();
            if (_guards != null) _guards.SetCount(n);
        }

        private void OnDefense(DefenseSuccess s)
        {
            if (WaveUpgrades.Drain > 0 && _party != null) _party.Heal(WaveUpgrades.Drain);
            if (WaveUpgrades.GaugeBonus > 0 && _gauge != null) _gauge.Add(WaveUpgrades.GaugeBonus);
            if (WaveUpgrades.Has(UpgradeId.CounterBurst) && (s.Perfect || s.Heavy)) Burst();
            if (WaveUpgrades.Has(UpgradeId.Reflect) && s.Attack != null && !s.Attack.IsSlash) Reflect(s.Attack);
        }

        // 반격 파동: 플레이어를 가운데로 한 충격파 (보이는 그림 = 판정 상자)
        private void Burst()
        {
            int l = WaveUpgrades.Level(UpgradeId.CounterBurst);
            float r = WaveUpgrades.BurstRadius[l - 1];
            var size = new Vector2(r * 2f, r * 0.9f);
            Vector2 pos = transform.position;
            var hit = HitInfo.Create(DamageSource.Upgrade, CharacterKind.Player, pos);
            hit.Stagger = HitStagger.Full;
            var fx = GameAssets.AttackFx;
            MeleeStrike.Spawn(pos, size, new Color(0.55f, 0.9f, 1f, 0.45f), WaveUpgrades.BurstDamage[l - 1], hit,
                art: fx != null ? fx.parryMelee : null, stretchArt: true, follow: transform);
            Fx.Ring(pos, new Color(0.6f, 0.95f, 1f, 0.8f), 0.6f, r * 2f, 0.12f, 0.25f);
            BurstCount++;
        }

        // 되받아치기: 막은 투사체가 쏜 몬스터에게 되돌아간다 (한 번에 여러 개 날아오는 공격은 묶음당 한 번)
        private void Reflect(EnemyAttack attack)
        {
            var source = attack.Source;
            if (source == null || source.IsDead) return;
            if (attack.Volley != 0)
            {
                if (attack.Volley == _lastReflectVolley && source == _lastReflectSource) return;
                _lastReflectVolley = attack.Volley;
                _lastReflectSource = source;
            }
            int l = WaveUpgrades.Level(UpgradeId.Reflect);
            int damage = Mathf.RoundToInt(WaveUpgrades.ReflectBaseDamage * WaveUpgrades.ReflectMultiplier[l - 1]);
            Vector2 from = attack.transform.position;
            var hit = HitInfo.Create(DamageSource.Upgrade, CharacterKind.Player, from);
            hit.Ranged = true;
            hit.Stagger = HitStagger.Full;
            PlayerProjectile.Spawn(from, (Vector2)source.transform.position - from, source, true, 15f, 30f, damage, hit,
                ReflectColor, new Vector2(0.6f, 0.26f));
            ReflectCount++;
        }

        // 연쇄 반격: 반격 대상 말고 가까운 적에게 부적이 튄다
        private void OnCounter(CounterShot shot)
        {
            int n = WaveUpgrades.Chains;
            if (n <= 0) return;
            Vector2 origin = shot.Target != null ? (Vector2)shot.Target.transform.position : shot.Origin;
            var near = new List<EnemyController>();
            foreach (var e in FindObjectsByType<EnemyController>()) // 기절 등으로 꺼진 몬스터도 포함
            {
                if (e == null || e.IsDead || e == shot.Target) continue;
                if (Vector2.Distance(e.transform.position, transform.position) <= WaveUpgrades.ChainRange) near.Add(e);
            }
            near.Sort((a, b) => Vector2.Distance(a.transform.position, transform.position)
                .CompareTo(Vector2.Distance(b.transform.position, transform.position)));
            for (int i = 0; i < near.Count && i < n; i++)
            {
                var target = near[i];
                var hit = HitInfo.Create(DamageSource.Upgrade, CharacterKind.Player, origin);
                hit.Ranged = true;
                hit.Stagger = HitStagger.Light;
                PlayerProjectile.Spawn(origin, (Vector2)target.transform.position - origin, target, true, 16f, 20f,
                    WaveUpgrades.ChainDamage, hit, ChainColor, new Vector2(0.5f, 0.2f)).Ignore(shot.Target);
                ChainCount++;
            }
        }
    }
}
