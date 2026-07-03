using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // TextMeshPro 컴포넌트 사용을 위한 네임스페이스 추가
using DungeonCombat.Data;

namespace DungeonCombat.Combat
{
    /// <summary>
    /// 기획안(image_e9baab.png)의 레이아웃을 기반으로 하며,
    /// Canvas의 BattleTurnManager 및 BattleUnit의 실시간 이벤트를 구독하여 UI를 갱신하는 컨트롤러입니다.
    /// TextMeshPro와의 완벽한 호환성을 위해 모든 텍스트 컴포넌트가 TextMeshProUGUI로 교체되었습니다.
    /// </summary>
    public class BattleUIController : MonoBehaviour
    {
        [Header("[ 핵심 매니저 참조 ]")]
        [SerializeField] private BattleTurnManager turnManager;

        [Header("[ 1. 라운드 관련 UI ]")]
        [SerializeField] private TextMeshProUGUI roundText; // 이제 TextMeshPro 컴포넌트를 드래그로 장착할 수 있습니다!

        [Header("[ 2. 턴 표시 UI (타임라인 리스트) ]")]
        [SerializeField] private RectTransform turnListParent; // 우측 상단 턴 표시 컨테이너 (TurnOrder 오브젝트)
        [SerializeField] private GameObject turnSlotPrefab;    // 캐릭터 초상화 또는 이름이 들어갈 프리팹 (TurnOrderPrefabs 에셋)

        [Header("[ 3. 아군 파티 캐릭터 UI 슬롯들 ]")]
        [SerializeField] private List<PartyUnitUISlot> partyUISlots; // 좌측 하단 캐릭터 3명의 HP/SP 슬롯 (순서대로 매칭)

        [Header("[ 4. 캐릭터 스킬 UI 제어 패널 ]")]
        [SerializeField] private GameObject skillPanelParent; // 적의 턴일 때 숨길 최상위 스킬 부모 패널 (Skills 오브젝트 권장)
        [SerializeField] private List<Button> skillButtons; // 우측 하단 스킬 버튼 4개 (고유1, 고유2, 일반1, 일반2 등)
        [SerializeField] private List<TextMeshProUGUI> skillButtonTexts; // 버튼 내 자식 텍스트 컴포넌트 리스트 (TMP)

        [Header("[ 5. 스킬 툴팁 UI (배경 & 텍스트 세트) ]")]
        [Tooltip("기획안 중앙의 '각 스킬 툴팁' 최상위 배경 이미지 오브젝트(ToolTip)를 연결하세요.")]
        [SerializeField] private GameObject tooltipPanel; // 툴팁 배경 게임오브젝트 (ToolTip 본체)

        [Tooltip("ToolTip 오브젝트의 자식에 있는 Text (TMP) 컴포넌트를 연결하세요.")]
        [SerializeField] private TextMeshProUGUI tooltipDescriptionText; // 툴팁 내 자식 텍스트 컴포넌트 (TMP)

        private void Start()
        {
            if (turnManager == null)
            {
                turnManager = FindFirstObjectByType<BattleTurnManager>();
            }

            // 캐릭터 UI 슬롯들의 초기화(이벤트 구독)를 직접 실행합니다.
            foreach (var slot in partyUISlots)
            {
                slot.Initialize();
            }

            // 라운드 텍스트 초기화 설정 (게임 시작 시 기본 표시용)
            if (roundText != null && turnManager != null)
            {
                roundText.text = $"ROUND {Mathf.Max(1, turnManager.CurrentRound)}";
            }

            // [툴팁 배경 & 텍스트 세트 초기화] 시작 시에는 툴팁 상자를 보이지 않게 가립니다.
            if (tooltipPanel != null)
            {
                tooltipPanel.SetActive(false);
            }
            if (tooltipDescriptionText != null)
            {
                tooltipDescriptionText.text = string.Empty;
            }

            // Canvas의 BattleTurnManager 전투 진행 이벤트 리스너 등록
            if (turnManager != null)
            {
                turnManager.OnRoundStarted += UpdateRoundUI;
                turnManager.OnTurnStarted += UpdateActiveUnitUI;
                turnManager.OnBattleEnded += HandleBattleEnd;
            }
        }

