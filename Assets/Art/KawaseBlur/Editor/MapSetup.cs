using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Sets M_Initialmap up the way the frosted material actually needs: opaque
/// ceramic on the map shell, frosted only on a couple of deliberate panels that
/// have geometry behind them.
///
/// Four of the five map pieces are exported from Maya with negative Y scale.
/// That mirrors them, and a mirrored transform turns the shading normal inward.
/// Until those are fixed with Freeze Transformations in Maya, this assigns a
/// _FlipNormals variant of the material to the mirrored renderers.
/// </summary>
public static class MapSetup
{
    const string k_MapAsset = "Assets/Art/M_Initialmap.fbx";
    const string k_ScenePath = "Assets/Scenes/MapFrosted.unity";
    const string k_MaterialDir = "Assets/Art/KawaseBlur/Materials";
    const string k_Ceramic = k_MaterialDir + "/M_Ceramic_White.mat";
    const string k_Frosted = k_MaterialDir + "/M_Frosted_Teal.mat";

    [MenuItem("Tools/WallBang/Setup Map Scene")]
    public static void SetupMapScene()
    {
        var map = AssetDatabase.LoadAssetAtPath<GameObject>(k_MapAsset);
        if (map == null)
        {
            Debug.LogError($"MapSetup: {k_MapAsset} not found.");
            return;
        }

        var ceramic = EnsureMaterial(k_Ceramic);
        var frosted = EnsureMaterial(k_Frosted);
        if (ceramic == null || frosted == null)
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // An empty scene has black ambient, which would make the ceramic's
        // unlit side read as black rather than white.
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.42f, 0.43f, 0.47f);

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(map);
        instance.name = map.name;

        var mirrored = ApplyMaterial(instance.GetComponentsInChildren<MeshRenderer>(true), ceramic);

