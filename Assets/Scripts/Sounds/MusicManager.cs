using UnityEngine;

public class MusicManager : MonoBehaviour
{
    private static MusicManager instance;

    private void Awake()
    {
        // Prüfen ob schon ein MusicManager existiert
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Diesen Manager speichern
        instance = this;

        // Objekt beim Szenenwechsel behalten
        DontDestroyOnLoad(gameObject);
    }
}