using UnityEngine;

public class MovingWall : MonoBehaviour
{
    [SerializeField] private Transform start;
    [SerializeField] private Transform end;
    [SerializeField] private GameObject wall;
    [SerializeField] private float wait = 5.0f;
    [SerializeField] private float speed = 5.0f;
    float currWait;
    bool toEnd;
    // Start is called before the first frame update
    void Start()
    {
        wall.transform.position = start.position;
    }

    // Update is called once per frame
    void Update()
    {
        if (toEnd)
        {
            if (Vector3.Distance(wall.transform.position, end.position) < speed * Time.deltaTime)
            {
                if (currWait >= wait)
                {
                    toEnd = false;
                    currWait = 0;
                }
                else
                {
                    currWait += Time.deltaTime;
                }
            }
            else
            {
                wall.transform.position = Vector3.MoveTowards(wall.transform.position, end.position, Time.deltaTime * speed);
            }
        }
        else
        {
            if (Vector3.Distance(wall.transform.position, start.position) < speed * Time.deltaTime)
            {
                if (currWait >= wait)
                {
                    toEnd = true;
                    currWait = 0;
                }
                else
                {
                    currWait += Time.deltaTime;
                }
            }
            else
            {
                wall.transform.position = Vector3.MoveTowards(wall.transform.position, start.position, Time.deltaTime * speed);
            }
        }
    }
}
