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
using System.Device.Location;
using ZSMapClusterer.Extensions;

namespace ZSMapClusterer
{
    public class CoordinateRect
    {
        public GeoCoordinate SouthwestCoordinates { get; set; }
        public GeoCoordinate NortheastCoordinates { get; set; }

        private double X { get; set; }
        private double Y { get; set; }
        private double Width { get; set; }
        private double Height { get; set; }

        private readonly CoordinateRect _leftRect;
        private readonly CoordinateRect _rightRect;

        public CoordinateRect(GeoCoordinate southwestCoordinates, GeoCoordinate northeastCoordinates)
        {
            SouthwestCoordinates = southwestCoordinates;
            NortheastCoordinates = northeastCoordinates;

            X = SouthwestCoordinates.Longitude;
            Y = SouthwestCoordinates.Latitude;

            Width = NortheastCoordinates.Longitude - SouthwestCoordinates.Longitude;
            Height = NortheastCoordinates.Latitude - SouthwestCoordinates.Latitude;

            if (northeastCoordinates.Longitude < southwestCoordinates.Longitude)
            {
                var northeastBoundCoordinate = new GeoCoordinate
                {
                    Latitude = NortheastCoordinates.Latitude,
                    Longitude = 180
                };
                _leftRect = new CoordinateRect(southwestCoordinates, northeastBoundCoordinate);

                var rightRectNortheastCoordinate = new GeoCoordinate
                {
                    Latitude = NortheastCoordinates.Latitude,
                    Longitude = NortheastCoordinates.Longitude
                };
                _rightRect = new CoordinateRect(new GeoCoordinate(southwestCoordinates.Latitude, -180), rightRectNortheastCoordinate);
            }
            else
            {
                _leftRect = null;
                _rightRect = null;
            }
        }

        public static CoordinateRect EarthCoordinateRect()
        {
            return new CoordinateRect(new GeoCoordinate(-90, - 180), new GeoCoordinate(90, 180));
        }
        
        public bool IntersectsWith(CoordinateRect rect)
        {
            if (_leftRect != null && _rightRect != null)
            {
                if (_leftRect.IntersectsWith(rect))
                {
                    return true;
                }
                return _rightRect.IntersectsWith(rect);
            }
            return (rect.X < this.X + this.Width) &&
            (this.X < (rect.X + rect.Width)) &&
            (rect.Y < this.Y + this.Height) &&
            (this.Y < rect.Y + rect.Height);
        }

        public bool PointWithin(GeoCoordinate pt)
        {
            if (_leftRect != null && _rightRect != null)
            {
                if (_leftRect.PointWithin(pt))
                {
                    return true;
                }
                return _rightRect.PointWithin(pt);
            }
            return pt.IsWithin(SouthwestCoordinates, NortheastCoordinates);
            
        }
    }
}
