using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DungeonCombat.Data;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DungeonCombat.Combat
{
    public class BattleUIController : MonoBehaviour
    {
        [Header("[ 핵심 매니저 참조 ]")]
        [SerializeField] private BattleTurnManager turnManager;

        [Header("[ 1. 라운드 관련 UI ]")]
        [SerializeField] private TextMeshProUGUI roundText;

        [Header("[ 2. 턴 표시 UI (타임라인 리스트) ]")]
        [SerializeField] private RectTransform turnListParent;
        [SerializeField] private GameObject turnSlotPrefab;

        [Header("[ 3. 아군 파티 캐릭터 UI 슬롯들 ]")]
        [SerializeField] private List<PartyUnitUISlot> partyUISlots;

        [Header("[ 4. 캐릭터 스킬 UI 제어 패널 ]")]
        [SerializeField] private GameObject skillPanelParent;
        [SerializeField] private List<Button> skillButtons;
        [SerializeField] private List<TextMeshProUGUI> skillButtonTexts;

        [Header("[ 5. 스킬 툴팁 UI (배경 & 텍스트 세트) ]")]
        [SerializeField] private GameObject tooltipPanel;
        [SerializeField] private List<TextMeshProUGUI> tooltipDescriptionTexts;
        [SerializeField] private TextMeshProUGUI tooltipDescriptionText;

        [Header("[ 6. 패시브 툴팁 UI (배경 & 텍스트 세트) ]")]
        [SerializeField] private GameObject passiveTooltipPanel;
        [SerializeField] private TextMeshProUGUI passiveTooltipText;

        public bool IsVisualTransitionActive { get; set; } = false;
        public static bool IsZSkillModeActive { get; private set; } = false;

        private BattleUnit currentPassiveTooltipUnit;
        private SkillDataSO currentlyPreviewedSkill;

        private void Start()
        {
            if (turnManager == null) turnManager = FindFirstObjectByType<BattleTurnManager>();

            foreach (var slot in partyUISlots)
            {
                slot.Initialize(OnCharacterImageClicked);
            }

            if (roundText != null && turnManager != null)
            {
                roundText.text = $"ROUND {Mathf.Max(1, turnManager.CurrentRound)}";
            }

            if (tooltipPanel != null) tooltipPanel.SetActive(false);
            if (passiveTooltipPanel != null) passiveTooltipPanel.SetActive(false);

            if (turnManager != null)
            {
                turnManager.OnRoundStarted += UpdateRoundUI;
                turnManager.OnTurnStarted += UpdateActiveUnitUI;
                turnManager.OnBattleEnded += HandleBattleEnd;
            }

            if (SkillCaster.Instance != null)
            {
                SkillCaster.Instance.OnTargetingModeEnded += () => { currentlyPreviewedSkill = null; };
            }
        }

        private void Update()
        {
            bool qPressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame) qPressed = true;
#else
            if (Input.GetKeyDown(KeyCode.Q)) qPressed = true;
#endif

            if (qPressed)
            {
                IsZSkillModeActive = !IsZSkillModeActive;
                Debug.Log($"[Z스킬 모드] {(IsZSkillModeActive ? "<color=gold>활성화</color>" : "비활성화")}");

                if (SkillCaster.Instance != null && SkillCaster.Instance.ActiveSkill != null)
                {
                    SkillCaster.Instance.ToggleZSkillState(IsZSkillModeActive);
                    currentlyPreviewedSkill = SkillCaster.Instance.ActiveSkill;

                    if (tooltipPanel != null && tooltipPanel.activeSelf)
                    {
                        tooltipDescriptionText.text = SkillCaster.Instance.ActiveSkill.GetFormattedDescription(1);
                    }
                }

                RebuildTurnTimelineUI();

                if (turnManager != null && turnManager.CurrentTurnUnit != null)
                {
                    UpdateSkillButtons(turnManager.CurrentTurnUnit);
                }
            }
        }

        private void UpdateRoundUI(int round)
        {
            if (roundText != null) roundText.text = $"ROUND {round}";
            SetSkillPanelActive(false);
            RebuildTurnTimelineUI();
        }

        private void UpdateActiveUnitUI(BattleUnit activeUnit)
        {
            for (int i = 0; i < partyUISlots.Count; i++)
            {
                partyUISlots[i].SetHighlight(partyUISlots[i].TargetUnit == activeUnit);
            }
            UpdateSkillButtons(activeUnit);
            RebuildTurnTimelineUI();
        }

        private void RebuildTurnTimelineUI()
        {
            if (turnListParent == null || turnSlotPrefab == null || turnManager == null) return;

            foreach (Transform child in turnListParent) Destroy(child.gameObject);

            var queue = turnManager.TurnQueue;
            int currentIndex = turnManager.CurrentQueueIndex;
            if (queue == null || currentIndex < 0) return;

            for (int i = currentIndex; i < queue.Count; i++)
            {
                TurnSlot slot = queue[i];
                if (slot.Unit == null || slot.Unit.CurrentHP <= 0) continue;

                GameObject slotObj = Instantiate(turnSlotPrefab, turnListParent);
                TextMeshProUGUI slotText = slotObj.GetComponentInChildren<TextMeshProUGUI>();
                if (slotText != null)
                {
                    string characterName = slot.Unit.UnitName;
                    string displayName = characterName;

                    if (slot.Unit.IsBoss && slot.ActionIndex != 99) displayName = $"{characterName} ({slot.ActionIndex + 1})";
                    else if (slot.ActionIndex == 99) displayName = $"{characterName} (추가행동)";

                    slotText.text = $"<color=#FFFFFF>{displayName}</color>";
                }

                Image bgImage = slotObj.GetComponent<Image>();
                if (bgImage != null)
                {
                    if (slot.Unit is EnemyUnit)
                    {
                        bgImage.color = slot.Unit.IsBoss ? new Color(0.85f, 0.25f, 0.25f, 1f) : new Color(0.75f, 0.35f, 0.35f, 1f);
                    }
                    else
                    {
                        bgImage.color = new Color(0.25f, 0.45f, 0.75f, 1f);
                    }
                }
            }
        }

        public void UpdateSkillButtons(BattleUnit unit)
        {
            if (unit == null || unit.IsBoss || unit is EnemyUnit)
            {
                SetSkillPanelActive(false);
                return;
            }

            PlayerUnit player = unit as PlayerUnit;
            if (player == null || player.CharacterData == null)
            {
                SetSkillPanelActive(false);
                return;
            }

            if (tooltipPanel != null) tooltipPanel.SetActive(false);

            ConfigureSkillButton(0, player.EquippedUniqueSkill, player.EquippedUniqueSkillLevel);

            for (int i = 1; i < skillButtons.Count; i++)
            {
                int normalSkillIndex = i - 1;
                if (normalSkillIndex < player.CharacterData.LearnableSkills.Count)
                {
                    ConfigureSkillButton(i, player.CharacterData.LearnableSkills[normalSkillIndex], 1);
                }
                else
                {
                    if (i < skillButtons.Count && skillButtons[i] != null) skillButtons[i].gameObject.SetActive(false);
                }
            }

            SetSkillPanelActive(!IsVisualTransitionActive);
        }

        private void ConfigureSkillButton(int buttonIndex, SkillDataSO skill, int currentLevel)
        {
            if (buttonIndex >= skillButtons.Count || skill == null) return;

            Button btn = skillButtons[buttonIndex];
            TextMeshProUGUI btnText = skillButtonTexts[buttonIndex];
            if (btn == null || btnText == null) return;

            btn.gameObject.SetActive(true);

            if (IsZSkillModeActive && skill.EnhancedSkillAsset != null && buttonIndex == 0)
            {
                SkillDataSO enhanced = skill.EnhancedSkillAsset;
                btnText.text = $"<color=#FFD700>★ {enhanced.SkillName} ★\n(Lv.{currentLevel})</color>";

                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => { OnSkillButtonClicked(enhanced, currentLevel); });
            }
            else
            {
                btnText.text = $"{skill.SkillName}\n(Lv.{currentLevel})";

                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => { OnSkillButtonClicked(skill, currentLevel); });
            }
        }

        public void SetSkillPanelActive(bool isActive)
        {
            if (skillPanelParent != null) skillPanelParent.SetActive(isActive);
            else
            {
                foreach (var btn in skillButtons)
                {
                    if (btn != null) btn.gameObject.SetActive(isActive);
                }
            }
        }

        private void OnSkillButtonClicked(SkillDataSO skill, int level)
        {
            if (tooltipPanel != null) tooltipPanel.SetActive(true);
            if (tooltipDescriptionText != null) tooltipDescriptionText.text = skill.GetFormattedDescription(level);

            PlayerUnit caster = turnManager.CurrentTurnUnit as PlayerUnit;
            if (caster == null) return;

            if (currentlyPreviewedSkill == skill)
            {
                if (SkillCaster.Instance != null) SkillCaster.Instance.SelectSkill(caster, skill, level);
                currentlyPreviewedSkill = null;
            }
            else
            {
                currentlyPreviewedSkill = skill;
                if (SkillCaster.Instance != null) SkillCaster.Instance.PreviewSkill(caster, skill, level);
            }
        }

        private void OnCharacterImageClicked(BattleUnit unit)
        {
            if (unit == null || unit.PassiveSkill == null) return;

            if (passiveTooltipPanel != null)
            {
                if (passiveTooltipPanel.activeSelf && currentPassiveTooltipUnit == unit)
                {
                    passiveTooltipPanel.SetActive(false);
                    currentPassiveTooltipUnit = null;
                }
                else
                {
                    passiveTooltipPanel.SetActive(true);
                    currentPassiveTooltipUnit = unit;

                    if (passiveTooltipText != null)
                    {
                        string passiveName = unit.PassiveSkill.PassiveName;
                        int passiveLevel = unit.PassiveLevel;
                        string formattedDesc = unit.PassiveSkill.GetFormattedDescription(passiveLevel);

                        passiveTooltipText.text = $"<color=#FFD700><b>[패시브] {passiveName} (Lv.{passiveLevel})</b></color>\n\n{formattedDesc}";
                    }
                }
            }
        }

        public void HideAllTooltips()
        {
            if (tooltipPanel != null) tooltipPanel.SetActive(false);
            if (passiveTooltipPanel != null) passiveTooltipPanel.SetActive(false);

            currentPassiveTooltipUnit = null;
            currentlyPreviewedSkill = null;
        }

        private void HandleBattleEnd()
        {
            SetSkillPanelActive(false);
            HideAllTooltips();
        }
    }

    [System.Serializable]
    public class PartyUnitUISlot
    {
        [SerializeField] private BattleUnit targetUnit;
        [SerializeField] private Image highlightBorder;
        [SerializeField] private Image hpFillImage;
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private Image overheatFillImage;
        [SerializeField] private List<Image> spBlocks;

        public BattleUnit TargetUnit => targetUnit;

        public void Initialize(System.Action<BattleUnit> onCharacterClick)
        {
            if (targetUnit == null) return;

            SetHighlight(false);

            if (overheatFillImage != null)
            {
                Button btn = overheatFillImage.GetComponent<Button>();
                if (btn == null) btn = overheatFillImage.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => onCharacterClick?.Invoke(targetUnit));
            }

            BindEvents();
        }

        private void BindEvents()
        {
            if (targetUnit == null) return;

            targetUnit.OnHPChanged += UpdateHP;
            targetUnit.OnSPChanged += UpdateSP;
            targetUnit.OnOverheatChanged += UpdateOverheat;

            targetUnit.TriggerAllEvents();
        }

        private void UnbindEvents()
        {
            if (targetUnit == null) return;

            targetUnit.OnHPChanged -= UpdateHP;
            targetUnit.OnSPChanged -= UpdateSP;
            targetUnit.OnOverheatChanged -= UpdateOverheat;
        }

        public void SetHighlight(bool isActive)
        {
            if (highlightBorder != null) highlightBorder.gameObject.SetActive(isActive);
        }

        private void UpdateHP(int current, int max)
        {
            if (hpFillImage != null) hpFillImage.fillAmount = (float)current / max;
            if (hpText != null) hpText.text = $"{current}/{max}";
        }

        private void UpdateOverheat(int current, int max)
        {
            if (overheatFillImage != null) overheatFillImage.fillAmount = (float)current / max;
        }

        private void UpdateSP(int currentSP, int bankSP, int maxSP)
        {
            for (int i = 0; i < spBlocks.Count; i++)
            {
                if (spBlocks[i] == null) continue;
                spBlocks[i].gameObject.SetActive(true);

                if (i < currentSP) spBlocks[i].color = new Color(0.2f, 0.45f, 0.9f, 1f);
                else if (i < currentSP + bankSP) spBlocks[i].color = new Color(0.0f, 0.85f, 0.9f, 1f);
                else spBlocks[i].color = new Color(0.15f, 0.15f, 0.15f, 0.6f);
            }
        }
    }
}