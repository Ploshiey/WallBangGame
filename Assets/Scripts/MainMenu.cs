using UnityEngine.SceneManagement;
using UnityEngine;

public class MainMenu : MonoBehaviour
{
    public void OnStartGame()
    {
        SceneManager.LoadScene("Main Level");
    }

    public void OnLevelSelect()
    {

    }

    public void OnSettings()
    {

    }

    public void OnQuitGame()
    {
        Application.Quit();
    }
}
