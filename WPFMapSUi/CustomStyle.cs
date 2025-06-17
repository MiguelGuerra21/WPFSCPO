using Mapsui;
using Mapsui.Styles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPFMapSUi
{
    public class CustomStyle : IStyle
    {
        private readonly Func<IFeature, double, bool> _selectionChecker;
        private readonly Color _selectedFillColor;
        private readonly Color _unselectedFillColor;
        private readonly Color _selectedOutlineColor;
        private readonly Color _unselectedOutlineColor;
        private readonly Func<object, object, bool> value;

        public CustomStyle(Func<object, object, bool> value)
        {
            this.value = value;
            _selectionChecker = (feature, zoomLevel) => false; // Default implementation  
            _selectedFillColor = Color.Green;
            _unselectedFillColor = Color.Cyan;
            _selectedOutlineColor = Color.DarkGoldenRod;
            _unselectedOutlineColor = Color.Black;
        }


        public bool IsSelected { get; set; } = false;
        public VectorStyle VectorStyle { get; set; } = new VectorStyle
        {
            Fill = new Brush(Color.Green), // GREEN fill  
            Outline = new Pen(Color.DarkGreen) // Black outline  
        };
        public double MinVisible { get; set; }
        public double MaxVisible { get; set; }
        public bool Enabled { get; set; }
        public float Opacity { get; set; } //0.0 to 1.0  
    }
}
