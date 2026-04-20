using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// TEMPORARY DEBUG SCRIPT — attach to any always-active GameObject in the Main scene.
/// Logs every frame during police chase + key events for MagnetAnchor / Plasma Sphere debugging.
/// REMOVE after debugging is complete.
/// </summary>
public class MagnetChaseDebugTracker : MonoBehaviour
{
    [Header("References (auto-found if null)")]
    public Transform carRoot;          // CarRoot (playerCar in PoliceCatchController)
    public Transform activeCarModel;   // The "Car"-tagged child of CarRoot
    public Transform magnetAnchor;     // MagnetAnchor under active car
    public Transform plasmaSphere;     // Plasma Sphere under MagnetAnchor
    public Transform nitroMagnetArea;  // NitroMagnetArea under MagnetAnchor

    [Header("Settings")]
    [Tooltip("Log every N frames during chase (1 = every frame, 5 = every 5th frame)")]
    public int logEveryNFrames = 3;
    [Tooltip("Also log outside of chase (very verbose)")]
    public bool logOutsideChase = false;

    // Tracking previous parent to detect reparenting
    private Transform _prevPlasmaSphereParent;
    private Transform _prevMagnetAnchorParent;
    private Transform _prevActiveCarParent;
    private int _prevActiveCarInstanceID;
    private int _prevMagnetAnchorInstanceID;
    private int _prevPlasmaSphereInstanceID;

    // Frame counter
    private int _frameCount;

    // Snapshot at chase start
    private Vector3 _chaseStartCarRootLocal;
    private Vector3 _chaseStartCarModelLocal;
    private Vector3 _chaseStartMagnetLocal;
    private Vector3 _chaseStartPlasmaLocal;
    private Vector3 _chaseStartPlasmaWorld;
    private bool _chaseWasActive;

    private void Start()
    {
        FindReferences();
        CacheParentState();
        Debug.Log("[MagnetChaseDebug] ★ Tracker started. Will auto-find references.");
    }

    private void LateUpdate()
    {
        _frameCount++;

        // Re-find if references lost
        if (activeCarModel == null || magnetAnchor == null || plasmaSphere == null)
        {
            FindReferences();
        }

        bool chaseActive = PoliceCatchController.Instance != null && PoliceCatchController.Instance.IsChaseActive;

        // Detect chase start
        if (chaseActive && !_chaseWasActive)
        {
            OnChaseJustStarted();
        }
        // Detect chase end
        if (!chaseActive && _chaseWasActive)
        {
            OnChaseJustEnded();
        }
        _chaseWasActive = chaseActive;

        // Per-frame logging during chase
        if (chaseActive && (_frameCount % logEveryNFrames == 0))
        {
            LogFramePositions("CHASE");
        }
        else if (logOutsideChase && (_frameCount % (logEveryNFrames * 10) == 0))
        {
            LogFramePositions("IDLE");
        }

        // Always check for unexpected changes
        CheckParentChanges();
        CheckForDuplicates();
    }

    private void OnChaseJustStarted()
    {
        Debug.Log("[MagnetChaseDebug] ═══════════ CHASE STARTED ═══════════");

        // Snapshot positions at chase start
        if (carRoot != null)
            _chaseStartCarRootLocal = carRoot.localPosition;
        if (activeCarModel != null)
            _chaseStartCarModelLocal = activeCarModel.localPosition;
        if (magnetAnchor != null)
            _chaseStartMagnetLocal = magnetAnchor.localPosition;
        if (plasmaSphere != null)
        {
            _chaseStartPlasmaLocal = plasmaSphere.localPosition;
            _chaseStartPlasmaWorld = plasmaSphere.position;
        }

        Debug.Log($"[MagnetChaseDebug] SNAPSHOT carRoot.local={F(carRoot?.localPosition)} carRoot.world={F(carRoot?.position)}");
        Debug.Log($"[MagnetChaseDebug] SNAPSHOT carModel.local={F(activeCarModel?.localPosition)} carModel.world={F(activeCarModel?.position)}");
        Debug.Log($"[MagnetChaseDebug] SNAPSHOT magnetAnchor.local={F(magnetAnchor?.localPosition)} magnetAnchor.world={F(magnetAnchor?.position)}");
        Debug.Log($"[MagnetChaseDebug] SNAPSHOT plasmaSphere.local={F(plasmaSphere?.localPosition)} plasmaSphere.world={F(plasmaSphere?.position)} scale={F(plasmaSphere?.localScale)}");

        // Log parent chain
        LogParentChain("Plasma Sphere", plasmaSphere);
        LogParentChain("MagnetAnchor", magnetAnchor);

        // Log active tweens
        LogActiveTweens();

        // Log NitroMagnet state
        LogMagnetState();

        // Log boost state
        LogBoostState();

        // Log Animator state on car model
        LogAnimatorState();

        // Check duplicate Plasma Spheres
        LogDuplicateCheck();
    }

