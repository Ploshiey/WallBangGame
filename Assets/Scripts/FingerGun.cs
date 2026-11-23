using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
    public enum Stage
    {
        BulletSelection,
        Aiming,
        MapMode
    }

public class FingerGun : MonoBehaviour
{
    [SerializeField] private Camera PlayerCamera;
    private RaycastHit hit;

    public Stage CurrentStage = Stage.BulletSelection;

    private int[]BulletSelection;

    void Start()
    {
        /*
        if (Physics.Raycast(PlayerCamera.transform.position, PlayerCamera.transform.forward, out RaycastHit hit))
        {
            if (hit.transform.gameObject.CompareTag("Wall"))
            {
                Debug.Log("This is wall");
            }else
            {
                Debug.Log(hit);
            }
        }*/

        
        
    }

    void Update()
    {
        switch (CurrentStage)
        {
            case Stage.BulletSelection:

            break;

            case Stage.Aiming:

            break;

            case Stage.MapMode:

            break;
        }
    }

    void BulletSelect()
    {
        //arm to side anim
                
                //bullet selection
                
                if (Input.GetKeyDown(KeyCode.Keypad1))
                {
                    BulletSelection[BulletSelection.Length] = 1;
                }else if (Input.GetKeyDown(KeyCode.Keypad2))
                {
                    BulletSelection[BulletSelection.Length] = 2;
                }else if (Input.GetKeyDown(KeyCode.Keypad3))
                {
                    BulletSelection[BulletSelection.Length] = 3;    
                }
                
                
    }

    public void Shoot()
    {
        if (Physics.Raycast(PlayerCamera.transform.position, PlayerCamera.transform.forward, out RaycastHit hit))
        {
            Debug.DrawLine(PlayerCamera.transform.position, hit.point, Color.red, 15);
            if (hit.transform.gameObject.CompareTag("Wall"))
            {
                Debug.Log("This is wall");
                // get what bullet effects applied on bullet selection
                //run through functions that run their effects

            }else
            {
                Debug.Log(hit);
            }
        }
    }

    void Ricochet ()
    {
        //ricochet logic on wall hit
        //
    }
}
