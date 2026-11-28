using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KingdomsClone.UI
{
    public class BuildingHoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Action<RectTransform> OnEnter;
        public Action OnExit;

        public void OnPointerEnter(PointerEventData eventData)
        {
            OnEnter?.Invoke(GetComponent<RectTransform>());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            OnExit?.Invoke();
        }
    }
}