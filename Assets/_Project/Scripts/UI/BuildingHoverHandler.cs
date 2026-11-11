using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KingdomsClone.UI
{
    /// <summary>
    /// Ловит наведение мыши на UI-элемент и вызывает коллбеки.
    /// Нужен для тултипов (Unity Button не даёт OnHover из коробки).
    /// </summary>
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