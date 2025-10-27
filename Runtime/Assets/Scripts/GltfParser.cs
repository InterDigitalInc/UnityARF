//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Interdigital {
namespace Gltf2 {

public class GltfParser
{
    public static GltfParser Load(string filePath, AssetCache cache = null) {
        Gltf gltf = Gltf.Load(filePath);
        return new GltfParser(gltf, cache);
    }

    Gltf gltf;
    AssetCache cache;

    public GltfParser(Gltf gltf, AssetCache cache = null) { 
        this.gltf = gltf; 
        this.cache = cache;
    }

    public void Dispose()
    {
        if (gltf != null) { 
            gltf.Dispose();
            gltf = null;
        }
    }
    ~GltfParser() { Dispose(); }

    public Interdigital.Gltf2.Mesh GetFirstMesh(Node node)
    {
        if (node.HasMesh()) {
            return node.mesh;
        }
        if (node.HasChildren()) {
            foreach (Node child in node.children) {
                Interdigital.Gltf2.Mesh mesh = GetFirstMesh(child);
                if (mesh != null) {
                    return mesh;
                }
            }
        }
        return null;
    }

    public Interdigital.Gltf2.Mesh GetFirstMesh()
    {
        Scene scene = gltf.scene;
        if (!scene.HasNodes()) {
            return null;
        }

        foreach (Node node in scene.nodes) {
            Interdigital.Gltf2.Mesh mesh = GetFirstMesh(node);
            if (mesh != null) {
                return mesh;
            }
        }
        return null;
    }

    public void SetMesh(SkinnedMeshRenderer renderer, Interdigital.Gltf2.Mesh mesh)
    {
        if (mesh.primitives.Count > 1) {
            Debug.LogWarning("glTF meshes with more than one primitive are not supported; only the first one is used");
        }

        // Parse primitive
        Primitive primitive = mesh.primitives[0];
        UnityEngine.Mesh unityMesh = new UnityEngine.Mesh();
        if (cache != null) {
            cache.AddMesh($"{mesh.GetPropertyIndex():D4}", unityMesh);
        }
        unityMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; 

        // Vertices
        DataTree vertices = primitive.GetAttribute("POSITION").GetTensor();
        long vertexCount = vertices.GetTensorSize(0);
        float[] vertexValues = vertices.GetValues<float>();
        Vector3[] unityVertices = new Vector3[vertexCount];
        for (long vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++) {
            unityVertices[vertexIndex] = new Vector3(
                -vertexValues[3*vertexIndex + 0],
                vertexValues[3*vertexIndex + 1],
                vertexValues[3*vertexIndex + 2]
            );
        }
        unityMesh.vertices = unityVertices;

        // Faces
        DataTree indices = primitive.indices.GetTensor();
        long indexCount = indices.GetTensorSize(0);
        uint[] indexValues = indices.GetValues<uint>();
        int[] unityTriangles = new int[indexCount];
        for (long i = 0; i < indexCount / 3; i++) {
            unityTriangles[3 * i + 0] = (int)indexValues[3 * i + 2];
            unityTriangles[3 * i + 1] = (int)indexValues[3 * i + 1];
            unityTriangles[3 * i + 2] = (int)indexValues[3 * i + 0];
        }
        unityMesh.triangles = unityTriangles;
        unityMesh.RecalculateNormals();

        // Uvs
        DataTree uvs = primitive.GetAttribute("TEXCOORD_0").GetTensor();
        long uvCount = uvs.GetTensorSize(0);
        float[] uvValues = uvs.GetValues<float>();
        Vector2[] unityUvs = new Vector2[uvCount];
        for (long vertexIndex = 0; vertexIndex < uvCount; vertexIndex++) {
            unityUvs[vertexIndex] = new Vector2(
                uvValues[2*vertexIndex + 0],
                uvValues[2*vertexIndex + 1]
            );
        }
        unityMesh.uv = unityUvs;

        // Texture
        Interdigital.Gltf2.Material material = primitive.material;
        TextureInfo baseColorTexture = material.pbrMetallicRoughness.baseColorTexture;
        Interdigital.Gltf2.Texture texture = baseColorTexture.index;
        DataTree image = texture.source.GetImage();
        DataTree pixels = image["pixels"];
        int width = (int)pixels.GetTensorSize(0);
        int height = (int)pixels.GetTensorSize(1);
        TextureFormat textureFormat;
        int pixelFormat = (int)image["pixelFormat"].GetInteger();
        if (pixelFormat == (int)PixelFormat.RGB) {
            textureFormat = TextureFormat.RGB24;
        }
        else if (pixelFormat == (int)PixelFormat.RGBA) {
            textureFormat = TextureFormat.RGBA32;
        }
        else {  
            throw new Exception($"Invalid or unsupported pixel format {pixelFormat}");
        }
        UnityEngine.Material unityMaterial = new UnityEngine.Material(Shader.Find("Standard"));
        if (cache != null) {
            cache.AddMaterial($"{material.GetPropertyIndex():D4}", unityMaterial);
        }
        Texture2D unityTexture = new Texture2D(width, height, textureFormat, false);
        if (cache != null) {
            cache.AddTexture($"{texture.GetPropertyIndex():D4}", unityTexture);
        }
        byte[] bytes = pixels.GetValues<byte>();
        unityTexture.LoadRawTextureData(bytes);
        unityTexture.Apply();
        unityMaterial.mainTexture = unityTexture;

        if (Application.isPlaying) {
            renderer.material = unityMaterial;
        }
        else {
            renderer.sharedMaterial = unityMaterial;
        }

        renderer.sharedMesh = unityMesh;
    }

