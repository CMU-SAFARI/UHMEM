using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MemMap
{
    public abstract class WriteBackSched : MemSched
    {
        public MemCtrl mctrl;
        public bool wb_mode;
    }
}
