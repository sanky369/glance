using UnityEngine;

[ExecuteAlways]
public class EdgeLine : MonoBehaviour
{
    [Header("Line Settings")]
    public Color glowColor = new Color(0f, 0.8f, 1f, 1f);

    [Range(0.001f, 0.1f)]
    public float lineThickness = 0.01f;

    [Header("Glow Settings")]
    [Range(0.1f, 5f)]
    public float glowIntensity = 1.5f;

    [Range(1, 6)]
    public int glowLayers = 3;

    [Range(1f, 4f)]
    public float glowThicknessMultiplier = 2f;

    [Header("Animation")]
    public bool animatePulse = true;

    [Range(0.1f, 5f)]
    public float pulseSpeed = 1.5f;

    [Range(0f, 1f)]
    public float pulseMinIntensity = 0.4f;

    Material glowMaterial;
    Mesh edgeMesh;

    // cached to stop GC
    int cachedGlowLayers = -1;
    Vector3[] worldVerts = new Vector3[8];
    Vector3[] verts;
    Color[] colors;
    int[] tris;

    static readonly Vector3[] UnitCubeVertices = new Vector3[]
    {
        new Vector3(-0.5f, -0.5f, -0.5f),
        new Vector3( 0.5f, -0.5f, -0.5f),
        new Vector3( 0.5f,  0.5f, -0.5f),
        new Vector3(-0.5f,  0.5f, -0.5f),
        new Vector3(-0.5f, -0.5f,  0.5f),
        new Vector3( 0.5f, -0.5f,  0.5f),
        new Vector3( 0.5f,  0.5f,  0.5f),
        new Vector3(-0.5f,  0.5f,  0.5f),
    };

    static readonly int[,] Edges = new int[,]
    {
        {0,1}, {1,2}, {2,3}, {3,0},
        {4,5}, {5,6}, {6,7}, {7,4},
        {0,4}, {1,5}, {2,6}, {3,7},
    };

    void OnEnable()
    {
        CreateGlowMaterial();
        edgeMesh = new Mesh
        {
            name = "EdgeLine Mesh",
            hideFlags = HideFlags.HideAndDontSave
        };
        // Ensure arrays are sized correctly on startup
        UpdateArraySizes();
    }

    void OnDisable()
    {
        if (glowMaterial != null)
        {
            if (Application.isPlaying) Destroy(glowMaterial);
            else DestroyImmediate(glowMaterial);
            glowMaterial = null;
        }
        if (edgeMesh != null)
        {
            if (Application.isPlaying) Destroy(edgeMesh);
            else DestroyImmediate(edgeMesh);
            edgeMesh = null;
        }
    }

    void CreateGlowMaterial()
    {
        if (glowMaterial != null) return;

        Shader shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null)
        {
            Debug.LogError("EdgeLine: Could not find 'Hidden/Internal-Colored' shader.");
            return;
        }

        glowMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        glowMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        glowMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        glowMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        glowMaterial.SetInt("_ZWrite", 0);
        glowMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
        glowMaterial.renderQueue = 3000;
    }

    void LateUpdate()
    {
        if (glowMaterial == null || edgeMesh == null) return;

        Camera cam = GetActiveCamera();
        if (cam == null) return;

        BuildMesh(cam);
        Graphics.DrawMesh(edgeMesh, Matrix4x4.identity, glowMaterial, gameObject.layer);
    }

    Camera GetActiveCamera()
    {
        if (Application.isPlaying)
            return Camera.main;

#if UNITY_EDITOR
        var sv = UnityEditor.SceneView.lastActiveSceneView;
        if (sv != null) return sv.camera;
#endif
        return null;
    }

    void UpdateArraySizes()
    {
        if (cachedGlowLayers == glowLayers && verts != null) return;

        cachedGlowLayers = glowLayers;
        int quadsPerLayer = 12 + 8;
        int totalQuads = quadsPerLayer * glowLayers;

        verts = new Vector3[totalQuads * 4];
        colors = new Color[totalQuads * 4];
        tris = new int[totalQuads * 6];
    }

    void BuildMesh(Camera cam)
    {
        // 1. Check if we need to resize arrays (e.g., if you changed glowLayers in the Inspector)
        UpdateArraySizes();

        Matrix4x4 localToWorld = transform.localToWorldMatrix;
        Vector3 camPos = cam.transform.position;
        Vector3 camRight = cam.transform.right;
        Vector3 camUp = cam.transform.up;

        // 2. Overwrite the cached worldVerts array instead of allocating a new one
        for (int i = 0; i < 8; i++)
            worldVerts[i] = localToWorld.MultiplyPoint3x4(UnitCubeVertices[i]);

        float pulse = 1f;
        if (animatePulse)
        {
            float t = (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
            pulse = Mathf.Lerp(pulseMinIntensity, 1f, t);
        }

        int vi = 0, ti = 0;

        for (int layer = 0; layer < glowLayers; layer++)
        {
            float layerNorm = (float)layer / Mathf.Max(glowLayers - 1, 1);
            float layerAlpha = 1f - layerNorm;
            layerAlpha *= layerAlpha;

            float thickness = lineThickness * Mathf.Lerp(1f, glowThicknessMultiplier, layerNorm);
            float half = thickness * 0.5f;

            Color c = glowColor * glowIntensity * pulse;
            c.a = glowColor.a * layerAlpha * (layer == 0 ? 0.9f : 0.4f);

            for (int i = 0; i < 12; i++)
            {
                Vector3 a = worldVerts[Edges[i, 0]];
                Vector3 b = worldVerts[Edges[i, 1]];

                Vector3 edgeDir = (b - a).normalized;
                Vector3 mid = (a + b) * 0.5f;
                Vector3 viewDir = (mid - camPos).normalized;
                Vector3 perp = Vector3.Cross(edgeDir, viewDir).normalized * half;

                if (perp.sqrMagnitude < 0.0001f)
                {
                    perp = Vector3.Cross(edgeDir, Vector3.up).normalized * half;
                    if (perp.sqrMagnitude < 0.0001f)
                        perp = Vector3.Cross(edgeDir, Vector3.right).normalized * half;
                }

                int baseV = vi;
                verts[vi] = a - perp; colors[vi] = c; vi++;
                verts[vi] = a + perp; colors[vi] = c; vi++;
                verts[vi] = b + perp; colors[vi] = c; vi++;
                verts[vi] = b - perp; colors[vi] = c; vi++;

                tris[ti++] = baseV;
                tris[ti++] = baseV + 1;
                tris[ti++] = baseV + 2;
                tris[ti++] = baseV;
                tris[ti++] = baseV + 2;
                tris[ti++] = baseV + 3;
            }

            Vector3 r = camRight * half;
            Vector3 u = camUp * half;

            for (int i = 0; i < 8; i++)
            {
                Vector3 p = worldVerts[i];
                int baseV = vi;

                verts[vi] = p - r - u; colors[vi] = c; vi++;
                verts[vi] = p + r - u; colors[vi] = c; vi++;
                verts[vi] = p + r + u; colors[vi] = c; vi++;
                verts[vi] = p - r + u; colors[vi] = c; vi++;

                tris[ti++] = baseV;
                tris[ti++] = baseV + 1;
                tris[ti++] = baseV + 2;
                tris[ti++] = baseV;
                tris[ti++] = baseV + 2;
                tris[ti++] = baseV + 3;
            }
        }

        // 3. Clear and assign the updated values from our cached arrays
        edgeMesh.Clear();
        edgeMesh.vertices = verts;
        edgeMesh.colors = colors;
        edgeMesh.triangles = tris;
    }
}