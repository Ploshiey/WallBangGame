using UnityEngine;

public class CamMov : MonoBehaviour
{
    public FingerGun gun;
    public Camera mainCam;
    public Transform AimPos;
    public Transform BulletSelectPos;
    public Transform MapPos;
    public float maxClamp = 26.0f;
    public float speed;
    Vector3 rotation;
    float distance;
    public Transform focalPoint;
    float mouseX;
    float mouseY;
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        ChangeToBulletSelect();
        
    }

    // Update is called once per frame
    void Update()
    {
        if (gun.CurrentStage == Stage.Aiming)
        {
            PlayerLook();
        }
        else if (gun.CurrentStage == Stage.MapMode)
        {
            //MapLook();
        }
        else if (gun.CurrentStage == Stage.BulletSelection)
        {

        }
    }

    public void ChangeToAim()
    {
        gun.CurrentStage = Stage.Aiming;
        mainCam.transform.SetPositionAndRotation(AimPos.position, AimPos.rotation);
    }

    void PlayerLook()
    {
        rotation.y += Input.GetAxis("Mouse X");
        rotation.y = Mathf.Clamp(rotation.y, -90, 90);
        mainCam.transform.eulerAngles = (Vector2)rotation * speed;
    }

    public void ChangeToMap()
    {
        gun.CurrentStage = Stage.MapMode;
        mainCam.transform.SetPositionAndRotation(MapPos.position, MapPos.rotation);
        mainCam.orthographic = true;
        mainCam.orthographicSize = 25;
    }

    void MapLook()
    {
        mouseX = Input.GetAxis("Mouse X");
        mouseY = Input.GetAxis("Mouse Y");

        mainCam.transform.LookAt(focalPoint);
        mainCam.transform.eulerAngles += new Vector3(-mouseY * speed, mouseX * speed, 0);

        distance -= Input.GetAxis("Mouse ScrollWheel") / (speed * 0.2f);
        distance = Mathf.Clamp(distance, 0, 60);

        mainCam.transform.position = focalPoint.position - mainCam.transform.forward * distance;
        
    }

    public void ChangeToBulletSelect()
    {
        mainCam.transform.SetPositionAndRotation(BulletSelectPos.position, BulletSelectPos.rotation);
        mainCam.orthographic = false;
    }
}