        /// <summary>
        /// 새로운 라운드가 개시될 때 상단 라운드 텍스트를 갱신합니다.
        /// </summary>
        private void UpdateRoundUI(int round)
        {
            if (roundText != null)
            {
                roundText.text = $"ROUND {round}";
            }

            // 기획안 우측 상단의 턴 리스트 타임라인 빌드
            RebuildTurnTimelineUI();
        }

        /// <summary>
        /// 특정 유닛의 턴이 돌아왔을 때, 해당 유닛의 스킬 정보와 선택 연출을 실시간 갱신합니다.
        /// </summary>
        private void UpdateActiveUnitUI(BattleUnit activeUnit)
        {
            // 1. 좌측 하단 캐릭터 UI 중 현재 턴을 가진 캐릭터에게 강조 표시(아웃라인 등) 적용
            for (int i = 0; i < partyUISlots.Count; i++)
            {
                if (partyUISlots[i].TargetUnit == activeUnit)
                {
                    partyUISlots[i].SetHighlight(true);
                }
                else
                {
                    partyUISlots[i].SetHighlight(false);
                }
            }

            // 2. 우측 하단의 스킬 슬롯 UI 갱신 (현재 활성화된 유닛의 스킬 데이터 매핑)
            UpdateSkillButtons(activeUnit);

            // 3. 턴이 진행될 때마다 타임라인 흐름을 실시간으로 다시 그립니다.
            RebuildTurnTimelineUI();
        }

