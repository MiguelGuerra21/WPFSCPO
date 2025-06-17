using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPFMapSUi
{
    public class BatchEditAttribute
    {
        public string Key { get; set; }
        public string CurrentValue { get; set; }
        public string NewValue { get; set; }
        public Type ValueType { get; set; }
    }
}
