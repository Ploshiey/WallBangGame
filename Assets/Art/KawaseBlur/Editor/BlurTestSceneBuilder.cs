using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Builds a scene for eyeballing the KawaseBlur renderer feature and the
/// Custom/FrostedGlass material: colourful geometry, then overlapping frosted
/// panels and a cube in front of it, so frosted-over-frosted is visible.
/// </summary>
public static class BlurTestSceneBuilder
{
    const string k_ScenePath = "Assets/Scenes/BlurTest.unity";
    const string k_MaterialDir = "Assets/Art/KawaseBlur/TestMaterials";
    const string k_PlainBlurMaterial = "Assets/Art/KawaseBlur/UnlitBlur.mat";
    const string k_FrostedShader = "Custom/FrostedGlass";
    const string k_CeramicShader = "Custom/SoftCeramic";
    const string k_SharedMaterialDir = "Assets/Art/KawaseBlur/Materials";

    /// <summary>
    /// Creates the reusable materials on their own, without touching the open
    /// scene — for dropping straight onto map geometry.
    /// </summary>
    [MenuItem("Tools/WallBang/Create Frosted + Ceramic Materials")]
    public static void CreateSharedMaterials()
    {
        Directory.CreateDirectory(k_SharedMaterialDir);

        MakeFrosted($"{k_SharedMaterialDir}/M_Frosted_Pink.mat", new Color(1.00f, 0.72f, 0.66f), 0.40f, 2.5f);
        MakeFrosted($"{k_SharedMaterialDir}/M_Frosted_Teal.mat", new Color(0.45f, 0.82f, 0.82f), 0.42f, 2.0f);
        MakeFrosted($"{k_SharedMaterialDir}/M_Frosted_Lilac.mat", new Color(0.78f, 0.70f, 0.95f), 0.50f, 3.0f);

        MakeCeramic($"{k_SharedMaterialDir}/M_Ceramic_White.mat",
            new Color(0.94f, 0.93f, 0.92f), new Color(0.60f, 0.58f, 0.66f), new Color(1.00f, 0.72f, 0.35f));
        MakeCeramic($"{k_SharedMaterialDir}/M_Ceramic_Red.mat",
            new Color(0.80f, 0.27f, 0.24f), new Color(0.45f, 0.18f, 0.22f), new Color(1.00f, 0.55f, 0.30f));

        AssetDatabase.SaveAssets();
        Debug.Log($"BlurTest: materials written to {k_SharedMaterialDir}.");
    }

    [MenuItem("Tools/WallBang/Create Kawase Blur Test Scene")]
    public static void Build()
    {
        if (Shader.Find(k_FrostedShader) == null)
        {
            Debug.LogError($"BlurTest: shader '{k_FrostedShader}' not found — let Unity finish importing, then retry.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        BuildCamera();
        BuildLight();
        BuildBackdrop();
        BuildCeramicObjects();
        BuildFrostedObjects();
        BuildComparisonPanel();

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, k_ScenePath);
        AssetDatabase.SaveAssets();

        Debug.Log($"BlurTest: created {k_ScenePath}.");
        ReportSetup();
    }

    static void BuildCamera()
    {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        go.transform.SetPositionAndRotation(new Vector3(0f, 0f, -9f), Quaternion.identity);

        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.10f, 1f);
        cam.fieldOfView = 55f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 100f;

        go.AddComponent<UniversalAdditionalCameraData>();
        go.AddComponent<AudioListener>();
    }

    static void BuildLight()
    {
        var go = new GameObject("Directional Light");
        go.transform.rotation = Quaternion.Euler(42f, -35f, 0f);

        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.3f;
        light.shadows = LightShadows.Soft;

        go.AddComponent<UniversalAdditionalLightData>();
    }

    /// <summary>
    /// Saturated primitives with high-contrast edges and thin gaps. A flat wall
    /// would not show the blur at all.
    /// </summary>
    static void BuildBackdrop()
    {
        var root = new GameObject("Backdrop");

        var colors = new[]
        {
            new Color(0.95f, 0.26f, 0.21f),
            new Color(0.30f, 0.69f, 0.31f),
            new Color(0.13f, 0.59f, 0.95f),
            new Color(1.00f, 0.76f, 0.03f),
            new Color(0.61f, 0.15f, 0.69f),
            new Color(0.00f, 0.74f, 0.83f),
        };

        var shapes = new[] { PrimitiveType.Cube, PrimitiveType.Sphere, PrimitiveType.Capsule };

        var mats = new List<Material>();
        for (var i = 0; i < colors.Length; i++)
            mats.Add(MakeLitMaterial($"BlurTest_{i}", colors[i]));

        var index = 0;
        for (var row = -2; row <= 2; row++)
        {
            for (var col = -3; col <= 3; col++)
            {
                var go = GameObject.CreatePrimitive(shapes[index % shapes.Length]);
                go.name = $"Shape_{row}_{col}";
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(col * 1.35f, row * 1.35f, 6f + (index % 3) * 1.2f);
                go.transform.rotation = Quaternion.Euler(index * 17f, index * 31f, index * 11f);
                go.transform.localScale = Vector3.one * 0.85f;

                go.GetComponent<Renderer>().sharedMaterial = mats[index % mats.Count];
                Object.DestroyImmediate(go.GetComponent<Collider>());
                index++;
            }
        }
    }

