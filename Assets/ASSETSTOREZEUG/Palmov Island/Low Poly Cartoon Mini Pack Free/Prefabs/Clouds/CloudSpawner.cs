using UnityEngine;

public class CloudSpawner : MonoBehaviour
{
    [Header("Cloud")]
    [SerializeField] private GameObject cloudPrefab;
    [SerializeField] private int cloudCount = 12;

    [Header("Spawn Bereich")]
    [SerializeField] private float minX = -25f;
    [SerializeField] private float maxX = 25f;

    [SerializeField] private float minY = 8f;
    [SerializeField] private float maxY = 15f;

    [SerializeField] private float minZ = 8f;
    [SerializeField] private float maxZ = 18f;

    [Header("Geschwindigkeit")]
    [SerializeField] private float minSpeed = 0.5f;
    [SerializeField] private float maxSpeed = 1.5f;

    [Header("Größe")]
    [SerializeField] private float minScale = 0.7f;
    [SerializeField] private float maxScale = 1.4f;

    [HideInInspector]
    public float despawnX = 30f;

    private void Start()
    {
        SpawnClouds();
    }

    private void SpawnClouds()
    {
        for (int i = 0; i < cloudCount; i++)
        {
            GameObject cloud = Instantiate(cloudPrefab, transform);

            CloudMover mover = cloud.GetComponent<CloudMover>();

            float x = Random.Range(minX, maxX);
            float y = Random.Range(minY, maxY);
            float z = Random.Range(minZ, maxZ);

            cloud.transform.position = new Vector3(x, y, z);

            float scale = Random.Range(minScale, maxScale);
            cloud.transform.localScale = Vector3.one * scale;

            float rotationY = Random.value < 0.5f ? 0f : 180f;

            cloud.transform.rotation = Quaternion.Euler(
                0f,
                rotationY,
                Random.Range(-5f, 5f)
            );

            float speed = Random.Range(minSpeed, maxSpeed);

            mover.Initialize(this, speed);
        }
    }

    public void RecycleCloud(CloudMover cloud)
    {
        float y = Random.Range(minY, maxY);
        float z = Random.Range(minZ, maxZ);

        cloud.transform.position = new Vector3(
            minX,
            y,
            z
        );

        cloud.transform.localScale =
            Vector3.one * Random.Range(minScale, maxScale);

        float rotationY = Random.value < 0.5f ? 0f : 180f;

        cloud.transform.rotation = Quaternion.Euler(
            0f,
            rotationY,
            Random.Range(-5f, 5f)
        );

        cloud.speed = Random.Range(minSpeed, maxSpeed);
    }
}
