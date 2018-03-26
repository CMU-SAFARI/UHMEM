using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace MemMap
{
    public class RBHR : MemSched
    {
        public override void enqueue_req(Req req) { }
        public override void dequeue_req(Req req) { }

        public override Req better_req(Req req1, Req req2)
        {
            Req crit = null;
            Req fr = null;

            double rbhr1 = ((double)Stat.procs[req1.pid].row_hit_rate_read.Count)
                    / Stat.procs[req1.pid].row_hit_rate_read.Sample;
            double rbhr2 = ((double)Stat.procs[req2.pid].row_hit_rate_read.Count)
                    / Stat.procs[req2.pid].row_hit_rate_read.Sample;

            if (rbhr1 < rbhr2) {
                crit = req1;
            }
            else if (rbhr2 < rbhr1) {
                crit = req2;
            }

            bool hit1 = is_row_hit(req1);
            bool hit2 = is_row_hit(req2);
            if (hit1 ^ hit2) {
                if (hit1) fr = req1;
                else fr = req2;
            }

            if (Config.sched.acts_prioritize_fr && fr != null) {
                return fr;
            }
            else if (crit != null) {
                return crit;
            }
            else if (fr != null) {
                return fr;
            }

            if (req1.ts_arrival <= req2.ts_arrival) return req1;

            else return req2;
        }

        public override void tick()
        {
            base.tick();
        }
    }
}
