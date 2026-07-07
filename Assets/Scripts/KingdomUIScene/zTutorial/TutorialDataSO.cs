using System.Collections.Generic;
using UnityEngine;

namespace Kingdom.Tutorial
{
    /// <summary>
    /// 정적 기획 데이터를 보관하는 ScriptableObject입니다.
    /// 일방적인 가이드 대사와 발동 조건을 관리합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewTutorialData", menuName = "Kingdom/Tutorial/Tutorial Data")]
    public class TutorialDataSO : ScriptableObject
    {
        [Header("기본 정보")]
        [Tooltip("튜토리얼 고유 식별 ID")]
        public string tutorialID;

        [Tooltip("이 튜토리얼을 작동시킬 외부 트리거 키 (예: UNLOCKED_BARRACKS)")]
        public string triggerKey;

        [Header("화자 정보")]
        [Tooltip("화자의 이름 (예: Dragon)")]
        public string speakerName;

        [Tooltip("화자의 초상화 이미지 (선택 사항)")]
        public Sprite speakerPortrait;

        [Header("내용 정보")]
        [TextArea(3, 5)]
        [Tooltip("순차적으로 출력할 가이드 텍스트 목록 (Rich Text 태그 지원)")]
        public List<string> guideTexts;

        [Header("추가 이벤트")]
        [Tooltip("완료 시 실행하고 싶은 특수 시스템 코드 (예: UNLOCK_STORE)")]
        public string eventTriggerCode;
    }
}