//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using Gsplat;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Interdigital.Arf
{
    public sealed class GaussianModel : MonoBehaviour
    {
        [NonSerialized]
        private bool disposed = false;
        [NonSerialized]
        private bool initialized = false;

        [field: NonSerialized] public long count { get; private set; }
        [field: NonSerialized] public DataTree means { get; private set; }
        [field: NonSerialized] public DataTree quaternions { get; private set; }
        [field: NonSerialized] public DataTree scales { get; private set; }
        [field: NonSerialized] public DataTree opacities { get; private set; }
        [field: NonSerialized] public DataTree[] shs { get; private set; }
        [field: NonSerialized] public string colorSpace { get; set; } = "srgb_rec709_display";

        [field: NonSerialized] public bool isStitched { get; private set; } = false;
        [field: NonSerialized] public DataTree faces { get; private set; }
        [field: NonSerialized] public DataTree baryCenters { get; private set; }
        [field: NonSerialized] public DataTree displacements { get; private set; }

        /// <summary>
        /// Takes ownership of all tensors after successful initialization. If validation fails,
        /// the caller remains responsible for disposing them.
        /// </summary>
        public void Initialize(
            DataTree positions, 
            DataTree rotations, 
            DataTree scales,
            DataTree opacities, 
            DataTree[] shs)
        {
            if (initialized) { 
                throw new InvalidOperationException("The Gaussian model is already initialized.");
            }
            ValidateTensor(positions, nameof(positions), -1, 3);
            long gaussianCount = positions.GetTensorSize(0);
            if (gaussianCount <= 0)
                throw new ArgumentException("Positions must contain at least one Gaussian.", nameof(positions));
            ValidateTensor(rotations, nameof(rotations), gaussianCount, 4);
            ValidateTensor(scales, nameof(scales), gaussianCount, 3);
            ValidateTensor(opacities, nameof(opacities), gaussianCount);
            if (ReferenceEquals(shs, null) || shs.Length == 0)
                throw new ArgumentException("At least degree-zero spherical harmonics are required.", nameof(shs));
            for (int degree = 0; degree < shs.Length; degree++)
            {
                string name = $"shs[{degree}]";
                ValidateTensor(shs[degree], name, gaussianCount, 2L * degree + 1, 3);
            }

            count = gaussianCount;
            means = positions;
            quaternions = rotations;
            this.scales = scales;
            this.opacities = opacities;
            this.shs = (DataTree[])shs.Clone();
            this.colorSpace = "srgb_rec709_display";
            initialized = true;
        }

        public void SetStitching(
            DataTree faces,
            DataTree baryCenters,
            DataTree displacements
        )
        {
            if (!initialized || disposed)
                throw new InvalidOperationException("The Gaussian model is not available.");
            if (isStitched)
                throw new InvalidOperationException("Stitching has already been set.");
            if (ReferenceEquals(faces, null) || !faces.IsValid() || !faces.IsTensor())
                throw new ArgumentException("A valid tensor is required.", nameof(faces));
            if (faces.GetScalarType() != ScalarType.UInteger32)
                throw new ArgumentException("The tensor must contain 32-bit unsigned ints.", nameof(faces));
            if (faces.GetTensorDim() != 1 || faces.GetTensorSize(0) != count)
                throw new ArgumentException("The face tensor must have shape [gaussianCount].", nameof(faces));

            ValidateTensor(baryCenters, nameof(baryCenters), count, 3);
            ValidateTensor(displacements, nameof(displacements), count, 3);
    
            isStitched = true;
            this.faces = faces;
            this.baryCenters = baryCenters;
            this.displacements = displacements;
        }

        private static void ValidateTensor(DataTree tensor, string name, long expectedCount,
            params long[] trailingDimensions)
        {
            if (ReferenceEquals(tensor, null) || !tensor.IsValid() || !tensor.IsTensor())
                throw new ArgumentException("A valid tensor is required.", name);
            if (tensor.GetScalarType() != ScalarType.Float)
                throw new ArgumentException("The tensor must contain 32-bit floats.", name);
            if (tensor.GetTensorDim() != trailingDimensions.Length + 1)
                throw new ArgumentException("The tensor has an invalid number of dimensions.", name);
            if (expectedCount >= 0 && tensor.GetTensorSize(0) != expectedCount)
                throw new ArgumentException("The tensor count must match the position count.", name);
            for (int dimension = 0; dimension < trailingDimensions.Length; dimension++)
            {
                if (tensor.GetTensorSize(dimension + 1) != trailingDimensions[dimension])
                    throw new ArgumentException($"Tensor dimension {dimension + 1} must be {trailingDimensions[dimension]}.", name);
            }
        }

        /// <summary>
        /// Converts the GLB splat tensors to UnitySplats' Unity-coordinate representation.
        /// The caller selects the renderer's GammaToLinear setting from colorSpace.
        /// </summary>
        public GsplatDecodedData ToGsplatDecodedData()
        {
            if (!initialized || disposed)
                throw new InvalidOperationException("The Gaussian model is not available.");

            int gaussianCount = checked((int)count);
            byte shBands = checked((byte)(shs.Length - 1));
            var decoded = new GsplatDecodedData(gaussianCount, shBands);
            int shStride = GsplatUtils.SHBandsToCoefficientCount(shBands);

            float[] positions = means.GetValues<float>();
            float[] rotations = quaternions.GetValues<float>();
            float[] linearScales = scales.GetValues<float>();
            float[] alphas = opacities.GetValues<float>();
            var coefficients = new float[shs.Length][];
            for (int degree = 0; degree < shs.Length; degree++)
                coefficients[degree] = shs[degree].GetValues<float>();

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

                for (int degree = 1; degree < shs.Length; degree++)
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

        private void OnDestroy()
        {
            if (!initialized || disposed)
                return;

            disposed = true;
            means.Dispose();
            quaternions.Dispose();
            scales.Dispose();
            opacities.Dispose();
            foreach(var sh in shs) { sh.Dispose(); }

            if (isStitched)
            {
                faces.Dispose();
                baryCenters.Dispose();
                displacements.Dispose();
            }
        }

    }
}