    private void OnChaseJustEnded()
    {
        Debug.Log("[MagnetChaseDebug] ═══════════ CHASE ENDED ═══════════");
        LogFramePositions("CHASE_END");
        LogActiveTweens();
        LogMagnetState();
        LogBoostState();
    }

    private void LogFramePositions(string phase)
    {
        string f = $"[MagnetChaseDebug][{phase}][F{_frameCount}]";

        Vector3 crL = carRoot != null ? carRoot.localPosition : Vector3.zero;
        Vector3 crW = carRoot != null ? carRoot.position : Vector3.zero;
        Vector3 cmL = activeCarModel != null ? activeCarModel.localPosition : Vector3.zero;
        Vector3 cmW = activeCarModel != null ? activeCarModel.position : Vector3.zero;
        Vector3 maL = magnetAnchor != null ? magnetAnchor.localPosition : Vector3.zero;
        Vector3 maW = magnetAnchor != null ? magnetAnchor.position : Vector3.zero;
        Vector3 psL = plasmaSphere != null ? plasmaSphere.localPosition : Vector3.zero;
        Vector3 psW = plasmaSphere != null ? plasmaSphere.position : Vector3.zero;
        Vector3 psS = plasmaSphere != null ? plasmaSphere.localScale : Vector3.zero;

        // Compute deltas from chase start
        Vector3 crDelta = crL - _chaseStartCarRootLocal;
        Vector3 cmDelta = cmL - _chaseStartCarModelLocal;
        Vector3 maDelta = maL - _chaseStartMagnetLocal;
        Vector3 psDelta = psL - _chaseStartPlasmaLocal;

        Debug.Log($"{f} CarRoot     local={F(crL)} world={F(crW)} Δlocal={F(crDelta)}");
        Debug.Log($"{f} CarModel    local={F(cmL)} world={F(cmW)} Δlocal={F(cmDelta)}");
        Debug.Log($"{f} MagnetAnchor local={F(maL)} world={F(maW)} Δlocal={F(maDelta)}");
        Debug.Log($"{f} PlasmaSphere local={F(psL)} world={F(psW)} Δlocal={F(psDelta)} scale={F(psS)}");

        // ALERT: if Plasma Sphere world Z diverges significantly from MagnetAnchor world Z
        float divergence = Mathf.Abs(psW.z - maW.z);
        if (divergence > 0.5f)
        {
            Debug.LogError($"{f} ⚠ DIVERGENCE DETECTED! PlasmaSphere.worldZ={psW.z:F3} vs MagnetAnchor.worldZ={maW.z:F3} diff={divergence:F3}");
        }

        // ALERT: if car model localPosition deviates from expected (should be ~0 or Animator bounce)
        if (Mathf.Abs(cmL.z) > 0.5f || Mathf.Abs(cmL.x) > 0.1f)
        {
            Debug.LogWarning($"{f} ⚠ CarModel localPos unexpected: {F(cmL)} — possible tween/animator conflict!");
        }

        // Check for active DOTween on each transform
        if (activeCarModel != null && DOTween.IsTweening(activeCarModel))
            Debug.LogWarning($"{f} ⚠ DOTween ACTIVE on CarModel!");
        if (magnetAnchor != null && DOTween.IsTweening(magnetAnchor))
            Debug.LogWarning($"{f} ⚠ DOTween ACTIVE on MagnetAnchor!");
        if (plasmaSphere != null && DOTween.IsTweening(plasmaSphere))
            Debug.LogWarning($"{f} ⚠ DOTween ACTIVE on PlasmaSphere!");
        if (carRoot != null && DOTween.IsTweening(carRoot))
            Debug.Log($"{f} DOTween active on CarRoot (expected during chase)");
    }

