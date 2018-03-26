using System;
using System.Collections.Generic;
using System.Text;

using System.IO;

namespace MemMap
{
    public class RWSqueeze : WriteBackSched
    {
        public RWSqueeze()
        {
            wb_mode = false;
        }

        public override void enqueue_req(Req req) { }
        public override void dequeue_req(Req req) { }

        public override void tick()
        {
            if (meta_mctrl.is_writeq_full()) wb_mode = true;
            else if (meta_mctrl.get_rload() == 0) wb_mode = true;
            else if (is_rw_hit()) wb_mode = true;
            else wb_mode = false;
        }

        public bool is_rw_hit()
        {
            foreach (Bank bank in meta_mctrl.banks) {
                bool read_hit = false;
                List<Req> readq = meta_mctrl.get_readq(bank);
                foreach (Req req in readq) {
                    if (meta_mctrl.is_row_hit(req)) {
                        read_hit = true;
                        break;
                    }
                }
                if (read_hit) continue;
                
                bool write_hit = false;
                List<Req> writeq = meta_mctrl.get_writeq(bank);
                foreach (Req req in writeq) {
                    if (meta_mctrl.is_row_hit(req)) {
                        write_hit = true;
                        break;
                    }
                }
                if(write_hit) return true;
            }
            return false;
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