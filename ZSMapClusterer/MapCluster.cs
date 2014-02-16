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
using System.Device.Location;
using System.Linq;

namespace ZSMapClusterer
{
    public class MapCluster : IAnnotation
    {
        private const double MapClusterDiscriminationPrecision = 1E-6;
        public GeoCoordinate Coordinate { get; set; }
    
        private readonly String _title;
        public String Title
        {
            get
            {
                if (Annotation == null)
                {
                    return String.Format(_title, NumberOfChildren());
                }
                else
                {
                    return Annotation.Title;
                }
            }
        }

        public String Subtitle
        {
            get
            {
                return "";
            }
        }

        public int Depth
        {
            get;
            private set;
        }
        //TODO: add protocol for Annotation with coordinate

        private CoordinateRect _coordinateRect;

        private CoordinateRect CoordinateRect 
        {
            get
            {
                return _coordinateRect;
            }
        }

        private MapCluster _leftChild;
        private MapCluster _rightChild;

        public IAnnotation Annotation { get; set; }

        public List<IAnnotation> OriginalAnnotations
        {
            get
            {
                List<IAnnotation> originalAnnotations = new List<IAnnotation>();
                if (Annotation != null)
                {
                    originalAnnotations.Add(Annotation);
                }
                else
                {
                    if (_leftChild != null)
                    {
                        originalAnnotations.AddRange(_leftChild.OriginalAnnotations);
                    }
                    if (_rightChild != null)
                    {
                        originalAnnotations.AddRange(_rightChild.OriginalAnnotations);
                    }
                }
                return originalAnnotations;
            }
        } 

        public int NumberOfChildren()
        {
            if (_leftChild == null && _rightChild == null)
            {
                return 1;
            }
            return _leftChild.NumberOfChildren() + _rightChild.NumberOfChildren();
        }

        public static MapCluster RootCluster(List<IAnnotation> annotations, String title, bool showSubtitle = false)
        {
            //TODO: add cutting off unused earth space
            MapCluster cluster = new MapCluster(annotations, 0, CoordinateRect.EarthCoordinateRect(), title, showSubtitle);
            ;
            return cluster;
        }