        /// <summary>
        /// 턴 순서 큐를 읽어와 우측 상단 턴 대기열 타임라인 UI를 동적으로 생성합니다.
        /// 아르세우스 방식의 중복 턴, 피아 식별 색상 구분 연동 포함.
        /// </summary>
        private void RebuildTurnTimelineUI()
        {
            if (turnListParent == null || turnSlotPrefab == null || turnManager == null) return;

            // 1. 기존에 배치되어 있는 모든 턴 슬롯 아이콘을 완전히 파괴하여 지웁니다.
            foreach (Transform child in turnListParent)
            {
                Destroy(child.gameObject);
            }

            var queue = turnManager.TurnQueue;
            int currentIndex = turnManager.CurrentQueueIndex;

            if (queue == null || currentIndex < 0) return;

            // 2. 현재 실행 중인 턴 인덱스부터 남은 턴 큐 전체를 순회하며 슬롯을 실시간 생성합니다.
            for (int i = currentIndex; i < queue.Count; i++)
            {
                TurnSlot slot = queue[i];
                if (slot.Unit == null || slot.Unit.CurrentHP <= 0) continue;

                // 프리팹을 TurnListParent(TurnOrder)의 자식으로 소환
                GameObject slotObj = Instantiate(turnSlotPrefab, turnListParent);

                // 3. 프리팹 하위의 텍스트 컴포넌트를 찾아 캐릭터 이름과 정보를 기입합니다.
                TextMeshProUGUI slotText = slotObj.GetComponentInChildren<TextMeshProUGUI>();
                if (slotText != null)
                {
                    string characterName = slot.Unit.CharacterData.CharacterName;

                    // 만약 보스 다중 행동일 경우 "(1), (2)" 처럼 표시해 줍니다.
                    if (slot.Unit.CharacterData.PositionType == PositionType.Boss && slot.ActionIndex != 99)
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

                // 4. 피아 식별 아르세우스 스타일 연출 적용 (아군은 청색 계열, 적은 적색 계열 배경)
                Image bgImage = slotObj.GetComponent<Image>();
                if (bgImage != null)
                {
                    if (slot.Unit.CharacterData.PositionType == PositionType.Boss)
                    {
                        // 몬스터/보스: 붉은색 톤 배경 적용
                        bgImage.color = new Color(0.85f, 0.35f, 0.35f, 1f);
                    }
                    else
                    {
                        // 플레이어 아군: 푸른색/청록색 톤 배경 적용
                        bgImage.color = new Color(0.25f, 0.45f, 0.75f, 1f);
                    }
                }
            }
        }

        /// <summary>
        /// 현재 행동권을 잡은 아군 유닛의 고유 스킬 및 일반 스킬을 우측 하단 버튼 레이아웃에 꽂아줍니다.
        /// </summary>
        private void UpdateSkillButtons(BattleUnit unit)
        {
            if (unit == null || unit.CharacterData == null) return;

            // 몬스터(적/보스)의 턴일 경우에는 아군 플레이어가 조작할 수 없으므로 전체 패널(뒷배경 포함)을 보이지 않게 처리합니다.
            if (unit.CharacterData.PositionType == PositionType.Boss)
            {
                SetSkillPanelActive(false);
                return;
            }

            // 플레이어 아군의 턴인 경우 뒷배경 이미지와 버튼 패널을 활성화합니다.
            SetSkillPanelActive(true);

            // [중요] 새로운 아군 턴이 개시되어 아직 스킬 버튼을 누르기 전이므로, 툴팁 상자 세트를 깔끔하게 화면에서 숨겨줍니다.
            if (tooltipPanel != null)
            {
                tooltipPanel.SetActive(false);
            }

            // 1. 고유 스킬 1 매핑 (예: 중력 응축)
            ConfigureSkillButton(0, unit.CharacterData.UniqueSkill1, unit.Skill1Level);

            // 2. 고유 스킬 2 매핑 (예: 궤도 섭동)
            ConfigureSkillButton(1, unit.CharacterData.UniqueSkill2, unit.Skill2Level);

            // 3. 습득한 일반 스킬 리스트 매핑
            for (int i = 2; i < skillButtons.Count; i++)
            {
                int learnableIndex = i - 2;
                if (learnableIndex < unit.CharacterData.LearnableSkills.Count)
                {
                    SkillDataSO normalSkill = unit.CharacterData.LearnableSkills[learnableIndex];
                    ConfigureSkillButton(i, normalSkill, 1); // 일반 스킬 레벨 적용
                }
                else
                {
                    // 배울 수 있는 스킬 슬롯이 비어있다면 해당 버튼만 숨김 처리
                    if (i < skillButtons.Count && skillButtons[i] != null)
                    {
                        skillButtons[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        /// <summary>
        /// 특정 번호의 스킬 버튼에 스킬 정보와 클릭 이벤트를 바인딩합니다.
        /// </summary>
        private void ConfigureSkillButton(int buttonIndex, SkillDataSO skill, int currentLevel)
        {
            if (buttonIndex >= skillButtons.Count || skill == null) return;

            Button btn = skillButtons[buttonIndex];
            TextMeshProUGUI btnText = skillButtonTexts[buttonIndex];

            if (btn == null || btnText == null) return;

            btn.gameObject.SetActive(true);

            // 포맷팅 완료된 설명과 레벨 표시
            btnText.text = $"{skill.SkillName}\n(Lv.{currentLevel})";

            // 기존 클릭 이벤트 리스너 제거 후 신규 연동
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                OnSkillButtonClicked(skill, currentLevel);
            });
        }

        /// <summary>
        /// 스킬 패널 전체(뒷배경 이미지 포함)의 활성화 여부를 결정합니다.
        /// </summary>
        private void SetSkillPanelActive(bool isActive)
        {
            if (skillPanelParent != null)
            {
                skillPanelParent.SetActive(isActive);
            }
            else
            {
                // 백업용 예외 처리
                foreach (var btn in skillButtons)
                {
                    if (btn != null) btn.gameObject.SetActive(isActive);
                }
            }
        }

        /// <summary>
        /// 플레이어가 스킬 버튼을 눌렀을 때 실행되는 트리거 함수입니다.
        /// 버튼이 클릭되면 툴팁 상자(배경)를 활성화하고 그 자식 텍스트 정보를 실시간으로 채워줍니다.
        /// </summary>
        private void OnSkillButtonClicked(SkillDataSO skill, int level)
        {
            Debug.Log($"[UI] 스킬 클릭됨: '{skill.SkillName}' (Lv.{level})");

            // [실시간 툴팁 매핑 및 팝업 연동 구현]
            // 스킬을 클릭하는 순간 숨겨져 있던 뒷배경 툴팁 상자가 나타납니다.
            if (tooltipPanel != null)
            {
                tooltipPanel.SetActive(true);
            }

            if (tooltipDescriptionText != null)
            {
                // 중괄호 {dmg:250} 등을 순수한 숫자 가공 결과로 자동 치환해서 툴팁 상자에 예쁘게 그려줍니다.
                tooltipDescriptionText.text = skill.GetFormattedDescription(level);
            }
        }

        private void HandleBattleEnd()
        {
            SetSkillPanelActive(false);

            // 전투가 종료되면 스킬 툴팁 상자도 화면에서 함께 정리합니다.
            if (tooltipPanel != null)
            {
                tooltipPanel.SetActive(false);
            }

            Debug.Log("[UI] 전투가 끝났으므로 모든 컨트롤을 정지합니다.");
        }
    }

    /// <summary>
    /// 좌측 하단 캐릭터 UI 슬롯 낱개를 조종하는 서브 클래스입니다.
    /// (TextMeshProUGUI 연동을 지원하도록 완전 수정되었습니다.)
    /// </summary>
    [System.Serializable]
    public class PartyUnitUISlot
    {
        [SerializeField] private BattleUnit targetUnit;
        [SerializeField] private Image highlightBorder; // 현재 턴임을 강조하는 이펙트 이미지

        [SerializeField] private Image hpFillImage;       // 세로형 체력 바 게이지 이미지 (Fill Amount 사용)
        [SerializeField] private TextMeshProUGUI hpText;  // 이제 TextMeshProUGUI를 드래그로 장착할 수 있습니다!
        [SerializeField] private Image overheatFillImage; // 가로형 과열 바 게이지 이미지 (Fill Amount 사용)
        [SerializeField] private List<Image> spBlocks;    // 기획안의 SPBar 하위 SP(1) ~ SP(10) 이미지 오브젝트 리스트

        public BattleUnit TargetUnit => targetUnit;

        public void Initialize()
        {
            if (targetUnit == null) return;

            BindEvents();
        }

        /// <summary>
        /// [고급 확장] 전투 중에 모험 캐릭터 선택이 바뀌거나 동적으로 매칭을 갈아끼우기 위한 아키텍처 지원 함수
        /// </summary>
        public void BindUnit(BattleUnit newUnit)
        {
            UnbindEvents();
            targetUnit = newUnit;
            BindEvents();
        }

        private void BindEvents()
        {
            if (targetUnit == null) return;

            targetUnit.OnHPChanged += UpdateHP;
            targetUnit.OnSPChanged += UpdateSP;
            targetUnit.OnOverheatChanged += UpdateOverheat;

            targetUnit.TriggerAllEvents(); // 초기 동기화
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
                highlightBorder.enabled = isActive;
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
            // 기획안 좌측 하단의 세분화된 SP 네모칸(spBlocks)을 갯수만큼 켜고 끄는 로직
            for (int i = 0; i < spBlocks.Count; i++)
            {
                if (spBlocks[i] == null) continue;

                // 오브젝트를 끄지 않고 항상 활성화 상태로 두어 10칸의 세그먼트 형태를 시각적으로 유지합니다.
                spBlocks[i].gameObject.SetActive(true);

                if (i < currentSP)
                {
                    // 보유 기본 SP: 파란색 활성화
                    spBlocks[i].color = new Color(0.2f, 0.45f, 0.9f, 1f);
                }
                else if (i < currentSP + bankSP)
                {
                    // 이월 은행 SP: 청록색 활성화
                    spBlocks[i].color = new Color(0.0f, 0.85f, 0.9f, 1f);
                }
                else
                {
                    // 미보유 빈 칸: 투명도가 있는 아주 어두운 회색으로 채워 경계 격자선을 이쁘게 유지합니다.
                    spBlocks[i].color = new Color(0.15f, 0.15f, 0.15f, 0.6f);
                }
            }
        }
    }
}