        var bounds = GetBounds(instance);
        var camera = BuildCamera(bounds, out var viewDir);
        BuildLight();
        BuildFrostedPanels(bounds, viewDir, frosted);

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, k_ScenePath);
        AssetDatabase.SaveAssets();

        Debug.Log($"MapSetup: created {k_ScenePath}. Camera framed on bounds {bounds.size}.");
        if (mirrored.Count > 0)
        {
            Debug.LogWarning(
                $"MapSetup: {mirrored.Count} mirrored piece(s) given the flipped-normal material: " +
                string.Join(", ", mirrored) +
                ". The real fix is Freeze Transformations in Maya and a re-export; " +
                "after that, switch them back to the plain material.");
        }

        Selection.activeObject = camera.gameObject;
    }

    [MenuItem("Tools/WallBang/Material/Apply Ceramic to Selection")]
    public static void ApplyCeramicToSelection() => ApplyToSelection(k_Ceramic, "ceramic");

    [MenuItem("Tools/WallBang/Material/Apply Frosted to Selection")]
    public static void ApplyFrostedToSelection() => ApplyToSelection(k_Frosted, "frosted");

    static void ApplyToSelection(string materialPath, string label)
    {
        var mat = EnsureMaterial(materialPath);
        if (mat == null)
            return;

        var renderers = new List<MeshRenderer>();
        foreach (var go in Selection.gameObjects)
            renderers.AddRange(go.GetComponentsInChildren<MeshRenderer>(true));

        if (renderers.Count == 0)
        {
            Debug.LogWarning("MapSetup: nothing with a MeshRenderer is selected.");
            return;
        }

        var mirrored = ApplyMaterial(renderers.ToArray(), mat);
        Debug.Log($"MapSetup: applied {label} to {renderers.Count} renderer(s)" +
                  (mirrored.Count > 0 ? $", {mirrored.Count} of them flipped ({string.Join(", ", mirrored)})." : "."));
    }

    /// <summary>
    /// Assigns the material, swapping in a _FlipNormals variant for any renderer
    /// whose transform has a negative determinant. Returns the mirrored names.
    /// </summary>
    static List<string> ApplyMaterial(MeshRenderer[] renderers, Material material)
    {
        var flipped = GetFlippedVariant(material);
        var mirrored = new List<string>();

        foreach (var renderer in renderers)
        {
            var isMirrored = renderer.transform.localToWorldMatrix.determinant < 0f;
            if (isMirrored)
                mirrored.Add(renderer.name);

            var chosen = isMirrored ? flipped : material;
            var slots = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
            for (var i = 0; i < slots.Length; i++)
                slots[i] = chosen;

            Undo.RecordObject(renderer, "Apply Blur Material");
            renderer.sharedMaterials = slots;
            EditorUtility.SetDirty(renderer);
        }

        return mirrored;
    }

    static Material GetFlippedVariant(Material source)
    {
        var sourcePath = AssetDatabase.GetAssetPath(source);
        if (string.IsNullOrEmpty(sourcePath))
            return source;

        var flippedPath = sourcePath.Substring(0, sourcePath.Length - ".mat".Length) + "_Flipped.mat";
        var flipped = AssetDatabase.LoadAssetAtPath<Material>(flippedPath);
        if (flipped == null)
        {
            flipped = new Material(source);
            AssetDatabase.CreateAsset(flipped, flippedPath);
        }
        else
        {
            flipped.CopyPropertiesFromMaterial(source);
        }

        flipped.SetFloat("_FlipNormals", 1f);
        EditorUtility.SetDirty(flipped);
        return flipped;
    }

    static Material EnsureMaterial(string path)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null)
            return mat;

        BlurTestSceneBuilder.CreateSharedMaterials();
        mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
            Debug.LogError($"MapSetup: could not create {path}. Let Unity finish importing the shaders, then retry.");
        return mat;
    }

    static Bounds GetBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(root.transform.position, Vector3.one * 10f);

        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    /// <summary>Frames the whole map regardless of the FBX's 51x root scale.</summary>
    static Camera BuildCamera(Bounds bounds, out Vector3 viewDir)
    {
        const float fov = 55f;
        var radius = Mathf.Max(0.001f, bounds.extents.magnitude);
        var distance = radius / Mathf.Sin(Mathf.Deg2Rad * fov * 0.5f);

        viewDir = new Vector3(0.45f, 0.55f, -1f).normalized;
        var position = bounds.center + viewDir * distance;

        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(bounds.center - position));

        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.09f, 0.09f, 0.11f, 1f);
        cam.fieldOfView = fov;
        cam.nearClipPlane = Mathf.Max(0.05f, distance * 0.002f);
        cam.farClipPlane = distance * 5f;

        go.AddComponent<AudioListener>();
        return cam;
    }

    static void BuildLight()
    {
        var go = new GameObject("Directional Light");
        go.transform.rotation = Quaternion.Euler(42f, -35f, 0f);

        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.25f;
        light.shadows = LightShadows.Soft;
    }

    /// <summary>
    /// Two frosted panels between the camera and the map centre, so they have
    /// map geometry behind them — which is the whole point. Sized from the
    /// bounds; move them where you actually want them.
    /// </summary>
    static void BuildFrostedPanels(Bounds bounds, Vector3 viewDir, Material frosted)
    {
        var root = new GameObject("FrostedPanels");
        var radius = bounds.extents.magnitude;

        // Unity's Plane primitive is 10x10 units facing local +Y.
        var size = bounds.size.magnitude * 0.18f / 10f;
        var right = Vector3.Cross(Vector3.up, viewDir).normalized;

        for (var i = 0; i < 2; i++)
        {
            var offset = right * (radius * (i == 0 ? -0.30f : 0.30f));
            var position = bounds.center + viewDir * (radius * 0.35f) + offset;

            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = $"FrostedPanel_{i}";
            go.transform.SetParent(root.transform, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, viewDir);
            go.transform.localScale = new Vector3(size, 1f, size);

            go.GetComponent<Renderer>().sharedMaterial = frosted;
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }
    }
}
