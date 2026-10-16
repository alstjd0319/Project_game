using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ParryRL
{
    /// <summary>
    /// 화면 하단 스킬바: Shift 이동 / A 공격 / D·F 스킬 / S 방어 / Q 무기 전환 (임시 아이콘).
    /// 원형 쿨타임 + 남은 초, 도력 코스트 배지(부족하면 빨강), 사용 불가 시 어둡게,
    /// 발동 시 튀어오름 / 실패 시 흔들림 / 쿨타임 끝나면 테두리 번쩍.
    /// </summary>
    public class SkillBar
    {
        private static readonly Color GaugeColor = new(0.72f, 0.62f, 1f);
        private static readonly Color LackColor = new(0.95f, 0.25f, 0.25f);

        private const float SlotSize = 104f;
        private const float Border = 4f;
        private const float Spacing = 30f;

        /// <summary>슬롯 한 칸이 매 프레임 표시할 상태.</summary>
        private struct SlotState
        {
            public Color color;
            public Sprite icon;       // 귀문 UI 아이콘 (없으면 글자 아이콘)
            public string name;
            public int cost;          // 0이면 코스트 배지 숨김
            public bool affordable;
            public bool blocked;      // 방어 활성 중 등 지금은 못 씀
            public float cooldownLeft;
            public float cooldownTotal;
            public float activeLeft;  // 방어 활성 시간 (S 전용)
            public float activeTotal;
        }

        private class Slot
        {
            public RectTransform root;
            public Vector2 basePos;
            public Image frame;
            public Image bg;
            public Text icon;
            public Image iconImage;
            public Image activeFill;
            public Image cooldownFill;
            public Image dim;
            public Text cooldownText;
            public Image costBadge;
            public Text costText;
            public Text nameText;
            public Func<SlotState> state;

            public float pulse;
            public float shake;
            public float readyFlash;
            public bool wasCoolingDown;
        }

        private readonly Dictionary<PlayerAction, Slot> _slots = new();

        private PlayerParty _party;
        private SkillGauge _gauge;
        private PlayerDefense _defense;
        private PlayerCombat _combat;

        public SkillBar(RectTransform canvasRoot)
        {
            var bar = new GameObject("SkillBar", typeof(RectTransform)).GetComponent<RectTransform>();
            bar.SetParent(canvasRoot, false);
            float slotOuter = SlotSize + Border * 2f;
            const int slotCount = 6;
            float width = slotOuter * slotCount + Spacing * (slotCount - 1);
            UiKit.Place(bar, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(width, slotOuter + 42f));

            // 바닥 지형 위에 겹쳐도 슬롯이 묻히지 않도록 어두운 받침
            var backing = UiKit.Image("Backing", bar, new Color(0.05f, 0.05f, 0.08f, 0.75f));
            UiKit.Stretch(backing.rectTransform, -18f);

            (PlayerAction action, string key, string glyph)[] defs =
            {
                (PlayerAction.Move, "Shift", "≫"),
                (PlayerAction.Attack, "A", "★"),
                (PlayerAction.Skill1, "D", "※"),
                (PlayerAction.Skill2, "F", "▼"),
                (PlayerAction.Defend, "S", "◈"),
                (PlayerAction.Weapon, "Q", "⇄"),
            };
            for (int i = 0; i < defs.Length; i++)
            {
                var pos = new Vector2(i * (slotOuter + Spacing), 42f);
                _slots[defs[i].action] = CreateSlot(bar, pos, defs[i].key, defs[i].glyph);
            }
        }

        public void Bind(PlayerParty party, SkillGauge gauge, PlayerDefense defense, PlayerCombat combat)
        {
            _party = party;
            _gauge = gauge;
            _defense = defense;
            _combat = combat;
            if (_party == null || _gauge == null || _defense == null || _combat == null) return;

            _slots[PlayerAction.Move].state = MoveState;
            _slots[PlayerAction.Attack].state = AttackState;
            _slots[PlayerAction.Skill1].state = () => SkillState(1);
            _slots[PlayerAction.Skill2].state = () => SkillState(2);
            _slots[PlayerAction.Defend].state = DefendState;
            _slots[PlayerAction.Weapon].state = WeaponState;

            _combat.ActionUsed += a => Pulse(a);
            _combat.ActionDenied += a => Deny(a);
            _defense.Activated += () => Pulse(PlayerAction.Defend);
            _defense.ActivateDenied += () => Deny(PlayerAction.Defend);
        }

        private void Pulse(PlayerAction a) => _slots[a].pulse = 1f;
        private void Deny(PlayerAction a) => _slots[a].shake = 1f;

        // ───────────── 슬롯별 상태 ─────────────

        private static UiArt Art => GameAssets.Ui;
        private static Sprite Skill(int i) => Art != null ? Art.Skill(i) : null;

        private SlotState MoveState() => new()
        {
            color = _party.Current.color,
            icon = Skill(3),
            name = _combat.MoveSkillMaxCharges > 1 ? $"돌진 {_combat.MoveSkillCharges}/{_combat.MoveSkillMaxCharges}" : "이동 · 돌진",
            cost = _combat.MoveSkillGaugeCost,
            affordable = _gauge.CanSpend(_combat.MoveSkillGaugeCost),
            cooldownLeft = _combat.MoveSkillCooldownLeft,
            cooldownTotal = _combat.MoveSkillCooldown,
        };

        private SlotState AttackState() => new()
        {
            color = _combat.WeaponColor,
            icon = Skill(_combat.IsRanged ? 4 : 1),
            name = _combat.IsRanged ? "일반공격 · 화살" : "일반공격 · 베기",
            cost = _combat.AttackSkillGaugeCost,
            affordable = _gauge.CanSpend(_combat.AttackSkillGaugeCost),
            blocked = _defense.State == DefenseState.Active,
            cooldownLeft = _combat.AttackSkillCooldownLeft,
            cooldownTotal = _combat.AttackSkillCooldown,
        };

        // D / F 스킬: 무기에 따라 이름이 바뀌고, 도력을 쓴다
        private SlotState SkillState(int slot)
        {
            int cost = slot == 1 ? _combat.Skill1Cost : _combat.Skill2Cost;
            return new SlotState
            {
                color = _combat.WeaponColor,
                icon = Skill(slot == 1 ? (_combat.IsRanged ? 5 : 0) : (_combat.IsRanged ? 6 : 2)),
                name = slot == 1 ? _combat.Skill1Name : _combat.Skill2Name,
                cost = cost,
                affordable = _gauge.CanSpend(cost),
                blocked = _defense.State == DefenseState.Active && !_defense.SucceededThisActive,
                cooldownLeft = slot == 1 ? _combat.Skill1CooldownLeft : _combat.Skill2CooldownLeft,
                cooldownTotal = slot == 1 ? _combat.Skill1Cooldown : _combat.Skill2Cooldown,
            };
        }

        // 무기 전환 슬롯: 지금 든 무기를 보여주고, 전환 쿨타임을 돈다 (도력 소모 없음)
        private SlotState WeaponState() => new()
        {
            color = _combat.WeaponColor,
            icon = Art != null ? (_combat.IsRanged ? Art.weaponTalisman : Art.weaponStaff) : null,
            name = _combat.IsRanged ? "무기 · 원거리" : "무기 · 근접",
            blocked = _defense.State == DefenseState.Active,
            cooldownLeft = _combat.WeaponCooldownLeft,
            cooldownTotal = _combat.WeaponCooldown,
        };

        private SlotState DefendState()
        {
            var s = new SlotState
            {
                color = _party.Current.color,
                icon = Skill(7),
                name = _party.Current.defenseName,
                affordable = true,
            };
            if (_defense.State == DefenseState.Active)
            {
                s.activeLeft = _defense.Timer;
                s.activeTotal = _defense.ActiveDuration;
            }
            else if (_defense.State == DefenseState.Cooldown)
            {
                s.name = "헛스윙";
                s.cooldownLeft = _defense.Timer;
                s.cooldownTotal = _defense.WhiffCooldown;
            }
            return s;
        }

        // ───────────── 갱신 ─────────────

        public void Tick(float dt)
        {
            foreach (var slot in _slots.Values)
            {
                if (slot.state == null) continue;
                Apply(slot, slot.state(), dt);
            }
        }

        private static void Apply(Slot slot, SlotState s, float dt)
        {
            bool coolingDown = s.cooldownLeft > 0f;
            bool active = s.activeLeft > 0f;
            bool usable = !coolingDown && !s.blocked && s.affordable;

            // 쿨타임이 막 끝난 순간 테두리 번쩍
            if (slot.wasCoolingDown && !coolingDown) slot.readyFlash = 1f;
            slot.wasCoolingDown = coolingDown;

            slot.pulse = Mathf.Max(0f, slot.pulse - dt * 6f);
            slot.shake = Mathf.Max(0f, slot.shake - dt * 4f);
            slot.readyFlash = Mathf.Max(0f, slot.readyFlash - dt * 4f);

            // 배경: 캐릭터 색을 어둡게
            slot.bg.color = Color.Lerp(s.color, Color.black, 0.55f);
            slot.icon.color = usable || active ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            bool hasIcon = s.icon != null;
            slot.iconImage.enabled = hasIcon;
            slot.icon.enabled = !hasIcon;
            if (hasIcon)
            {
                slot.bg.color = new Color(0.07f, 0.08f, 0.1f, 1f); // 메달처럼 네모가 아닌 아이콘 뒤가 무기 색으로 비치지 않게
                slot.iconImage.sprite = s.icon;
                // 못 쓸 때는 그림을 어둡게 (아이콘 자체가 틀을 포함한다)
                slot.iconImage.color = usable || active ? Color.white : new Color(0.55f, 0.55f, 0.6f, 1f);
            }

            // 방어 활성: 남은 활성 시간을 밝은 원형으로
            slot.activeFill.fillAmount = active && s.activeTotal > 0f ? s.activeLeft / s.activeTotal : 0f;

            // 쿨타임: 남은 비율만큼 어두운 원형 + 남은 초
            slot.cooldownFill.fillAmount = coolingDown && s.cooldownTotal > 0f ? s.cooldownLeft / s.cooldownTotal : 0f;
            slot.cooldownText.enabled = coolingDown || active;
            if (coolingDown) slot.cooldownText.text = s.cooldownLeft.ToString("0.0");
            else if (active) slot.cooldownText.text = s.activeLeft.ToString("0.00");

            // 도력 부족 / 지금 못 씀 → 전체를 어둡게
            slot.dim.enabled = !coolingDown && !active && (!s.affordable || s.blocked);

            // 코스트 배지
            slot.costBadge.gameObject.SetActive(s.cost > 0);
            slot.costText.text = s.cost.ToString();
            slot.costBadge.color = s.affordable ? GaugeColor : LackColor;
            slot.costText.color = s.affordable ? new Color(0.15f, 0.1f, 0f) : Color.white;

            slot.nameText.text = s.name;
            slot.nameText.color = s.name == "헛스윙" ? new Color(0.75f, 0.75f, 0.8f) : Color.Lerp(s.color, Color.white, 0.55f);

            // 테두리: 기본 흐림 / 활성 흰색 / 준비 완료 번쩍 / 실패 빨강
            Color frame = new(1f, 1f, 1f, usable ? 0.35f : 0.15f);
            if (active) frame = Color.white;
            frame = Color.Lerp(frame, Color.white, slot.readyFlash);
            frame = Color.Lerp(frame, LackColor, slot.shake);
            slot.frame.color = frame;

            float scale = 1f + 0.15f * slot.pulse * slot.pulse;
            slot.root.localScale = new Vector3(scale, scale, 1f);
            float shakeX = Mathf.Sin(slot.shake * 40f) * 8f * slot.shake;
            slot.root.anchoredPosition = slot.basePos + new Vector2(shakeX, 0f);
        }

        // ───────────── 생성 ─────────────

        private static Slot CreateSlot(RectTransform bar, Vector2 pos, string key, string glyph)
        {
            float outer = SlotSize + Border * 2f;
            var slot = new Slot();

            // 테두리 이미지를 루트로 쓰고, 중앙 피벗으로 두어 튀어오름이 가운데서 커지게 한다
            slot.frame = UiKit.Image($"Slot_{key}", bar, Color.white);
            slot.root = slot.frame.rectTransform;
            slot.root.anchorMin = slot.root.anchorMax = new Vector2(0f, 0f);
            slot.root.pivot = new Vector2(0.5f, 0.5f);
            slot.root.sizeDelta = new Vector2(outer, outer);
            slot.basePos = pos + new Vector2(outer * 0.5f, outer * 0.5f);
            slot.root.anchoredPosition = slot.basePos;

            slot.bg = UiKit.Image("Bg", slot.root, Color.black);
            UiKit.Stretch(slot.bg.rectTransform, Border);

            slot.icon = UiKit.Text("Icon", slot.bg.rectTransform, 58, TextAnchor.MiddleCenter, outline: false);
            UiKit.Stretch(slot.icon.rectTransform);
            // 귀문 UI 아이콘 (그림 자체가 금빛 틀을 포함한다. 가로세로 비율을 지킨다)
            slot.iconImage = UiKit.Image("IconImage", slot.bg.rectTransform, Color.white);
            slot.iconImage.preserveAspect = true;
            UiKit.Stretch(slot.iconImage.rectTransform);
            slot.iconImage.enabled = false;

            slot.activeFill = UiKit.RadialImage("ActiveFill", slot.bg.rectTransform, new Color(1f, 1f, 1f, 0.45f));
            UiKit.Stretch(slot.activeFill.rectTransform);

            slot.cooldownFill = UiKit.RadialImage("CooldownFill", slot.bg.rectTransform, new Color(0f, 0f, 0f, 0.72f));
            UiKit.Stretch(slot.cooldownFill.rectTransform);

            slot.dim = UiKit.Image("Dim", slot.bg.rectTransform, new Color(0f, 0f, 0f, 0.55f));
            UiKit.Stretch(slot.dim.rectTransform);

            slot.cooldownText = UiKit.Text("Cooldown", slot.bg.rectTransform, 36, TextAnchor.MiddleCenter);
            slot.cooldownText.fontStyle = FontStyle.Bold;
            UiKit.Stretch(slot.cooldownText.rectTransform);

            // 키 라벨 (좌상단)
            var keyBg = UiKit.Image("KeyBg", slot.root, new Color(0f, 0f, 0f, 0.8f));
            float keyWidth = key.Length > 1 ? 58f : 30f;
            UiKit.Place(keyBg.rectTransform, new Vector2(0f, 1f), new Vector2(-6f, 8f), new Vector2(keyWidth, 28f));
            var keyText = UiKit.Text("Key", keyBg.rectTransform, 19, TextAnchor.MiddleCenter, outline: false);
            keyText.fontStyle = FontStyle.Bold;
            UiKit.Stretch(keyText.rectTransform);
            keyText.text = key;

            // 도력 코스트 배지 (우하단)
            slot.costBadge = UiKit.Image("CostBadge", slot.root, GaugeColor);
            UiKit.Place(slot.costBadge.rectTransform, new Vector2(1f, 0f), new Vector2(8f, -8f), new Vector2(40f, 32f));
            slot.costText = UiKit.Text("Cost", slot.costBadge.rectTransform, 22, TextAnchor.MiddleCenter, outline: false);
            slot.costText.fontStyle = FontStyle.Bold;
            UiKit.Stretch(slot.costText.rectTransform);

            // 스킬 이름 (아래)
            slot.nameText = UiKit.Text("Name", slot.root, 21, TextAnchor.UpperCenter);
            UiKit.Place(slot.nameText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -40f), new Vector2(200f, 28f));
            slot.nameText.rectTransform.pivot = new Vector2(0.5f, 0f);

            slot.icon.text = glyph;
            slot.cooldownText.enabled = false;
            slot.dim.enabled = false;
            return slot;
        }
    }
}
