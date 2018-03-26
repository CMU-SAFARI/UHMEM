using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MemMap
{
    public class AndCoupledStaticWindow : MemWBMode
    {
        public uint window;
        public uint wb_mode_cycles;

        public AndCoupledStaticWindow(MemCtrl[] mctrls)
            : base(mctrls)
        {
            window = Config.mctrl.wb_window;
        }

        public override void tick(uint cid)
        {
            if (cid != 0) return;

            cycles++;

            //check for end of wb_mode
            if (wb_mode[0]) {
                wb_mode_cycles++;
                if (wb_mode_cycles == window) {
                    for (uint i = 0; i < cmax; i++) {
                        wb_mode[i] = false;
                    }
                }
            }

            //check for start of wb_mode
            if (wb_mode[0])
                return;

            bool all_writeq_full = is_writeq_full(0);
            bool all_readq_empty = is_readq_empty(0);

            for (uint i = 1; i < cmax; i++) {
                all_writeq_full = all_writeq_full && is_writeq_full(i);
                all_readq_empty = all_readq_empty && is_readq_empty(i);
            }

            if (all_writeq_full || all_readq_empty) {
                for (uint i = 0; i < cmax; i++) {
                    wb_mode[i] = true;
                }
                wb_mode_cycles = 0;
            }
        }
    }
}
