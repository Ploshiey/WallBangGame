using UnityEngine.SceneManagement;
using UnityEngine;
using TMPro;

public class Player : MonoBehaviour
{
    [SerializeField] private FingerGun gun;
    [SerializeField] private CamMov camMov;
    [SerializeField] private GameObject HUDPanel;
    [SerializeField] private GameObject BulletSelectionPanel;
    [SerializeField] private GameObject PausePanel;
    [SerializeField] private TextMeshProUGUI scoreText;

    public int score;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        scoreText.text = score.ToString();
        if (score >= 300 && gun.activeBullets == 0)
        {
            SceneManager.LoadScene(2);
        }

        if (score <= 300 && gun.Shotsleft == 0 && gun.activeBullets == 0)
        {
            SceneManager.LoadScene(3);
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            PausePanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
        }

        if (Input.GetKeyDown(KeyCode.Mouse0) && gun.CurrentStage == Stage.Aiming)
        {
            gun.Shoot();
        }
    }



    public void OnResume()
    {
        PausePanel.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void OnMainMenu()
    {
        SceneManager.LoadScene(0);
    }

    public void OnSettings()
    {

    }

    public void OnQuit()
    {
        Application.Quit();
    }

}
