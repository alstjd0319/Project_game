using System.Collections;
using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 히트스탑(timeScale 0) → 슬로우모션 → 복귀. 전부 실시간(WaitForSecondsRealtime) 기준.
    /// 일반 성공 / 강공격 성공 / 피격의 타격감 층을 나눈다 (기획서 3.1). 수치는 GameTuning (F1 패널).
    /// </summary>
    public class HitFeel : MonoBehaviour
    {
        public static HitFeel Instance { get; private set; }

        private Coroutine _routine;

        private void Awake() => Instance = this;

        private static GameTuning T => GameTuning.Current;

        public void ParrySuccess(bool heavy) => Play(heavy ? T.heavyParry : T.normalParry);
        public void PlayerHurt() => Play(T.playerHurt);
        public void SkillHit() => Play(T.skillHit);

        public void Play(HitFeelProfile p)
        {
            if (GameManager.InputBlocked) return;
            if (CameraRig.Instance != null) CameraRig.Instance.Shake(p.shake, p.shakeDuration);
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Run(p));
        }

        public void Cancel()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
        }

        private IEnumerator Run(HitFeelProfile p)
        {
            if (p.hitStop > 0f)
            {
                Time.timeScale = 0f;
                yield return new WaitForSecondsRealtime(p.hitStop);
            }
            if (p.slowMotion > 0f)
            {
                Time.timeScale = Mathf.Clamp(p.slowScale, 0.01f, 1f);
                yield return new WaitForSecondsRealtime(p.slowMotion);
            }
            Time.timeScale = GameManager.InputBlocked ? 0f : 1f;
            _routine = null;
        }
    }
}
