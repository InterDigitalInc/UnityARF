//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using System;
using UnityEngine;

namespace Interdigital { 

public class UnityConvert
{ 
    public static Matrix4x4 ToMatrix4x4(float[] data, int offset = 0)
    {
        Vector4 column0 = new Vector4(
            data[offset + 0],
            -data[offset + 1],
            -data[offset + 2],
            -data[offset + 3]
        );
        Vector4 column1 = new Vector4(
            -data[offset + 4],
            data[offset + 5],
            data[offset + 6],
            data[offset + 7]
        );
        Vector4 column2 = new Vector4(
            -data[offset + 8],
            data[offset + 9],
            data[offset + 10],
            data[offset + 11]
        );
        Vector4 column3 = new Vector4(
            -data[offset + 12],
            data[offset + 13],
            data[offset + 14],
            data[offset + 15]
        );
        return new Matrix4x4(
            column0, column1, column2, column3
        );
    }

    public static Matrix4x4 ToMatrix4x4(double[] data, int offset = 0)
    {
        Vector4 column0 = new Vector4(
            (float)data[offset + 0],
            (float)-data[offset + 1],
            (float)-data[offset + 2],
            (float)-data[offset + 3]
        );
        Vector4 column1 = new Vector4(
            (float)-data[offset + 4],
            (float)data[offset + 5],
            (float)data[offset + 6],
            (float)data[offset + 7]
        );
        Vector4 column2 = new Vector4(
            (float)-data[offset + 8],
            (float)data[offset + 9],
            (float)data[offset + 10],
            (float)data[offset + 11]
        );
        Vector4 column3 = new Vector4(
            (float)-data[offset + 12],
            (float)data[offset + 13],
            (float)data[offset + 14],
            (float)data[offset + 15]
        );
        return new Matrix4x4(
            column0, column1, column2, column3
        );
    }

    public static Vector3 ToScale(double[] data, int offset = 0)
    {
        return new Vector3(
            (float)data[offset + 0],
            (float)data[offset + 1],
            (float)data[offset + 2]
        );
    }

    public static Vector3 ToTranslation(double[] data, int offset = 0)
    {
        return new Vector3(
            -(float)data[offset + 0],
            (float)data[offset + 1],
            (float)data[offset + 2]
        );
    }

    public static Quaternion ToRotation(double[] data, int offset = 0)
    {
        return new Quaternion(
            (float)data[offset + 0],
            -(float)data[offset + 1],
            -(float)data[offset + 2],
            (float)data[offset + 3]
        );
    }

    public static void SetTransform(Transform transform, float[] data, int offset = 0)
    {
        Matrix4x4 mat = UnityConvert.ToMatrix4x4(data, offset);
        UnityConvert.SetTransform(transform, mat);
    }

    public static void SetTransform(Transform transform, Matrix4x4 mat)
    {
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
        transform.localPosition = position;
        transform.localRotation = rotation;
        transform.localScale = scale;
    }

    public static void saveVector3Ds(string filePath, Vector3[] vectors)
    {
        using (System.IO.StreamWriter outputFile = new System.IO.StreamWriter(filePath)) 
        {
            int i = 0;
            foreach (Vector3 vector in vectors) 
            {
                outputFile.WriteLine(String.Format("{0}: {1:F4} {2:F4} {3:F4}", i, vector.x, vector.y, vector.z));
                i ++;
            }
        }
    }

    public static void saveMatrix4x4s(string filePath, Matrix4x4[] matrices)
    {
        using (System.IO.StreamWriter outputFile = new System.IO.StreamWriter(filePath)) 
        {
            int i = 0;
            foreach (Matrix4x4 matrix in matrices) 
            {
                outputFile.WriteLine(String.Format("{0}: {1}", i, matrix));
                i ++;
            }
        }
    }

    public static void saveTransforms(string filePath, Transform[] transforms)
    {
        using (System.IO.StreamWriter outputFile = new System.IO.StreamWriter(filePath)) 
        {
            int i = 0;
            foreach (Transform transform in transforms) 
            {
                outputFile.WriteLine(String.Format("{0}: {1}\n{2}\n{3}\n{4}", i, transform.name, transform.localPosition, transform.localRotation, transform.localScale));
                i ++;
            }
        }
    }

    public static void saveBoneWeights(string filePath, BoneWeight[] boneWeights)
    {
        using (System.IO.StreamWriter outputFile = new System.IO.StreamWriter(filePath)) 
        {
            int i = 0;
            int[] indices = new int[4];
            float[] weights = new float[4];
            foreach (BoneWeight boneWeight in boneWeights) 
            {
                indices[0] = boneWeight.boneIndex0;
                indices[1] = boneWeight.boneIndex1;
                indices[2] = boneWeight.boneIndex2;
                indices[3] = boneWeight.boneIndex3;
                weights[0] = boneWeight.weight0;
                weights[1] = boneWeight.weight1;
                weights[2] = boneWeight.weight2;
                weights[3] = boneWeight.weight3;
                outputFile.WriteLine(String.Format("{0}: {1}:{2:F4} {3}:{4:F4} {5}:{6:F4} {7}:{8:F4}", i, 
                    indices[0], weights[0],
                    indices[1], weights[1],
                    indices[2], weights[2],
                    indices[3], weights[3]
                ));
                i ++;
            }
        }
    }
}

}

