using UnityEngine;

/// <summary>
/// Spawns the rugby pitch prefab when the pitch scene loads. Assign the prefab on the component in the RugbyPitch scene.
/// </summary>
public class PitchSetup : MonoBehaviour
{
    [SerializeField] GameObject rugbyPitchPrefab;
    [SerializeField] string homeSceneName = "HomePage";

    public string HomeSceneName => homeSceneName;

    void Start()
    {
        if (rugbyPitchPrefab == null)
        {
            Debug.LogError($"{nameof(PitchSetup)}: assign Rugby Pitch Prefab on {gameObject.name}.", this);
            return;
        }

        Instantiate(rugbyPitchPrefab, transform);
    }
}
