using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Sets up Street Lights Pack 01 for voxels:
//  - Read/Write on all street light models
//  - each part gets VoxelScript with its own mesh, Use UV on, and the albedo from the part's original material
//  - each original (Built-in Standard) material gets a URP voxel copy based on Big Deez 3's settings,
//    with that material's own albedo (pole, glass, and the glowing bulb keeps its emission)
// Runs once automatically after this script compiles; rerun any time from Tools > Voxel Setup.
public static class StreetLightVoxelSetup
{
    const string Root = "Assets/Sat Productions/G-01/Street Lights Pack 01";
    const string VoxelMatFolder = Root + "/Materials/Voxel";
    const string VoxelizerPath = "Assets/Packages/Voxelizer/Shaders/Voxelizer.compute";
    const string TemplateMaterialPath = "Assets/Art/Materials/Big Deez 3.mat";
    const string RanKey = "WallBang.StreetLightVoxelSetup.Ran";

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

    [MenuItem("Tools/Voxel Setup/Apply To Street Lights")]
    public static void Run()
    {
        int readable = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root + "/Mesh" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(path) is ModelImporter mi && !mi.isReadable)
            {
                mi.isReadable = true;
                mi.SaveAndReimport();
                readable++;
            }
        }

        var voxelizer = AssetDatabase.LoadAssetAtPath<ComputeShader>(VoxelizerPath);
        var template = AssetDatabase.LoadAssetAtPath<Material>(TemplateMaterialPath);
        if (voxelizer == null || template == null)
        {
            Debug.LogError($"[VoxelSetup] Street Lights: missing asset. voxelizer={voxelizer} template={template}");
            return;
        }
        if (!AssetDatabase.IsValidFolder(VoxelMatFolder))
            AssetDatabase.CreateFolder(Root + "/Materials", "Voxel");

        var voxelMats = new Dictionary<Material, Material>();
        Material VoxelMaterialFor(Material original)
        {
            if (original == null) return null;
            if (voxelMats.TryGetValue(original, out var cached)) return cached;
            // Rerun: the part already has its voxel material, keep it.
            if (AssetDatabase.GetAssetPath(original).StartsWith(VoxelMatFolder)) return original;

            var path = $"{VoxelMatFolder}/M_{original.name}_Voxel.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(template) { name = $"M_{original.name}_Voxel" };
                AssetDatabase.CreateAsset(mat, path);
            }
            var albedo = original.GetTexture("_MainTex");
            mat.SetTexture("_BaseMap", albedo);
            mat.SetTexture("_MainTex", albedo);
            mat.SetColor("_BaseColor", original.HasProperty("_Color") ? original.GetColor("_Color") : Color.white);

            if (original.IsKeywordEnabled("_EMISSION"))
            {
                // Original glow is HDR 1024, far too bright for URP bloom; keep it white but tame.
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.white * 2f);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(mat);
            voxelMats[original] = mat;
            return mat;
        }

        int prefabs = 0, meshes = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Root + "/Prefabs" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;
            try
            {
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(mf) || mf.sharedMesh == null) continue;
                    var mr = mf.GetComponent<MeshRenderer>();
                    if (mr == null) continue;

                    var original = mr.sharedMaterials.Length > 0 ? mr.sharedMaterials[0] : null;
                    var voxelMat = VoxelMaterialFor(original);
                    if (voxelMat == null) continue;

                    var vs = mf.GetComponent<VoxelScript>() ?? mf.gameObject.AddComponent<VoxelScript>();
                    var so = new SerializedObject(vs);
                    so.FindProperty("voxelizer").objectReferenceValue = voxelizer;
                    so.FindProperty("mesh").objectReferenceValue = mf.sharedMesh;
                    so.FindProperty("useUV").boolValue = true;
                    so.FindProperty("texture").objectReferenceValue = voxelMat.GetTexture("_BaseMap");
                    so.ApplyModifiedPropertiesWithoutUndo();

                    var mats = new Material[mr.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = voxelMat;
                    mr.sharedMaterials = mats;
                    meshes++;
                    changed = true;
                }
                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    prefabs++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[VoxelSetup] Street Lights done. Models made readable: {readable}. Prefabs updated: {prefabs}. Meshes set up: {meshes}. Voxel materials: {voxelMats.Count}.");
    }
}