        public MapCluster(List<IAnnotation> annotations, int depth, CoordinateRect coordRect, String title,
            bool showSubtitle = false)
        {
            Depth = depth;
            _coordinateRect = coordRect;
            _title = title;
            if (!annotations.Any()) {
                _leftChild = null;
                _rightChild = null;
                Annotation = null;
                Coordinate = null;
            } else if (annotations.Count() == 1)
            {
                _leftChild = null;
                _rightChild = null;
                Annotation = annotations.LastOrDefault();
                if (Annotation != null) Coordinate = Annotation.Coordinate;
            }
            else
            {
                Annotation = null;

                // Principal Component Analysis
                // If cov(x,y) = ∑(x-x_mean) * (y-y_mean) != 0 (covariance different from zero), we are looking for the following principal vector:
                // a (aX)
                //   (aY)
                //
                // x_ = x - x_mean ; y_ = y - y_mean
                //
                // aX = cov(x_,y_)
                // 
                //
                // aY = 0.5/n * ( ∑(x_^2) + ∑(y_^2) + sqrt( (∑(x_^2) + ∑(y_^2))^2 + 4 * cov(x_,y_)^2 ) ) 
            
                // Latitude = y, Longitude = x
                // compute the means of the coordinate
                double xSum = 0.0;
                double ySum = 0.0;
                foreach (IAnnotation annotation in annotations) {
                    xSum += annotation.Coordinate.Longitude;
                    ySum += annotation.Coordinate.Latitude;
                }
                double xMean = xSum / (double)annotations.Count();
                double yMean = ySum / (double)annotations.Count();

                // compute coefficients
            
                double sumXsquared = 0.0;
                double sumYsquared = 0.0;
// ReSharper disable once NotAccessedVariable
                double xByYSum = 0.0;

                foreach (IAnnotation annotation in annotations) {
                    double x = annotation.Coordinate.Longitude - xMean;
                    double y = annotation.Coordinate.Latitude - yMean;
                    sumXsquared += x * x;
                    sumYsquared += y * y;
                    xByYSum += x * y;
                }
            
                double aX = 0.0;
                double aY = 0.0;

                if (Math.Abs(xByYSum) / annotations.Count() > MapClusterDiscriminationPrecision)
                {
                    aX = xByYSum;
                    double lambda = 0.5 * ((sumXsquared + sumYsquared) + Math.Sqrt((sumXsquared + sumYsquared) * (sumXsquared + sumYsquared) + 4 * xByYSum * xByYSum));
                    aY = lambda - sumXsquared;
                } else {
                    aX = sumXsquared > sumYsquared ? 1.0 : 0.0;
                    aY = sumXsquared > sumYsquared ? 0.0 : 1.0;
                }
            
                List<IAnnotation> leftAnnotations = null;
                List<IAnnotation> rightAnnotations = null;
                if (Math.Abs(sumXsquared)/annotations.Count() < MapClusterDiscriminationPrecision ||
                    Math.Abs(sumYsquared)/annotations.Count() < MapClusterDiscriminationPrecision) 
                {   // all X and Y are the same => same coordinates
                    // then every x equals XMean and we have to arbitrarily choose where to put the pivotIndex
                    int pivotIndex = annotations.Count() /2 ;
                    leftAnnotations = annotations.GetRange(0, pivotIndex);
                    rightAnnotations = annotations.GetRange(pivotIndex, annotations.Count() - pivotIndex);
                } else {
                    // compute scalar product between the vector of this regression line and the vector
                    // (x - x(mean))
                    // (y - y(mean))
                    // the sign of this scalar product determines which cluster the point belongs to
                    leftAnnotations = new List<IAnnotation>(annotations.Count());
                    rightAnnotations = new List<IAnnotation>(annotations.Count());
                    foreach (IAnnotation annotation in annotations) {
                        GeoCoordinate point = annotation.Coordinate;
                        bool positivityConditionOfScalarProduct = true;
                        if (true) {
                            positivityConditionOfScalarProduct = (point.Longitude - xMean) * aX + (point.Latitude - yMean) * aY > 0.0;
                        } 
                        /*else {
                            positivityConditionOfScalarProduct = (point.Latitude - yMean) > 0.0;
                        }*/
                        //var cs = (ChargerStationPushpin)annotation;
                        if (positivityConditionOfScalarProduct)
                        {

                            //System.Diagnostics.Debug.WriteLine(cs.ChargerStation.Name + " LEFT");
                            leftAnnotations.Add(annotation);
                        }
                        else
                        {
                            //System.Diagnostics.Debug.WriteLine(cs.ChargerStation.Name + " RIGHT");
                            rightAnnotations.Add(annotation);
                        }
                    }
                }

                CoordinateRect leftMapRect = null;
                CoordinateRect rightMapRect = null;
            
                // compute map rects
                double latMin = double.MaxValue, latMax = 0.0, longMin = double.MaxValue, longMax = 0.0;
                foreach (IAnnotation annotation in leftAnnotations) {
                    GeoCoordinate point = annotation.Coordinate;
                    if (point.Longitude > longMax) {
                        longMax = point.Longitude;
                    }
                    if (point.Latitude > latMax) {
                        latMax = point.Latitude;
                    }
                    if (point.Longitude < longMin) {
                        longMin = point.Longitude;
                    }
                    if (point.Latitude < latMin) {
                        latMin = point.Latitude;
                    }
                }
                leftMapRect = new CoordinateRect(new GeoCoordinate(latMin, longMin), new GeoCoordinate(latMax, longMax));

                latMin = double.MaxValue;
                latMax = 0.0;
                longMin = double.MaxValue;
                longMax = 0.0;
                foreach (IAnnotation annotation in rightAnnotations) {
                    GeoCoordinate point = annotation.Coordinate;
                    if (point.Longitude > longMax) {
                        longMax = point.Longitude;
                    }
                    if (point.Latitude > latMax) {
                        latMax = point.Latitude;
                    }
                    if (point.Longitude < longMin) {
                        longMin = point.Longitude;
                    }
                    if (point.Latitude < latMin) {
                        latMin = point.Latitude;
                    }
                }
                rightMapRect = new CoordinateRect(new GeoCoordinate(latMin, longMin), new GeoCoordinate(latMax, longMax));
            
                Coordinate = new GeoCoordinate(yMean, xMean);
            
                _leftChild = new MapCluster(leftAnnotations, depth + 1, leftMapRect, title, showSubtitle);
                _rightChild = new MapCluster(rightAnnotations, depth + 1, rightMapRect, title, showSubtitle);
            }
        }


