using UnityEngine;

namespace KingdomsClone.Grid
{
    public static class FootprintUtil
    {
        public static Vector2Int GetRotatedSize(Vector2Int size, Quaternion rotation)
        {
            float yaw = Mathf.Round(rotation.eulerAngles.y / 90f) * 90f;
            yaw = Mathf.Repeat(yaw, 360f);

            if (Mathf.Approximately(yaw, 90f) || Mathf.Approximately(yaw, 270f)) {
                return new Vector2Int(size.y, size.x);
            }

            return size;
        }

        public static System.Collections.Generic.IEnumerable<Vector2Int> GetOccupiedCells(
            Vector2Int anchor, Vector2Int size, Quaternion rotation)
        {
            Vector2Int rotated = GetRotatedSize(size, rotation);

            for (int x = 0; x < rotated.x; x++) {
                for (int y = 0; y < rotated.y; y++) {
                    yield return new Vector2Int(anchor.x + x, anchor.y + y);
                }
            }
        }
    }
}