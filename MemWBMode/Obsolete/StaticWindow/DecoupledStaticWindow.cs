using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace MemMap
{
    public class DecoupledStaticWindow : MemWBMode
    {
        public uint window;
        public uint[] wb_mode_cycles;

        public DecoupledStaticWindow(MemCtrl[] mctrls)
            : base(mctrls)
        {
            window = Config.mctrl.wb_window;
            wb_mode_cycles = new uint[cmax];
        }

        public override void tick(uint cid)
        {
            if (cid != 0) return;

            cycles++;

            //check for end of wb_mode
            for (uint i = 0; i < cmax; i++) {
                if (!wb_mode[i])
                    continue;

                wb_mode_cycles[i]++;
                if (wb_mode_cycles[i] < window)
                    continue;

                wb_mode[i] = false;
            }

            //check for start of wb_mode
            for (uint i = 0; i < cmax; i++) {
                if (wb_mode[i])
                    continue;

                if (!is_writeq_full(i) && !is_readq_empty(i))
                    continue;
                
                wb_mode[i] = true;
                wb_mode_cycles[i] = 0;
            }
        }
    }
}
