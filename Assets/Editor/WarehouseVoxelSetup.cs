using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// One-click setup for the SciFi Warehouse Kit:
//  - turns on Read/Write for every model in the kit
//  - adds VoxelScript to every mesh in every kit prefab, pointing at that object's own mesh,
//    with Use UV on and UniversalBoy1 as the texture
//  - sets every material slot on those meshes to "Big Deez 3"
// Runs once automatically after this script compiles; rerun any time from Tools > Voxel Setup.
public static class WarehouseVoxelSetup
{
    const string KitFolder = "Assets/SciFi Warehouse Kit";
    const string VoxelizerPath = "Assets/Packages/Voxelizer/Shaders/Voxelizer.compute";
    const string TexturePath = "Assets/UniversalBoy1.png";
    const string MaterialPath = "Assets/Art/Materials/Big Deez 3.mat";
    const string RanKey = "WallBang.WarehouseVoxelSetup.Ran";

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

    [MenuItem("Tools/Voxel Setup/Apply To SciFi Warehouse Kit")]
    public static void Run()
    {
        var voxelizer = AssetDatabase.LoadAssetAtPath<ComputeShader>(VoxelizerPath);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (voxelizer == null || texture == null || material == null)
        {
            Debug.LogError($"[VoxelSetup] Missing asset. voxelizer={voxelizer} texture={texture} material={material}");
            return;
        }

        // 1. Read/Write on every model in the kit
        int readable = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { KitFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is ModelImporter mi && !mi.isReadable)
                {
                    mi.isReadable = true;
                    mi.SaveAndReimport();
                    readable++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        // 2 + 3. VoxelScript + material on every mesh owned by each kit prefab
        int prefabs = 0, meshes = 0;
        var skipped = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { KitFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;
            try
            {
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    // Meshes inside nested prefabs are set up in their own source prefab.
                    if (PrefabUtility.IsPartOfPrefabInstance(mf)) continue;
                    if (mf.sharedMesh == null) { skipped.Add($"{path} ({mf.name}: no mesh)"); continue; }

                    var vs = mf.GetComponent<VoxelScript>() ?? mf.gameObject.AddComponent<VoxelScript>();
                    var so = new SerializedObject(vs);
                    so.FindProperty("voxelizer").objectReferenceValue = voxelizer;
                    so.FindProperty("mesh").objectReferenceValue = mf.sharedMesh;
                    so.FindProperty("useUV").boolValue = true;
                    so.FindProperty("texture").objectReferenceValue = texture;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    var mr = mf.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        var mats = new Material[Mathf.Max(1, mr.sharedMaterials.Length)];
                        for (int i = 0; i < mats.Length; i++) mats[i] = material;
                        mr.sharedMaterials = mats;
                    }
                    meshes++;
                    changed = true;
                }
                foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (!PrefabUtility.IsPartOfPrefabInstance(smr))
                        skipped.Add($"{path} ({smr.name}: skinned mesh, VoxelScript needs a MeshFilter)");

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
        Debug.Log($"[VoxelSetup] Done. Models made readable: {readable}. Prefabs updated: {prefabs}. Meshes set up: {meshes}.");
        foreach (var s in skipped) Debug.LogWarning($"[VoxelSetup] Skipped {s}");
    }
}
