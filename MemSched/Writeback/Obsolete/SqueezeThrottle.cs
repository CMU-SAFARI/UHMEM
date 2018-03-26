using System;
using System.Collections.Generic;
using System.Text;

using System.IO;

namespace MemMap
{
    public class SqueezeThrottle : WriteBackSched
    {
        public uint[] cooltime_perproc;
        public List<List<Req>> writeqs;

        public SqueezeThrottle()
        {
            wb_mode = false;
            cooltime_perproc = new uint[Config.N];

            MemCtrl[] mctrls = meta_mctrl.mctrls;
            foreach (MemCtrl m in mctrls) {
                writeqs.Add(m.mctrl_writeq);
            }
        }

        public override void enqueue_req(Req req) { }
        public override void dequeue_req(Req req) { }

        public override void tick()
        {
            if (meta_mctrl.is_writeq_full()) {
                wb_mode = true;
            }
            else if (meta_mctrl.get_rload() == 0) {
                wb_mode = true;
            }
            else wb_mode = false;
        }

        public uint find_wb_culprit()
        {
            uint[] pids = new uint[Config.N];
            for (uint p = 0; p < pids.Length; p++){
                pids[p] = p;
            }

            uint[] wb_perproc = new uint[Config.N];
            foreach (List<Req> q in writeqs) {
                foreach (Req req in q) {
                    wb_perproc[req.pid]++;
                }
            }

            Array.Sort(wb_perproc, pids);
            return pids[pids.Length - 1];
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