        public List<MapCluster> FindAnnotationsInMapCoordinateRect(int N, CoordinateRect mapRect)
        {
            // Start from the root (this)
            // Adopt a breadth-first search strategy
            // If MapRect intersects the bounds, then keep this element for next iteration
            // Stop if there are N elements or more or if the bottom of the tree was reached

            var clusters = new List<MapCluster> {this};
            var annotations = new List<MapCluster>();
            List<MapCluster> previousLevelClusters = null;
            List<MapCluster> previousLevelAnnotations = null;
            bool clustersDidChange = true;
            while (clusters.Count() + annotations.Count() < N && clusters.Any() && clustersDidChange)
            {
                previousLevelAnnotations = new List<MapCluster>(annotations);
                previousLevelClusters = new List<MapCluster>(clusters);
                clustersDidChange = false;
                var nextLevelClusters = new List<MapCluster>();
                foreach (MapCluster cluster in clusters) {
                    foreach (MapCluster child in cluster.Children()) {
                        if (child.Annotation != null) {
                            annotations.Add(child);
                        } else {
                            if (mapRect.IntersectsWith(child.CoordinateRect)) {
                                nextLevelClusters.Add(child);
                            }
                        }
                    }
                }  
                if (nextLevelClusters.Any()) {
                    clusters = nextLevelClusters;
                    clustersDidChange = true;
                }
            }
            CleanClustersFromAncestorsOfClusters(clusters, annotations);
    
            if (clusters.Count() + annotations.Count() > N) { 
                // if there are too many clusters and annotations, that means that we went one level too far in depth
                clusters = previousLevelClusters;
                annotations = previousLevelAnnotations;
                CleanClustersFromAncestorsOfClusters(clusters, annotations);
            }
            if (clusters != null) annotations.AddRange(clusters);

            CleanClustersOutsideMapRect(annotations, mapRect);
            

            return annotations;
        }

        private void CleanClustersOutsideMapRect(ICollection<MapCluster> clusters, CoordinateRect mapRect)
        {
            var clustersToRemove = clusters.Where(cluster =>
            {
                bool toRemove = !mapRect.PointWithin(cluster.Coordinate);
                if (toRemove)
                {
                    return true;
                }
                List<IAnnotation> originalAnnotations = cluster.OriginalAnnotations;
                foreach (var annotation in originalAnnotations)
                {
                    if (mapRect.PointWithin(annotation.Coordinate))
                    {
                        return false;
                    }
                }
                return true;
            }).ToList();
            foreach (var mapCluster in clustersToRemove)
            {
                clusters.Remove(mapCluster);
            }
        }

        private void CleanClustersFromAncestorsOfClusters(ICollection<MapCluster> clusters, IEnumerable<MapCluster> referenceClusters)
        {
            var clustersToRemove = clusters.Where(cluster => referenceClusters.Any(cluster.IsAncestorOfCluster)).ToList();
            foreach (var mapCluster in clustersToRemove)
            {
                clusters.Remove(mapCluster);
            }
        }

        public bool IsAncestorOfCluster(MapCluster mapCluster) {
            return Depth < mapCluster.Depth && 
                (_leftChild == mapCluster || 
                _rightChild == mapCluster || 
                _leftChild.IsAncestorOfCluster(mapCluster) ||
                _rightChild.IsAncestorOfCluster(mapCluster));
        }

        public bool IsRootClusterForAnnotation(IAnnotation annotation){
            return Annotation == annotation ||
            _leftChild.IsRootClusterForAnnotation(annotation) ||
            _rightChild.IsRootClusterForAnnotation(annotation);
        }

        private IEnumerable<MapCluster> Children()
        {
            var children = new List<MapCluster>(2);
            if (_leftChild != null) {
                children.Add(_leftChild);
            }
            if (_rightChild != null) {
                children.Add(_rightChild);
            }
            return children;
        }
    }
}
