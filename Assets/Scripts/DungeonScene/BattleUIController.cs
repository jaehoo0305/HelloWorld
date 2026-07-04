using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DungeonCombat.Data;

namespace DungeonCombat.Combat
{
    /// <summary>
    /// Canvas의 BattleTurnManager 및 BattleUnit의 실시간 이벤트를 구독하여 UI를 갱신하는 컨트롤러입니다.
    /// </summary>
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
        [SerializeField] private TextMeshProUGUI tooltipDescriptionText;

        [Header("[ 6. 패시브 툴팁 UI (배경 & 텍스트 세트) ]")]
        [SerializeField] private GameObject passiveTooltipPanel;
        [SerializeField] private TextMeshProUGUI passiveTooltipText;

        private BattleUnit currentPassiveTooltipUnit;

        private void Start()
        {
            if (turnManager == null)
            {
                turnManager = FindFirstObjectByType<BattleTurnManager>();
            }

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
        }

        private void UpdateRoundUI(int round)
        {
            if (roundText != null)
            {
                roundText.text = $"ROUND {round}";
            }
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

            foreach (Transform child in turnListParent)
            {
                Destroy(child.gameObject);
            }

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

                    if (slot.Unit.IsBoss && slot.ActionIndex != 99)
                    {
                        slotText.text = $"{characterName} ({slot.ActionIndex + 1})";
                    }
                    else if (slot.ActionIndex == 99)
                    {
                        slotText.text = $"{characterName} (추가행동)";
                    }
                    else
                    {
                        slotText.text = characterName;
                    }
                }

                Image bgImage = slotObj.GetComponent<Image>();
                if (bgImage != null)
                {
                    if (slot.Unit.IsBoss)
                    {
                        bgImage.color = new Color(0.85f, 0.35f, 0.35f, 1f);
                    }
                    else
                    {
                        bgImage.color = new Color(0.25f, 0.45f, 0.75f, 1f);
                    }
                }
            }
        }

        private void UpdateSkillButtons(BattleUnit unit)
        {
            if (unit == null) return;

            // 보스나 몬스터의 차례에는 스킬 조작창 숨기기
            if (unit.IsBoss)
            {
                SetSkillPanelActive(false);
                return;
            }

            // 플레이어 캐릭터 데이터 타입으로 하향 캐스팅
            PlayerUnit player = unit as PlayerUnit;
            if (player == null || player.CharacterData == null)
            {
                SetSkillPanelActive(false);
                return;
            }

            SetSkillPanelActive(true);

            if (tooltipPanel != null)
            {
                tooltipPanel.SetActive(false);
            }

            ConfigureSkillButton(0, player.EquippedUniqueSkill, player.EquippedUniqueSkillLevel);

            for (int i = 1; i < skillButtons.Count; i++)
            {
                int normalSkillIndex = i - 1;
                if (normalSkillIndex < player.CharacterData.LearnableSkills.Count)
                {
                    SkillDataSO normalSkill = player.CharacterData.LearnableSkills[normalSkillIndex];
                    ConfigureSkillButton(i, normalSkill, 1);
                }
                else
                {
                    if (i < skillButtons.Count && skillButtons[i] != null)
                    {
                        skillButtons[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        private void ConfigureSkillButton(int buttonIndex, SkillDataSO skill, int currentLevel)
        {
            if (buttonIndex >= skillButtons.Count || skill == null) return;

            Button btn = skillButtons[buttonIndex];
            TextMeshProUGUI btnText = skillButtonTexts[buttonIndex];

            if (btn == null || btnText == null) return;

            btn.gameObject.SetActive(true);
            btnText.text = $"{skill.SkillName}\n(Lv.{currentLevel})";

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                OnSkillButtonClicked(skill, currentLevel);
            });
        }

        private void SetSkillPanelActive(bool isActive)
        {
            if (skillPanelParent != null)
            {
                skillPanelParent.SetActive(isActive);
            }
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
            if (tooltipDescriptionText != null)
            {
                tooltipDescriptionText.text = skill.GetFormattedDescription(level);
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

        private void HandleBattleEnd()
        {
            SetSkillPanelActive(false);

            if (tooltipPanel != null)
            {
                tooltipPanel.SetActive(false);
            }

            if (passiveTooltipPanel != null)
            {
                passiveTooltipPanel.SetActive(false);
            }

            Debug.Log("[UI] 전투가 끝났으므로 모든 컨트롤을 정지합니다.");
        }
    }
}