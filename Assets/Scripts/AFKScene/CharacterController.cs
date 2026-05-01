using UnityEngine;
using UnityEngine.InputSystem;

namespace DefaultNamespace
{

    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Collider2D))]
    public class CharacterController : MonoBehaviour
    {

        [SerializeField] private Sprite _undraggingState;
        [SerializeField] private Sprite _draggingState;

        private bool _isDragging = false;
        private SpriteRenderer _renderer = null;
        private Camera _mainCamera;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _mainCamera = Camera.main;

            if (_undraggingState != null) _renderer.sprite = _undraggingState;
        }

        private void Update()
        {
            HandleInput();

            if (_isDragging)
            {
                DragCharacter();
            }
        }

        private void HandleInput()
        {
            // New Input System을 사용하여 마우스 왼쪽 버튼 상태를 직접 체크
            bool leftDown = Mouse.current.leftButton.wasPressedThisFrame;
            bool leftUp = Mouse.current.leftButton.wasReleasedThisFrame;

            Vector2 mousePos = Mouse.current.position.ReadValue();

            // 카메라와의 거리를 고려하여 월드 좌표 계산 (Z축 보정)
            Vector3 worldPos = _mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, -_mainCamera.transform.position.z));
            worldPos.z = 0;

            // 클릭 시작 시점 (캐릭터의 콜라이더 위에 마우스가 있을 때)
            if (leftDown && Physics2D.OverlapPoint(worldPos) == gameObject.GetComponent<Collider2D>())
            {
                StartDragging();
            }

            // 클릭 해제 시점
            if (leftUp && _isDragging)
            {
                StopDragging();
            }
        }

        private void StartDragging()
        {
            _isDragging = true;
            if (_draggingState != null) _renderer.sprite = _draggingState;
        }

        private void StopDragging()
        {
            _isDragging = false;
            if (_undraggingState != null) _renderer.sprite = _undraggingState;
        }

        private void DragCharacter()
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();

            // 카메라 좌표를 월드 좌표로 변환
            Vector3 targetPos = _mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, -_mainCamera.transform.position.z));
            targetPos.z = 0;

            // 위치만 업데이트 (좌우 반전 로직 제거됨)
            transform.position = targetPos;
        }
    }
}