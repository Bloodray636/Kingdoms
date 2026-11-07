using UnityEngine;

namespace KingdomsClone.Core.DebugTools
{
    public class GameStateDebugControls : MonoBehaviour
    {
        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (Input.GetKeyDown(KeyCode.F1)) gm.StartGame();
            if (Input.GetKeyDown(KeyCode.F2)) gm.PauseGame();
            if (Input.GetKeyDown(KeyCode.F3)) gm.ResumeGame();
            if (Input.GetKeyDown(KeyCode.F4)) gm.QuitToMenu();
        }

        private void OnGUI()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 90), GUI.skin.box);
            GUILayout.Label($"State: {gm.CurrentState}");
            GUILayout.Label("F1=Start  F2=Pause  F3=Resume  F4=Menu");
            GUILayout.EndArea();
        }
    }
}