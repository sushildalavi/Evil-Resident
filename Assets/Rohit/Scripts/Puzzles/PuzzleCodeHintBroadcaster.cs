using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class PuzzleCodeHintBroadcaster : MonoBehaviour
{
    [Header("Trigger Zone")]
    [SerializeField] bool playOnlyOnce = true;
    [SerializeField] bool requirePlayerEnterZone = true;
    [SerializeField] bool triggerFromPuzzleFront = true;
    [SerializeField] Vector3 frontDirectionLocal = new Vector3(0f, 0f, -1f);
    [SerializeField] float frontDistance = 2.2f;
    [SerializeField] float frontWidth = 3.8f;
    [SerializeField] float frontDepth = 2.2f;
    [SerializeField] bool requireFacingPuzzle = false;
    [Range(-1f, 1f)] [SerializeField] float minFacingDot = 0.1f;
    [SerializeField] bool autoZoneFromSquareKey = true;
    [SerializeField] bool useHorizontalDistanceOnly = true;
    [SerializeField] Vector3 roomZoneCenter = new Vector3(8.379f, 0.625f, -21.165f);
    [SerializeField] float roomZoneRadius = 4.5f;
    [SerializeField] float initialDelay = 1f;
    [SerializeField] bool ignoreIfPuzzleAlreadySolved = false;

    [Header("Wheel Order")]
    [SerializeField] PuzzleWheel circleWheel;
    [SerializeField] PuzzleWheel squareWheel;
    [SerializeField] PuzzleWheel rectangleWheel;
    [SerializeField] ColorWheelPuzzleManager puzzleManager;

    [Header("Blink Timing")]
    [SerializeField] float quickOnDuration = 0.16f;
    [SerializeField] float quickOffDuration = 0.14f;
    [SerializeField] float slowOnDuration = 0.6f;
    [SerializeField] float slowOffDuration = 0.25f;

    [Header("Debug")]
    [SerializeField] bool verboseLogging = false;
    [SerializeField] bool hasPlayed;

    bool wasInsideTrigger;
    Coroutine sequenceRoutine;
    float nextSquareKeySearchTime;

    void Reset()
    {
        puzzleManager = GetComponent<ColorWheelPuzzleManager>();
        if (circleWheel == null || squareWheel == null || rectangleWheel == null)
        {
            PuzzleWheel[] found = GetComponentsInChildren<PuzzleWheel>(true);
            for (int i = 0; i < found.Length; i++)
            {
                string n = found[i].name.ToLowerInvariant();
                if (circleWheel == null && n.Contains("circle")) circleWheel = found[i];
                else if (squareWheel == null && n.Contains("square")) squareWheel = found[i];
                else if (rectangleWheel == null && n.Contains("rect")) rectangleWheel = found[i];
            }
        }
    }

    void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        hasPlayed = false;
        sequenceRoutine = null;

        TryRefreshAutoZoneCenter(force: true);
        Transform player = ResolvePlayerTransform();
        wasInsideTrigger = IsInsideTrigger(player);
    }

    void Update()
    {
        TryRefreshAutoZoneCenter(force: false);

        if (playOnlyOnce && hasPlayed)
            return;

        if (sequenceRoutine != null)
            return;

        if (!requirePlayerEnterZone)
        {
            StartHintSequenceIfAllowed();
            return;
        }

        Transform player = ResolvePlayerTransform();
        if (player == null)
            return;

        bool inside = IsInsideTrigger(player);
        if (inside && !wasInsideTrigger)
            StartHintSequenceIfAllowed();

        wasInsideTrigger = inside;
    }

    void StartHintSequenceIfAllowed()
    {
        if (sequenceRoutine != null)
            return;

        if (ignoreIfPuzzleAlreadySolved && puzzleManager != null && puzzleManager.IsSolved)
        {
            if (verboseLogging)
                Debug.Log("[ColorWheelPuzzle] Hint sequence skipped because puzzle is already solved.", this);
            hasPlayed = true;
            return;
        }

        sequenceRoutine = StartCoroutine(HintSequenceRoutine());
    }

    IEnumerator HintSequenceRoutine()
    {
        float delay = Mathf.Max(0f, initialDelay);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (verboseLogging)
            Debug.Log("[ColorWheelPuzzle] Playing code hint sequence: Circle x2, Square x1, Rectangle x1 (slow).", this);

        if (circleWheel != null)
            yield return circleWheel.PlayHintBlinks(2, Mathf.Max(0.01f, quickOnDuration), Mathf.Max(0f, quickOffDuration));

        if (squareWheel != null)
            yield return squareWheel.PlayHintBlinks(1, Mathf.Max(0.01f, quickOnDuration), Mathf.Max(0f, quickOffDuration));

        if (rectangleWheel != null)
            yield return rectangleWheel.PlayHintBlinks(1, Mathf.Max(0.01f, slowOnDuration), Mathf.Max(0f, slowOffDuration));

        hasPlayed = true;
        sequenceRoutine = null;
    }

    Transform ResolvePlayerTransform()
    {
        if (PlayerInventory.instance != null)
            return PlayerInventory.instance.transform;

        RohitFPSController controller = FindFirstObjectByType<RohitFPSController>();
        if (controller != null)
            return controller.transform;

        return null;
    }

    bool IsInsideTrigger(Transform player)
    {
        if (player == null)
            return false;

        if (triggerFromPuzzleFront)
            return IsInsidePuzzleFrontZone(player);

        return IsInsideRoomZone(player.position);
    }

    bool IsInsideRoomZone(Vector3 worldPosition)
    {
        float radius = Mathf.Max(0.1f, roomZoneRadius);
        Vector3 delta = worldPosition - roomZoneCenter;
        if (useHorizontalDistanceOnly)
            delta.y = 0f;
        return delta.sqrMagnitude <= radius * radius;
    }

    bool IsInsidePuzzleFrontZone(Transform player)
    {
        Vector3 dirLocal = frontDirectionLocal.sqrMagnitude > 0.0001f
            ? frontDirectionLocal.normalized
            : Vector3.back;

        Vector3 frontDir = transform.TransformDirection(dirLocal);
        Vector3 center = transform.position + frontDir * Mathf.Max(0f, frontDistance);
        Vector3 toPlayer = player.position - center;

        if (useHorizontalDistanceOnly)
            toPlayer.y = 0f;

        Vector3 right = transform.right;
        Vector3 depthAxis = frontDir;
        if (useHorizontalDistanceOnly)
        {
            right.y = 0f;
            depthAxis.y = 0f;
            right = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
            depthAxis = depthAxis.sqrMagnitude > 0.0001f ? depthAxis.normalized : Vector3.forward;
        }

        float halfWidth = Mathf.Max(0.1f, frontWidth) * 0.5f;
        float halfDepth = Mathf.Max(0.1f, frontDepth) * 0.5f;
        float lateral = Vector3.Dot(toPlayer, right);
        float depth = Vector3.Dot(toPlayer, depthAxis);

        bool inRect = Mathf.Abs(lateral) <= halfWidth && Mathf.Abs(depth) <= halfDepth;
        if (!inRect)
            return false;

        if (!requireFacingPuzzle)
            return true;

        Vector3 playerForward = player.forward;
        Vector3 toPuzzle = transform.position - player.position;
        if (useHorizontalDistanceOnly)
        {
            playerForward.y = 0f;
            toPuzzle.y = 0f;
        }

        if (playerForward.sqrMagnitude <= 0.0001f || toPuzzle.sqrMagnitude <= 0.0001f)
            return true;

        float dot = Vector3.Dot(playerForward.normalized, toPuzzle.normalized);
        return dot >= minFacingDot;
    }

    void TryRefreshAutoZoneCenter(bool force)
    {
        if (!autoZoneFromSquareKey || triggerFromPuzzleFront)
            return;

        if (!force && Time.time < nextSquareKeySearchTime)
            return;

        nextSquareKeySearchTime = Time.time + 1f;

        KeyItem[] keys = FindObjectsByType<KeyItem>(FindObjectsSortMode.None);
        if (keys == null || keys.Length == 0)
            return;

        for (int i = 0; i < keys.Length; i++)
        {
            KeyItem key = keys[i];
            if (key == null) continue;
            if (key.keyType != KeyType.Silver) continue;

            roomZoneCenter = key.transform.position;
            return;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.25f, 0.8f, 0.9f, 0.35f);
        if (triggerFromPuzzleFront)
        {
            Vector3 dirLocal = frontDirectionLocal.sqrMagnitude > 0.0001f
                ? frontDirectionLocal.normalized
                : Vector3.back;
            Vector3 frontDir = transform.TransformDirection(dirLocal);
            Vector3 center = transform.position + frontDir * Mathf.Max(0f, frontDistance);
            Gizmos.DrawSphere(center, 0.12f);
            Gizmos.DrawWireCube(
                center,
                new Vector3(Mathf.Max(0.1f, frontWidth), 0.2f, Mathf.Max(0.1f, frontDepth))
            );
        }
        else
        {
            Gizmos.DrawSphere(roomZoneCenter, 0.12f);
            Gizmos.DrawWireSphere(roomZoneCenter, Mathf.Max(0.1f, roomZoneRadius));
        }
    }
#endif
}
