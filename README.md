ZSMapClusterer
==============

Windows Phone 8 Map Clusterer

How to use:
On Map Loaded:

Loaded += (s, e) =>
{
	if (_mapWorker == null)
  	{
    	_mapWorker = new MapWorker(Map, SingleTemplate, GroupTemplate, UserLocationTemplate);
    }
};

//...

~MapPanoramaPage()
{
	_mapWorker = null;
}

When you need to set some annotations just prepare them like this:

List<IAnnotation> annotations = 
	App.ViewModel.FilteredChargerStations.Select(cs =>
	{
    	var object = new ModelContainsPushpinAndYourClass // simple object contains ref to Pushpin And to your Model object
        {
        	Pushpin = new Pushpin
            {
            	GeoCoordinate = cs.Coordinate
            },
             	Model = object
       	};
        return (IAnnotation)object;
    }).ToList();
Then call:

_mapWorker.UpdateWithAnnotations(annotations);

//I will make some improvements with pushpins later.

