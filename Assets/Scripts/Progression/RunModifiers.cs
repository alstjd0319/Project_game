using System;
using System.Collections.Generic;
using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// "이번 판 보정" 층 (기획서 5.2). 획득한 증강 효과들을 모아 두고, 다른 시스템이 보정값을 물어보는 창구.
    /// 튜닝 값(GameTuning / tuning.json)은 절대 건드리지 않는다 — 최종값 = 튜닝 기본값 + 여기 보정.
    /// 판이 끝나면(씬 재시작) 플레이어와 함께 사라진다.
    /// </summary>
    public class RunModifiers : MonoBehaviour
    {
        public static RunModifiers Instance { get; private set; }

        private readonly List<AugmentEffect> _effects = new();
        public IReadOnlyList<AugmentEffect> Effects => _effects;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>증강 효과를 붙이거나(처음) 중첩을 1 올린다.</summary>
        public AugmentEffect AddStack(Type effectType)
        {
            // GetComponent가 아니라 목록에서 찾는다 — 방금 RemoveAll로 지운(프레임 끝에 사라질) 컴포넌트를 다시 잡지 않게
            var effect = _effects.Find(e => e != null && e.GetType() == effectType);
            if (effect == null)
            {
                effect = (AugmentEffect)gameObject.AddComponent(effectType);
                _effects.Add(effect);
            }
            effect.AddStack();
            return effect;
        }

        public T Get<T>() where T : AugmentEffect => (T)_effects.Find(e => e is T);

        /// <summary>증강 효과를 전부 뗀다. 끄기부터 해서 이벤트 구독이 즉시 풀리게 한다 (Destroy는 프레임 끝에 처리됨).</summary>
        public void RemoveAll()
        {
            foreach (var e in _effects)
            {
                if (e == null) continue;
                e.enabled = false;
                Destroy(e);
            }
            _effects.Clear();
        }

        // ───────────── 보정값 조회 ─────────────

        /// <summary>방어 활성 시간 추가 (초).</summary>
        public float DefenseWindowBonus
        {
            get
            {
                float sum = 0f;
                foreach (var e in _effects) sum += e.DefenseWindowBonus;
                return sum;
            }
        }

        /// <summary>이동 스킬 거리 추가 (유닛).</summary>
        public float DashDistanceBonus(CharacterKind kind)
        {
            float sum = 0f;
            foreach (var e in _effects) sum += e.DashDistanceBonus(kind);
            return sum;
        }

        /// <summary>공격 발동 순간 — 증강이 공격 성질(관통·조준 등)을 바꿀 기회.</summary>
        public void PrepareHit(ref HitInfo hit)
        {
            foreach (var e in _effects) e.PrepareHit(ref hit);
        }

        /// <summary>명중 순간 — 증강 데미지 보너스의 합 (기획서 5.3: 증강끼리는 더한다).</summary>
        public float DamageBonus(in HitInfo hit, EnemyController target, List<string> notes)
        {
            float sum = 0f;
            foreach (var e in _effects)
            {
                float b = e.DamageBonus(hit, target);
                if (Mathf.Approximately(b, 0f)) continue;
                sum += b;
                notes?.Add($"{e.BonusLabel} +{Mathf.RoundToInt(b * 100f)}%");
            }
            return sum;
        }
    }
}
