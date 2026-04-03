//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using Interdigital;
using Interdigital.Arf;
using Interdigital.Gltf2;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Interdigital {
namespace Arf {

public class ArfParser
{
    public static ArfParser Load(string filePath, AssetCache cache = null) {
        Arf arf = Arf.Load(filePath);
        return new ArfParser(arf, cache);
    }

    public class UnitySkeleton {
        public Transform[] bones;
        public Matrix4x4[] bindPoses;
        public UnitySkeleton(Transform[] bones, Matrix4x4[] bindPoses) {
            this.bones = bones;
            this.bindPoses = bindPoses;
        }
    }

    public Arf arf;
    public AssetCache cache;

    public ArfParser(Arf arf, AssetCache cache = null) { 
        this.arf = arf; 
        this.cache = cache;
    }

    public void Dispose()
    {
        if (arf != null) { 
            arf.Dispose();
            arf = null;
        }
    }
    ~ArfParser() { Dispose(); }

    public void Close()
    {
        arf.Close();
    }

    public GameObject createAvatar(Transform transform, 
                                   string lodName, 
                                   bool loadBlendshapes = true, 
                                   bool loadSkeletons = true, 
                                   bool loadSkins = true, 
                                   bool transposeNodeTransforms = true, 
                                   bool transposeInverseBindMatrices = true)
    {
        GameObject avatar = new GameObject(arf.metadata.name);
        if (transform != null) {
            avatar.transform.SetParent(transform);
        }
        avatar.AddComponent<ArfAvatar>();
        avatar.GetComponent<ArfAvatar>().Init(arf);
        
        
        Dictionary<long, UnitySkeleton> skeletons = new Dictionary<long, UnitySkeleton>();
        foreach (Interdigital.Arf.Asset asset in arf.structure.assets)
        {
            if (!asset.lods.HasByName(lodName)) {
                Debug.LogWarning($"No LoD {lodName} in asset {asset.name}");
                continue;
            }

            //Debug.Log($"Load Asset {asset.name}...");
            GameObject assetGO = new GameObject(asset.name);
            assetGO.transform.SetParent(avatar.transform);

            Lod lod = asset.lods.GetByName(lodName);

            if (loadSkeletons) { 
                foreach (Interdigital.Arf.Skeleton skeleton in lod.skeletons) {
                    if (!skeletons.ContainsKey(skeleton.id)) { 
                        skeletons[skeleton.id] = LoadSkeleton(
                            skeleton, avatar.transform, transposeNodeTransforms: transposeNodeTransforms, 
                            transposeInverseBindMatrices: transposeInverseBindMatrices
                        );
                    }
                }
            }

            for(long lodMeshIndex = 0; lodMeshIndex < lod.meshes.Count; lodMeshIndex++)
            {
                long meshId = lod.meshes.GetValue(lodMeshIndex);
                if (!arf.components.meshes.Has(meshId)) {
                    //Debug.LogWarning($"Invalid mesh id {meshId}");
                    continue;
                }
                Interdigital.Arf.Mesh mesh = arf.components.meshes[meshId];
                //Debug.Log($"Load Mesh {mesh.name}...");
                GameObject meshGO = new GameObject(mesh.name);
                meshGO.transform.SetParent(assetGO.transform);

                ArfComponent component = meshGO.AddComponent<ArfComponent>();
                component.asset = asset.id;
                component.mesh = mesh.id;

                SkinnedMeshRenderer renderer = meshGO.AddComponent<SkinnedMeshRenderer>();
                try { 
                    SetMesh(renderer, mesh);                
                }
                catch(Exception e) {
                    throw new Exception($"Error setting mesh {mesh.id}, lod {lodName}, asset {asset.id}: {e.Message}");
                }

                if (loadBlendshapes) 
                {
                    Interdigital.Arf.BlendshapeSet meshBlendshapeSet = null;
                    for(long lodBSIndex = 0; lodBSIndex < lod.blendshapeSets.Count; lodBSIndex++)
                    {
                        long blendshapeSetId = lod.blendshapeSets.GetValue(lodBSIndex);
                        if (!arf.components.blendshapeSets.Has(blendshapeSetId)) {
                            //Debug.LogWarning($"Invalid blendshape set id {blendshapeSetId}");
                            continue;
                        }
                        Interdigital.Arf.BlendshapeSet blendshapeSet = arf.components.blendshapeSets[blendshapeSetId];
                        if (blendshapeSet.baseMesh == mesh) {
                            meshBlendshapeSet = blendshapeSet;
                            break;
                        }
                    }
                    if (meshBlendshapeSet != null) { 
                        try { 
                            SetMeshBlendshapeSet(renderer, meshBlendshapeSet);
                            component.blendshapeSet = meshBlendshapeSet.id;
                        }
                        catch(Exception e) {
                            throw new Exception($"Error setting blendshape set of mesh {mesh.id}, lod {lodName}, asset {asset.id}: {e.Message}");
                        }
                    }
                }

                if (loadSkins)
                { 
                    Interdigital.Arf.Skin meshSkin = null;
                    for(long lodSkinIndex = 0; lodSkinIndex < lod.skins.Count; lodSkinIndex++)
                    {
                        long skinId = lod.skins.GetValue(lodSkinIndex);
                        if (!arf.components.skins.Has(skinId)) {
                            //Debug.LogWarning($"Invalid skin id {skinId}");
                            continue;
                        }
                        Interdigital.Arf.Skin skin = arf.components.skins[skinId];                    
                        if (skin.mesh == mesh) {
                            meshSkin = skin;
                            break;
                        }
                    }
                    if (meshSkin != null) { 
                        try { 
                            SetMeshSkin(renderer, meshSkin, skeletons);
                            component.skeleton = meshSkin.skeleton.id;
                        }
                        catch(Exception e) {
                            Debug.LogError(e.StackTrace);
                            throw new Exception($"Error setting skin of mesh {mesh.id}, lod {lodName}, asset {asset.id}: {e.Message}");
                        }
                    }
                }

                /*string outputDir = $"C:\\temp\\unity\\Imed\\{asset.name}\\{mesh.name}";
                System.IO.Directory.CreateDirectory(outputDir);
                UnityConvert.saveVector3Ds($"{outputDir}\\vertices.txt", renderer.sharedMesh.vertices);
                UnityConvert.saveBoneWeights($"{outputDir}\\boneWeights.txt", renderer.sharedMesh.boneWeights);
                UnityConvert.saveMatrix4x4s($"{outputDir}\\ibm.txt", renderer.sharedMesh.bindposes);
                UnityConvert.saveTransforms($"{outputDir}\\transforms.txt", renderer.bones);*/
            }
        }

        return avatar;
    }

    public UnitySkeleton LoadSkeleton(Interdigital.Arf.Skeleton skeleton, Transform transform, bool transposeNodeTransforms = false, bool transposeInverseBindMatrices = false)
    {
        Transform[] bones = null;
        Matrix4x4[] bindPoses = null;
        long[] jointIds = skeleton.joints.GetValues();
        bones = new Transform[jointIds.Length];
        Dictionary<long, int> jointId2Idx = new Dictionary<long, int>();
        for (int jointIndex = 0; jointIndex < jointIds.Length; jointIndex++) 
        {
            long jointId = jointIds[jointIndex];    
            Interdigital.Arf.Node jointNode = arf.components.nodes[jointId];
            //Debug.Log(String.Format("{0} {1}", jointId, jointNode.name));
            Transform bone = bone = new GameObject(jointNode.name).transform;
            bone.SetParent(transform);
            if (jointNode.HasTransform()) 
            {
                Matrix4x4 mat = UnityConvert.ToMatrix4x4(jointNode.transform);
                if (transposeNodeTransforms) {
                    mat = mat.transpose;
                }
                UnityConvert.SetLocalTransform(bone, mat);
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
            //if (jointIndex < 3) Debug.Log($"{bone.localPosition}, {bone.localRotation}, {bone.localScale}");
            bones[jointIndex] = bone;
            jointId2Idx.Add(jointId, jointIndex);
        }
        for (int jointIndex = 0; jointIndex < jointIds.Length; jointIndex++) 
        {
            long jointId = jointIds[jointIndex];    
            Interdigital.Arf.Node jointNode = arf.components.nodes[jointId];
            if (jointNode.HasChildren()) {
                foreach (long childId in jointNode.children.GetValues()) {
                    int childIndex = jointId2Idx[childId];
                    bones[childIndex].SetParent(bones[jointIndex], false);
                }
            }
        }

        if (skeleton.HasInverseBindMatrix()) 
        {
            DataTree ibm = skeleton.inverseBindMatrix.GetTensor();
            float[] ibmData = ibm.GetValues<float>();
            bindPoses = new Matrix4x4[jointIds.Length];
            for (int jointIndex = 0; jointIndex < jointIds.Length; jointIndex++) {
                Matrix4x4 matrix = UnityConvert.ToMatrix4x4(ibmData, jointIndex * 16);
                if (transposeInverseBindMatrices) {
                    matrix = matrix.transpose;
                }
                bindPoses[jointIndex] = matrix;
                //if (jointIndex < 10) Debug.Log(bindPoses[jointIndex]);
            }
        }

        return new UnitySkeleton(bones, bindPoses);
    }

    public void SetMesh(SkinnedMeshRenderer renderer, Interdigital.Arf.Mesh mesh)
    {
        Data geometry = mesh.data[0];
        Gltf gltf = geometry.GetGltf();
        if (cache != null) { 
            cache.baseName = $"data{geometry.GetPropertyId():D4}:";
        }
        GltfParser gltfParser = new GltfParser(gltf, cache);
        gltfParser.SetMesh(renderer, gltfParser.GetFirstMesh());
        if (cache != null) { 
            cache.baseName = "";
        }
        gltfParser.Dispose();
    }

    public void SetMeshBlendshapeSet(SkinnedMeshRenderer renderer, Interdigital.Arf.BlendshapeSet blendshapeSet)
    {
        UnityEngine.Mesh unityMesh = renderer.sharedMesh;
        Vector3[] baseVertices = unityMesh.vertices;
        //Debug.Log($"Add blendshapes to mesh...");
        int blendshapeIndex = 0;
        foreach(var blendshape in blendshapeSet.shapes) 
        {
            var shape = blendshape.shape;
            //Debug.Log($"Add blendshape {shape.name}...");
            Gltf gltf = shape.GetGltf();
            GltfParser gltfParser = new GltfParser(gltf);
            Interdigital.Gltf2.Mesh mesh = gltfParser.GetFirstMesh();
            Primitive primitive = mesh.primitives[0];
            DataTree vertices = primitive.GetAttribute("POSITION").GetTensor();
            float[] shapeValues = vertices.GetValues<float>();

            Vector3[] deltaVertices = new Vector3[unityMesh.vertexCount];
            for (long vertexIndex = 0; vertexIndex < unityMesh.vertexCount; vertexIndex++) {
                deltaVertices[vertexIndex] = new Vector3(
                    -shapeValues[3*vertexIndex + 0],
                    shapeValues[3*vertexIndex + 1],
                    shapeValues[3*vertexIndex + 2]
                ) - baseVertices[vertexIndex];
            }
            unityMesh.AddBlendShapeFrame(shape.name, 1.0f, deltaVertices, null, null);
            blendshapeIndex ++;
        }
    }

    public void SetMeshSkin(SkinnedMeshRenderer renderer, Interdigital.Arf.Skin skin, Dictionary<long, UnitySkeleton> skeletons)
    {
        if (skeletons.ContainsKey(skin.skeleton.id)) { 
            UnitySkeleton skeleton = skeletons[skin.skeleton.id];
            renderer.sharedMesh.bindposes = skeleton.bindPoses; 
            renderer.bones = skeleton.bones;
        }
        else {
            Debug.LogWarning($"Skeleton {skin.skeleton.id} not found or not loaded");
        }

        DataTree jointIndices, jointWeights;
        long jointPerVertex = 4;
        long vertexCount = renderer.sharedMesh.vertexCount;
        Data weightsData = skin.weights;
        (jointIndices, jointWeights) = weightsData.GetColSparseTensor(jointPerVertex, jointPerVertex, normalize: true);
        if (vertexCount != jointIndices.GetTensorSize(0) || jointPerVertex != jointIndices.GetTensorSize(1)) {
            throw new Exception($"Invalid joint indices {vertexCount} != {jointIndices.GetTensorSize(0)} || {jointPerVertex} != {jointIndices.GetTensorSize(1)}");
        }
        if (vertexCount != jointWeights.GetTensorSize(0) || jointPerVertex != jointWeights.GetTensorSize(1)) {
            throw new Exception($"Invalid joint weights {vertexCount} != {jointWeights.GetTensorSize(0)} || {jointPerVertex} != {jointWeights.GetTensorSize(1)}");
        }
        long[] jointIndicesData = jointIndices.GetValues<long>();
        float[] jointWeightsData = jointWeights.GetValues<float>();
        BoneWeight[] boneWeights = new BoneWeight[vertexCount];
        for (long vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++) 
        {
            boneWeights[vertexIndex].boneIndex0 = (int)jointIndicesData[vertexIndex * jointPerVertex + 0];
            boneWeights[vertexIndex].boneIndex1 = (int)jointIndicesData[vertexIndex * jointPerVertex + 1];
            boneWeights[vertexIndex].boneIndex2 = (int)jointIndicesData[vertexIndex * jointPerVertex + 2];
            boneWeights[vertexIndex].boneIndex3 = (int)jointIndicesData[vertexIndex * jointPerVertex + 3];

            boneWeights[vertexIndex].weight0 = jointWeightsData[vertexIndex * jointPerVertex + 0];
            boneWeights[vertexIndex].weight1 = jointWeightsData[vertexIndex * jointPerVertex + 1];
            boneWeights[vertexIndex].weight2 = jointWeightsData[vertexIndex * jointPerVertex + 2];
            boneWeights[vertexIndex].weight3 = jointWeightsData[vertexIndex * jointPerVertex + 3];
        } 
        renderer.sharedMesh.boneWeights = boneWeights;
    }

}

}
}
