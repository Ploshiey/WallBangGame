using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
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

    private int[]BulletSelection = new int [5] {0,0,0,0,0};

    private int Shotsleft = 2;
    private bool shot = false;

    void Start()
    {    
    }

    void Update()
    {
        switch (CurrentStage)
        {
            case Stage.BulletSelection:
            shot = false;
            //changing enum
            if (Input.GetKeyDown(KeyCode.Return))
                {
                    CurrentStage = Stage.Aiming;
                }
                BulletSelect();
            break;

            case Stage.Aiming:
            //changing enum
            if (shot)
                {
                    CurrentStage = Stage.MapMode;
                }
            break;

            case Stage.MapMode:
            //changing enum
                if (Input.GetKeyDown(KeyCode.Tab))
                {
                    CurrentStage = Stage.BulletSelection;
                }
            break;
        }
    }

    void BulletSelect()
    {
        //arm to side anim
                
                //bullet selection
                
                if (Input.GetKeyDown(KeyCode.Keypad1))
                {
                    for (int i = 0; i < BulletSelection.Length; i++)
                    {
                        BulletSelection[i] = 1;
                    }
                }else if (Input.GetKeyDown(KeyCode.Keypad2))
                {
                    for (int i = 0; i < BulletSelection.Length; i++)
                    {
                        BulletSelection[i] = 2;
                    }
                }else if (Input.GetKeyDown(KeyCode.Keypad3))
                {
                    for (int i = 0; i < BulletSelection.Length; i++)
                    {
                        BulletSelection[i] = 3;
                    }    
                }
                
                
    }

    public void Shoot()
    {
        Shotsleft--;
        shot = true;
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

    void Pierce ()
    {
        
    }

    void Splitter()
    {
        
    }

    void Explosion ()
    {
        
    }

    void StickyNade()
    {
        
    }
}
