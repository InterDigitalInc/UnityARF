//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using System;
using UnityEngine;

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
        }

    }
}
