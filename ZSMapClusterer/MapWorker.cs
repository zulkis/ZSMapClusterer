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
using System.Device.Location;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Phone.Maps.Controls;
using ZSMapClusterer.Extensions;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ZSMapClusterer
{
    public class MapWorker
    {
        private const int MaximumClustersNumber = 13;
        private const int MapUpdateDelayMillisecods = 400;

        private readonly Map _map;
        private List<IAnnotation> _annotations;
        private MapOverlay _userLocationOverlay;

        private readonly DataTemplate _singleTemplate;
        private readonly DataTemplate _groupTemplate;
        private readonly DataTemplate _userTemplate;

        private Point _lastMapCenter;

        //private bool _mapInteractionsInProgress = false;
        private DispatcherTimer _updateTimer = null;
        private bool _recalculatingGroupsInProgress = false;
        private bool _shouldRecalculateGroups = false;
        private bool _shouldUpdatePinsOnMap = false;
        private readonly CoordinateRect _visibleRect = new CoordinateRect(GeoCoordinate.Unknown, GeoCoordinate.Unknown);

        private readonly MapClusterer _mapClusterer;

        private GeoCoordinate _userLocation = null;
        
		#region Public Only

		public GeoCoordinate UserLocation
        {
            set
            {
                _userLocation = value;
                _map.SetView(myGeoCoordinate, 9, MapAnimationKind.Parabolic);
            }
        }

        public MapWorker(Map map, DataTemplate single, DataTemplate group, DataTemplate user)
        {
            _map = map;
            _singleTemplate = single;
            _groupTemplate = group;
            _userTemplate = user;

            PrepareMapOverlaysPool();

            if (_mapClusterer == null)
            {
                _mapClusterer = new MapClusterer(MaximumClustersNumber);
            }

            _map.ViewChanged += OnViewChanged;
            //_map.CenterChanged += MapInteracted;
            //_map.ZoomLevelChanged += MapInteracted;
            _map.ZoomLevelChanged += _map_ZoomLevelChanged;
            _map.CenterChanged += _map_CenterChanged;
            _map.Loaded += _map_Loaded;
        }

        public void UpdateWithAnnotations(List<IAnnotation> annotations)
        {
            _annotations = annotations;
            UpdatePinsOnMap();
        }

		#endregion


        #region Private Only
        private void _map_ZoomLevelChanged(object sender, MapZoomLevelChangedEventArgs e)
        {
            UpdateWithDelay();
        }

        private void _map_CenterChanged(object sender, MapCenterChangedEventArgs e)
        {
            Point mapCenter = _map.ConvertGeoCoordinateToViewportPoint(_map.Center);
            //System.Diagnostics.Debug.WriteLine(_lastMapCenter + "TO:" + mapCenter);
            double dist = Math.Abs(_lastMapCenter.GetDistanceTo(mapCenter));
            bool shouldReload = dist > 1;
            _lastMapCenter = mapCenter;

            if (shouldReload)
            {
                UpdateWithDelay();
            }
        }

        private void _map_Loaded(object sender, RoutedEventArgs e)
        {
            if (_shouldUpdatePinsOnMap)
            {
                RenderPushpins();
                _shouldUpdatePinsOnMap = false;
            }
        }

        private void UpdateWithDelay()
        {

            if (_updateTimer != null)
            {
                _updateTimer.Stop();
            }
            UpdateFilter();
            if (_recalculatingGroupsInProgress)
            {
                _shouldRecalculateGroups = true;
            }
            else
            {
                _updateTimer = ActionUtil.Run(RenderPushpins, TimeSpan.FromMilliseconds(MapUpdateDelayMillisecods));
            }
        }


        private void PrepareMapOverlaysPool()
        {
            MapLayer layer = null;
            if (_map.Layers.Count == 0)
            {
                layer = new MapLayer();
                _map.Layers.Add(layer);
            }
            else
            {
                layer = _map.Layers.First();
            }
            for (int i = 0; i < MaximumClustersNumber; i++)
            {
                var overlay = new MapOverlay
                {
                    GeoCoordinate = GeoCoordinate.Unknown
                };
                layer.Add(overlay);
            }
        }

        private void UpdatePinsOnMap()
        {
            _mapClusterer.SetAnnotations(_chargerStationPushpins);
            if (_map.ActualWidth > 0 && _map.ActualHeight > 0)
            {
                UpdateWithDelay();
            }
            else
            {
                _shouldUpdatePinsOnMap = true;
            }
        }

        private void OnViewChanged(object sender, MapViewChangedEventArgs a)
        {
            UpdateFilter();
            RenderPushpins();
        }

        private void UpdateFilter()
        {
            GeoCoordinate topRight = _map.ConvertViewportPointToGeoCoordinate(new Point(_map.ActualWidth, 0));
            if (topRight == null)
            {
                //Hack for getting coordinates height. Can be improved
                topRight = _map.ConvertViewportPointToGeoCoordinate(new Point(_map.ActualWidth, _map.ActualHeight / 2 + _map.ActualHeight / 3));
                topRight.Latitude = 85;
            }
            GeoCoordinate bottomLeft = _map.ConvertViewportPointToGeoCoordinate(new Point(0, _map.ActualHeight));
            if (bottomLeft == null)
            {
                //Hack for getting coordinates height. Can be improved
                bottomLeft = _map.ConvertViewportPointToGeoCoordinate(new Point(0, _map.ActualHeight / 2 - _map.ActualHeight / 3));
                bottomLeft.Latitude = -85;
            }
            _visibleRect.NortheastCoordinates = topRight;
            _visibleRect.SouthwestCoordinates = bottomLeft;
        }

        private void RenderPushpins()
        {
            if (!_recalculatingGroupsInProgress)
            {
                _recalculatingGroupsInProgress = true;
                var context = TaskScheduler.FromCurrentSynchronizationContext();

                Task<List<ClusterAnnotation>> clusterMapTask = Task.Factory.StartNew(() =>
                {
                    List<ClusterAnnotation> annotationsToShow = _mapClusterer.ClusterAnnotationsInCoordinateRect(
                        new CoordinateRect(_visibleRect.SouthwestCoordinates, _visibleRect.NortheastCoordinates));
                    return annotationsToShow;
                });

                clusterMapTask.ContinueWith((task) =>
                {
                    if (_shouldRecalculateGroups)
                    {
                        _shouldRecalculateGroups = false;
                        _recalculatingGroupsInProgress = false;
                        RenderPushpins();
                    }
                    else
                    {
                        List<ClusterAnnotation> annotationsToShow = task.Result;
                        _map.Dispatcher.BeginInvoke(() =>
                        {
                            MapLayer layer = null;
                            if (_map.Layers.Count == 0)
                            {
                                layer = new MapLayer();
                                _map.Layers.Add(layer);
                            }
                            else
                            {
                                layer = _map.Layers.First();
                            }
                            List<MapOverlay> overlaysWithoutUserLocation = layer.ToList();
                            overlaysWithoutUserLocation.Remove(_userLocationOverlay);

                            int overlaysDiff = overlaysWithoutUserLocation.Count() - annotationsToShow.Count();

                            //TODO: check for overlaysDiff < 1 and handle this
                            Debug.Assert(overlaysDiff > 1);
                            if (overlaysDiff > 0)
                            {
                                for (int i = 0; i < overlaysDiff; i++)
                                {
                                    MapOverlay o = overlaysWithoutUserLocation.First();
                                    o.ContentTemplate = null;
                                    o.Content = null;
                                    o.GeoCoordinate = GeoCoordinate.Unknown;
                                    overlaysWithoutUserLocation.Remove(o);
                                }
                            }
                            int annotationsCount = annotationsToShow.Count();
                            for (int i = 0; i < annotationsCount; i++)
                            {
                                var annotation = annotationsToShow[i];
                                MapOverlay overlay = overlaysWithoutUserLocation[i];
                                overlay.GeoCoordinate = annotation.Coordinate;
                                overlay.Content = annotation.Cluster;
                                if (annotation.Cluster != null)
                                {
                                    overlay.ContentTemplate = annotation.Cluster.Annotation == null
                                        ? _groupTemplate
                                        : _singleTemplate;
                                }
                            }

                            if (_userLocation != null)
                            {
                                if (_userLocationOverlay == null)
                                {
                                    _userLocationOverlay = new MapOverlay
                                    {
                                        //Uncomment following to get user location title from app Localized strings:

                                        //Content = AppResources.ResourceManager.GetString("MeString",
                                        //   AppResources.Culture),
                                        Content = "Me",
                                        ContentTemplate = _userTemplate,
                                        GeoCoordinate = _userLocation
                                    };
                                    layer.Add(_userLocationOverlay);
                                }
                                _userLocationOverlay.GeoCoordinate = _userLocation;
                            }

                            _recalculatingGroupsInProgress = false;
                        });
                    }
                }, context);

            }
        }
        #endregion
    }
}
