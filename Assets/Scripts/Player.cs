using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField] private FingerGun gun;
    [SerializeField] private GameObject HUDPanel;
    [SerializeField] private GameObject MapPanel;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Mouse0) && gun.CurrentStage == Stage.Aiming)
        {
            gun.Shoot();
        }

        if (Input.GetKeyDown(KeyCode.Tab) && gun.CurrentStage != Stage.MapMode)
        {
            gun.CurrentStage = Stage.MapMode;
        }
    }
}
