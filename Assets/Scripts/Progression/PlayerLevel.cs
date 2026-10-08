using System;
using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 경험치 / 레벨 (기획서 5장). 몬스터 처치 시 경험치를 얻고, 필요량을 채우면 레벨업.
    /// 남은 경험치는 이월되고, 한 번에 여러 레벨이 오를 수 있다.
    /// </summary>
    public class PlayerLevel : MonoBehaviour
    {
        [SerializeField, Tooltip("Lv.1 → 2 필요 경험치")] private int baseXpToLevel = 20;
        [SerializeField, Tooltip("레벨마다 늘어나는 필요 경험치")] private int xpIncreasePerLevel = 10;

        public int Level { get; private set; } = 1;
        public int Xp { get; private set; }
        public int XpToNext => baseXpToLevel + (Level - 1) * xpIncreasePerLevel;

        public event Action<int> XpGained;
        /// <summary>새 레벨. 여러 레벨이 한 번에 오르면 레벨마다 한 번씩 호출된다.</summary>
        public event Action<int> LeveledUp;

        private void OnEnable() => EnemyController.AnyDied += OnEnemyDied;
        private void OnDisable() => EnemyController.AnyDied -= OnEnemyDied;

        private void OnEnemyDied(EnemyController enemy)
        {
            if (enemy.XpReward <= 0) return;
            Hud.WorldText(enemy.transform.position + Vector3.up * 1.4f, $"+{enemy.XpReward} EXP", new Color(0.45f, 0.9f, 1f), 1.1f);
            AddXp(enemy.XpReward);
        }

        public void AddXp(int amount)
        {
            if (amount <= 0) return;
            Xp += amount;
            XpGained?.Invoke(amount);

            while (Xp >= XpToNext)
            {
                Xp -= XpToNext;
                Level++;
                LeveledUp?.Invoke(Level);
            }
        }
    }
}
