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

    public enum BulletEffect
    {
    Ricochet,
    Pierce,
    Splitter,
    Explosion,
    StickyNade,
    None
    }

    public struct ShotContext
    {
    public BulletEffect[] Effects;
    public int Index;
    public Vector3 Point;
    public Vector3 Normal;
    public Vector3 Direction;
    public Collider HitCollider;

    public bool HasNext;
    }

public class FingerGun : MonoBehaviour
{
    [SerializeField] private Camera PlayerCamera;
    private RaycastHit hit;

    public Stage CurrentStage = Stage.BulletSelection;
    //Loadout
    private BulletEffect[]BulletSelection = new BulletEffect[3] {BulletEffect.None,BulletEffect.None,BulletEffect.None };
    private const int MaxSlots = 3; 
    //Ray
    [SerializeField] private LayerMask Hitmask = Physics.DefaultRaycastLayers;

    //Splitter
    [SerializeField] private int SplitCount = 3;
    [SerializeField] private float SplitSpreadAngle = 30f;

    //Explosion
    [SerializeField] private float ExplosionRadius = 3f;
    [SerializeField] private float ExplosionForce = 3f;
    [SerializeField] private GameObject ExplosionVfx;

    //stickyNade
    [SerializeField] private float stickyFuseTime = 1.5f;
    [SerializeField] private GameObject StickyPrefab;

    //bullet visuals
    [SerializeField] private GameObject BulletVisualPrefab;//the projectile ellacoco makes
    [SerializeField] private float BulletSpeed = 40f;


    private int Shotsleft = 2;
    private bool shot = false;

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

                    //reset bullets
                    for (int i = 0; i < BulletSelection.Length; i++)
                    {
                        BulletSelection[i] = 0;
                    }
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
        //play inspect arm animation

