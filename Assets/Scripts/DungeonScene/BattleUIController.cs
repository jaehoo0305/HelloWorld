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

        // 비주얼 라운드 및 턴 전환 배너 연출 중에는 입력을 통제하기 위한 게이트 제어 플래그 프로퍼티
        public bool IsVisualTransitionActive { get; set; } = false;

        // Z-Skill 토글 상태 공유 스태틱 프로퍼티
        public static bool IsZSkillModeActive { get; private set; } = false;

        private BattleUnit currentPassiveTooltipUnit;

        // --- [기획 변경 사항] 실시간 클릭된 고유 스킬 캐시 장치 ---
        private SkillDataSO currentlyPreviewedSkill;

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

            // 우클릭 등으로 조준 철회 시 프리뷰 캐시도 완전 동기화 청소 처리
            if (SkillCaster.Instance != null)
            {
                SkillCaster.Instance.OnTargetingModeEnded += () =>
                {
                    currentlyPreviewedSkill = null;
                };
            }
        }

        private void Update()
        {
            bool qPressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
            {
                qPressed = true;
            }
#else
            if (Input.GetKeyDown(KeyCode.Q))
            {
                qPressed = true;
            }
#endif

            if (qPressed)
            {
                IsZSkillModeActive = !IsZSkillModeActive;
                Debug.Log($"[Z스킬 모드] {(IsZSkillModeActive ? "<color=gold>활성화</color>" : "비활성화")}");

                // 모드 스왑 시 프리뷰 및 타겟 조작 중이던 캐시 전면 초기화
                if (SkillCaster.Instance != null)
                {
                    SkillCaster.Instance.CancelTargetingMode();
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
            if (roundText != null)
            {
                roundText.text = $"ROUND {round}";
            }

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
                    string displayName = characterName;

                    if (slot.Unit.IsBoss && slot.ActionIndex != 99)
                    {
                        displayName = $"{characterName} ({slot.ActionIndex + 1})";
                    }
                    else if (slot.ActionIndex == 99)
                    {
                        displayName = $"{characterName} (추가행동)";
                    }

                    slotText.text = $"<color=#FFFFFF>{displayName}</color>";
                }

                Image bgImage = slotObj.GetComponent<Image>();
                if (bgImage != null)
                {
                    if (slot.Unit is EnemyUnit)
                    {
                        if (slot.Unit.IsBoss)
                        {
                            bgImage.color = new Color(0.85f, 0.25f, 0.25f, 1f);
                        }
                        else
                        {
                            bgImage.color = new Color(0.75f, 0.35f, 0.35f, 1f);
                        }
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
            if (unit == null) return;

            if (unit.IsBoss || unit is EnemyUnit)
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

            SetSkillPanelActive(!IsVisualTransitionActive);
        }

        private void ConfigureSkillButton(int buttonIndex, SkillDataSO skill, int currentLevel)
        {
            if (buttonIndex >= skillButtons.Count || skill == null) return;

            Button btn = skillButtons[buttonIndex];
            TextMeshProUGUI btnText = skillButtonTexts[buttonIndex];

            if (btn == null || btnText == null) return;

            btn.gameObject.SetActive(true);

            // Z스킬 모드 시 버튼 매핑 분기 처리
            if (IsZSkillModeActive && skill.EnhancedSkillAsset != null && buttonIndex == 0)
            {
                SkillDataSO enhanced = skill.EnhancedSkillAsset;
                btnText.text = $"<color=#FFD700>★ {enhanced.SkillName} ★\n(Lv.{currentLevel})</color>";

                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    OnSkillButtonClicked(enhanced, currentLevel);
                });
            }
            else
            {
                btnText.text = $"{skill.SkillName}\n(Lv.{currentLevel})";

                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    OnSkillButtonClicked(skill, currentLevel);
                });
            }
        }

        public void SetSkillPanelActive(bool isActive)
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

        /// <summary>
        /// 스킬 단추를 누를 때 호출되는 기획형 2단계 토글 조작 판정식입니다.
        /// </summary>
        private void OnSkillButtonClicked(SkillDataSO skill, int level)
        {
            if (tooltipPanel != null) tooltipPanel.SetActive(true);
            if (tooltipDescriptionText != null)
            {
                tooltipDescriptionText.text = skill.GetFormattedDescription(level);
            }

            PlayerUnit caster = turnManager.CurrentTurnUnit as PlayerUnit;
            if (caster == null) return;

            // --- 2단계 스킬 시전 흐름 제어 ---
            if (currentlyPreviewedSkill == skill)
            {
                // [2타째 클릭]: 타겟팅 모드로 격상! (마우스 호버 가이드 활성화 및 클릭 시 즉시 스킬 실행)
                if (SkillCaster.Instance != null)
                {
                    SkillCaster.Instance.SelectSkill(caster, skill, level);
                }
                currentlyPreviewedSkill = null; // 타겟팅 모드 진입했으므로 프리뷰 캐시 클리어
            }
            else
            {
                // [1타째 클릭]: 프리뷰 모드 작동! (설명문 띄우고, 격자에 사거리를 은은한 하늘색으로만 표시)
                currentlyPreviewedSkill = skill;
                if (SkillCaster.Instance != null)
                {
                    SkillCaster.Instance.PreviewSkill(caster, skill, level);
                }
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
            if (tooltipPanel != null)
            {
                tooltipPanel.SetActive(false);
            }

            if (passiveTooltipPanel != null)
            {
                passiveTooltipPanel.SetActive(false);
            }

            currentPassiveTooltipUnit = null;
            currentlyPreviewedSkill = null; // 모든 툴팁 소멸 시 프리뷰 캐시도 완전 청소
        }

        private void HandleBattleEnd()
        {
            SetSkillPanelActive(false);
            HideAllTooltips();
        }
    }
}