    private void LogActiveTweens()
    {
        Debug.Log("[MagnetChaseDebug] ── Active Tweens ──");

        CheckAndLogTweens("CarRoot", carRoot);
        CheckAndLogTweens("CarModel", activeCarModel);
        CheckAndLogTweens("MagnetAnchor", magnetAnchor);
        CheckAndLogTweens("PlasmaSphere", plasmaSphere);

        // Also check all children of carRoot for any active tweens
        if (carRoot != null)
        {
            int tweenCount = 0;
            foreach (Transform child in carRoot.GetComponentsInChildren<Transform>(true))
            {
                if (DOTween.IsTweening(child))
                {
                    tweenCount++;
                    Debug.Log($"[MagnetChaseDebug]   tween on child: {child.name} (path={GetPath(child)})");
                }
            }
            Debug.Log($"[MagnetChaseDebug]   Total tweened children under CarRoot: {tweenCount}");
        }
    }

    private void CheckAndLogTweens(string label, Transform target)
    {
        if (target == null)
        {
            Debug.Log($"[MagnetChaseDebug]   {label}: NULL ref");
            return;
        }

        bool isTweening = DOTween.IsTweening(target);
        int tweenCount = DOTween.TweensById(target.GetInstanceID())?.Count ?? 0;
        Debug.Log($"[MagnetChaseDebug]   {label}: isTweening={isTweening} tweenCount={tweenCount}");
    }

    private void LogMagnetState()
    {
        var magnet = NitroMagnetController.Instance;
        if (magnet == null)
        {
            Debug.Log("[MagnetChaseDebug] NitroMagnetController.Instance is NULL");
            return;
        }

        Debug.Log($"[MagnetChaseDebug] ── NitroMagnet State ──");
        Debug.Log($"[MagnetChaseDebug]   isArmed={magnet.IsArmed} cooldown={magnet.IsOnCooldown} collected={magnet.CoinsCollected}/{magnet.Quota} inFlight={magnet.InFlightCount}");

        // Check if shieldVFX reference is valid via reflection or public field
        // (shieldVFX is public)
        if (magnet.shieldVFX != null)
        {
            Transform svfx = magnet.shieldVFX.transform;
            Debug.Log($"[MagnetChaseDebug]   shieldVFX.name={magnet.shieldVFX.name} localPos={F(svfx.localPosition)} worldPos={F(svfx.position)} scale={F(svfx.localScale)} active={magnet.shieldVFX.activeSelf}");
            Debug.Log($"[MagnetChaseDebug]   shieldVFX.parent={svfx.parent?.name ?? "NULL"} fullPath={GetPath(svfx)}");
            Debug.Log($"[MagnetChaseDebug]   shieldVFX.instanceID={magnet.shieldVFX.GetInstanceID()}");

            // Compare with our tracked plasmaSphere reference
            if (plasmaSphere != null)
            {
                bool sameObject = magnet.shieldVFX.GetInstanceID() == plasmaSphere.gameObject.GetInstanceID();
                Debug.Log($"[MagnetChaseDebug]   shieldVFX == trackedPlasmaSphere? {sameObject} (tracked ID={plasmaSphere.gameObject.GetInstanceID()})");
                if (!sameObject)
                {
                    Debug.LogError("[MagnetChaseDebug] ⚠ REFERENCE MISMATCH! shieldVFX points to a DIFFERENT object than the tracked Plasma Sphere!");
                }
            }
        }
        else
        {
            Debug.LogWarning("[MagnetChaseDebug]   shieldVFX is NULL!");
        }

        // Check magnetTarget reference
        if (magnet.magnetTarget != null)
        {
            Debug.Log($"[MagnetChaseDebug]   magnetTarget.name={magnet.magnetTarget.name} localPos={F(magnet.magnetTarget.localPosition)} worldPos={F(magnet.magnetTarget.position)} parent={magnet.magnetTarget.parent?.name ?? "NULL"}");
            Debug.Log($"[MagnetChaseDebug]   magnetTarget.instanceID={magnet.magnetTarget.GetInstanceID()}");

            if (magnetAnchor != null)
            {
                bool sameAnchor = magnet.magnetTarget.GetInstanceID() == magnetAnchor.GetInstanceID();
                Debug.Log($"[MagnetChaseDebug]   magnetTarget == trackedMagnetAnchor? {sameAnchor} (tracked ID={magnetAnchor.GetInstanceID()})");
                if (!sameAnchor)
                {
                    Debug.LogError("[MagnetChaseDebug] ⚠ ANCHOR MISMATCH! magnetTarget points to a DIFFERENT MagnetAnchor!");
                }
            }
        }
        else
        {
            Debug.LogWarning("[MagnetChaseDebug]   magnetTarget is NULL!");
        }
    }

