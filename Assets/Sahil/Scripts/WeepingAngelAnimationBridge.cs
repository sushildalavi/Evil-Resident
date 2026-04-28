using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[DisallowMultipleComponent]
public class WeepingAngelAnimationBridge : MonoBehaviour
{
    [Header("References")]
    public Transform animatedRoot;
    public Animator animator;
    public Transform idleVisualRoot;
    public Transform walkVisualRoot;
    public Transform alternateWalkVisualRoot;

    [Header("Clips")]
    public AnimationClip idleClip;
    public AnimationClip walkClip;

    [Header("Blend")]
    [Min(0f)] public float blendSpeed = 8f;
    public bool startInIdle = true;
    [Min(0.05f)] public float poseSwapInterval = 0.14f;

    private PlayableGraph graph;
    private AnimationMixerPlayable mixer;
    private AnimationClipPlayable idlePlayable;
    private AnimationClipPlayable walkPlayable;
    private bool graphReady;
    private bool moving;
    private float walkWeight;
    private float idleSampleTime;
    private float walkSampleTime;
    private bool useAlternateWalkPose;
    private float nextPoseSwapTime;
    private Animator idleAnimator;
    private Animator walkAnimator;
    private PlayableGraph idleGraph;
    private PlayableGraph walkGraph;
    private bool idleGraphReady;
    private bool walkGraphReady;

    private void Awake()
    {
        ResolveAnimator();
        TryBuildGraph();
        walkWeight = startInIdle ? 0f : 1f;
        ApplyWeightsImmediate();
        ApplyVisualState();
    }

    private void OnEnable()
    {
        if (graphReady && graph.IsValid() && !graph.IsPlaying())
            graph.Play();
    }

    private void OnDisable()
    {
        SetMoving(false);
    }

    private void OnDestroy()
    {
        if (graph.IsValid())
            graph.Destroy();
        if (idleGraph.IsValid())
            idleGraph.Destroy();
        if (walkGraph.IsValid())
            walkGraph.Destroy();
    }

    public void SetMoving(bool shouldMove)
    {
        bool changed = moving != shouldMove;
        moving = shouldMove;
        if (changed && !moving)
        {
            useAlternateWalkPose = false;
            nextPoseSwapTime = 0f;
        }
        ApplyVisualState();
    }

    private void Update()
    {
        if (UseDualAnimatorMode())
        {
            UpdateDualAnimatorMode();
            return;
        }

        if (UsePoseSwapFallback())
        {
            UpdatePoseSwapFallback();
            return;
        }

        if (UseDualVisualSampling())
        {
            UpdateDualVisualSampling();
            return;
        }

        if (!graphReady)
        {
            ResolveAnimator();
            TryBuildGraph();
            if (!graphReady)
                return;
        }

        float target = moving ? 1f : 0f;
        float step = Mathf.Max(0f, blendSpeed) * Time.deltaTime;
        walkWeight = Mathf.MoveTowards(walkWeight, target, step);
        ApplyWeightsImmediate();
        LoopClipIfNeeded(idlePlayable, idleClip);
        LoopClipIfNeeded(walkPlayable, walkClip);
        ApplyVisualState();
    }

    private void ResolveAnimator()
    {
        if (animatedRoot == null && transform.childCount > 0)
            animatedRoot = transform.GetChild(0);

        if (animator == null && animatedRoot != null)
            animator = animatedRoot.GetComponent<Animator>();

        if (animator == null && animatedRoot != null)
            animator = animatedRoot.gameObject.AddComponent<Animator>();

        if (idleVisualRoot == null)
            idleVisualRoot = animatedRoot;
    }

    private bool UseDualAnimatorMode()
    {
        return idleVisualRoot != null && walkVisualRoot != null && idleClip != null && walkClip != null;
    }

    private void UpdateDualAnimatorMode()
    {
        EnsureDualAnimatorGraph(idleVisualRoot, idleClip, ref idleAnimator, ref idleGraph, ref idleGraphReady, "Idle");
        EnsureDualAnimatorGraph(walkVisualRoot, walkClip, ref walkAnimator, ref walkGraph, ref walkGraphReady, "Walk");
        ApplyVisualState();
    }

    private void EnsureDualAnimatorGraph(
        Transform root,
        AnimationClip clip,
        ref Animator targetAnimator,
        ref PlayableGraph targetGraph,
        ref bool ready,
        string label)
    {
        if (root == null || clip == null)
            return;

        if (targetAnimator == null)
            targetAnimator = root.GetComponent<Animator>();
        if (targetAnimator == null)
            targetAnimator = root.gameObject.AddComponent<Animator>();

        if (ready)
            return;

        targetGraph = PlayableGraph.Create($"{name}_{label}");
        targetGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(targetGraph, clip);
        clipPlayable.SetApplyFootIK(false);
        clipPlayable.SetApplyPlayableIK(false);
        clipPlayable.SetTime(0d);
        clipPlayable.SetDuration(double.MaxValue);

        var output = AnimationPlayableOutput.Create(targetGraph, $"{label}Output", targetAnimator);
        output.SetSourcePlayable(clipPlayable);
        targetGraph.Play();
        ready = true;
    }

