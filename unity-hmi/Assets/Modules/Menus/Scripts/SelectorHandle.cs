using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Modules.Menus.Scripts
{
    public class SelectorHandle : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IDragHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        public event Action<PointerEventData> OnDragEvent;
        public event Action<PointerEventData> OnBeginDragEvent;
        public event Action<PointerEventData> OnEndDragEvent;
        public event Action<PointerEventData> OnPointerDownEvent;
        public event Action<PointerEventData> OnPointerUpEvent;

        public void OnBeginDrag(PointerEventData eventData) => OnBeginDragEvent?.Invoke(eventData);
        public void OnEndDrag(PointerEventData eventData) => OnEndDragEvent?.Invoke(eventData);
        public void OnDrag(PointerEventData eventData) => OnDragEvent?.Invoke(eventData);
        public void OnPointerDown(PointerEventData eventData) => OnPointerDownEvent?.Invoke(eventData);
        public void OnPointerUp(PointerEventData eventData) => OnPointerUpEvent?.Invoke(eventData);
    }
}