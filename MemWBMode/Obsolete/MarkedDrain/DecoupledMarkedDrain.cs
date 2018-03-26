using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace MemMap
{
    public class DecoupledMarkedDrain : MemWBMode
    {
        public double drain_fraction;

        public DecoupledMarkedDrain(MemCtrl[] mctrls)
            : base(mctrls)
        {
            drain_fraction = Config.mctrl.drain_fraction;
        }

        public override void tick(uint cid)
        {
            if (cid != 0) return;

            cycles++;

            //check for end of wb_mode
            for (uint i = 0; i < cmax; i++) {
                if (!wb_mode[i])
                    continue;

                MemCtrl mctrl = mctrls[i];

                int wb_marked_cnt = 0;
                foreach (Req req in mctrl.mctrl_writeq) {
                    if (req.wb_marked) {
                        wb_marked_cnt++;
                    }
                }

                if (wb_marked_cnt > (1 - drain_fraction) * mctrl.mctrl_writeq.Capacity)
                    continue;

                foreach (Req req in mctrl.mctrl_writeq) {
                    req.wb_marked = false;
                }

                wb_mode[i] = false;
            }

            //check for start of wb_mode
            for (uint i = 0; i < cmax; i++) {
                if (wb_mode[i])
                    continue;

                if (!is_writeq_full(i))
                    continue;

                MemCtrl mctrl = mctrls[i];
                foreach (Req req in mctrl.mctrl_writeq) {
                    Dbg.Assert(!req.wb_marked);
                    req.wb_marked = true;
                }

                wb_mode[i] = true;
            }
        }
    }
}
