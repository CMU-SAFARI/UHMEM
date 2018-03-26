using System;
using System.Collections.Generic;
using System.Text;

using System.IO;

namespace MemMap
{
    public class MDrain : WriteBackSched
    {
        public uint wb_marked;

        public MDrain()
        {
            wb_mode = false;
        }

        public override void enqueue_req(Req req) { }
        public override void dequeue_req(Req req) {
            if (req.wb_marked) {
                Dbg.Assert(req.type == ReqType.WR);
                wb_marked--;
                Dbg.Assert(wb_marked >= 0);
            }
        }

        public override void tick()
        {
            if (wb_mode) {
                if (wb_marked == 0) {
                    wb_mode = false;
                }
                return;
            }
            if (!meta_mctrl.is_writeq_full()) return;

            wb_mode = true;
            for (int b = 0; b < meta_mctrl.get_bmax(); b++) {
                Bank bank = meta_mctrl.banks[b];
                List<Req> q = meta_mctrl.get_writeq(bank);
                foreach (Req req in q) {
                    Dbg.Assert(!req.wb_marked);
                    req.wb_marked = true;
                    wb_marked++;
                }
            }
        }

        public override Req better_req(Req req1, Req req2)
        {
            bool marked1 = req1.wb_marked;
            bool marked2 = req2.wb_marked;
            if (marked1 ^ marked2) {
                if (marked1) return req1;
                else return req2;
            }

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