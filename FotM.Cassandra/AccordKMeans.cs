﻿using System;
using System.Collections.Generic;
using System.Linq;
using Accord.MachineLearning;

namespace FotM.Cassandra
{
    public class AccordKMeans : IKMeans<PlayerChange>
    {
        private readonly Func<double[], double[], double> _distance;
        private readonly bool _normalize;

        public AccordKMeans(bool normalize = false, Func<double[], double[], double> distance = null)
        {
            _normalize = normalize;
            _distance = distance;
        }

        public int[] ComputeGroups(PlayerChange[] dataSet, int nGroups)
        {
            var kmeans = (_distance == null)
                ? new KMeans(nGroups)
                : new KMeans(nGroups, _distance);

            var descriptor = new FeatureAttributeDescriptor<PlayerChange>();

            if (_normalize)
            {
                descriptor.NormalizeFor(dataSet);
            }

            //descriptor.SetWeights(new[] {1.79, -1.13, -1.40, 0.32, 2.15, 2.28, 1.07});

            return kmeans.Compute(dataSet, descriptor);
        }
    }
}