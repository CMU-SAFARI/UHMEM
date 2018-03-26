using System;
using System.Collections.Generic;
using System.Text;

using System.IO;

namespace MemMap
{
    public class WBMarkedFRFCFS : MemSched
    {
        public override void enqueue_req(Req req) { }
        public override void dequeue_req(Req req) { }

        public override Req better_req(Req req1, Req req2)
        {
            Dbg.Assert(req1.type == ReqType.WR && req2.type == ReqType.WR);

            bool wb_marked1 = req1.wb_marked;
            bool wb_marked2 = req2.wb_marked;
            if (wb_marked1 ^ wb_marked2) {
                if (wb_marked1) return req1;
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