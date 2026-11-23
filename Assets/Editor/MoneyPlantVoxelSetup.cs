using System.IO;
using UnityEditor;
using UnityEngine;

// Sets up the Money Plant (Megascans) for voxels:
//  - Read/Write on all plant models
//  - T_MoneyPlant_Albedo (base colour + opacity in alpha) imported with transparency
//  - M_MoneyPlant_Voxel: copy of Big Deez 3's settings, using the plant albedo with alpha clipping
//    so voxels that land on see-through parts of the leaf cards are cut away
//  - one voxel prefab per variation (A-F), built from the LOD0 mesh, with VoxelScript set up
// Runs once automatically after this script compiles; rerun any time from Tools > Voxel Setup.
public static class MoneyPlantVoxelSetup
{
    const string Root = "Assets/Art/MoneyPlant";
    const string AlbedoPath = Root + "/Textures/T_MoneyPlant_Albedo.png";
    const string MaterialPath = Root + "/M_MoneyPlant_Voxel.mat";
    const string PrefabFolder = Root + "/Prefabs";
    const string VoxelizerPath = "Assets/Packages/Voxelizer/Shaders/Voxelizer.compute";
    const string TemplateMaterialPath = "Assets/Art/Materials/Big Deez 3.mat";
    const string RanKey = "WallBang.MoneyPlantVoxelSetup.Ran";

    [InitializeOnLoadMethod]
    static void RunOnceAfterCompile()
    {
        if (EditorPrefs.GetBool(RanKey, false)) return;
        EditorApplication.delayCall += () =>
        {
            EditorPrefs.SetBool(RanKey, true);
            Run();
        };
    }

    [MenuItem("Tools/Voxel Setup/Apply To Money Plant")]
    public static void Run()
    {
        // Models: Read/Write on
        int readable = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(path) is ModelImporter mi && !mi.isReadable)
            {
                mi.isReadable = true;
                mi.SaveAndReimport();
                readable++;
            }
        }

        // Albedo: keep the packed opacity as alpha
        if (AssetImporter.GetAtPath(AlbedoPath) is TextureImporter ti)
        {
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = true;
            ti.SaveAndReimport();
        }
        var normalPath = Root + "/Textures/Money_Plant_uchmfdyia_Low_1K_Normal.jpg";
        if (AssetImporter.GetAtPath(normalPath) is TextureImporter ni && ni.textureType != TextureImporterType.NormalMap)
        {
            ni.textureType = TextureImporterType.NormalMap;
            ni.SaveAndReimport();
        }

        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath);
        var voxelizer = AssetDatabase.LoadAssetAtPath<ComputeShader>(VoxelizerPath);
        var template = AssetDatabase.LoadAssetAtPath<Material>(TemplateMaterialPath);
        if (albedo == null || voxelizer == null || template == null)
        {
            Debug.LogError($"[VoxelSetup] Money Plant: missing asset. albedo={albedo} voxelizer={voxelizer} template={template}");
            return;
        }

        // Material
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            mat = new Material(template) { name = "M_MoneyPlant_Voxel" };
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }
        mat.SetTexture("_BaseMap", albedo);
        mat.SetTexture("_MainTex", albedo);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_AlphaClip", 1f);
        mat.SetFloat("_Cutoff", 0.5f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.SetOverrideTag("RenderType", "TransparentCutout");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        EditorUtility.SetDirty(mat);

        // Prefabs: one per variation, from LOD0
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder(Root, "Prefabs");

        int prefabs = 0, meshes = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root + "/Models" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var file = Path.GetFileNameWithoutExtension(path);
            if (!file.EndsWith("_LOD0")) continue;
            var variant = file.Substring(file.IndexOf("_Var") + 4, 1); // A-F

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = (GameObject)Object.Instantiate(model);
            go.name = "MoneyPlant_Var" + variant;
            try
            {
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                {
                    var vs = mf.GetComponent<VoxelScript>() ?? mf.gameObject.AddComponent<VoxelScript>();
                    var so = new SerializedObject(vs);
                    so.FindProperty("voxelizer").objectReferenceValue = voxelizer;
                    so.FindProperty("mesh").objectReferenceValue = mf.sharedMesh;
                    so.FindProperty("useUV").boolValue = true;
                    so.FindProperty("texture").objectReferenceValue = albedo;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    var mr = mf.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        var mats = new Material[Mathf.Max(1, mr.sharedMaterials.Length)];
                        for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                        mr.sharedMaterials = mats;
                    }
                    meshes++;
                }
                PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabFolder}/{go.name}.prefab");
                prefabs++;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[VoxelSetup] Money Plant done. Models made readable: {readable}. Prefabs: {prefabs}. Meshes set up: {meshes}.");
    }
}
