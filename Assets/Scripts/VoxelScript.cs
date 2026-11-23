using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelSystem;
using static UnityEngine.UIElements.UxmlAttributeDescription;

public class VoxelScript : MonoBehaviour
{
    [SerializeField] private ComputeShader voxelizer;
    [SerializeField] private Mesh mesh;
    [SerializeField] private bool useUV;
    [SerializeField] private Texture2D texture;

    // Start is called before the first frame update
    void Start()
    {
        GPUVoxelData data = GPUVoxelizer.Voxelize(
        voxelizer,  // ComputeShader (Voxelizer.compute)
        mesh,       // a target mesh
        64,         // # of voxels for largest AABB bounds
        true        // flag to fill in volume or not; if set flag to false, sample a surface only
);

        // build voxel cubes integrated mesh
        GetComponent<MeshFilter>().sharedMesh = VoxelMesh.Build(data.GetData(), data.UnitLength, useUV);

        // build 3D texture represent a volume by voxels.
        RenderTexture volumeTexture = GPUVoxelizer.BuildTexture3D(
          voxelizer,
          data,
          texture,    // Texture2D to color voxels based on uv coordinates in voxels
          RenderTextureFormat.ARGBFloat,
          FilterMode.Bilinear
        );

        // need to release a voxel buffer
        data.Dispose();

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
