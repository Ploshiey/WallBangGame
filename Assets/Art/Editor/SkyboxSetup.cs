using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Creates a panoramic (equirectangular) HDRI skybox material and wires it into
/// the open scene's lighting.
///
/// Uses Unity's built-in Skybox/Panoramic shader, which already handles latlong
/// mapping, exposure, rotation and the 180/360 and stereo layout cases — there
/// is nothing a hand-written skybox shader would add here.
/// </summary>
public static class SkyboxSetup
{
    const string k_Dir = "Assets/Art/Skybox";
    const string k_Path = k_Dir + "/M_SkyboxHDRI.mat";
    const string k_ShaderName = "Skybox/Panoramic";

    [MenuItem("Tools/WallBang/Create HDRI Skybox Material")]
    public static void CreateSkyboxMaterial()
    {
        var shader = Shader.Find(k_ShaderName);
        if (shader == null)
        {
            Debug.LogError($"SkyboxSetup: shader '{k_ShaderName}' not found in this Unity install.");
            return;
        }

        Directory.CreateDirectory(k_Dir);

        var mat = AssetDatabase.LoadAssetAtPath<Material>(k_Path);
        var isNew = mat == null;
        if (isNew)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, k_Path);
        }
        else
        {
            mat.shader = shader;
        }

        // Skybox/Panoramic's own defaults, set explicitly so a reused asset is
        // never left on a stale value.
        mat.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f));
        mat.SetFloat("_Exposure", 1.0f);
        mat.SetFloat("_Rotation", 0f);
        mat.SetFloat("_Mapping", 1f);       // Latitude Longitude Layout
        mat.SetFloat("_ImageType", 0f);     // 360 Degrees
        mat.SetFloat("_MirrorOnBack", 0f);
        mat.SetFloat("_Layout", 0f);        // None (non-stereo)

        // If an HDRI is selected in the Project window, drop it straight in.
        var selected = Selection.activeObject as Texture;
        if (selected != null && AssetDatabase.Contains(selected))
        {
            mat.SetTexture("_MainTex", selected);
            Debug.Log($"SkyboxSetup: assigned '{selected.name}' to the Spherical (HDR) slot.");
        }

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        // Wire it into the open scene: visible backdrop, ambient light source and
        // the default reflection source.
        RenderSettings.skybox = mat;
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        DynamicGI.UpdateEnvironment();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Selection.activeObject = mat;
        EditorGUIUtility.PingObject(mat);

        Debug.Log(
            $"SkyboxSetup: {(isNew ? "created" : "updated")} {k_Path} and set it as the skybox, " +
            "ambient source and reflection source for the open scene. " +
            (mat.GetTexture("_MainTex") == null
                ? "Drop your .hdr/.exr into its Spherical (HDR) slot."
                : "Save the scene to keep the lighting settings."));
    }
}
