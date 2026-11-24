using System.Collections;
using System.Collections.Generic;
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
//everything one bullet has to carry
public class BulletState
{
    public BulletEffect[] Effects;
    public int NextSlot;
    public int WallHits;
    public int LastWall;
    public bool Exploding;

    //stops once out of walls and every effect has shown up once
    public bool IsFinished => WallHits >= LastWall && NextSlot >= Effects.Length;
    public BulletState Clone() => (BulletState)MemberwiseClone();
}

//what happens at one wall
public struct WallPlan
{
    public Collider Wall;
    public Vector3 InDir;
    public Vector3 Point;
    public Vector3 Normal;
    public Vector3 OutDir;
    public bool Split;
    public bool stick;
}

public class FingerGun : MonoBehaviour
{
    [SerializeField] private Camera PlayerCamera;
    [SerializeField] private Transform Muzzle; //spawn the visible bullet at fingertips
    public CamMov camMov;

    public Stage CurrentStage = Stage.BulletSelection;
    //Loadout
    private BulletEffect[] BulletSelection = new BulletEffect[3] { BulletEffect.None, BulletEffect.None, BulletEffect.None };
    private const int MaxSlots = 3;
    //Ray
    [SerializeField] private LayerMask Hitmask = Physics.DefaultRaycastLayers;

    //Splitter
    [SerializeField] private int SplitCount = 3;
    [SerializeField] private float SplitSpreadAngle = 40f;

    //Explosion
    [SerializeField] private float ExplosionRadius = 3f;
    [SerializeField] private float ExplosionForce = 3f;
    [SerializeField] private GameObject ExplosionVfx;

    //stickyNade
    [SerializeField] private float stickyFuseTime = 1.5f;
    [SerializeField] private int ConeCount = 5;
    [SerializeField] private float ConeSpreadAngle = 45f;
    [SerializeField] private GameObject StickyPrefab;

    //Ricochet
    [SerializeField] private int RicochetBounces = 3;

    //Pierce
    [SerializeField] private float MaxWallThickness = 3f; //must be thicker than our thickest wall otherwise wont go through

    //bullet visuals
    [SerializeField] private GameObject BulletVisualPrefab;//the projectile ellacoco makes
    [SerializeField] private float BulletSpeed = 40f;
    [SerializeField] private string EnemyTag = "Enemy";

    private const float Skin = 0.01f; //nudge bullet of surfaces so it dosent start inside


    private int Shotsleft = 2;
    private bool shot = false;
    private int activeBullets = 0;
    private readonly List<GameObject> shotLeftovers = new List<GameObject>(); // trails + nades from the last shot

