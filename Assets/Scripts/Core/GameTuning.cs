using System;
using System.IO;
using UnityEngine;

namespace ParryRL
{
    /// <summary>히트스탑 → 슬로우모션 → 복귀 + 화면 흔들림 한 세트 (기획서 3.1).</summary>
    [Serializable]
    public class HitFeelProfile
    {
        [Tooltip("히트스탑 실시간 길이 (초)")] public float hitStop;
        [Tooltip("슬로우모션 실시간 길이 (초)")] public float slowMotion;
        [Tooltip("슬로우모션 중 timeScale")] public float slowScale = 1f;
        [Tooltip("화면 흔들림 세기")] public float shake;
        [Tooltip("화면 흔들림 길이 (실시간 초)")] public float shakeDuration;
    }

    /// <summary>
    /// 플레이 감각에 관한 튜닝 값을 한곳에 모은 것. F1 튜닝 패널에서 실시간으로 바꾸고 JSON으로 저장된다.
    /// 에디터: 프로젝트 루트 Tuning/tuning.json (팀 공유·버전 관리용) / 빌드: persistentDataPath.
    /// 기본값 = 기획서 8장 가안표.
    /// </summary>
    [Serializable]
    public class GameTuning
    {
        // ── 타격감 ──
        public HitFeelProfile normalParry = new() { hitStop = 0.04f, slowMotion = 0.08f, slowScale = 0.3f, shake = 0.18f, shakeDuration = 0.15f };
        public HitFeelProfile heavyParry = new() { hitStop = 0.06f, slowMotion = 0.12f, slowScale = 0.15f, shake = 0.42f, shakeDuration = 0.3f };
        public HitFeelProfile playerHurt = new() { hitStop = 0.03f, slowMotion = 0f, slowScale = 1f, shake = 0.25f, shakeDuration = 0.18f };
        public HitFeelProfile skillHit = new() { hitStop = 0.025f, slowMotion = 0f, slowScale = 1f, shake = 0.1f, shakeDuration = 0.1f };

        // ── 판정 ──
        public float defenseActiveTime = 0.3f;
        public float whiffCooldown = 1f;
        [Tooltip("활성 시간이 끝난 직후 이 시간 안에 닿아도 성공 (살짝 이른 입력 보정, 기획서 9장)")]
        public bool inputBufferEnabled = true;
        public float inputBuffer = 0.06f;
        public bool showTiming = true;
        [Tooltip("퍼펙트 방어: 방어 입력 직후 이 시간(초) 안에 공격이 닿으면 퍼펙트 — 연출만 강해지고 판정 결과는 같다")]
        public float perfectWindow = 0.08f;
        [Tooltip("이동 스킬(대시) 중 무적 — 닿은 공격은 판정 없이 통과 (게이지·반격 없음)")]
        public bool dashInvulnerable = true;
        [Tooltip("대시가 끝난 뒤 무적을 조금 더 이어주는 여유 시간 (초)")]
        public float dashInvulnerableExtra = 0f;

        // ── 캐릭터 패시브 (기획서 4.1/4.2) ──
        [Tooltip("플레이어: 사거리가 짧은 대신 강한 공격 — 반격·공격 스킬 데미지 배율")]
        public float playerAttackMultiplier = 1.5f;
        [Tooltip("플레이어: 방어력 — 플레이어로 맞을 때 받는 피해 감소 비율 (0.3 = 30% 감소)")]
        public float playerDamageReduction = 0.3f;

        [Tooltip("걷기 속도 (유닛/초) — 궁수가 더 날쌤")]
        public float playerMoveSpeed = 5f;

        [Tooltip("원거리 무기: 반격·일반공격 데미지 배율 (근접은 playerAttackMultiplier)")]
        public float rangedAttackMultiplier = 1f;
        [Tooltip("무기 전환 쿨타임 (초, 게이지 소모 없음)")]
        public float weaponSwitchCooldown = 0.5f;


        public float AttackMultiplier(CharacterKind kind) =>
            playerAttackMultiplier;

