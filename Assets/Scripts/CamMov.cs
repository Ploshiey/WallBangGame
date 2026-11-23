using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class CamMov : MonoBehaviour
{
    public Camera mainCam;
    public float maxClamp = 26.0f;
    public float speed;
    Vector3 rotation;
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    // Update is called once per frame
    void Update()
    {
        rotation.y += Input.GetAxis("Mouse X");
        rotation.y = Mathf.Clamp(rotation.y, -90, 90);
        rotation.x += -Input.GetAxis("Mouse Y");
        rotation.x = Mathf.Clamp(rotation.x, -90, 90);
        Debug.Log(rotation);
        mainCam.transform.eulerAngles = (Vector2)rotation * speed;
    }
}