    public bool ShotInProgress => activeBullets > 0;

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
                    camMov.ChangeToAim();
                }
                BulletSelect();
                break;

            case Stage.Aiming:
                //changing enum
                if (shot)
                {
                    CurrentStage = Stage.MapMode;
                    camMov.ChangeToMap();

                    //reset bullets 
                    for (int i = 0; i < BulletSelection.Length; i++)
                    {
                        BulletSelection[i] = BulletEffect.None;
                    }
                }
                break;

            case Stage.MapMode:
                //changing enum
                if (Input.GetKeyDown(KeyCode.Tab))
                {
                    ClearLastShot(); // readying the next shot wipes the old trails
                    CurrentStage = Stage.BulletSelection;
                    camMov.ChangeToBulletSelect();
                }
                break;
        }
    }

    void BulletSelect()
    {
        //play inspect arm animation

        //only has to check last one as it fills up in order, i think
        if (BulletSelection[MaxSlots - 1] == BulletEffect.None)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
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
            else if (Input.GetKeyDown(KeyCode.Alpha2))
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
            else if (Input.GetKeyDown(KeyCode.Alpha3))
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
            else if (Input.GetKeyDown(KeyCode.Alpha4))
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
            else if (Input.GetKeyDown(KeyCode.Alpha5))
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
            for (int i = BulletSelection.Length - 1; i >= 0; i--)
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

        Vector3 visualStart = Muzzle != null ? Muzzle.position : PlayerCamera.transform.position;
        List<BulletEffect> picks = new List<BulletEffect>();
        foreach (BulletEffect e in BulletSelection)
        {
            if (e != BulletEffect.None) picks.Add(e);
        }
        BulletState state = new BulletState { Effects = picks.ToArray() };


        StartCoroutine(BulletRoutine(state, PlayerCamera.transform.position, PlayerCamera.transform.forward, null, visualStart));
    }
        //fires a ray and runs effect in slot or chain ends if there isnt one
        IEnumerator BulletRoutine(BulletState s, Vector3 pos, Vector3 dir, Collider ignore, Vector3 visualFrom)
        {
            activeBullets++;
            GameObject visual = SpawnVisual(visualFrom, dir);

            while (true)
            {
                //make sure the dir is being normalized
                dir.Normalize();
                bool didHit = CastRay(pos, dir, ignore, out RaycastHit hit);
                Vector3 end = didHit ? hit.point : pos + dir * 1000;
                Debug.DrawLine(pos, end, Color.red, 15);

                //this flys the visual bullet along the stretch and the actual hit logic happens once it gets there
                if (visual != null) visual.transform.rotation = Quaternion.LookRotation(dir);
                float travelTime = Vector3.Distance(visualFrom, end) / BulletSpeed;
                for (float t = 0f; t < travelTime; t += Time.deltaTime)
                {
                    if (visual != null) visual.transform.position = Vector3.Lerp(visualFrom, end, t / travelTime);
                    yield return null;
                }
                if (visual != null) visual.transform.position = end;

                //the bullet flew off into nothing or the target got destroyed already
                if (!didHit || hit.collider == null) break;

                //hurt it but keep going through e.g. light work no reaction
                if (IsEnemy(hit.collider))
                {
                    HitEnemy(hit.collider, hit.point, dir);
                    pos = hit.point;
                    visualFrom = pos;
                    ignore = hit.collider;
                    continue;
                }

                //efects decide what comes into play
                WallPlan plan = HitWall(s, hit, dir);

                if (plan.stick)
                {
                    GameObject nade = SpawnNade(plan.Point, plan.Normal);
                    yield return new WaitForSeconds(stickyFuseTime);
                    if (nade != null) Destroy(nade);

                    //nade blast explodes then bullets spray out in cone
                    Explode(plan.Point);
                    foreach (Vector3 d in Fan(plan.Normal, plan.Normal, ConeCount, ConeSpreadAngle))
                    {
                        SpawnBullet(s, plan.Point + plan.Normal * Skin, d);
                    }
                    break;
                }

                if (s.Exploding) Explode(plan.Point);

                if (s.IsFinished) break;

                if (plan.Split)
                {
                    foreach (Vector3 d in Fan(plan.OutDir, plan.Normal, SplitCount, SplitSpreadAngle))
                    {
                        SpawnBullet(s, plan.Point + plan.Normal * Skin, d);
                    }
                    break;
                }

                //carry on it bounce or out the far side if it pierced through
                pos = plan.Point + plan.Normal * Skin;
                visualFrom = pos;
                dir = plan.OutDir;
                ignore = null;
            }

            EndVisual(visual);
            activeBullets--;
        }

        void SpawnBullet(BulletState s, Vector3 pos, Vector3 dir)
        {
            StartCoroutine(BulletRoutine(s.Clone(), pos, dir, null, pos));
        }

        WallPlan HitWall(BulletState s, RaycastHit hit, Vector3 dir)
        {
            s.WallHits++;

            WallPlan plan = new WallPlan
            {
                Wall = hit.collider,
                InDir = dir,
                Point = hit.point,
                Normal = hit.normal,
                OutDir = Vector3.Reflect(dir, hit.normal)// bounce off by default
            };

            foreach (BulletEffect effect in EffectsForThisWall(s))
            {
                switch (effect)
                {
                    case BulletEffect.Ricochet: Ricochet(s, ref plan); break;
                    case BulletEffect.Pierce: Pierce(s, ref plan); break;
                    case BulletEffect.Splitter: Splitter(s, ref plan); break;
                    case BulletEffect.Explosion: Explosion(s, ref plan); break;
                    case BulletEffect.StickyNade: StickyNade(s, ref plan); break;
                }
            }

            return plan;
        }

        List<BulletEffect> EffectsForThisWall(BulletState s)
        {
            List<BulletEffect> KickIn = new List<BulletEffect>();
            if (s.NextSlot >= s.Effects.Length) return KickIn;

            BulletEffect current = s.Effects[s.NextSlot++];
            KickIn.Add(current);

            while (s.NextSlot < s.Effects.Length)
            {
                BulletEffect next = s.Effects[s.NextSlot];

                bool sameWall = (current == BulletEffect.Pierce && next != BulletEffect.Pierce) ||
                (current == BulletEffect.Ricochet && next == BulletEffect.Splitter) ||
                (current == BulletEffect.Splitter && next == BulletEffect.Ricochet);

                if (!sameWall) break;

                KickIn.Add(next);
                s.NextSlot++;
                current = next;
            }

            return KickIn;
        }

        //so bullet routine does the actual splitting, sticking and exploding now
        void Ricochet(BulletState s, ref WallPlan plan)
        {
            //ricochet logic on wall hit
            s.LastWall = Mathf.Max(s.LastWall, s.WallHits + RicochetBounces);
        }

        void Pierce(BulletState s, ref WallPlan plan)
        {
            s.LastWall = Mathf.Max(s.LastWall, s.WallHits + 1);

            if (FindExit(plan.Wall, plan.Point, plan.InDir, out Vector3 exitPoint, out Vector3 exitNormal))
            {
                plan.Point = exitPoint;
                plan.Normal = exitNormal;
                plan.OutDir = plan.InDir;
            }
        }

        void Splitter(BulletState s, ref WallPlan plan)
        {
            s.LastWall = Mathf.Max(s.LastWall, s.WallHits + 1);
            plan.Split = true;
        }

        void Explosion(BulletState s, ref WallPlan plan)
        {
            s.Exploding = true;
            s.LastWall = Mathf.Max(s.LastWall, s.WallHits);

        }

        void StickyNade(BulletState s, ref WallPlan plan)
        {
            s.LastWall = Mathf.Max(s.LastWall, s.WallHits) + 1;
            plan.stick = true;
        }

        bool CastRay(Vector3 origin, Vector3 direction, Collider ignore, out RaycastHit result)
        {
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, 1000, Hitmask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance)); //sorts is since Raycastall isnt sorted and it needs to be sorted so it checks in order

            foreach (RaycastHit h in hits)
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

        bool IsEnemy(Collider col) => col.CompareTag(EnemyTag);

        void HitEnemy(Collider enemy, Vector3 point, Vector3 dir)
        {

        }

        void Explode(Vector3 centre)
        {
            if (ExplosionVfx != null)
            {
                Destroy(Instantiate(ExplosionVfx, centre, Quaternion.identity), 3f);
            }

        }

        bool FindExit(Collider wall, Vector3 entry, Vector3 dir, out Vector3 exitPoint, out Vector3 exitNormal)
        {
            Vector3 inside = entry + dir * Skin;
            float reach = MaxWallThickness;

            // don't look further than whatever is right behind the wall
            if (Physics.Raycast(inside, dir, out RaycastHit behind, reach, Hitmask, QueryTriggerInteraction.Ignore))
            {
                reach = behind.distance;
            }

            // cast back towards the entry - the first thing that ray hits is this wall's back face
            Vector3 from = inside + dir * reach;
            if (reach > 0f && wall.Raycast(new Ray(from, -dir), out RaycastHit back, reach))
            {
                exitPoint = back.point;
                exitNormal = back.normal;
                return true;
            }

            exitPoint = entry;
            exitNormal = -dir;
            return false;
        }

        List<Vector3> Fan(Vector3 centre, Vector3 surfaceNormal, int count, float spread)
        {
            centre.Normalize();
            List<Vector3> dirs = new List<Vector3>(count);
            Vector3 axis = Mathf.Abs(Vector3.Dot(centre, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up;

            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : i / (float)(count - 1);
                Vector3 d = Quaternion.AngleAxis(Mathf.Lerp(-spread, spread, t), axis) * centre;

                if (Vector3.Dot(d, surfaceNormal) < 0f)
                {
                    d = Vector3.Reflect(d, surfaceNormal);
                }
                dirs.Add(d);
            }

            return dirs;
        }

        GameObject SpawnVisual(Vector3 pos, Vector3 dir)
        {
            if (BulletVisualPrefab == null) return null;

            GameObject v = Instantiate(BulletVisualPrefab, pos, Quaternion.LookRotation(dir));
            foreach (TrailRenderer trail in v.GetComponentsInChildren<TrailRenderer>())
            {
                trail.time = 9999f; // trails stay up until the next shot is readied
            }
            shotLeftovers.Add(v);
            return v;
        }

        void EndVisual(GameObject v)
        {
            if (v == null) return;

            foreach (MeshRenderer r in v.GetComponentsInChildren<MeshRenderer>())
            {
                r.enabled = false;
            }
            foreach (ParticleSystem ps in v.GetComponentsInChildren<ParticleSystem>())
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        GameObject SpawnNade(Vector3 point, Vector3 normal)
        {
            GameObject nade;
            if (StickyPrefab != null)
            {
                nade = Instantiate(StickyPrefab);
            }
            else
            {
                nade = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                nade.transform.localScale = Vector3.one * 0.25f;
                DestroyImmediate(nade.GetComponent<Collider>()); // so rays and blasts don't hit the nade itself
            }

            nade.transform.SetPositionAndRotation(point, Quaternion.LookRotation(normal));
            shotLeftovers.Add(nade);
            return nade;
        }

        void ClearLastShot()
        {
            StopAllCoroutines(); // note: stops every coroutine on this script
            activeBullets = 0;

            foreach (GameObject g in shotLeftovers)
            {
                if (g != null) Destroy(g);
            }
            shotLeftovers.Clear();
        }

    
}