    private void LogBoostState()
    {
        var boost = BoostModeController.Instance;
        var cinematic = BoostModeCinematicController.Instance;

        Debug.Log("[MagnetChaseDebug] ── Boost State ──");

        if (boost != null)
            Debug.Log($"[MagnetChaseDebug]   BoostActive={boost.IsBoostActive}");
        else
            Debug.Log("[MagnetChaseDebug]   BoostModeController.Instance is NULL");

        if (cinematic != null)
        {
            // Access what we can
            Debug.Log($"[MagnetChaseDebug]   CinematicController exists. verboseLogs={cinematic.verboseLogs}");
        }
        else
        {
            Debug.Log("[MagnetChaseDebug]   BoostModeCinematicController.Instance is NULL");
        }
    }

    private void LogAnimatorState()
    {
        if (activeCarModel == null) return;

        Animator anim = activeCarModel.GetComponent<Animator>();
        if (anim == null)
        {
            Debug.Log("[MagnetChaseDebug] No Animator on active car model");
            return;
        }

        Debug.Log($"[MagnetChaseDebug] ── Animator on {activeCarModel.name} ──");
        Debug.Log($"[MagnetChaseDebug]   enabled={anim.enabled} speed={anim.speed} applyRootMotion={anim.applyRootMotion}");

        AnimatorClipInfo[] clips = anim.GetCurrentAnimatorClipInfo(0);
        for (int i = 0; i < clips.Length; i++)
        {
            Debug.Log($"[MagnetChaseDebug]   clip[{i}]={clips[i].clip.name} weight={clips[i].weight:F3}");
        }

        AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);
        Debug.Log($"[MagnetChaseDebug]   stateHash={state.shortNameHash} normalizedTime={state.normalizedTime:F3} length={state.length:F3}");
    }

    private void CheckParentChanges()
    {
        // Check Plasma Sphere parent
        if (plasmaSphere != null)
        {
            Transform curParent = plasmaSphere.parent;
            if (_prevPlasmaSphereParent != null && curParent != _prevPlasmaSphereParent)
            {
                Debug.LogError($"[MagnetChaseDebug] ⚠ PLASMA SPHERE REPARENTED! old={_prevPlasmaSphereParent.name} new={curParent?.name ?? "NULL"}");
            }
            _prevPlasmaSphereParent = curParent;
        }

        // Check MagnetAnchor parent
        if (magnetAnchor != null)
        {
            Transform curParent = magnetAnchor.parent;
            if (_prevMagnetAnchorParent != null && curParent != _prevMagnetAnchorParent)
            {
                Debug.LogError($"[MagnetChaseDebug] ⚠ MAGNET ANCHOR REPARENTED! old={_prevMagnetAnchorParent.name} new={curParent?.name ?? "NULL"}");
            }
            _prevMagnetAnchorParent = curParent;
        }

        // Check if active car model changes
        if (activeCarModel != null)
        {
            int curID = activeCarModel.GetInstanceID();
            if (_prevActiveCarInstanceID != 0 && curID != _prevActiveCarInstanceID)
            {
                Debug.LogWarning($"[MagnetChaseDebug] ⚠ Active car model CHANGED! oldID={_prevActiveCarInstanceID} newID={curID}");
            }
            _prevActiveCarInstanceID = curID;
        }
    }

    private void CheckForDuplicates()
    {
        // Only run every 60 frames to save performance
        if (_frameCount % 60 != 0) return;

        // Check for duplicate visible Plasma Spheres
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        int visiblePlasmaCount = 0;
        int activePlasmaCount = 0;

        foreach (var obj in allObjects)
        {
            if (obj.name.StartsWith("Plasma Sphere"))
            {
                if (obj.activeInHierarchy)
                {
                    activePlasmaCount++;
                    Vector3 scale = obj.transform.localScale;
                    if (scale.sqrMagnitude > 0.01f) // visible = non-zero scale
                    {
                        visiblePlasmaCount++;
                    }
                }
            }
        }

        if (visiblePlasmaCount > 1)
        {
            Debug.LogError($"[MagnetChaseDebug] ⚠ MULTIPLE VISIBLE Plasma Spheres! visible={visiblePlasmaCount} activeInHierarchy={activePlasmaCount}");
        }
    }

    private void LogDuplicateCheck()
    {
        Debug.Log("[MagnetChaseDebug] ── Duplicate / Visibility Check ──");
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);

        foreach (var obj in allObjects)
        {
            if (!obj.name.StartsWith("Plasma Sphere")) continue;

            Transform t = obj.transform;
            bool activeInHierarchy = obj.activeInHierarchy;
            Vector3 scale = t.localScale;
            bool visible = activeInHierarchy && scale.sqrMagnitude > 0.01f;

            Debug.Log($"[MagnetChaseDebug]   {obj.name} id={obj.GetInstanceID()} active={activeInHierarchy} visible={visible} scale={F(scale)} localPos={F(t.localPosition)} worldPos={F(t.position)} parent={t.parent?.name ?? "NULL"} grandparent={t.parent?.parent?.name ?? "NULL"}");
        }
    }

    private void LogParentChain(string label, Transform t)
    {
        if (t == null) return;
        Debug.Log($"[MagnetChaseDebug] ── Parent chain: {label} ──");
        Transform cur = t;
        int depth = 0;
        while (cur != null && depth < 12)
        {
            Debug.Log($"[MagnetChaseDebug]   [{depth}] {cur.name} localPos={F(cur.localPosition)} worldPos={F(cur.position)} localRot={cur.localEulerAngles.ToString("F2")} scale={F(cur.localScale)}");
            cur = cur.parent;
            depth++;
        }
    }

    // ════════════════════════════════════════
    // Reference finding
    // ════════════════════════════════════════

    private void FindReferences()
    {
        // Find CarRoot: look for MainSceneCarController
        if (carRoot == null)
        {
            var mscc = FindFirstObjectByType<MainSceneCarController>();
            if (mscc != null)
                carRoot = mscc.transform; // MainSceneCarController is on CarRoot
        }

        // Find active car model: tagged "Car"
        if (activeCarModel == null)
        {
            GameObject carObj = GameObject.FindGameObjectWithTag("Car");
            if (carObj != null)
                activeCarModel = carObj.transform;
        }

        // Find MagnetAnchor under active car
        if (magnetAnchor == null && activeCarModel != null)
        {
            magnetAnchor = FindChildByPrefix(activeCarModel, "MagnetAnchor");
        }

        // Find Plasma Sphere under MagnetAnchor
        if (plasmaSphere == null && magnetAnchor != null)
        {
            plasmaSphere = FindChildByPrefix(magnetAnchor, "Plasma Sphere");
        }

        // Find NitroMagnetArea under MagnetAnchor
        if (nitroMagnetArea == null && magnetAnchor != null)
        {
            nitroMagnetArea = FindChildByPrefix(magnetAnchor, "NitroMagnetArea");
        }

        CacheParentState();

        if (activeCarModel != null)
            Debug.Log($"[MagnetChaseDebug] Refs: carRoot={carRoot?.name} carModel={activeCarModel?.name} anchor={magnetAnchor?.name} plasma={plasmaSphere?.name} area={nitroMagnetArea?.name}");
    }

    private void CacheParentState()
    {
        _prevPlasmaSphereParent = plasmaSphere?.parent;
        _prevMagnetAnchorParent = magnetAnchor?.parent;
        _prevActiveCarInstanceID = activeCarModel != null ? activeCarModel.GetInstanceID() : 0;
    }

    private static Transform FindChildByPrefix(Transform parent, string prefix)
    {
        foreach (Transform child in parent)
        {
            if (child.name.StartsWith(prefix))
                return child;
        }
        foreach (Transform child in parent)
        {
            Transform found = FindChildByPrefix(child, prefix);
            if (found != null) return found;
        }
        return null;
    }

    // ════════════════════════════════════════
    // Gizmos — visual debugging in Scene view
    // ════════════════════════════════════════

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // CarRoot — blue
        if (carRoot != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(carRoot.position, new Vector3(2f, 0.5f, 4f));
            UnityEditor.Handles.Label(carRoot.position + Vector3.up * 1.5f, $"CarRoot Z={carRoot.position.z:F2}");
        }

        // Active car model — cyan
        if (activeCarModel != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(activeCarModel.position, new Vector3(1.8f, 0.4f, 3.8f));
            UnityEditor.Handles.Label(activeCarModel.position + Vector3.up * 2f, $"CarModel localZ={activeCarModel.localPosition.z:F3}");
        }

        // MagnetAnchor — green sphere
        if (magnetAnchor != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(magnetAnchor.position, 0.3f);
            UnityEditor.Handles.Label(magnetAnchor.position + Vector3.up * 0.5f, $"Anchor localZ={magnetAnchor.localPosition.z:F3}");
        }

        // Plasma Sphere — magenta sphere
        if (plasmaSphere != null)
        {
            Gizmos.color = Color.magenta;
            float radius = plasmaSphere.localScale.magnitude * 0.3f;
            Gizmos.DrawWireSphere(plasmaSphere.position, radius);
            UnityEditor.Handles.Label(plasmaSphere.position + Vector3.up * 0.8f,
                $"Plasma wZ={plasmaSphere.position.z:F2} lZ={plasmaSphere.localPosition.z:F3} s={plasmaSphere.localScale.x:F2}");
        }

        // Draw line from MagnetAnchor to Plasma Sphere (should overlap if co-located)
        if (magnetAnchor != null && plasmaSphere != null)
        {
            float dist = Vector3.Distance(magnetAnchor.position, plasmaSphere.position);
            Gizmos.color = dist > 0.5f ? Color.red : Color.green;
            Gizmos.DrawLine(magnetAnchor.position, plasmaSphere.position);
            if (dist > 0.5f)
            {
                UnityEditor.Handles.Label(
                    (magnetAnchor.position + plasmaSphere.position) * 0.5f + Vector3.up * 0.3f,
                    $"DETACHED! dist={dist:F2}");
            }
        }
    }
#endif

    // ════════════════════════════════════════
    // Helpers
    // ════════════════════════════════════════

    private static string F(Vector3? v)
    {
        return v.HasValue ? v.Value.ToString("F4") : "NULL";
    }

    private static string F(Vector3 v)
    {
        return v.ToString("F4");
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        Transform p = t.parent;
        while (p != null)
        {
            path = p.name + "/" + path;
            p = p.parent;
        }
        return path;
    }
}
