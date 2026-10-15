using System;
using UnityEngine;

namespace ParryRL
{
    /// <summary>공용 도력 (기획서 4.4). 패링/회피 성공으로만 충전된다.</summary>
    public class SkillGauge : MonoBehaviour
    {
        [SerializeField] private int max = 20;
        [SerializeField] private int startValue = 0;

        public int Value { get; private set; }
        public int Max => max;
        public event Action<int> Changed;

        private void Awake() => Value = Mathf.Clamp(startValue, 0, max);

        /// <summary>씬을 넘어 이어받은 도력을 되돌린다.</summary>
        public void SetValue(int value)
        {
            Value = Mathf.Clamp(value, 0, max);
            Changed?.Invoke(Value);
        }

        public void Add(int amount)
        {
            Value = Mathf.Clamp(Value + amount, 0, max);
            Changed?.Invoke(Value);
        }

        /// <summary>연습 스위치 "도력 무한" (튜닝 패널) — 써도 줄지 않는다.</summary>
        public static bool Infinite => GameTuning.Current.infiniteGauge;

        public bool CanSpend(int amount) => Infinite || Value >= amount;

        public bool TrySpend(int amount)
        {
            if (!CanSpend(amount)) return false;
            if (Infinite) return true;
            Value -= amount;
            Changed?.Invoke(Value);
            return true;
        }
    }
}
