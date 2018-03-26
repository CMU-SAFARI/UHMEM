using System;
using System.Collections.Generic;
using System.Text;

using System.IO;

namespace MemMap
{
    public class MDrainPI : WriteBackSched
    {
        public uint wb_marked;
        public uint[] wb_marked_perproc;

        public MDrainPI()
        {
            wb_mode = false;
            wb_marked_perproc = new uint[Config.N];
        }

        public override void enqueue_req(Req req) { }
        public override void dequeue_req(Req req)
        {
            if (req.wb_marked) {
                Dbg.Assert(req.type == ReqType.WR);
                wb_marked--;
                wb_marked_perproc[req.pid]--;
                Dbg.Assert(wb_marked >= 0);
                Dbg.Assert(wb_marked_perproc[req.pid] >= 0);
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
                    wb_marked_perproc[req.pid]++;
                }
            }
        }

        public override Req find_best_req(Bank bank)
        {
            List<Req> readq = meta_mctrl.get_readq(bank);
            List<Req> writeq = meta_mctrl.get_writeq(bank);

            //build r/w merged queue
            List<Req> mergedq = new List<Req>();
            mergedq.AddRange(readq);
            foreach (Req req in writeq) {
                if (!req.wb_marked) continue;
                mergedq.Add(req);
            }

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