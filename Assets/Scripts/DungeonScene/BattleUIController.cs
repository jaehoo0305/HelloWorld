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

        // 비주얼 라운드 및 턴 전환 배너 연출 중에는 입력을 통제하기 위한 게이트 제어 플래그 프로퍼티
        public bool IsVisualTransitionActive { get; set; } = false;

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

            SetSkillPanelActive(false); // 라운드가 전환되는 연출 기간 동안 스킬 패널을 명시적으로 비활성화
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

                    // 순서 큐 플레이트 내부 글씨는 다시 흰색으로 통일하여 가독성 보장
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

            // 보스나 몬스터의 차례에는 스킬 조작창 숨기기
            if (unit.IsBoss || unit is EnemyUnit)
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

            // 비주얼 시네마틱 연출 가드가 풀렸을 때만 스킬창 물리 노출 활성화
            SetSkillPanelActive(!IsVisualTransitionActive);
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

        /// <summary>
        /// 화면 상에 활성화되어 떠 있는 모든 스킬 및 패시브 관련 세부 정보 툴팁창을 강제로 클리어하고 닫습니다.
        /// </summary>
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
        }

        private void HandleBattleEnd()
        {
            SetSkillPanelActive(false);
            HideAllTooltips();
        }
    }

    /// <summary>
    /// 좌측 하단 캐릭터 UI 슬롯 낱개를 조종하는 서브 클래스입니다.
    /// </summary>
    [System.Serializable]
    public class PartyUnitUISlot
    {
        [SerializeField] private BattleUnit targetUnit;
        [SerializeField] private Image highlightBorder;

        [SerializeField] private Image hpFillImage;
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private Image overheatFillImage; // 프로필 이미지(CharacterImage)를 그대로 할당하여 위아래로 차오르게 연출하는 타겟이자 패시브 클릭 타겟
        [SerializeField] private List<Image> spBlocks;

        public BattleUnit TargetUnit => targetUnit;

        public void Initialize(System.Action<BattleUnit> onCharacterClick)
        {
            if (targetUnit == null) return;

            SetHighlight(false);

            // 대기열 및 드래그 슬롯 설정이 필요 없도록 overheatFillImage에 Button 컴포넌트를 런타임에 동적으로 처리 및 연동
            if (overheatFillImage != null)
            {
                Button btn = overheatFillImage.GetComponent<Button>();
                if (btn == null)
                {
                    btn = overheatFillImage.gameObject.AddComponent<Button>();
                }
                // 초상화 과열 게이지 렌더링에 영향을 주지 않도록 트랜지션 효과를 없음(None)으로 정의
                btn.transition = Selectable.Transition.None;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => onCharacterClick?.Invoke(targetUnit));
            }

            BindEvents();
        }

        public void BindUnit(BattleUnit newUnit, System.Action<BattleUnit> onCharacterClick)
        {
            UnbindEvents();
            targetUnit = newUnit;

            if (overheatFillImage != null)
            {
                Button btn = overheatFillImage.GetComponent<Button>();
                if (btn == null)
                {
                    btn = overheatFillImage.gameObject.AddComponent<Button>();
                }
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
            if (highlightBorder != null)
            {
                highlightBorder.gameObject.SetActive(isActive);
            }
        }

        private void UpdateHP(int current, int max)
        {
            if (hpFillImage != null)
            {
                hpFillImage.fillAmount = (float)current / max;
            }
            if (hpText != null)
            {
                hpText.text = $"{current}/{max}";
            }
        }

        private void UpdateOverheat(int current, int max)
        {
            if (overheatFillImage != null)
            {
                overheatFillImage.fillAmount = (float)current / max;
            }
        }

        private void UpdateSP(int currentSP, int bankSP, int maxSP)
        {
            for (int i = 0; i < spBlocks.Count; i++)
            {
                if (spBlocks[i] == null) continue;

                spBlocks[i].gameObject.SetActive(true);

                if (i < currentSP)
                {
                    spBlocks[i].color = new Color(0.2f, 0.45f, 0.9f, 1f);
                }
                else if (i < currentSP + bankSP)
                {
                    spBlocks[i].color = new Color(0.0f, 0.85f, 0.9f, 1f);
                }
                else
                {
                    spBlocks[i].color = new Color(0.15f, 0.15f, 0.15f, 0.6f);
                }
            }
        }
    }
}