using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Make objects between the camera and the target translucent so the player is visible.
/// Attach to the camera (or a manager). Assign target (player transform).
/// </summary>
[DisallowMultipleComponent]
public class CameraObstructionHandler : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Transform of the player or object camera follows")]
    public Transform target;

    [Header("Detection")]
    [Tooltip("Layers considered potential obstructions")]
    public LayerMask obstructionMask = ~0;
    [Tooltip("Include trigger colliders in the obstruction check?")]
    public bool includeTriggerColliders = true;
    [Tooltip("Interval (seconds) between obstruction checks. Lower = more responsive, higher = cheaper.")]
    [Range(0.02f, 1f)]
    public float checkInterval = 0.08f;

    [Header("Fading")]
    [Range(0f, 1f)]
    public float minAlpha = 0.25f;
    [Tooltip("Seconds to fade out/in")]
    public float fadeDuration = 0.2f;
    [Tooltip("If true, will attempt to set Standard shader to Fade rendering mode on the cloned material.")]
    public bool forceFadeModeForStandardShader = true;

    // Add these public fields near the top of the class:
    [Header("Debug / Robustness")]
    [Tooltip("Draw the camera->target ray and log hits to Console.")]
    public bool debugDrawRay = false;
    [Tooltip("If true, use a small spherecast (radius) in addition to a raycast to catch thin geometry.")]
    public bool useSphereFallback = true;
    [Tooltip("Sphere radius used by fallback SphereCastAll.")]
    public float sphereFallbackRadius = 0.25f;

    [Header("Filter")]
    [Tooltip("If set, objects with any of these tags will be ignored")]
    public string[] ignoreTags;

    // runtime maps
    private readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
    private readonly Dictionary<Renderer, Material[]> instanceMaterials = new Dictionary<Renderer, Material[]>();
    private readonly HashSet<Renderer> currentlyObstructing = new HashSet<Renderer>();
    private readonly Dictionary<Renderer, Coroutine> activeFadeCoroutines = new Dictionary<Renderer, Coroutine>();

    private Camera cam;
    private Coroutine checkCoroutine;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        if (cam == null) Debug.LogWarning("[CameraObstructionHandler] No Camera found on object and Camera.main is null.");
    }

    private void OnEnable()
    {
        if (checkCoroutine != null) StopCoroutine(checkCoroutine);
        checkCoroutine = StartCoroutine(PeriodicCheck());
    }

    private void OnDisable()
    {
        if (checkCoroutine != null) StopCoroutine(checkCoroutine);
        // restore everything
        foreach (var kv in activeFadeCoroutines)
            if (kv.Value != null) StopCoroutine(kv.Value);

        foreach (var r in new List<Renderer>(instanceMaterials.Keys))
            RestoreRendererMaterials(r);
    }

    private IEnumerator PeriodicCheck()
    {
        while (true)
        {
            DoObstructionCheck();
            yield return new WaitForSeconds(checkInterval);
        }
    }

    private void DoObstructionCheck()
    {
        if (target == null || cam == null) return;

        Vector3 origin = cam.transform.position;
        Vector3 dest = target.position + Vector3.up * 0.9f; // torso aim
        Vector3 dir = dest - origin;
        float dist = dir.magnitude;
        if (dist <= 0.0001f) return;
        dir /= dist;

        if (debugDrawRay)
        {
            Debug.DrawLine(origin, dest, Color.cyan, checkInterval);
        }

        QueryTriggerInteraction qti = includeTriggerColliders ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;

        // 1) RaycastAll
        RaycastHit[] rayHits = Physics.RaycastAll(origin, dir, dist, obstructionMask, qti);

        // 2) Optional SphereCastAll fallback (useful for thin meshes or holes)
        RaycastHit[] sphereHits = null;
        if ((rayHits == null || rayHits.Length == 0) && useSphereFallback)
        {
            if (debugDrawRay) Debug.Log("[CameraObstructionHandler] No ray hits; trying SphereCastAll fallback.");
            sphereHits = Physics.SphereCastAll(origin, sphereFallbackRadius, dir, dist, obstructionMask, qti);
        }

        // 3) Overlap checks (IMPORTANT for "camera inside collider" cases)
        // We'll check the camera origin, the target point, and a couple samples along the line.
        // Tunable: sample count and radius (we're reusing sphereFallbackRadius as overlap radius).
        var overlapColliders = new HashSet<Collider>();
        try
        {
            // Always check origin (camera might be partially inside object)
            var oc = Physics.OverlapSphere(origin, sphereFallbackRadius, obstructionMask, qti);
            foreach (var c in oc) overlapColliders.Add(c);

            // Also check a small sphere around the destination (in case target overlaps)
            var dc = Physics.OverlapSphere(dest, Mathf.Min(0.5f, sphereFallbackRadius), obstructionMask, qti);
            foreach (var c in dc) overlapColliders.Add(c);

            // Sample along the ray a few times (to catch thin or oddly scaled colliders)
            int samples = Mathf.Clamp((int)(dist / 1.0f), 1, 6); // 1 sample per ~1 unit, cap 6
            for (int i = 1; i <= samples; i++)
            {
                float t = (float)i / (samples + 1);
                Vector3 samplePos = Vector3.Lerp(origin, dest, t);
                var sc = Physics.OverlapSphere(samplePos, sphereFallbackRadius, obstructionMask, qti);
                foreach (var c in sc) overlapColliders.Add(c);
            }
        }
        catch (System.Exception ex)
        {
            // OverlapSphere shouldn't usually throw — catch to be safe in exotic physics setups
            if (debugDrawRay) Debug.LogWarning("[CameraObstructionHandler] OverlapSphere exception: " + ex.Message);
        }

        // Consolidate colliders from rayHits, sphereHits, and overlapColliders into a single set
        var collidersSet = new HashSet<Collider>();
        if (rayHits != null)
        {
            foreach (var h in rayHits) if (h.collider != null) collidersSet.Add(h.collider);
        }
        if (sphereHits != null)
        {
            foreach (var h in sphereHits) if (h.collider != null) collidersSet.Add(h.collider);
        }
        foreach (var c in overlapColliders) collidersSet.Add(c);

        // Build a set of renderers from those colliders (look on object, children, and parent)
        HashSet<Renderer> hitRenderers = new HashSet<Renderer>();
        foreach (var col in collidersSet)
        {
            if (col == null) continue;
            var go = col.gameObject;
            if (ShouldIgnore(go)) continue;

            if (debugDrawRay)
                Debug.Log($"[CameraObstructionHandler] Detected collider: {go.name} (isTrigger={col.isTrigger})");

            var rend = go.GetComponent<Renderer>();
            if (rend != null) hitRenderers.Add(rend);

            foreach (var cr in go.GetComponentsInChildren<Renderer>(true))
                if (cr != null) hitRenderers.Add(cr);

            var parentR = go.GetComponentInParent<Renderer>();
            if (parentR != null) hitRenderers.Add(parentR);
        }

        // Fade out newly obstructing renderers
        foreach (var r in hitRenderers)
        {
            if (!currentlyObstructing.Contains(r))
            {
                currentlyObstructing.Add(r);
                StartFadeOut(r);
            }
        }

        // Restore those no longer obstructing
        var toRestore = new List<Renderer>();
        foreach (var r in currentlyObstructing)
        {
            if (!hitRenderers.Contains(r))
                toRestore.Add(r);
        }
        foreach (var r in toRestore)
        {
            currentlyObstructing.Remove(r);
            StartFadeInAndRestore(r);
        }
    }

    private bool ShouldIgnore(GameObject go)
    {
        if (go == null) return true;
        if (ignoreTags != null && ignoreTags.Length > 0)
        {
            string t = go.tag;
            foreach (var it in ignoreTags)
                if (!string.IsNullOrEmpty(it) && it == t) return true;
        }
        return false;
    }

    private void StartFadeOut(Renderer r)
    {
        if (r == null) return;
        // If already have a fade coroutine, stop it.
        if (activeFadeCoroutines.TryGetValue(r, out var existing) && existing != null)
        {
            StopCoroutine(existing);
            activeFadeCoroutines.Remove(r);
        }

        // store original materials if not already
        if (!originalMaterials.ContainsKey(r))
            originalMaterials[r] = r.sharedMaterials;

        // create instances for fading if not already
        if (!instanceMaterials.ContainsKey(r))
        {
            var shared = r.sharedMaterials;
            var instances = new Material[shared.Length];
            for (int i = 0; i < shared.Length; i++)
            {
                Material orig = shared[i];
                if (orig == null) { instances[i] = null; continue; }
                Material inst = new Material(orig); // create instance copy
                instances[i] = inst;

                // if the shader is Standard and we want Fade mode, try to configure
                if (forceFadeModeForStandardShader && IsStandardShader(orig))
                {
                    SetupMaterialWithRenderingMode(inst, RenderingMode.Fade);
                }
            }
            instanceMaterials[r] = instances;
            r.materials = instances; // assign instance materials (this creates material instances)
        }

        // start fade coroutine
        var co = StartCoroutine(FadeRendererToAlpha(r, minAlpha, fadeDuration));
        activeFadeCoroutines[r] = co;
    }

    private void StartFadeInAndRestore(Renderer r)
    {
        if (r == null) return;
        if (activeFadeCoroutines.TryGetValue(r, out var existing) && existing != null)
        {
            StopCoroutine(existing);
            activeFadeCoroutines.Remove(r);
        }

        // start fade back to full and then restore original materials
        var co = StartCoroutine(FadeRendererToAlphaRestore(r, 1f, fadeDuration));
        activeFadeCoroutines[r] = co;
    }

    private IEnumerator FadeRendererToAlpha(Renderer r, float targetAlpha, float duration)
    {
        if (r == null) yield break;
        if (!instanceMaterials.TryGetValue(r, out var mats))
        {
            yield break;
        }

        // get initial alphas
        float elapsed = 0f;
        // capture per-material initial colors
        Color[] startColors = new Color[mats.Length];
        for (int i = 0; i < mats.Length; i++)
        {
            var m = mats[i];
            if (m == null) { startColors[i] = Color.white; continue; }
            startColors[i] = GetMaterialColor(m);
        }

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float a = Mathf.Lerp(startColors.Length > 0 ? startColors[0].a : 1f, targetAlpha, t);
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                SetMaterialAlpha(m, Mathf.Lerp(startColors[i].a, targetAlpha, t));
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // ensure final
        for (int i = 0; i < mats.Length; i++)
            if (mats[i] != null) SetMaterialAlpha(mats[i], targetAlpha);

        // remove coroutine reference
        activeFadeCoroutines.Remove(r);
    }

    private IEnumerator FadeRendererToAlphaRestore(Renderer r, float targetAlpha, float duration)
    {
        if (r == null) yield break;
        if (!instanceMaterials.TryGetValue(r, out var mats))
        {
            yield break;
        }

        // get initial alphas
        float elapsed = 0f;
        Color[] startColors = new Color[mats.Length];
        for (int i = 0; i < mats.Length; i++)
        {
            var m = mats[i];
            if (m == null) { startColors[i] = Color.white; continue; }
            startColors[i] = GetMaterialColor(m);
        }

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                float a = Mathf.Lerp(startColors[i].a, targetAlpha, t);
                SetMaterialAlpha(m, a);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < mats.Length; i++)
            if (mats[i] != null) SetMaterialAlpha(mats[i], targetAlpha);

        // After restored to fully opaque, swap back to original shared materials to avoid leaking instances
        RestoreRendererMaterials(r);

        activeFadeCoroutines.Remove(r);
    }

    private void RestoreRendererMaterials(Renderer r)
    {
        if (r == null) return;

        // restore original shared materials
        if (originalMaterials.TryGetValue(r, out var orig))
        {
            r.sharedMaterials = orig;
            originalMaterials.Remove(r);
        }

        // destroy instances we created (they are not assigned anymore)
        if (instanceMaterials.TryGetValue(r, out var insts))
        {
            for (int i = 0; i < insts.Length; i++)
            {
                if (insts[i] != null)
                {
                    Destroy(insts[i]);
                    insts[i] = null;
                }
            }
            instanceMaterials.Remove(r);
        }
    }

    #region Material Helpers

    private enum RenderingMode { Opaque = 0, Cutout = 1, Fade = 2, Transparent = 3 }

    private static bool IsStandardShader(Material mat)
    {
        if (mat == null) return false;
        var s = mat.shader;
        if (s == null) return false;
        // crude check: name contains "Standard"
        return s.name != null && s.name.ToLowerInvariant().Contains("standard");
    }

    // Try to set material rendering mode similar to Unity Standard shader helper
    // Replace the existing SetupMaterialWithRenderingMode function with this:
    private static void SetupMaterialWithRenderingMode(Material material, RenderingMode renderingMode)
    {
        if (material == null) return;

        // Try Standard-style properties first (safe no-op for non-Standard shaders)
        if (material.HasProperty("_Mode"))
            material.SetFloat("_Mode", (float)renderingMode);

        // Apply common changes (blend, zwrite, keywords, renderQueue)
        switch (renderingMode)
        {
            case RenderingMode.Opaque:
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                material.SetInt("_ZWrite", 1);
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = -1;
                material.SetOverrideTag("RenderType", "Opaque");
                break;

            case RenderingMode.Cutout:
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                material.SetInt("_ZWrite", 1);
                material.EnableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                material.SetOverrideTag("RenderType", "TransparentCutout");
                break;

            case RenderingMode.Fade:
            case RenderingMode.Transparent:
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                material.SetOverrideTag("RenderType", "Transparent");
                break;
        }

        // EXTRA: try to support URP Lit (Universal Render Pipeline)
        // URP uses a "_Surface" float (0=Opaque,1=Transparent) and keywords like "_SURFACE_TYPE_TRANSPARENT"
        // and often uses "_BaseColor" property for color.
        string shaderName = material.shader != null ? material.shader.name : "";
        if (!string.IsNullOrEmpty(shaderName) && shaderName.ToLowerInvariant().Contains("universal render pipeline"))
        {
            // make it transparent in URP terms
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", renderingMode == RenderingMode.Opaque ? 0f : 1f);

            // URP sometimes needs this keyword:
            if (renderingMode == RenderingMode.Opaque)
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            else
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            // ensure render queue is transparent
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");
        }

        // EXTRA: try to support HDRP lit shaders (best-effort)
        if (!string.IsNullOrEmpty(shaderName) && shaderName.ToLowerInvariant().Contains("hdrp"))
        {
            // HDRP uses "_SurfaceType" or different properties depending on HDRP version.
            if (material.HasProperty("_SurfaceType"))
                material.SetFloat("_SurfaceType", renderingMode == RenderingMode.Opaque ? 0f : 1f);

            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");
        }
    }


    private static Color GetMaterialColor(Material m)
    {
        if (m == null) return Color.white;
        if (m.HasProperty("_Color")) return m.color;
        if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
        // fallback
        return Color.white;
    }

    private static void SetMaterialAlpha(Material m, float a)
    {
        if (m == null) return;
        if (m.HasProperty("_Color"))
        {
            Color c = m.color;
            c.a = a;
            m.color = c;
            return;
        }
        if (m.HasProperty("_BaseColor"))
        {
            Color c = m.GetColor("_BaseColor");
            c.a = a;
            m.SetColor("_BaseColor", c);
            return;
        }

        // Some shaders have no color property we can tweak; we try to set _TintColor or fallback to enabling keyword
        if (m.HasProperty("_TintColor"))
        {
            Color c = m.GetColor("_TintColor");
            c.a = a;
            m.SetColor("_TintColor", c);
        }
    }

    #endregion
}
