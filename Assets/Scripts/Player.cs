using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField] private FingerGun gun;
    [SerializeField] private CamMov camMov;
    [SerializeField] private GameObject HUDPanel;
    [SerializeField] private GameObject PausePanel;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
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

    }

    public void OnQuit()
    {
        Application.Quit();
    }

}
