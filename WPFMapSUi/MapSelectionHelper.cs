// Renaming the class to avoid conflict with another definition of 'SelectionState' in the same namespace.
using Mapsui;
using System.Collections.Generic;
using Mapsui.Nts;
using NetTopologySuite.Geometries;
using Mapsui.Nts.Extensions;

namespace WPFMapSUi
{
    public class MapSelectionState // Updated class name to 'MapSelectionState'
    {
        private IFeature? _selectionBox;
        private Polygon? _selectionBoxNts;

        public bool IsSelecting { get; set; }
        public MPoint? StartPoint { get; set; }
        public List<IFeature> SelectedFeatures { get; set; } = new List<IFeature>();

        public IFeature? SelectionBox
        {
            get => _selectionBox;
            set
            {
                _selectionBox = value;
                _selectionBoxNts = value?.RenderedGeometry as Polygon;
            }
        }

        public Polygon? SelectionBoxNTS
        {
            get => _selectionBoxNts;
            set
            {
                _selectionBoxNts = value;
                _selectionBox = value?.ToFeature();
            }
        }
    }
}