    public void SetPrimitiveSkinWeights(UnityEngine.Mesh unityMesh, Interdigital.Gltf2.Primitive primitive)
    {
        DataTree jointIndices, jointWeights;
        long jointPerVertex = 4;
        int vertexCount = unityMesh.vertexCount;
        (jointIndices, jointWeights) = primitive.GetAttributesJointsTensor(jointPerVertex, jointPerVertex);
        if (vertexCount != jointIndices.GetTensorSize(0) || jointPerVertex != jointIndices.GetTensorSize(1)) {
            throw new SystemException("Invalid joint indices");
        }
        if (vertexCount != jointWeights.GetTensorSize(0) || jointPerVertex != jointWeights.GetTensorSize(1)) {
            throw new SystemException("Invalid joint weights");
        }
        byte[] jointIndicesData = jointIndices.GetValues<byte>();
        float[] jointWeightsData = jointWeights.GetValues<float>();
        BoneWeight[] boneWeights = new BoneWeight[vertexCount];
        for (long vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++) 
        {
            boneWeights[vertexIndex].boneIndex0 = jointIndicesData[vertexIndex * jointPerVertex + 0];
            boneWeights[vertexIndex].boneIndex1 = jointIndicesData[vertexIndex * jointPerVertex + 1];
            boneWeights[vertexIndex].boneIndex2 = jointIndicesData[vertexIndex * jointPerVertex + 2];
            boneWeights[vertexIndex].boneIndex3 = jointIndicesData[vertexIndex * jointPerVertex + 3];

            boneWeights[vertexIndex].weight0 = jointWeightsData[vertexIndex * jointPerVertex + 0];
            boneWeights[vertexIndex].weight1 = jointWeightsData[vertexIndex * jointPerVertex + 1];
            boneWeights[vertexIndex].weight2 = jointWeightsData[vertexIndex * jointPerVertex + 2];
            boneWeights[vertexIndex].weight3 = jointWeightsData[vertexIndex * jointPerVertex + 3];
        } 
        unityMesh.boneWeights = boneWeights;
    }

