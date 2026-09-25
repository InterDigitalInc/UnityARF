//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using Gsplat;
using Interdigital.Arf;
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

    public void SetMesh(SkinnedMeshRenderer renderer, Interdigital.Gltf2.Mesh mesh, ArfParserOptions options)
    {
        if (mesh.primitives.Count == 0) {
            Debug.LogWarning("No primitive in mesh");
            return;
        }

        // Parse primitive
        Primitive primitive = mesh.primitives[0];
        if (primitive.mode != PrimitiveMode.TRIANGLES) {
            Debug.LogWarning("Only TRIANGLES primitives are supported");
            return;
        }
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

        Shader shader = UnityTools.GetStandardShader();
        if (shader == null) {
            throw new Exception("Shader was not found");
        }
        UnityEngine.Material unityMaterial = new UnityEngine.Material(shader);

        // Texture
        bool foundTexture = false;
        if (primitive.HasAttribute("TEXCOORD_0"))
        { 
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
            if (material.HasPbrMetallicRoughness())
            { 
                var pbrMetallicRoughness = material.pbrMetallicRoughness;
                if (pbrMetallicRoughness.HasBaseColorTexture()) 
                { 
                    TextureInfo baseColorTexture = pbrMetallicRoughness.baseColorTexture;
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

                    Texture2D unityTexture = new Texture2D(width, height, textureFormat, false);
                    if (cache != null) {
                        cache.AddTexture($"{texture.GetPropertyIndex():D4}", unityTexture);
                    }
                    byte[] bytes = pixels.GetValues<byte>();
                    unityTexture.LoadRawTextureData(bytes);
                    unityTexture.Apply();
                    unityMaterial.mainTexture = unityTexture;

                    if (cache != null) {
                        cache.AddMaterial($"{material.GetPropertyIndex():D4}", unityMaterial);
                    }
                    foundTexture = true;
                }
            }
        }
        if (!foundTexture)
        {
            unityMaterial.color = Color.white;
        }
        if (Application.isPlaying) {
            renderer.material = unityMaterial;
        }
        else {
            renderer.sharedMaterial = unityMaterial;
        }

        renderer.sharedMesh = unityMesh;


        // Gaussian Splatting
        if (!options.loadGaussianSplatting)
        {
            if (mesh.primitives.Count != 1) {
                Debug.LogWarning("meshes with more than one primitive are not supported; only the first one is used");
                return;
            }
        }

        if (mesh.primitives.Count == 1) {
            return;
        }
        primitive = mesh.primitives[1];
        if (primitive.mode != PrimitiveMode.POINTS) {
            Debug.LogWarning("Only POINTS primitives are supported for Gaussians Splatting");
            return;
        }
        if (!primitive.HasExtensions() || !primitive.extensions.Has("KHR_gaussian_splatting"))
        {
            Debug.LogWarning("If a mesh has two primitives, the second one shall be gaussian splatting");
            return;
        }
        var KHR_gaussian_splatting = primitive.extensions.KHR_gaussian_splatting;
        string kernel = KHR_gaussian_splatting.kernel;
        if (kernel != "ellipse") {
            Debug.LogWarning($"Unsupported Gaussian Splatting kernel {kernel}");
            return;
        }

        // Static model
        DataTree gsPositions = null;
        DataTree gsRotations = null;
        DataTree gsScales = null;
        DataTree gsOpacities = null;
        DataTree[] gsShs = null;
        GaussianModel model = null;
        long gaussianCount;
        try
        {
            gsPositions = primitive.GetAttribute("POSITION").GetTensor();
            gaussianCount = gsPositions.GetTensorSize(0);
            Debug.Log($"Found {gaussianCount} Gaussians");
            gsRotations = primitive.GetAttribute("KHR_gaussian_splatting:ROTATION").GetTensor();
            gsScales = primitive.GetAttribute("KHR_gaussian_splatting:SCALE").GetTensor();
            gsOpacities = primitive.GetAttribute("KHR_gaussian_splatting:OPACITY").GetTensor();
            gsShs = LoadGaussianSphericalHarmonics(primitive, gaussianCount);

            model = renderer.gameObject.AddComponent<GaussianModel>();
            model.Initialize(gsPositions, gsRotations, gsScales, gsOpacities, gsShs);
        }
        catch
        {
            if (model != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(model);
                else
                    UnityEngine.Object.DestroyImmediate(model);
            }
            if (!ReferenceEquals(gsPositions, null)) gsPositions.Dispose();
            if (!ReferenceEquals(gsRotations, null)) gsRotations.Dispose();
            if (!ReferenceEquals(gsScales, null)) gsScales.Dispose();
            if (!ReferenceEquals(gsOpacities, null)) gsOpacities.Dispose();
            if (gsShs != null)
                foreach (DataTree sh in gsShs) sh.Dispose();
            throw;
        }
        model.colorSpace = KHR_gaussian_splatting.colorSpace;

        GsplatDecodedData data = model.ToGsplatDecodedData();
        var asset = ScriptableObject.CreateInstance<GsplatAssetUncompressed>();
        asset.LoadFromDecoded(data);

        var splats = renderer.gameObject.AddComponent<GsplatRenderer>();
        splats.GsplatAsset = asset;
        splats.SHDegree = model.shs.Length - 1;
        splats.GammaToLinear = model.colorSpace != "lin_rec709_display";

        // No more need the mesh rendering
        renderer.enabled = false;

        // Stitching
        if (KHR_gaussian_splatting.HasExtensions() && KHR_gaussian_splatting.extensions.Has("MPEG_gaussian_splatting_transport"))
        {
            var MPEG_gaussian_splatting_transport = KHR_gaussian_splatting.extensions.MPEG_gaussian_splatting_transport;

            var shEncoding = MPEG_gaussian_splatting_transport.shEncoding;
            string layout = shEncoding.layout;
            long maxDegree = shEncoding.maxDegree;
            bool dcFromColor0 = shEncoding.dcFromColor0;

            var stitching = MPEG_gaussian_splatting_transport.stitching;
            if (stitching.mesh != mesh.GetPropertyIndex()) {
                throw new SystemException("Only Gaussian Splatting defined in the same mesh is supported");
            }
            if (stitching.primitive != 0) {
                throw new SystemException("Only Gaussian Splatting defined in the second primitive is supported");
            }

            model.SetStitching(
                stitching.faces.GetTensor(),
                stitching.weights.GetTensor(),
                stitching.displacement.GetTensor()
            );

            // Utilities to link mesh to GS 
            var bakedMesh = renderer.gameObject.AddComponent<BakedMesh>();
        }
    }

    private static DataTree[] LoadGaussianSphericalHarmonics(Primitive primitive, long gaussianCount)
    {
        const int maxDegree = 3;
        var harmonics = new List<DataTree>();
        try
        {
            for (int degree = 0; degree <= maxDegree; degree++)
            {
                string firstAttribute = $"KHR_gaussian_splatting:SH_DEGREE_{degree}_COEF_0";
                if (!primitive.HasAttribute(firstAttribute))
                    break;

                int coefficientCount = 2 * degree + 1;
                var values = new float[checked((int)gaussianCount), coefficientCount, 3];
                for (int coefficient = 0; coefficient < coefficientCount; coefficient++)
                {
                    string attribute = $"KHR_gaussian_splatting:SH_DEGREE_{degree}_COEF_{coefficient}";
                    if (!primitive.HasAttribute(attribute))
                        throw new SystemException($"Missing Gaussian Splatting coefficient {attribute}");

                    DataTree part = primitive.GetAttribute(attribute).GetTensor();
                    try
                    {
                        ValidateGaussianShCoefficient(part, gaussianCount, attribute);
                        float[] rgb = part.GetValues<float>();
                        for (int gaussian = 0; gaussian < values.GetLength(0); gaussian++)
                        {
                            int offset = 3 * gaussian;
                            values[gaussian, coefficient, 0] = rgb[offset];
                            values[gaussian, coefficient, 1] = rgb[offset + 1];
                            values[gaussian, coefficient, 2] = rgb[offset + 2];
                        }
                    }
                    finally
                    {
                        part.Dispose();
                    }
                }
                harmonics.Add(DataTree.CreateTensor(values));
            }

            if (harmonics.Count == 0)
                throw new SystemException("Gaussian Splatting degree-zero SH is missing");
            return harmonics.ToArray();
        }
        catch
        {
            foreach (DataTree tensor in harmonics)
                tensor.Dispose();
            throw;
        }
    }

    private static void ValidateGaussianShCoefficient(DataTree tensor, long gaussianCount, string attribute)
    {
        if (!tensor.IsTensor() || tensor.GetScalarType() != ScalarType.Float ||
            tensor.GetTensorDim() != 2 || tensor.GetTensorSize(0) != gaussianCount ||
            tensor.GetTensorSize(1) != 3)
            throw new SystemException($"Invalid Gaussian Splatting coefficient {attribute}: expected [{gaussianCount}, 3] float tensor");
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

    public void SetSkinnedMeshRenderer(Transform transform, long nodeId, ArfParserOptions options, bool withSkin = true)
    {
        Node node = gltf.nodes[nodeId];
        if (!node.HasMesh()) {
            return;
        }

        Interdigital.Gltf2.Mesh mesh = node.mesh;
        GameObject meshGO = new GameObject(mesh.name ?? "mesh");
        meshGO.transform.SetParent(transform);
        SkinnedMeshRenderer renderer = meshGO.AddComponent<SkinnedMeshRenderer>();
        SetMesh(renderer, mesh, options);

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
