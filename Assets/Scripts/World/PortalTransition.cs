using System;
using System.Collections;
using UnityEngine;

namespace ParryRL
{
    /// <summary>
    /// 포탈을 지나는 연출 (1017 추가분 `portal_enter` / `portal_exit`): 들어갈 때 몸이 보라 빛으로 흩어진 뒤 다음 방을 불러오고,
    /// 새 방에 도착하면 빛이 감겨 몸이 된다. 연출 동안 플레이어는 움직이거나 공격하지 못한다(판정은 그대로).
    /// </summary>
    public static class PortalTransition
    {
        /// <summary>테스트는 끄고 바로 넘어간다 (연출 테스트만 켠다).</summary>
        public static bool PlayEffects = true;
        public const float EnterTime = 0.5f;   // 흩어지는 장면 6장 / 12fps
        public const float ArriveTime = 0.5f;

        /// <summary>몸이 흩어지는 연출 뒤 load를 부른다. 연출을 끄면 바로 부른다.</summary>
        public static IEnumerator Enter(Action load)
        {
            var party = UnityEngine.Object.FindAnyObjectByType<PlayerParty>();
            if (!PlayEffects || party == null)
            {
                load();
                yield break;
            }
            SetControls(party, false);
            PlayAtFeet(party, GameAssets.AttackFx != null ? GameAssets.AttackFx.portalEnter : null);
            Sfx.Play(SfxId.SkillSeal, 0.5f);
            for (float t = 0f; t < EnterTime; t += Time.unscaledDeltaTime)
            {
                party.Visibility = 1f - Mathf.Clamp01(t / (EnterTime * 0.35f)); // 첫 장면(빛으로 된 몸)이 몸을 덮는 동안 사라진다
                yield return null;
            }
            party.Visibility = 0f;
            Hud.ScreenFlash(new Color(0.65f, 0.45f, 1f, 0.8f));
            load();
        }

        /// <summary>새 방 도착: 빛이 감겨 몸이 되고, 마지막 장면쯤 몸이 나타나 다시 움직일 수 있다.</summary>
        public static IEnumerator Arrive()
        {
            var party = UnityEngine.Object.FindAnyObjectByType<PlayerParty>();
            if (!PlayEffects || party == null) yield break;
            SetControls(party, false);
            party.Visibility = 0f;
            yield return null; // 카메라·위치가 자리 잡은 뒤
            PlayAtFeet(party, GameAssets.AttackFx != null ? GameAssets.AttackFx.portalExit : null);
            Sfx.Play(SfxId.SkillSeal, 0.4f);
            for (float t = 0f; t < ArriveTime; t += Time.deltaTime)
            {
                party.Visibility = Mathf.Clamp01((t - ArriveTime * 0.55f) / (ArriveTime * 0.4f));
                yield return null;
            }
            party.Visibility = 1f;
            SetControls(party, true);
        }

        private static void PlayAtFeet(PlayerParty party, SpriteClip clip)
        {
            if (clip == null || clip.IsEmpty) return;
            var motor = party.GetComponent<PlayerMotor>();
            float feet = party.transform.position.y - party.transform.localScale.y * 0.5f;
            float h = clip.frames[0].bounds.size.y;
            FxClip.PlayAt(clip, new Vector2(party.transform.position.x, feet + h * 0.5f), 1f, motor != null && motor.Facing < 0, 46);
        }

        private static void SetControls(PlayerParty party, bool on)
        {
            var motor = party.GetComponent<PlayerMotor>();
            var combat = party.GetComponent<PlayerCombat>();
            if (motor != null) motor.enabled = on;
            if (combat != null) combat.enabled = on;
            var rb = party.GetComponent<Rigidbody2D>();
            if (!on && rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
    }
}
