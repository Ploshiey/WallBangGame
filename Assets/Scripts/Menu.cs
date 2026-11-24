using UnityEngine.SceneManagement;
using UnityEngine;

public class MainMenu : MonoBehaviour
{
    public void OnStartGame()
    {
        SceneManager.LoadScene(1);
    }

    public void OnMainMenu()
    {
        SceneManager.LoadScene(0);
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
