using System;
using System.Collections.Generic;
using System.Text;

using System.IO;

namespace MemMap
{
    public class SqueezeWindowPreempt : WriteBackSched
    {
        public ulong cycles;
        public ulong wb_mode_start;
        public ulong wb_mode_end;

        public SqueezeWindowPreempt()
        {
            wb_mode = false;
        }

        public override void enqueue_req(Req req) { }
        public override void dequeue_req(Req req) { }

        public override void tick()
        {
            cycles++;

            if (!wb_mode) {
                if (meta_mctrl.get_wload() >= Config.sched.preempt_fraction * meta_mctrl.get_writeq_max() || meta_mctrl.get_rload() == 0) {
                    wb_mode = true;
                    wb_mode_start = cycles;
                }
            }
            else if (cycles - wb_mode_start > Config.sched.squeeze_window) wb_mode = false;
            
        }

        public override Req better_req(Req req1, Req req2)
        {
            bool hit1 = is_row_hit(req1);
            bool hit2 = is_row_hit(req2);
            if (hit1 ^ hit2) {
                if (hit1) return req1;
                else return req2;
            }
            if (req1.ts_arrival <= req2.ts_arrival) return req1;
            else return req2;
        }
    }
}