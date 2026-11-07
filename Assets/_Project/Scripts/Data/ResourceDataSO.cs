using UnityEngine;

namespace KingdomsClone.Data
{
    [CreateAssetMenu(
        menuName = "KingdomsClone/Resource",
        fileName = "Resource_New",
        order = 1)]
    public class ResourceDataSO : DataSO
    {
        [Header("Classification")]
        [SerializeField] private ResourceCategory category = ResourceCategory.Material;

        [Header("UI")]
        [SerializeField] private Color uiColor = Color.white;
        [SerializeField, Min(1)] private int maxStack = 100;

        public ResourceCategory Category => category;
        public Color UiColor => uiColor;
        public int MaxStack => maxStack;
    }
}