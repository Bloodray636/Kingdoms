using UnityEngine;

namespace KingdomsClone.Grid
{
    /// <summary>
    /// Единая точка правды для расчёта footprint с учётом поворота.
    /// БЛЯДЬ, ИСПОЛЬЗУЙ ЭТО ВЕЗДЕ, ГДЕ СЧИТАЕШЬ КЛЕТКИ.
    /// </summary>
    public static class FootprintUtil
    {
        /// <summary>
        /// Возвращает размер footprint после поворота.
        /// 0°/180° — как есть. 90°/270° — меняем X и Y местами.
        /// </summary>
        public static Vector2Int GetRotatedSize(Vector2Int size, Quaternion rotation)
        {
            // Берём угол по Y и округляем до ближайших 90°
            float yaw = Mathf.Round(rotation.eulerAngles.y / 90f) * 90f;
            yaw = Mathf.Repeat(yaw, 360f); // нормализация в 0..360

            if (Mathf.Approximately(yaw, 90f) || Mathf.Approximately(yaw, 270f))
                return new Vector2Int(size.y, size.x);

            return size;
        }

        /// <summary>
        /// Все клетки, которые занимает здание с учётом поворота. Anchor = левый нижний угол.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<Vector2Int> GetOccupiedCells(
            Vector2Int anchor, Vector2Int size, Quaternion rotation)
        {
            Vector2Int rotated = GetRotatedSize(size, rotation);
            for (int x = 0; x < rotated.x; x++)
                for (int y = 0; y < rotated.y; y++)
                    yield return new Vector2Int(anchor.x + x, anchor.y + y);
        }
    }
}