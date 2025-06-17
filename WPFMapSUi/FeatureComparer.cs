using Mapsui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPFMapSUi
{
    // Helper class for comparing features
    public class FeatureComparer : IEqualityComparer<IFeature>
    {
        public bool Equals(IFeature x, IFeature y)
        {
            if (x == null || y == null) return false;
            return x.GetHashCode() == y.GetHashCode(); // Or better comparison logic
        }

        public int GetHashCode(IFeature obj) => obj?.GetHashCode() ?? 0;
    }
}