        public float DamageReduction(CharacterKind kind) =>
            Mathf.Clamp01(playerDamageReduction);

        public float MoveSpeed(CharacterKind kind) =>
            Mathf.Max(0.5f, playerMoveSpeed);

        // ── 적 ──
        [Tooltip("근접 / 원거리 각각 동시에 유지할 몬스터 수 (F1 몬스터 탭)")]
        public int enemiesPerKind = 1;
        // 근접 반응시간 = 근접 예비 모션 길이. 베기는 예비 모션이 끝나면 한 번에 나가므로 (기획서 3.1)
        public float meleeNormalReaction = 0.65f;
        public float meleeHeavyReaction = 1.2f;
        public float heavyChance = 0.3f;
        public float attackIntervalMin = 1.4f;
        public float attackIntervalMax = 2.4f;
        // 원거리 예비 모션 (원거리는 투사체가 날아오는 동안이 반응시간이라 예비 모션은 짧게)
        public float normalWindup = 0.22f;
        public float heavyWindup = 0.45f;
        public float rangedNormalSpeed = 8f;
        public float rangedHeavySpeed = 5f;

        // ── 연습 스위치 (저장하지 않음 — 켜둔 채 잊어버리면 헷갈리므로) ──
        [NonSerialized] public bool godMode;
        [NonSerialized] public bool enemyInvincible;
        [NonSerialized] public bool heavyOnly;
        [NonSerialized] public bool enemyPassive;
        /// <summary>게이지를 써도 줄지 않음 (스킬·수동 스왑 연습용).</summary>
        [NonSerialized] public bool infiniteGauge;
        /// <summary>근접 / 원거리 몬스터 치우기 (한 종류씩 연습용).</summary>
        [NonSerialized] public bool hideMelee;
        [NonSerialized] public bool hideRanged;
        /// <summary>도트 그림 위에 판정 네모를 반투명하게 겹쳐 본다 (그림과 판정이 맞는지 확인용).</summary>
        [NonSerialized] public bool showHitboxes;

        public float InputBufferOrZero => inputBufferEnabled ? inputBuffer : 0f;

        // ───────────── 저장 / 불러오기 ─────────────

        /// <summary>테스트에서 false로 두면 파일을 읽지도 쓰지도 않고 항상 기본값을 쓴다.</summary>
        public static bool FileIOEnabled = true;

        public static event Action Changed;

        private static GameTuning _current;

        public static GameTuning Current => _current ??= Load();

        public static string FilePath => Application.isEditor
            ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tuning", "tuning.json"))
            : Path.Combine(Application.persistentDataPath, "tuning.json");

        private static GameTuning Load()
        {
            var tuning = new GameTuning();
            if (!FileIOEnabled || !File.Exists(FilePath)) return tuning;
            try
            {
                JsonUtility.FromJsonOverwrite(File.ReadAllText(FilePath), tuning);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ParryRL] 튜닝 파일을 읽지 못해 기본값 사용: {e.Message}");
            }
            return tuning;
        }

        public static void Save()
        {
            if (!FileIOEnabled || _current == null) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonUtility.ToJson(_current, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ParryRL] 튜닝 파일 저장 실패: {e.Message}");
            }
        }

        /// <summary>값을 바꾼 쪽에서 호출 (패널 등). 저장은 호출한 쪽이 모아서.</summary>
        public static void NotifyChanged() => Changed?.Invoke();

        /// <summary>튜닝 값만 기본값으로 (연습 스위치는 유지).</summary>
        public static void ResetToDefaults()
        {
            var practice = _current;
            _current = new GameTuning();
            if (practice != null)
            {
                _current.godMode = practice.godMode;
                _current.enemyInvincible = practice.enemyInvincible;
                _current.heavyOnly = practice.heavyOnly;
                _current.enemyPassive = practice.enemyPassive;
            }
            Save();
            NotifyChanged();
        }

        /// <summary>테스트용: 파일 없이 완전한 기본값으로.</summary>
        public static void UseDefaultsForTests()
        {
            FileIOEnabled = false;
            _current = new GameTuning();
        }
    }
}