    private void TryBuildGraph()
    {
        if (graphReady || animator == null || idleClip == null || walkClip == null)
            return;

        graph = PlayableGraph.Create($"{name}_AngelAnimGraph");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        mixer = AnimationMixerPlayable.Create(graph, 2, true);
        idlePlayable = AnimationClipPlayable.Create(graph, idleClip);
        walkPlayable = AnimationClipPlayable.Create(graph, walkClip);
        idlePlayable.SetApplyFootIK(false);
        walkPlayable.SetApplyFootIK(false);
        idlePlayable.SetApplyPlayableIK(false);
        walkPlayable.SetApplyPlayableIK(false);

        graph.Connect(idlePlayable, 0, mixer, 0);
        graph.Connect(walkPlayable, 0, mixer, 1);

        var output = AnimationPlayableOutput.Create(graph, "Animation", animator);
        output.SetSourcePlayable(mixer);
        graph.Play();
        graphReady = true;
    }

    private void ApplyWeightsImmediate()
    {
        if (!graphReady)
            return;

        float clampedWalk = Mathf.Clamp01(walkWeight);
        mixer.SetInputWeight(0, 1f - clampedWalk);
        mixer.SetInputWeight(1, clampedWalk);
    }

    private static void LoopClipIfNeeded(AnimationClipPlayable playable, AnimationClip clip)
    {
        if (!playable.IsValid() || clip == null || clip.length <= 0.0001f)
            return;

        double t = playable.GetTime();
        double len = clip.length;
        if (t >= len)
            playable.SetTime(t % len);
    }

    private void ApplyVisualState()
    {
        if (idleVisualRoot == null || walkVisualRoot == null)
            return;

        GameObject idleObj = idleVisualRoot.gameObject;
        GameObject walkObj = walkVisualRoot.gameObject;
        GameObject altObj = alternateWalkVisualRoot != null ? alternateWalkVisualRoot.gameObject : null;

        if (!moving)
        {
            if (!idleObj.activeSelf) idleObj.SetActive(true);
            if (walkObj.activeSelf) walkObj.SetActive(false);
            if (altObj != null && altObj.activeSelf) altObj.SetActive(false);
            return;
        }

        if (idleObj.activeSelf) idleObj.SetActive(false);

        bool showAlt = altObj != null && useAlternateWalkPose;
        if (walkObj.activeSelf != !showAlt) walkObj.SetActive(!showAlt);
        if (altObj != null && altObj.activeSelf != showAlt) altObj.SetActive(showAlt);
    }

    private bool UseDualVisualSampling()
    {
        return !UsePoseSwapFallback() &&
               idleVisualRoot != null &&
               walkVisualRoot != null &&
               idleClip != null &&
               walkClip != null;
    }

    private bool UsePoseSwapFallback()
    {
        return !UseDualAnimatorMode() &&
               idleVisualRoot != null &&
               walkVisualRoot != null &&
               alternateWalkVisualRoot != null;
    }

    private void UpdatePoseSwapFallback()
    {
        if (moving)
        {
            if (Time.time >= nextPoseSwapTime)
            {
                useAlternateWalkPose = !useAlternateWalkPose;
                nextPoseSwapTime = Time.time + Mathf.Max(0.05f, poseSwapInterval);
            }
        }

        ApplyVisualState();
    }

    private void UpdateDualVisualSampling()
    {
        ApplyVisualState();

        if (moving)
        {
            walkSampleTime = AdvanceSampleTime(walkSampleTime, walkClip);
            if (walkVisualRoot != null && walkVisualRoot.gameObject.activeInHierarchy)
                walkClip.SampleAnimation(walkVisualRoot.gameObject, walkSampleTime);
        }
        else
        {
            idleSampleTime = AdvanceSampleTime(idleSampleTime, idleClip);
            if (idleVisualRoot != null && idleVisualRoot.gameObject.activeInHierarchy)
                idleClip.SampleAnimation(idleVisualRoot.gameObject, idleSampleTime);
        }
    }

    private static float AdvanceSampleTime(float current, AnimationClip clip)
    {
        if (clip == null || clip.length <= 0.0001f)
            return 0f;

        float next = current + Time.deltaTime;
        float len = clip.length;
        while (next >= len)
            next -= len;
        return next;
    }
}
