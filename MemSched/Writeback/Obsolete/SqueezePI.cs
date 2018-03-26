using System;
using System.Collections.Generic;
using System.Text;

using System.IO;

namespace MemMap
{
    public class SqueezePI : WriteBackSched
    {
        public SqueezePI()
        {
            wb_mode = false;
        }

        public override void enqueue_req(Req req) { }
        public override void dequeue_req(Req req) { }

        public override void tick()
        {
            if (meta_mctrl.is_writeq_full()) wb_mode = true;
            else if (meta_mctrl.get_rload() == 0) wb_mode = true;
            else wb_mode = false;
        }

        public override Req find_best_req(Bank bank)
        {
            List<Req> readq = meta_mctrl.get_readq(bank);
            List<Req> writeq = meta_mctrl.get_writeq(bank);

            //build r/w merged queue
            List<Req> mergedq = new List<Req>();
            mergedq.AddRange(readq);
            mergedq.AddRange(writeq);
            return meta_mctrl.sched.find_best_req(mergedq);
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