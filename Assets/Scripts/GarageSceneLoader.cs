// ════════════════════════════════════════════════════════════════
// GarageSceneLoader.cs – Saves the game, then loads the Garage scene.
// Attach to any GameObject in Main and wire the onClick of
// Btn_Garage to call LoadGarageScene().
// ════════════════════════════════════════════════════════════════
using UnityEngine;
using UnityEngine.SceneManagement;

public class GarageSceneLoader : MonoBehaviour
{
    public void LoadGarageScene()
    {
        if (SaveSystem.Instance != null)
            SaveSystem.Instance.SaveGame();

        SceneManager.LoadScene("NewGarage");
    }
}
