//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using Gsplat;
using UnityEngine;

namespace Interdigital.Arf
{
    public static class UnitySplatsGaussianModel
    {
        /// <summary>
        /// Converts the GLB splat tensors to UnitySplats' Unity-coordinate representation.
        /// The caller selects the renderer's GammaToLinear setting from colorSpace.
        /// </summary>
        public static GsplatDecodedData ToGsplatDecodedData(this GaussianModel model)
        {
            int gaussianCount = checked((int)model.count);
            byte shBands = checked((byte)(model.shs.Length - 1));
            var decoded = new GsplatDecodedData(gaussianCount, shBands);
            int shStride = GsplatUtils.SHBandsToCoefficientCount(shBands);

            float[] positions = model.means.GetValues<float>();
            float[] rotations = model.quaternions.GetValues<float>();
            float[] linearScales = model.scales.GetValues<float>();
            float[] alphas = model.opacities.GetValues<float>();
            var coefficients = new float[model.shs.Length][];
            for (int degree = 0; degree < model.shs.Length; degree++)
                coefficients[degree] = model.shs[degree].GetValues<float>();

            Bounds bounds = default;
            for (int gaussian = 0; gaussian < gaussianCount; gaussian++)
            {
                int xyz = 3 * gaussian;
                int xyzw = 4 * gaussian;
                Vector3 position = new Vector3(-positions[xyz], positions[xyz + 1], positions[xyz + 2]);
                Vector3 scale = new Vector3(linearScales[xyz], linearScales[xyz + 1], linearScales[xyz + 2]);
                Vector4 rotation = new Vector4(
                    rotations[xyzw + 3], rotations[xyzw], -rotations[xyzw + 1], -rotations[xyzw + 2]);

                decoded.Positions[gaussian] = position;
                decoded.Scales[gaussian] = scale;
                decoded.Rotations[gaussian] = rotation.normalized;
                decoded.Colors[gaussian] = new Vector4(
                    coefficients[0][xyz], coefficients[0][xyz + 1], coefficients[0][xyz + 2], alphas[gaussian]);

                // Three standard deviations enclose the visible extent of each Gaussian.
                float radius = 3f * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
                Bounds gaussianBounds = new Bounds(position, Vector3.one * (2f * radius));
                if (gaussian == 0)
                    bounds = gaussianBounds;
                else
                    bounds.Encapsulate(gaussianBounds);

                for (int degree = 1; degree < model.shs.Length; degree++)
                {
                    int coefficientCount = 2 * degree + 1;
                    int bandOffset = degree * degree - 1;
                    float[] band = coefficients[degree];
                    for (int coefficient = 0; coefficient < coefficientCount; coefficient++)
                    {
                        int source = (gaussian * coefficientCount + coefficient) * 3;
                        float sign = GsplatUtils.ShSign(SourceCoordinates.LUF, degree, coefficient);
                        decoded.SHs[gaussian * shStride + bandOffset + coefficient] = sign * new Vector3(
                            band[source], band[source + 1], band[source + 2]);
                    }
                }
            }

            decoded.Bounds = bounds;
            decoded.Validate();
            return decoded;
        }

    }
}
