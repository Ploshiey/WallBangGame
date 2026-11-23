using UnityEngine;

public class CamMov : MonoBehaviour
{
    public FingerGun gun;
    public Camera mainCam;
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
            MapLook();
        }
        else if (gun.CurrentStage == Stage.BulletSelection)
        {

        }
    }

    public void ChangeToAim()
    {
        gun.CurrentStage = Stage.Aiming;
        mainCam.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
    }

    void PlayerLook()
    {
        rotation.y += Input.GetAxis("Mouse X");
        rotation.y = Mathf.Clamp(rotation.y, -90, 90);
        rotation.x += -Input.GetAxis("Mouse Y");
        rotation.x = Mathf.Clamp(rotation.x, -90, 90);
        mainCam.transform.eulerAngles = (Vector2)rotation * speed;
    }

    public void ChangeToMap()
    {
        gun.CurrentStage = Stage.MapMode;
        distance = 10;
        mainCam.transform.position = focalPoint.position - mainCam.transform.forward * distance;
    }

    void MapLook()
    {
        mouseX = Input.GetAxis("Mouse X");
        mouseY = Input.GetAxis("Mouse Y");

        mainCam.transform.LookAt(focalPoint);
        mainCam.transform.eulerAngles += new Vector3(-mouseY * speed, mouseX * speed, 0);

        distance -= Input.GetAxis("Mouse ScrollWheel") / speed;
        distance = Mathf.Clamp(distance, 0, 20);

        mainCam.transform.position = focalPoint.position - mainCam.transform.forward * distance;
        
    }
}