    public void SetSkinnedMeshRenderer(Transform transform, long nodeId, bool withSkin = true)
    {
        Node node = gltf.nodes[nodeId];
        if (!node.HasMesh()) {
            return;
        }

        Interdigital.Gltf2.Mesh mesh = node.mesh;
        GameObject meshGO = new GameObject(mesh.name ?? "mesh");
        meshGO.transform.SetParent(transform);
        SkinnedMeshRenderer renderer = meshGO.AddComponent<SkinnedMeshRenderer>();
        SetMesh(renderer, mesh);

        if (withSkin && node.HasSkin()) { 
            SetPrimitiveSkinWeights(renderer.sharedMesh, mesh.primitives[0]);
        }

        // Skin
        if (withSkin)
        { 
            Transform[] bones = null;
            Matrix4x4[] bindPoses = null;
            if (node.HasSkin()) 
            {
                Skin skin = node.skin;
                long[] jointIds = skin.joints.GetValues();
                bones = new Transform[jointIds.Length];
                Dictionary<long, int> jointId2Idx = new Dictionary<long, int>();
                for (int jointIndex = 0; jointIndex < jointIds.Length; jointIndex++) 
                {
                    long jointId = jointIds[jointIndex];    
                    Node jointNode = gltf.nodes[jointId];
                    Transform bone = null;
                    if (jointNode.HasName()) {
                        bone = new GameObject(jointNode.name).transform;
                    }
                    else {
                        bone = new GameObject(String.Format("Bone{0}", jointIndex)).transform;
                    }
                    bone.parent = transform;
                    if (jointNode.HasMatrix()) 
                    {
                        Matrix4x4 mat = UnityConvert.ToMatrix4x4(jointNode.matrix);
                        Vector3 position = mat.GetColumn(3);
                        Vector3 scale = new Vector3(
                            mat.GetColumn(0).magnitude,
                            mat.GetColumn(1).magnitude,
                            mat.GetColumn(2).magnitude
                        );
                        Quaternion rotation = Quaternion.LookRotation(
                            mat.GetColumn(2).normalized,
                            mat.GetColumn(1).normalized
                        );
                        bone.localPosition = position;
                        bone.localRotation = rotation;
                        bone.localScale = scale;
                    }
                    else
                    { 
                        if (jointNode.HasTranslation()) {
                            bone.localPosition = UnityConvert.ToTranslation(jointNode.translation);
                        }
                        else {
                            bone.localPosition = Vector3.zero;
                        }
                        if (jointNode.HasScale()) {
                            bone.localScale = UnityConvert.ToScale(jointNode.scale);
                        }
                        if (jointNode.HasRotation()) {
                            bone.localRotation = UnityConvert.ToRotation(jointNode.rotation);
                        }
                        else {
                            bone.localRotation = Quaternion.identity;
                        }
                    }
                    bones[jointIndex] = bone;
                    jointId2Idx.Add(jointId, jointIndex);
                }
                for (int jointIndex = 0; jointIndex < jointIds.Length; jointIndex++) 
                {
                    long jointId = jointIds[jointIndex];    
                    Node jointNode = gltf.nodes[jointId];
                    if (jointNode.HasChildren()) {
                        foreach (long childId in jointNode.children.GetValues()) {
                            int childIndex = jointId2Idx[childId];
                            bones[childIndex].SetParent(bones[jointIndex], false);
                        }
                    }
                }

                if (skin.HasInverseBindMatrices()) 
                {
                    DataTree ibm = skin.inverseBindMatrices.GetTensor();
                    float[] ibmData = ibm.GetValues<float>();
                    bindPoses = new Matrix4x4[jointIds.Length];
                    for (int jointIndex = 0; jointIndex < jointIds.Length; jointIndex++) {
                        bindPoses[jointIndex] = UnityConvert.ToMatrix4x4(ibmData, jointIndex * 16);
                        //if (jointIndex < 10) Debug.Log(bindPoses[jointIndex]);
                    }
                }

                renderer.sharedMesh.bindposes = bindPoses;
                renderer.bones = bones;
            }
        }

        //UnityConvert.saveVector3Ds("C:\\temp\\unity\\vertices-gltf.txt", renderer.sharedMesh.vertices);
        //UnityConvert.saveBoneWeights("C:\\temp\\unity\\boneWeights-gltf.txt", renderer.sharedMesh.boneWeights);
        //UnityConvert.saveMatrix4x4s("C:\\temp\\unity\\ibm-gltf.txt", renderer.sharedMesh.bindposes);

    }

}

}
}