        //only has to check last one as it fills up in order, i think
        if (BulletSelection[3] == BulletEffect.None)
        {
            if (Input.GetKeyDown(KeyCode.Keypad1))
            {
                for (int i = 0; i < BulletSelection.Length; i++)
                {
                    if (BulletSelection[i] == BulletEffect.None)
                    {
                        BulletSelection[i] = BulletEffect.Ricochet;
                        break;
                    }
                }
            }
            else if (Input.GetKeyDown(KeyCode.Keypad2))
            {
                for (int i = 0; i < BulletSelection.Length; i++)
                {
                    if (BulletSelection[i] == BulletEffect.None)
                    {
                        BulletSelection[i] = BulletEffect.Pierce;
                        break;
                    }
                }
            }
            else if (Input.GetKeyDown(KeyCode.Keypad3))
            {
                for (int i = 0; i < BulletSelection.Length; i++)
                {
                    if (BulletSelection[i] == BulletEffect.None)
                    {
                        BulletSelection[i] = BulletEffect.Splitter;
                        break;
                    }
                }
            }
            else if (Input.GetKeyDown(KeyCode.Keypad4))
            {
                for (int i = 0; i < BulletSelection.Length; i++)
                {
                    if (BulletSelection[i] == BulletEffect.None)
                    {
                        BulletSelection[i] = BulletEffect.Explosion;
                        break;
                    }
                }
            }
            else if (Input.GetKeyDown(KeyCode.Keypad5))
            {
                for (int i = 0; i < BulletSelection.Length; i++)
                {
                    if (BulletSelection[i] == BulletEffect.None)
                    {
                        BulletSelection[i] = BulletEffect.StickyNade;
                        break;
                    }
                }
            }
        }
        else
        {
            //say slot full or something
        }

        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            for (int i = BulletSelection.Length; i != 0; i--)
            {
                if (BulletSelection[i] == BulletEffect.None)
                {
                    continue;
                }
                BulletSelection[i] = BulletEffect.None;
                break;
            }
        }
    }

    public void Shoot()
    {
        if (Shotsleft <= 0)
        {
            //play win or gameover depending on score before this activates e.g. this should never activate just a safeguard
            return;
        }
        Shotsleft--;
        shot = true;

        FireRay(BulletSelection, 0, PlayerCamera.transform.position, PlayerCamera.transform.forward, null);

        //if (Physics.Raycast(PlayerCamera.transform.position, PlayerCamera.transform.forward, out RaycastHit hit))
        //{
        //    Debug.DrawLine(PlayerCamera.transform.position, hit.point, Color.red, 15);
        //    if (hit.transform.gameObject.CompareTag("Wall"))
        //    {
        //        Debug.Log("This is wall");
        //        // get what bullet effects applied on bullet selection
        //        //run through functions that run their effects

        //    }else
        //    {
        //        Debug.Log(hit);
        //    }
        //}
    }

    //fires a ray and runs effect in slot or chain ends if there isnt one
    void FireRay(BulletEffect[] effects, int slot, Vector3 origin, Vector3 direction, Collider ignore)
    {
        StartCoroutine(TravelRay(effects, slot, origin, direction, ignore));
    }

    IEnumerator TravelRay (BulletEffect[] effects, int slot, Vector3 origin, Vector3 direction, Collider ignore)
    {
        //make sure it is forward vector
        direction.Normalize();

        bool didHit = CastRay(origin, direction, ignore, out RaycastHit hit);
        Vector3 end = didHit ? hit.point : origin + direction * 1000;

        Debug.DrawLine(origin, end, Color.red, 15);

        if (BulletSpeed > 0f)
        {
            GameObject bullet = BulletVisualPrefab != null ? Instantiate(BulletVisualPrefab, origin, Quaternion.LookRotation(direction)) : null;

            float travelTime = Vector3.Distance(origin, end) / BulletSpeed;

            for (float t = 0f; t < travelTime; t += Time.deltaTime)
            {
                if (bullet != null)
                {
                    bullet.transform.position = Vector3.Lerp(origin, end, t / travelTime);
                    yield return null;
                }
            }

            if (bullet != null)
            {
                DespawnBullet(bullet, end);
            }
        }

        //flew off into nothingness
        if (!didHit) yield break;
        //what it was flying at got destroyed mid flight
        if (hit.collider == null) yield break;


        ShotContext ctx = new ShotContext
        {
            Effects = effects,
            Index = slot,
            Point = hit.point,
            Normal = hit.normal,
            Direction = direction,
            HitCollider = hit.collider
        };

        RunEffect(ctx);
    }

    void RunEffect (ShotContext ctx)
    {
        //checks if theres bullets left 
        if (ctx.Index >= ctx.Effects.Length)
        {
            return;
        }

        switch (ctx.Effects[ctx.Index])
        {
            case BulletEffect.Ricochet: Ricochet(ctx); break;
            case BulletEffect.Pierce: Pierce(ctx); break;
            case BulletEffect.Splitter: Splitter(ctx); break;
            case BulletEffect.Explosion: Explosion(ctx); break;
            case BulletEffect.StickyNade: StickyNade(ctx); break;
        }
    }
    void Ricochet (ShotContext ctx)
    {
        //ricochet logic on wall hit
        //
    }

    void Pierce (ShotContext ctx)
    {
        
    }

    void Splitter(ShotContext ctx)
    {
        
    }

    void Explosion (ShotContext ctx)
    {
        
    }

    void StickyNade(ShotContext ctx)
    {
        
    }

    bool CastRay (Vector3 origin, Vector3 direction,  bool ignore, out RaycastHit result)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, 1000, Hitmask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance)); //sorts is since Raycastall isnt sorted and it needs to be sorted so it checks in order

        foreach (RaycastHit h in hits )
        {
            if (h.collider != ignore)
            {
                result = h;
                return true;
            }
        }

        result = default;
        return false;
    }

    void DespawnBullet (GameObject bullet, Vector3 end)
    {
        bullet.transform.position = end;

        Destroy(bullet);
    }
}
