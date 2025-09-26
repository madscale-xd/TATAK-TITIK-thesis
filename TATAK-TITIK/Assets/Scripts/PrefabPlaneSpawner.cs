using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class PrefabPlaneSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [Tooltip("Assign one or more prefab GameObjects. One will be chosen at random per spawn.")]
    public GameObject[] prefabs;

    [Header("Spawn Settings")]
    [Tooltip("Number of prefabs to spawn when you run Spawn()")]
    public int spawnCount = 20;
    [Tooltip("Vertical offset above the plane surface (in world units).")]
    public float verticalOffset = 0.5f;
    [Tooltip("If true, spawns automatically in Start (Play mode only).")]
    public bool spawnOnStart = true;

    [Header("Optional randomness")]
    public bool randomRotation = true;
    public Vector3 rotationMin = Vector3.zero;
    public Vector3 rotationMax = new Vector3(0f, 360f, 0f);
    public bool randomUniformScale = false;
    public float scaleMin = 1f;
    public float scaleMax = 1f;

    [Header("Collision / Overlap Avoidance")]
    [Tooltip("If > 0, the spawner will attempt to avoid placing objects that overlap this radius.")]
    public float avoidOverlapRadius = 0f;
    [Tooltip("Layer mask used for overlap checks (what counts as blocking).")]
    public LayerMask overlapCheckMask = ~0;
    [Tooltip("Max attempts per spawn before giving up on that spawn (prevents infinite loop).")]
    public int maxAttemptsPerSpawn = 10;

    [Header("Parenting & lifespan")]
    [Tooltip("Parent spawned objects under this transform if true (keeps hierarchy tidy).")]
    public bool parentToThis = true;
    [Tooltip("If > 0, spawned objects will be auto-destroyed after this many seconds.")]
    public float autoDestroyAfter = 0f;

    [Header("Debug / Visual")]
    [Tooltip("Draw gizmos showing spawn area (editor & play mode).")]
    public bool drawGizmo = true;
    public Color gizmoColor = new Color(0f, 1f, 0f, 0.15f);

    // internal
    MeshFilter meshFilter;
    Collider areaCollider;

    private void Awake()
    {
        CacheComponents();
    }

    private void Start()
    {
        if (!Application.isPlaying) return;

        if (spawnOnStart)
            Spawn(spawnCount);
    }

    private void CacheComponents()
    {
        meshFilter = GetComponent<MeshFilter>();
        areaCollider = GetComponent<Collider>();
    }

    /// <summary>
    /// Public method to spawn N prefabs.
    /// </summary>
    public void Spawn(int count)
    {
        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.LogWarning($"[{nameof(PrefabPlaneSpawner)}] No prefabs assigned.", this);
            return;
        }

        CacheComponents();
        if (meshFilter == null && areaCollider == null)
        {
            // If there's no mesh or collider, try to add a BoxCollider sized to transform scale (useful for Unity Plane)
            var bc = GetComponent<BoxCollider>();
            if (bc == null)
            {
                bc = gameObject.AddComponent<BoxCollider>();
                bc.center = Vector3.zero;
                bc.size = Vector3.one; // default; user can tweak
            }
            areaCollider = bc;
            Debug.LogWarning($"[{nameof(PrefabPlaneSpawner)}] No MeshFilter or Collider found — added a BoxCollider automatically. Tweak size if necessary.", this);
        }

        int spawned = 0;
        for (int i = 0; i < count; i++)
        {
            bool placed = TryPlaceRandomPrefab(out GameObject go);
            if (placed) spawned++;
        }

        Debug.Log($"[{nameof(PrefabPlaneSpawner)}] Requested: {count}, Spawned: {spawned}");
    }

    /// <summary>
    /// Tries to sample a random point on the plane area and instantiate a random prefab there.
    /// Returns true on success.
    /// </summary>
    private bool TryPlaceRandomPrefab(out GameObject spawnedObject)
    {
        spawnedObject = null;
        for (int attempt = 0; attempt < Mathf.Max(1, maxAttemptsPerSpawn); attempt++)
        {
            Vector3 worldPos = SampleRandomPointOnArea();

            // final spawn position with vertical offset
            Vector3 spawnPos = worldPos + (transform.up.normalized * verticalOffset);

            // overlap check
            if (avoidOverlapRadius > 0f)
            {
                if (Physics.CheckSphere(spawnPos, avoidOverlapRadius, overlapCheckMask, QueryTriggerInteraction.Ignore))
                {
                    // overlapping — try again
                    continue;
                }
            }

            // choose random prefab
            GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
            if (prefab == null) return false;

            // instantiate
            spawnedObject = Instantiate(prefab, spawnPos, Quaternion.identity);
            if (parentToThis) spawnedObject.transform.SetParent(this.transform, true);

            // random rotation
            if (randomRotation)
            {
                float rx = Random.Range(rotationMin.x, rotationMax.x);
                float ry = Random.Range(rotationMin.y, rotationMax.y);
                float rz = Random.Range(rotationMin.z, rotationMax.z);
                spawnedObject.transform.rotation = Quaternion.Euler(rx, ry, rz);
            }

            // random scale
            if (randomUniformScale)
            {
                float s = Random.Range(scaleMin, scaleMax);
                spawnedObject.transform.localScale = Vector3.one * s;
            }
            else if (scaleMin != 1f || scaleMax != 1f)
            {
                // if non-uniform scaling is wanted later, could add fields. For now keep simple.
            }

            if (autoDestroyAfter > 0f)
            {
                Destroy(spawnedObject, autoDestroyAfter);
            }

            return true; // placed successfully
        }

        // failed
        return false;
    }

    /// <summary>
    /// Samples a random point on the area. If a MeshFilter exists, it uses the mesh bounds in local space and transforms the sampled local point to world space.
    /// Otherwise it falls back to using the Collider bounds (world AABB).
    /// </summary>
    private Vector3 SampleRandomPointOnArea()
    {
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            var meshBounds = meshFilter.sharedMesh.bounds; // in mesh local space
            // make use of transform.lossyScale to account for parent scaling
            Vector3 extents = Vector3.Scale(meshBounds.extents, transform.lossyScale);
            Vector3 centerLocal = meshBounds.center;
            // sample in local mesh space (use original bounds extents but relative to transform)
            float rx = Random.Range(-meshBounds.extents.x, meshBounds.extents.x);
            float rz = Random.Range(-meshBounds.extents.z, meshBounds.extents.z);
            // preserve mesh center offset
            Vector3 localSample = new Vector3(centerLocal.x + rx, centerLocal.y, centerLocal.z + rz);
            // Because meshBounds are in the object's local space (not scaled), we must transform using TransformPoint.
            // TransformPoint will account for rotation, scale and position.
            return transform.TransformPoint(localSample);
        }
        else if (areaCollider != null)
        {
            // collider.bounds is world-axis aligned — simpler fallback
            Bounds b = areaCollider.bounds;
            float x = Random.Range(b.min.x, b.max.x);
            float z = Random.Range(b.min.z, b.max.z);
            // place at collider surface center Y (we add verticalOffset later)
            float y = b.center.y;
            return new Vector3(x, y, z);
        }
        else
        {
            // ultimate fallback: use transform position
            return transform.position;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmo) return;
        Gizmos.color = gizmoColor;

        CacheComponents();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            var meshBounds = meshFilter.sharedMesh.bounds;
            // draw a wire cube transformed to world
            Matrix4x4 trs = transform.localToWorldMatrix;
            Gizmos.matrix = trs;
            Gizmos.DrawCube(meshBounds.center, meshBounds.size);
            Gizmos.color = Color.green * 0.6f;
            Gizmos.DrawWireCube(meshBounds.center, meshBounds.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
        else if (areaCollider != null)
        {
            var b = areaCollider.bounds;
            Gizmos.DrawCube(b.center, b.size);
        }
    }

#if UNITY_EDITOR
    // Editor utility to spawn from the inspector
    [ContextMenu("Spawn Prefabs (Editor)")]
    public void ContextSpawn()
    {
        if (!EditorApplication.isPlaying)
        {
            // Spawn in editor: create instances as prefab instances
            int spawned = 0;
            for (int i = 0; i < spawnCount; i++)
            {
                if (TryPlaceRandomPrefab(out GameObject go))
                {
                    // mark as dirty
                    Undo.RegisterCreatedObjectUndo(go, "Spawn Prefab");
                    spawned++;
                }
            }
            Debug.Log($"[PrefabPlaneSpawner] Editor spawn: {spawned} objects created.");
        }
        else
        {
            Spawn(spawnCount);
        }
    }
#endif
}
