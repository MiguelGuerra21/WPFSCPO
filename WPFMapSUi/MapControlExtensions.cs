using Mapsui;
using System;
using System.Threading.Tasks;
using Mapsui.UI.Wpf;
using System.Windows;
using Mapsui.Extensions;

namespace WPFMapSUi
{
    public static class MapControlExtensions
    {
        public static MPoint ScreenToWorld(this MapControl mapControl, System.Windows.Point screenPoint)
        {
            return mapControl.Map.Navigator.Viewport.ScreenToWorld(screenPoint.X, screenPoint.Y);
        }

        public static System.Windows.Point WorldToScreen(this MapControl mapControl, MPoint worldPoint)
        {
            var screen = mapControl.Map.Navigator.Viewport.WorldToScreen(worldPoint.X, worldPoint.Y);
            return new System.Windows.Point(screen.X, screen.Y);
        }
    }
}
