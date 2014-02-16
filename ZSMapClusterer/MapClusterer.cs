/*
Copyright (c) 2014 Alexey Minaev

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
using System;
using System.Collections.Generic;
using System.Linq;

namespace ZSMapClusterer
{
    public class MapClusterer
    {
        public int NumberOfClusters { get; set; }
        public String ClusterTitleFormat { get; set; }

        public MapClusterer(int numberOfClusters, String clusterTitleFormat = "{0}")
        {
            NumberOfClusters = numberOfClusters;
            ClusterTitleFormat = clusterTitleFormat;
        }

        private readonly List<ClusterAnnotation> _annotations = new List<ClusterAnnotation>();
        private List<ClusterAnnotation> AnnotationsPool { get; set; }
        private MapCluster RootMapCluster { get; set; }

        public void SetAnnotations(List<IAnnotation> annotations)
        {
            int numberOfAnnotationsInPool = NumberOfClusters;
            AnnotationsPool = new List<ClusterAnnotation>(numberOfAnnotationsInPool);
            for (int i = 0; i < numberOfAnnotationsInPool; i++)
            {
                var annotation = new ClusterAnnotation();
                AnnotationsPool.Add(annotation);
            }
            _annotations.Clear();
            _annotations.AddRange(AnnotationsPool);

            RootMapCluster = MapCluster.RootCluster(annotations, ClusterTitleFormat);
        }

        public List<ClusterAnnotation> ClusterAnnotationsInCoordinateRect(CoordinateRect rect)
        {
            List<MapCluster> clustersToShowOnMap = RootMapCluster.FindAnnotationsInMapCoordinateRect(NumberOfClusters,
                rect);

            foreach (var clusterAnnotation in _annotations)
            {
                AnnotationsPool.Add(clusterAnnotation);
            }
            _annotations.Clear();

            foreach (var mapCluster in clustersToShowOnMap)
            {
                ClusterAnnotation annotation = AnnotationsPool.First();
                AnnotationsPool.Remove(annotation);

                annotation.Coordinate = mapCluster.Coordinate;
                annotation.Cluster = mapCluster;
                _annotations.Add(annotation);
            }
            return _annotations;
        }

        private bool IsAnnotationBelongsToClusters(ClusterAnnotation annotation, List<MapCluster> clusters) {
            if (annotation.Cluster != null) {
                foreach (MapCluster cluster in clusters) {
                    if (cluster.IsAncestorOfCluster(annotation.Cluster) || 
                        cluster.Equals(annotation.Cluster)) {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
