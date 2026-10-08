using System;
using UnityEngine;

namespace ParryRL
{
    /// <summary>공용 게이지 (기획서 4.4). 패링/회피 성공으로만 충전된다.</summary>
    public class SkillGauge : MonoBehaviour
    {
        [SerializeField] private int max = 20;
        [SerializeField] private int startValue = 0;

        public int Value { get; private set; }
        public int Max => max;
        public event Action<int> Changed;

        private void Awake() => Value = Mathf.Clamp(startValue, 0, max);

        public void Add(int amount)
        {
            Value = Mathf.Clamp(Value + amount, 0, max);
            Changed?.Invoke(Value);
        }

        /// <summary>연습 스위치 "게이지 무한" (튜닝 패널) — 써도 줄지 않는다.</summary>
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
