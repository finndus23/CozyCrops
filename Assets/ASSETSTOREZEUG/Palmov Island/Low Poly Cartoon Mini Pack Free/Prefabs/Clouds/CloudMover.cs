using UnityEngine;

public class CloudMover : MonoBehaviour
{
    [HideInInspector]
    public float speed;

    private CloudSpawner spawner;

    public void Initialize(CloudSpawner cloudSpawner, float cloudSpeed)
    {
        spawner = cloudSpawner;
        speed = cloudSpeed;
    }

    private void Update()
    {
        transform.position += Vector3.right * speed * Time.deltaTime;

        if (transform.position.x > spawner.despawnX)
        {
            spawner.RecycleCloud(this);
        }
    }
}