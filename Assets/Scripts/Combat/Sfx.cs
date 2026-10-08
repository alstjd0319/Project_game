using System;
using System.Collections.Generic;
using UnityEngine;

namespace ParryRL
{
    public enum SfxId
    {
        Parry,
        HeavyParry,
        Hurt,
        Whiff,
        DefendStart,
        EnemyHit,
        EnemyDie,
        Skill,
        Dash,
        Jump,
        NormalCue,
        HeavyCue,
        Denied,
        LevelUp,
        UiMove,
        UiConfirm,
        PerfectParry,
    }

    /// <summary>
    /// 외부 에셋 없이 코드로 합성한 효과음 (화이트박스 단계 한정).
    /// AudioSource는 timeScale 영향을 받지 않으므로 히트스탑 중에도 재생된다.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class Sfx : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private static readonly float[] LevelUpNotes = { 523f, 659f, 784f, 1047f };

        public static Sfx Instance { get; private set; }

        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.7f;

        [Header("에셋 효과음 (비워두면 코드 합성음 사용)")]
        [SerializeField] private AudioClip parryClip;
        [SerializeField] private AudioClip perfectParryClip;

        private AudioSource _source;
        private readonly Dictionary<SfxId, AudioClip> _clips = new();

        private void Awake()
        {
            Instance = this;
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            Build();
            ApplyAssetClips();
        }

        /// <summary>에셋 클립이 있으면 합성음 대신 쓴다. 퍼펙트가 비어 있으면 강공격 합성음으로 대신한다.</summary>
        private void ApplyAssetClips()
        {
            if (parryClip != null) _clips[SfxId.Parry] = parryClip;
            _clips[SfxId.PerfectParry] = perfectParryClip != null ? perfectParryClip : _clips[SfxId.HeavyParry];
        }

        public static void Play(SfxId id, float volume = 1f)
        {
            if (Instance == null || !Instance._clips.TryGetValue(id, out var clip)) return;
            Instance._source.PlayOneShot(clip, volume * Instance.masterVolume);
        }

        private void Build()
        {
            // 패링: 금속성 "팅" + 저음 쿵 + 짧은 노이즈
            _clips[SfxId.Parry] = Make("parry", 0.35f, (t, w, l) =>
                Env(t, 18f) * (0.45f * Sin(2300f, t) + 0.3f * Sin(3450f, t) + 0.15f * Sin(1480f, t))
                + Env(t, 25f) * 0.9f * Sin(Sweep(160f, 60f, t, 0.12f), t)
                + Env(t, 70f) * 0.8f * w);

            // 강공격 패링: 더 낮고 길고 두꺼운 임팩트
            _clips[SfxId.HeavyParry] = Make("heavyParry", 0.7f, (t, w, l) =>
                Env(t, 8f) * (0.4f * Sin(1650f, t) + 0.3f * Sin(2480f, t) + 0.2f * Sin(3300f, t))
                + Env(t, 12f) * 1.1f * Sin(Sweep(120f, 38f, t, 0.3f), t)
                + Env(t, 20f) * 1.2f * l
                + Env(t, 60f) * 0.7f * w);

            _clips[SfxId.Hurt] = Make("hurt", 0.22f, (t, w, l) =>
                Env(t, 16f) * (0.8f * Sin(Sweep(220f, 80f, t, 0.2f), t) + 1.2f * l));

            _clips[SfxId.Whiff] = Make("whiff", 0.2f, (t, w, l) =>
                Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.2f)) * 0.35f * (0.6f * l + 0.25f * w));

            _clips[SfxId.DefendStart] = Make("defend", 0.08f, (t, w, l) =>
                Env(t, 45f) * (0.25f * Sin(900f, t) + 0.2f * w));

            _clips[SfxId.EnemyHit] = Make("enemyHit", 0.18f, (t, w, l) =>
                Env(t, 22f) * (0.9f * Sin(Sweep(180f, 70f, t, 0.15f), t) + 0.9f * l) + Env(t, 90f) * 0.4f * w);

            _clips[SfxId.EnemyDie] = Make("enemyDie", 0.45f, (t, w, l) =>
                Env(t, 7f) * (0.7f * Sin(Sweep(320f, 50f, t, 0.4f), t) + 0.8f * l));

            _clips[SfxId.Skill] = Make("skill", 0.2f, (t, w, l) =>
                Env(t, 14f) * (0.35f * Sin(Sweep(700f, 350f, t, 0.2f), t) + 0.5f * l + 0.2f * w));

            _clips[SfxId.Dash] = Make("dash", 0.12f, (t, w, l) =>
                Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.12f)) * 0.4f * (0.7f * l + 0.3f * w));

            _clips[SfxId.Jump] = Make("jump", 0.08f, (t, w, l) =>
                Env(t, 30f) * 0.2f * Sin(Sweep(420f, 760f, t, 0.08f), t));

            // 텔레그래프 사운드: 일반은 가벼운 틱, 강공격은 묵직한 경고음
            _clips[SfxId.NormalCue] = Make("normalCue", 0.06f, (t, w, l) =>
                Env(t, 60f) * 0.2f * Sin(1200f, t));

            _clips[SfxId.HeavyCue] = Make("heavyCue", 0.3f, (t, w, l) =>
                Env(t, 9f) * 0.45f * Square(Sweep(140f, 110f, t, 0.3f), t));

            _clips[SfxId.Denied] = Make("denied", 0.16f, (t, w, l) =>
                (t < 0.06f || t > 0.1f ? 0.25f : 0f) * Square(180f, t) * Env(t, 10f));

            // 레벨업: 도-미-솔-도 상승 아르페지오
            _clips[SfxId.LevelUp] = Make("levelUp", 0.6f, (t, w, l) =>
            {
                int i = Mathf.Min(3, (int)(t / 0.08f));
                float local = t - i * 0.08f;
                float tail = i == 3 ? Env(local, 5f) : Env(local, 14f);
                return 0.3f * tail * (Sin(LevelUpNotes[i], t) + 0.4f * Sin(LevelUpNotes[i] * 2f, t));
            });

            _clips[SfxId.UiMove] = Make("uiMove", 0.05f, (t, w, l) =>
                Env(t, 70f) * 0.25f * Sin(950f, t));

            _clips[SfxId.UiConfirm] = Make("uiConfirm", 0.25f, (t, w, l) =>
                Env(t, 10f) * 0.3f * Sin(t < 0.07f ? 660f : 990f, t));
        }

        private static float Env(float t, float decay) => Mathf.Exp(-decay * t);
        private static float Sin(float freq, float t) => Mathf.Sin(2f * Mathf.PI * freq * t);
        private static float Square(float freq, float t) => Mathf.Sign(Sin(freq, t)) * 0.6f;

        /// <summary>주파수 스윕을 위상 누적 없이 근사 (시작~끝 주파수의 평균 주파수 사용).</summary>
        private static float Sweep(float from, float to, float t, float duration)
        {
            float k = Mathf.Clamp01(t / duration);
            return Mathf.Lerp(from, (from + to) * 0.5f, k);
        }

        /// <summary>f(t, whiteNoise, lowpassNoise) 로 샘플을 만든다.</summary>
        private static AudioClip Make(string name, float duration, Func<float, float, float, float> f)
        {
            int n = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            float low = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                low += (white - low) * 0.12f;
                float fadeOut = Mathf.Clamp01((n - i) / (SampleRate * 0.01f)); // 끝 클릭 방지
                data[i] = Mathf.Clamp(f(t, white, low * 2.5f) * fadeOut, -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