    /// <summary>
    /// Opaque ceramic forms sitting between the backdrop and the frosted panels.
    /// These are the only objects here that look right on their own — they need
    /// no blur behind them — and they give the frosted panels something with
    /// real shape to blur.
    /// </summary>
    static void BuildCeramicObjects()
    {
        var root = new GameObject("Ceramic");

        var white = MakeCeramicMaterial("Ceramic_White",
            new Color(0.94f, 0.93f, 0.92f), new Color(0.60f, 0.58f, 0.66f), new Color(1.00f, 0.72f, 0.35f));
        var red = MakeCeramicMaterial("Ceramic_Red",
            new Color(0.80f, 0.27f, 0.24f), new Color(0.45f, 0.18f, 0.22f), new Color(1.00f, 0.55f, 0.30f));

        var shapes = new[]
        {
            (PrimitiveType.Sphere, new Vector3(-2.6f, 0.6f, 3.0f), 1.9f, white),
            (PrimitiveType.Capsule, new Vector3(0.4f, -0.4f, 2.6f), 1.5f, red),
            (PrimitiveType.Sphere, new Vector3(2.7f, 1.1f, 3.4f), 1.3f, white),
        };

        foreach (var (shape, pos, scale, mat) in shapes)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = $"Ceramic_{shape}";
            go.transform.SetParent(root.transform, false);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }
    }

    /// <summary>
    /// A large panel, a smaller panel crossing it, and a cube in front of both —
    /// so every pair of frosted surfaces overlaps somewhere on screen.
    /// </summary>
    static void BuildFrostedObjects()
    {
        var root = new GameObject("Frosted");

        var pink = MakeFrostedMaterial("Frosted_Pink", new Color(1.00f, 0.72f, 0.66f), 0.40f, 2.5f);
        var teal = MakeFrostedMaterial("Frosted_Teal", new Color(0.45f, 0.82f, 0.82f), 0.42f, 2.0f);
        var lilac = MakeFrostedMaterial("Frosted_Lilac", new Color(0.78f, 0.70f, 0.95f), 0.50f, 3.0f);

        // Unity's Plane primitive is 10x10 in XZ facing +Y, so stand it up with -90 on X.
        MakePanel(root, "Panel_Pink", pink,
            new Vector3(-0.6f, 0.2f, 1.5f), Quaternion.Euler(-90f, 0f, 0f), new Vector3(0.50f, 1f, 0.34f));

        MakePanel(root, "Panel_Teal", teal,
            new Vector3(1.6f, -0.9f, 0.2f), Quaternion.Euler(-90f, -18f, 0f), new Vector3(0.30f, 1f, 0.22f));

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Frosted_Cube";
        cube.transform.SetParent(root.transform, false);
        cube.transform.SetPositionAndRotation(new Vector3(-1.4f, -0.6f, -2.5f), Quaternion.Euler(0f, 28f, 0f));
        cube.transform.localScale = Vector3.one * 2.4f;
        cube.GetComponent<Renderer>().sharedMaterial = lilac;
        Object.DestroyImmediate(cube.GetComponent<Collider>());
    }

    /// <summary>
    /// The original UnlitBlur material, for side-by-side: no tint, no rim, no
    /// thickness — which is why it vanishes into whatever is behind it.
    /// </summary>
    static void BuildComparisonPanel()
    {
        var plain = AssetDatabase.LoadAssetAtPath<Material>(k_PlainBlurMaterial);
        if (plain == null)
        {
            Debug.LogWarning($"BlurTest: {k_PlainBlurMaterial} not found, skipping comparison panel.");
            return;
        }

        MakePanel(null, "Panel_PlainUnlitBlur", plain,
            new Vector3(3.4f, 1.4f, 0.8f), Quaternion.Euler(-90f, 0f, 0f), new Vector3(0.22f, 1f, 0.22f));
    }

    static void MakePanel(GameObject parent, string name, Material mat, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
        go.name = name;
        if (parent != null)
            go.transform.SetParent(parent.transform, false);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(go.GetComponent<Collider>());
    }

    static Material MakeCeramicMaterial(string name, Color baseColor, Color shadowColor, Color rimColor)
    {
        return MakeCeramic($"{k_MaterialDir}/{name}.mat", baseColor, shadowColor, rimColor);
    }

    static Material MakeCeramic(string path, Color baseColor, Color shadowColor, Color rimColor)
    {
        var mat = LoadOrCreateAt(path, k_CeramicShader);
        if (mat == null)
            return null;

        mat.SetColor("_BaseColor", baseColor);
        mat.SetColor("_ShadowColor", shadowColor);
        mat.SetFloat("_WrapAmount", 0.55f);
        mat.SetFloat("_AmbientStrength", 1f);
        mat.SetFloat("_Smoothness", 0.90f);
        mat.SetColor("_SpecularTint", Color.white);
        mat.SetFloat("_SpecularStrength", 1.4f);
        mat.SetColor("_RimColor", rimColor);
        mat.SetFloat("_RimPower", 4f);
        mat.SetFloat("_RimStrength", 0.55f);
        mat.SetColor("_TranslucencyColor", new Color(1f, 0.55f, 0.40f));
        mat.SetFloat("_TranslucencyPower", 5f);
        mat.SetFloat("_TranslucencyStrength", 0.35f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material MakeFrostedMaterial(string name, Color tint, float milkiness, float thicknessDistance)
    {
        return MakeFrosted($"{k_MaterialDir}/{name}.mat", tint, milkiness, thicknessDistance);
    }

    static Material MakeFrosted(string path, Color tint, float milkiness, float thicknessDistance)
    {
        var mat = LoadOrCreateAt(path, k_FrostedShader);
        if (mat == null)
            return null;

        mat.SetColor("_BaseColor", tint);
        mat.SetFloat("_TintStrength", 0.8f);
        mat.SetFloat("_Milkiness", milkiness);
        mat.SetFloat("_Opacity", 1f);
        mat.SetFloat("_UseThickness", 1f);
        mat.EnableKeyword("_THICKNESS_ON");
        mat.SetFloat("_ThicknessDistance", thicknessDistance);
        mat.SetFloat("_ThicknessStrength", 0.6f);
        mat.SetColor("_FresnelColor", Color.white);
        mat.SetFloat("_FresnelPower", 3f);
        mat.SetFloat("_FresnelStrength", 0.35f);
        mat.SetFloat("_LightInfluence", 0.35f);
        mat.SetFloat("_Smoothness", 0.75f);
        mat.SetFloat("_SpecularStrength", 0.35f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material MakeLitMaterial(string name, Color color)
    {
        var mat = LoadOrCreateAt($"{k_MaterialDir}/{name}.mat", "Universal Render Pipeline/Lit");
        if (mat == null)
            return null;

        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", 0.2f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material LoadOrCreateAt(string path, string shaderName)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null)
            return mat;

        var shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogError($"BlurTest: shader '{shaderName}' not found.");
            return null;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        mat = new Material(shader);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    /// <summary>
    /// Prints the things that break this silently: a shader that did not compile,
    /// the feature missing from the renderer the active quality level uses,
    /// copyToFramebuffer swallowing _blurTexture, and the depth texture being off.
    /// </summary>
    static void ReportSetup()
    {
        var frosted = Shader.Find(k_FrostedShader);
        if (frosted != null && ShaderUtil.ShaderHasError(frosted))
            Debug.LogError($"BlurTest: '{k_FrostedShader}' has compile errors — frosted objects will render magenta.");

        if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp))
        {
            Debug.LogError("BlurTest: the active render pipeline is not URP.");
            return;
        }

        Debug.Log($"BlurTest: active URP asset is '{urp.name}' (quality level '{QualitySettings.names[QualitySettings.GetQualityLevel()]}').");

        if (!urp.supportsCameraDepthTexture)
            Debug.LogWarning($"BlurTest: '{urp.name}' has Depth Texture off, so FrostedGlass thickness will not work. Turn it on, or untick Use Depth Thickness on the materials.");

        var found = false;
        var so = new SerializedObject(urp);
        var dataList = so.FindProperty("m_RendererDataList");
        for (var i = 0; i < dataList.arraySize; i++)
        {
            var data = dataList.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererData;
            if (data == null)
                continue;

            foreach (var feature in data.rendererFeatures)
            {
                if (!(feature is KawaseBlur blur))
                    continue;

                found = true;
                if (!blur.isActive)
                    Debug.LogWarning($"BlurTest: KawaseBlur on '{data.name}' is disabled.");
                if (blur.settings.blurMaterial == null)
                    Debug.LogError($"BlurTest: KawaseBlur on '{data.name}' has no blur material assigned.");
                if (blur.settings.copyToFramebuffer)
                    Debug.LogError($"BlurTest: KawaseBlur on '{data.name}' has copyToFramebuffer ON, so it never sets the global '{blur.settings.targetName}'. Turn it off.");

                Debug.Log($"BlurTest: KawaseBlur found on '{data.name}' — {blur.settings.blurPasses} passes, downsample {blur.settings.downsample}, event {blur.settings.renderPassEvent}, target '{blur.settings.targetName}'.");
            }
        }

        if (!found)
            Debug.LogError($"BlurTest: no KawaseBlur feature on any renderer used by '{urp.name}'. Add it to that asset's renderer.");
    }
}
