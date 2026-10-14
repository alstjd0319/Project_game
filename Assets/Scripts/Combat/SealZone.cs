using System.Collections.Generic;
using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 부적 스킬 "봉인진": 바닥에 깔리는 판정 영역. 지속 시간 동안 안에 있는 적을 계속 기절시키고(묶는다),
    /// 일정 간격마다 약한 데미지를 준다. 판정 상자(박스) = 그림의 진 범위. 그림은 시작 3장 → 반복 2장 → 끝 2장.
    /// </summary>
    public class SealZone : MonoBehaviour
    {
        private const float BindInterval = 0.2f;
        private const float BindSeconds = 0.35f;
        private const float GroundOffset = 0.8f; // 256칸 그림에서 진 바닥선이 중심보다 0.8유닛(×배율) 아래

        private Vector2 _size;
        private float _duration;
        private float _tickInterval;
        private int _tickDamage;
        private HitInfo _hit;
        private SpriteClip _clip;
        private SpriteRenderer _art;
        private float _t;
        private float _nextBind;
        private float _nextTick;

        private static readonly List<EnemyController> Inside = new();

        public float TimeLeft => Mathf.Max(0f, _duration - _t);

        /// <param name="groundCenter">진 가운데 바닥 좌표</param>
        public static SealZone Spawn(Vector2 groundCenter, Vector2 size, float duration, float tickInterval, int tickDamage,
            HitInfo hit, SpriteClip art, float artScale)
        {
            var go = Box.Create("SealZone", new Vector2(groundCenter.x, groundCenter.y + size.y * 0.5f), size,
                new Color(1f, 0.8f, 0.3f, 0.35f), 7);
            var z = go.AddComponent<SealZone>();
            z._size = size;
            z._duration = duration;
            z._tickInterval = tickInterval;
            z._tickDamage = tickDamage;
            z._hit = hit;
            z._clip = art;
            var box = go.GetComponent<SpriteRenderer>();
            box.enabled = GameTuning.Current.showHitboxes || art == null || art.IsEmpty;
            if (art != null && !art.IsEmpty)
            {
                var child = new GameObject("Art");
                child.transform.SetParent(go.transform, false);
                // 부모 스케일 = 박스 크기 → 상쇄해서 그림을 원본 비율(× artScale)로, 진 바닥선이 땅에 오게
                child.transform.localScale = new Vector3(artScale / size.x, artScale / size.y, 1f);
                child.transform.localPosition = new Vector3(0f, (-size.y * 0.5f + GroundOffset * artScale) / size.y, 0f);
                z._art = child.AddComponent<SpriteRenderer>();
                z._art.sortingOrder = 6;
                z._art.sprite = art.frames[0];
            }
            return z;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            if (_t >= _duration)
            {
                Destroy(gameObject);
                return;
            }
            UpdateArt();

            if (_t >= _nextBind || _t >= _nextTick)
            {
                Physics2D.SyncTransforms();
                Inside.Clear();
                foreach (var col in Physics2D.OverlapBoxAll(transform.position, _size, 0f))
                {
                    var e = col.attachedRigidbody != null
                        ? col.attachedRigidbody.GetComponent<EnemyController>()
                        : col.GetComponent<EnemyController>();
                    if (e != null && !e.IsDead && !Inside.Contains(e)) Inside.Add(e);
                }
                if (_t >= _nextBind)
                {
                    _nextBind = _t + BindInterval;
                    foreach (var e in Inside) e.Stun(BindSeconds);
                }
                if (_t >= _nextTick)
                {
                    _nextTick = _t + _tickInterval;
                    foreach (var e in Inside)
                        if (!e.IsDead) DamageCalc.Apply(_tickDamage, _hit, e, 0f); // 방향 0 = 밀치지 않는다 (진 안에 묶어 둔다)
                }
            }
        }

        private void UpdateArt()
        {
            if (_art == null) return;
            int n = _clip.frames.Length;
            int intro = Mathf.Min(3, n);
            int outro = n > 4 ? 2 : 0;
            float frameTime = 1f / Mathf.Max(1f, _clip.fps);
            float left = _duration - _t;
            int frame;
            if (_t < intro * frameTime) frame = (int)(_t / frameTime);
            else if (outro > 0 && left < outro * frameTime) frame = n - outro + Mathf.Clamp((int)((outro * frameTime - left) / frameTime), 0, outro - 1);
            else
            {
                // 가운데 반복: 마지막 intro 장면(가장 밝은 쪽)과 그 다음 장면을 번갈아
                int loopStart = Mathf.Max(0, intro - 1);
                int loopLen = Mathf.Max(1, Mathf.Min(2, n - outro - loopStart));
                frame = loopStart + (int)((_t - intro * frameTime) / frameTime) % loopLen;
            }
            _art.sprite = _clip.frames[Mathf.Clamp(frame, 0, n - 1)];
        }
    }
}
