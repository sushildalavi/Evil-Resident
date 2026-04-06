using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialDoorTransition : MonoBehaviour
{
    [Tooltip("Name of the main game scene to load when the door opens.")]
    public string mainGameSceneName = "NewLevel";

    [Tooltip("Delay in seconds after the door opens before loading the scene.")]
    public float transitionDelay = 1.5f;

    Door door;
    bool triggered;
    float timer;

    void Start()
    {
        door = GetComponent<Door>();
    }

    void Update()
    {
        if (triggered)
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
                SceneManager.LoadScene(mainGameSceneName);
            return;
        }

        if (door != null && door.IsOpen)
        {
            triggered = true;
            timer = transitionDelay;
        }
    }
}
