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
        Vector3 dest = target.position + Vector3.up * 0.9f; // slight offset so we aim roughly at torso
        Vector3 dir = dest - origin;
        float dist = dir.magnitude;
        if (dist <= 0.0001f) return;

        dir /= dist;

        // choose whether to hit trigger colliders
        QueryTriggerInteraction qti = includeTriggerColliders ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;

        // RaycastAll so we can detect multiple obstructions
        RaycastHit[] hits = Physics.RaycastAll(origin, dir, dist, obstructionMask, qti);

        // Build a set of renderers hit (including children of hit collider)
        HashSet<Renderer> hitRenderers = new HashSet<Renderer>();
        foreach (var h in hits)
        {
            var go = h.collider.gameObject;
            if (ShouldIgnore(go)) continue;

            // Add any Renderer on the hit object or its parents - and its children too if needed.
            var rend = go.GetComponent<Renderer>();
            if (rend != null) hitRenderers.Add(rend);

            // Also consider children renderers (some large walls have children)
            var childRenderers = go.GetComponentsInChildren<Renderer>();
            foreach (var cr in childRenderers)
                hitRenderers.Add(cr);

            // consider parent renderers
            var parentRenderer = go.GetComponentInParent<Renderer>();
            if (parentRenderer != null) hitRenderers.Add(parentRenderer);
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

        // Fade in / restore renderers that are no longer obstructing
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
    private static void SetupMaterialWithRenderingMode(Material material, RenderingMode renderingMode)
    {
        if (material == null) return;
        // This is based on Unity's Standard shader properties - may not work for URP/HDRP/custom shaders.
        if (material.HasProperty("_Mode"))
        {
            material.SetFloat("_Mode", (float)renderingMode);
        }

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
                break;

            case RenderingMode.Cutout:
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                material.SetInt("_ZWrite", 1);
                material.EnableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                break;

            case RenderingMode.Fade:
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                break;

            case RenderingMode.Transparent:
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                break;
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
