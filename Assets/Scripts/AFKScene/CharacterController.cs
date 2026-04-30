using System;
using UnityEngine;

namespace DefaultNamespace {
    
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Collider2D))]
    public class CharacterController: MonoBehaviour {

        [SerializeField] private Sprite _undraggingState;
        [SerializeField] private Sprite _draggingState;
        
        private bool _isDragging = false;
        private SpriteRenderer _renderer = null;
        
       //==================================================||Unity 

       private void OnMouseDown() {
           
           _isDragging = true;
           _renderer.sprite = _draggingState;
       }

       private void OnMouseUp() {
           
           _isDragging = false;
           _renderer.sprite = _undraggingState;
       }

       private void Awake() {
           
           _renderer ??= GetComponent<SpriteRenderer>();
            Debug.Log("SuccesslyStart");
       }

       private void Update() {
           if (!_isDragging)
               return;
           
           var pos = Camera.main!.ScreenToWorldPoint(Input.mousePosition);
           pos.z = 0;
           
           transform.position = pos;
       }
    }
}