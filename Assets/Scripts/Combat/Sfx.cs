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
        Dash,
        Jump,
        NormalCue,
        HeavyCue,
        Denied,
        LevelUp,
        UiMove,
        UiConfirm,
        PerfectParry,
        // 플레이어 공격 (무기·단계별)
        StaffSwing1,
        StaffSwing2,
        StaffSwing3,
        TalismanThrow,
        // 스킬 D / F
        SkillSweep,
        SkillSlam,
        SkillFan,
        SkillSeal,
        // 몬스터 피격 (무게별) — EnemyHit = 보통
        HitLight,
        HitHeavy,
        HitMetal,
        // 플레이어 피격
        HurtHeavy,
        // 보스
        BossLand,
        BossRoar,
        BossSlam,
        BossDash,
        SpineShot,
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

        private readonly Dictionary<SfxId, float> _lastPlayed = new();
        /// <summary>같은 소리가 이 간격 안에 또 오면 건너뛴다 (부채꼴 5장이 한 프레임에 맞으면 5배로 커지지 않게).</summary>
        public const float RepeatGap = 0.04f;

        public static void Play(SfxId id, float volume = 1f)
        {
            if (Instance == null || !Instance._clips.TryGetValue(id, out var clip)) return;
            float now = Time.unscaledTime;
            if (Instance._lastPlayed.TryGetValue(id, out float last) && now - last < RepeatGap) return;
            Instance._lastPlayed[id] = now;
            Instance._source.PlayOneShot(clip, volume * Instance.masterVolume);
        }

        /// <summary>테스트·미리 듣기용: 이 효과음의 클립 (없으면 null).</summary>
        public static AudioClip ClipOf(SfxId id) =>
            Instance != null && Instance._clips.TryGetValue(id, out var c) ? c : null;

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

            _clips[SfxId.Whiff] = Make("whiff", 0.2f, (t, w, l) =>
                Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.2f)) * 0.35f * (0.6f * l + 0.25f * w));

            _clips[SfxId.DefendStart] = Make("defend", 0.08f, (t, w, l) =>
                Env(t, 45f) * (0.25f * Sin(900f, t) + 0.2f * w));

            _clips[SfxId.EnemyDie] = Make("enemyDie", 0.45f, (t, w, l) =>
                Env(t, 7f) * (0.7f * Sin(Sweep(320f, 50f, t, 0.4f), t) + 0.8f * l));

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

            BuildCombat();
        }

        // ───────────── 전투 효과음 (2026-10-19: 스킬별·피격 무게별로 나눔) ─────────────
        // 쓸 수 있는 소리 에셋이 없어 코드로 합성한다. 재료: 휘두름(대역 노이즈 스윕) · 쿵(내려가는 사인) · 노이즈 덩어리 · 쇳소리(배음이 어긋난 사인 묶음) · 종소리.
        // 소리마다 최대 음량을 맞춰(Finish) 크기가 들쭉날쭉하지 않게 하고, 마지막 인자로 상대 크기를 준다.

        private void BuildCombat()
        {
            // 봉 평타: 1단 가벼운 휘두름 → 2단 더 높고 날카롭게 → 3단 몸을 도는 두 번 휘두름 + 쿵
            _clips[SfxId.StaffSwing1] = Synth("staffSwing1", 0.22f, 0.55f, b =>
            {
                Whoosh(b, 0f, 0.18f, 500f, 2600f, 1f, 11);
                Tone(b, 0f, 0.12f, 190f, 120f, 0.25f, 30f);
            });
            _clips[SfxId.StaffSwing2] = Synth("staffSwing2", 0.24f, 0.6f, b =>
            {
                Whoosh(b, 0f, 0.2f, 800f, 3400f, 1f, 12);
                Tone(b, 0f, 0.1f, 260f, 160f, 0.2f, 35f);
            });
            _clips[SfxId.StaffSwing3] = Synth("staffSwing3", 0.42f, 0.75f, b =>
            {
                Whoosh(b, 0f, 0.2f, 400f, 2200f, 0.9f, 13);
                Whoosh(b, 0.12f, 0.24f, 600f, 3000f, 1f, 14);
                Tone(b, 0.14f, 0.25f, 150f, 70f, 0.5f, 14f);
            });
            // 부적 던지기: 종이가 탁 튕기는 소리 + 짧은 바람 + 작은 방울
            _clips[SfxId.TalismanThrow] = Synth("talismanThrow", 0.2f, 0.45f, b =>
            {
                Burst(b, 0f, 0.05f, 7000f, 0.8f, 70f, 21, highPass: true);
                Whoosh(b, 0.01f, 0.12f, 2200f, 5200f, 0.5f, 22);
                Tone(b, 0.02f, 0.15f, 1850f, 1850f, 0.15f, 28f);
            });

            // D 휩쓸기: 크게 한 바퀴 휘두르는 긴 바람 + 묵직한 몸통
            _clips[SfxId.SkillSweep] = Synth("skillSweep", 0.45f, 0.8f, b =>
            {
                Whoosh(b, 0f, 0.38f, 300f, 2400f, 1f, 31);
                Whoosh(b, 0.05f, 0.3f, 900f, 3600f, 0.5f, 32);
                Tone(b, 0f, 0.3f, 140f, 85f, 0.4f, 9f);
            });
            // F 내려찍기: 땅을 치는 쿵 + 갈라지는 딱 + 낮게 울리는 땅울림
            _clips[SfxId.SkillSlam] = Synth("skillSlam", 0.8f, 1f, b =>
            {
                Tone(b, 0f, 0.5f, 120f, 38f, 1f, 8f);
                Burst(b, 0f, 0.06f, 3500f, 0.7f, 45f, 41);
                Burst(b, 0f, 0.8f, 260f, 0.9f, 5f, 42);
            });
            // D 부채꼴: 부적 다섯 장이 촤라락
            _clips[SfxId.SkillFan] = Synth("skillFan", 0.3f, 0.65f, b =>
            {
                for (int i = 0; i < 5; i++) Burst(b, i * 0.025f, 0.05f, 6500f, 0.6f, 60f, 51 + i, highPass: true);
                Whoosh(b, 0f, 0.22f, 1500f, 4800f, 0.6f, 56);
            });
            // F 봉인진: 맑은 종소리가 떨리며 퍼지고 아래로 낮은 울림
            _clips[SfxId.SkillSeal] = Synth("skillSeal", 1.1f, 0.6f, b =>
            {
                float[] notes = { 660f, 990f, 1320f, 1980f };
                for (int i = 0; i < notes.Length; i++) Chime(b, 0f, 1.1f, notes[i], 0.5f / (i + 1), 3f + i * 1.5f, 6.5f);
                Tone(b, 0f, 1.1f, 110f, 110f, 0.35f, 3.5f);
                Whoosh(b, 0f, 0.3f, 800f, 2400f, 0.25f, 61);
            });

            // 몬스터 피격: 가벼움(평타 1·2단·부적 — 경직 없음) / 보통 / 무거움(강공격 반격·3단·스킬)
            _clips[SfxId.HitLight] = Synth("hitLight", 0.12f, 0.4f, b =>
            {
                Burst(b, 0f, 0.08f, 2600f, 0.8f, 70f, 71);
                Tone(b, 0f, 0.08f, 320f, 210f, 0.5f, 40f);
            });
            _clips[SfxId.EnemyHit] = Synth("enemyHit", 0.2f, 0.6f, b =>
            {
                Tone(b, 0f, 0.18f, 210f, 90f, 1f, 22f);
                Burst(b, 0f, 0.15f, 1300f, 0.8f, 30f, 72);
                Burst(b, 0f, 0.02f, 6000f, 0.4f, 120f, 73);
            });
            _clips[SfxId.HitHeavy] = Synth("hitHeavy", 0.38f, 0.85f, b =>
            {
                Tone(b, 0f, 0.35f, 170f, 55f, 1f, 12f);
                Burst(b, 0f, 0.3f, 850f, 1f, 14f, 74);
                Burst(b, 0f, 0.04f, 5000f, 0.6f, 60f, 75);
            });
            // 불가살이(쇠 가시·철판)를 치면 쇳소리를 겹친다
            _clips[SfxId.HitMetal] = Synth("hitMetal", 0.5f, 0.4f, b =>
            {
                float[] partials = { 430f, 1080f, 1720f, 2650f, 3900f };
                float[] decays = { 8f, 11f, 16f, 22f, 30f };
                for (int i = 0; i < partials.Length; i++) Tone(b, 0f, 0.5f, partials[i], partials[i], 0.6f / (1f + i * 0.4f), decays[i]);
                Burst(b, 0f, 0.03f, 6000f, 0.5f, 90f, 76, highPass: true);
            });

            // 플레이어 피격: 몸에 맞는 둔탁한 소리 + 짧은 신음 느낌(낮은 사각파). 큰 피해·추락은 더 낮고 길게
            _clips[SfxId.Hurt] = Synth("hurt", 0.26f, 0.7f, b =>
            {
                Tone(b, 0f, 0.2f, 240f, 110f, 0.9f, 18f);
                Burst(b, 0f, 0.18f, 900f, 0.9f, 25f, 81);
                Buzz(b, 0.02f, 0.16f, 135f, 100f, 0.25f, 14f);
            });
            _clips[SfxId.HurtHeavy] = Synth("hurtHeavy", 0.45f, 0.85f, b =>
            {
                Tone(b, 0f, 0.4f, 200f, 60f, 1f, 10f);
                Burst(b, 0f, 0.35f, 600f, 1f, 12f, 82);
                Buzz(b, 0.03f, 0.25f, 120f, 85f, 0.3f, 9f);
            });

            // 보스
            _clips[SfxId.BossLand] = Synth("bossLand", 1.3f, 1f, b =>
            {
                Tone(b, 0f, 1f, 85f, 28f, 1f, 4.5f);
                Burst(b, 0f, 1.3f, 150f, 1f, 3f, 91);
                Burst(b, 0f, 0.08f, 2500f, 0.6f, 35f, 92);
            });
            _clips[SfxId.BossRoar] = Synth("bossRoar", 1.3f, 0.8f, b => Growl(b, 0f, 1.3f, 88f, 0.9f, 93));
            _clips[SfxId.BossSlam] = Synth("bossSlam", 0.9f, 0.95f, b =>
            {
                Tone(b, 0f, 0.7f, 95f, 32f, 1f, 6f);
                Burst(b, 0f, 0.07f, 3000f, 0.7f, 40f, 94);
                Burst(b, 0f, 0.9f, 200f, 0.9f, 4.5f, 95);
            });
            _clips[SfxId.BossDash] = Synth("bossDash", 0.6f, 0.75f, b =>
            {
                Whoosh(b, 0f, 0.5f, 150f, 900f, 1f, 96);
                Tone(b, 0f, 0.3f, 90f, 45f, 0.7f, 9f);
                Burst(b, 0f, 0.6f, 180f, 0.6f, 5f, 97);
            });
            _clips[SfxId.SpineShot] = Synth("spineShot", 0.22f, 0.45f, b =>
            {
                Whoosh(b, 0f, 0.14f, 1500f, 4200f, 0.8f, 98);
                Tone(b, 0f, 0.2f, 2500f, 2300f, 0.35f, 30f);
                Tone(b, 0f, 0.2f, 3700f, 3500f, 0.2f, 40f);
            });
        }

        // ── 합성 재료 (버퍼에 더한다) ──

        private static int Idx(float seconds) => Mathf.Max(0, Mathf.RoundToInt(seconds * SampleRate));

        /// <summary>휘두르는 바람: 대역 노이즈(빠른 로우패스 − 느린 로우패스)의 중심 주파수를 f0 → f1로 올리며 부풀었다 사라진다.</summary>
        private static void Whoosh(float[] b, float start, float dur, float f0, float f1, float amp, int seed)
        {
            var rng = new System.Random(seed);
            float lpHi = 0f, lpLo = 0f;
            int s0 = Idx(start), n = Idx(dur);
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                float k = (float)i / n;
                float cut = Mathf.Lerp(f0, f1, Mathf.Sin(k * Mathf.PI * 0.5f));
                float aHi = 1f - Mathf.Exp(-2f * Mathf.PI * cut / SampleRate);
                float aLo = 1f - Mathf.Exp(-2f * Mathf.PI * cut * 0.3f / SampleRate);
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lpHi += aHi * (white - lpHi);
                lpLo += aLo * (white - lpLo);
                float env = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(k, 0.7f)), 2f);
                b[s0 + i] += (lpHi - lpLo) * env * amp * 3f;
            }
        }

        /// <summary>사인 톤 (f0 → f1로 미끄러짐, 위상 누적이라 매끄럽다) + 지수 감쇠.</summary>
        private static void Tone(float[] b, float start, float dur, float f0, float f1, float amp, float decay)
        {
            int s0 = Idx(start), n = Idx(dur);
            float phase = 0f;
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                float t = (float)i / SampleRate;
                float k = (float)i / n;
                phase += 2f * Mathf.PI * Mathf.Lerp(f0, f1, 1f - (1f - k) * (1f - k)) / SampleRate;
                b[s0 + i] += Mathf.Sin(phase) * Mathf.Exp(-decay * t) * amp;
            }
        }

        /// <summary>노이즈 덩어리 (로우패스 = 둔탁한 쿵·땅울림, highPass = 종이·금속의 사각거림) + 지수 감쇠.</summary>
        private static void Burst(float[] b, float start, float dur, float cutoff, float amp, float decay, int seed, bool highPass = false)
        {
            var rng = new System.Random(seed);
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SampleRate);
            float lp = 0f;
            int s0 = Idx(start), n = Idx(dur);
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                float t = (float)i / SampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += a * (white - lp);
                float v = highPass ? white - lp : lp * 2.5f;
                b[s0 + i] += v * Mathf.Exp(-decay * t) * amp;
            }
        }

        /// <summary>종소리: 사인 + 떨림(트레몰로)으로 천천히 사라진다.</summary>
        private static void Chime(float[] b, float start, float dur, float freq, float amp, float decay, float tremolo)
        {
            int s0 = Idx(start), n = Idx(dur);
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                float t = (float)i / SampleRate;
                float attack = Mathf.Clamp01(t / 0.008f);
                float trem = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * tremolo * t);
                b[s0 + i] += Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-decay * t) * trem * attack * amp;
            }
        }

        /// <summary>낮은 사각파를 부드럽게 깎은 웅얼거림 (피격 신음).</summary>
        private static void Buzz(float[] b, float start, float dur, float f0, float f1, float amp, float decay)
        {
            int s0 = Idx(start), n = Idx(dur);
            float phase = 0f, lp = 0f;
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * 700f / SampleRate);
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                float t = (float)i / SampleRate;
                phase += 2f * Mathf.PI * Mathf.Lerp(f0, f1, (float)i / n) / SampleRate;
                float sq = Mathf.Sin(phase) >= 0f ? 1f : -1f;
                lp += a * (sq - lp);
                float attack = Mathf.Clamp01(t / 0.01f);
                b[s0 + i] += lp * Mathf.Exp(-decay * t) * attack * amp;
            }
        }

        /// <summary>짐승의 포효: 톱니파(음높이가 떨리고 거칠게 흔들림)를 노이즈와 섞어 로우패스. 천천히 커졌다 사라진다.</summary>
        private static void Growl(float[] b, float start, float dur, float freq, float amp, int seed)
        {
            var rng = new System.Random(seed);
            int s0 = Idx(start), n = Idx(dur);
            float phase = 0f, lp1 = 0f, lp2 = 0f, jitter = 0f;
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * 900f / SampleRate);
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                float t = (float)i / SampleRate;
                float k = (float)i / n;
                jitter += ((float)(rng.NextDouble() * 2.0 - 1.0) - jitter) * 0.002f;
                float f = freq * (1f + 0.25f * Mathf.Sin(k * Mathf.PI)) * (1f + 0.06f * Mathf.Sin(2f * Mathf.PI * 6f * t) + jitter * 0.6f);
                phase += f / SampleRate;
                float saw = 2f * (phase - Mathf.Floor(phase + 0.5f));
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp1 += a * (saw * 0.8f + noise * 0.5f - lp1);
                lp2 += a * (lp1 - lp2);
                float env = Mathf.Clamp01(t / 0.15f) * Mathf.Pow(1f - k, 1.5f);
                b[s0 + i] += lp2 * env * amp * 2f;
            }
        }

        /// <summary>버퍼를 만들어 재료를 더하고, 부드럽게 눌러(tanh) 최대 음량을 gain에 맞춘다. 앞뒤 끝은 클릭이 없게 짧게 페이드.</summary>
        private static AudioClip Synth(string name, float duration, float gain, Action<float[]> build)
        {
            var data = new float[Idx(duration)];
            build(data);
            float peak = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (float)Math.Tanh(data[i] * 1.2f);
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }
            float scale = peak > 1e-5f ? gain * 0.9f / peak : 0f;
            int fadeIn = Idx(0.002f), fadeOut = Idx(0.015f);
            for (int i = 0; i < data.Length; i++)
            {
                float f = Mathf.Min(1f, Mathf.Min((float)(i + 1) / Mathf.Max(1, fadeIn), (float)(data.Length - i) / Mathf.Max(1, fadeOut)));
                data[i] *= scale * f;
            }
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>미리 듣기용: 클립을 16비트 WAV 파일로 저장한다.</summary>
        public static void SaveWav(AudioClip clip, string path)
        {
            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            using var fs = new System.IO.FileStream(path, System.IO.FileMode.Create);
            using var w = new System.IO.BinaryWriter(fs);
            int bytes = samples.Length * 2;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)clip.channels);
            w.Write(clip.frequency); w.Write(clip.frequency * clip.channels * 2); w.Write((short)(clip.channels * 2)); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(bytes);
            foreach (float v in samples) w.Write((short)(Mathf.Clamp(v, -1f, 1f) * short.MaxValue));
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
