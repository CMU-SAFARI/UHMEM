using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace MemMap {
    public class FRLTF : MemSched {
        public override void enqueue_req(Req req) { }
        public override void dequeue_req(Req req) { }

        static Random rand = new Random(0);

        public override Req find_best_req(List<Req> q)
        {
            if (q.Count == 0)
                return null;

            Dictionary<ulong, int> dict = new Dictionary<ulong, int>();
            foreach (Req req in q) {
                if (!dict.ContainsKey(req.addr.rowid))
                    dict.Add(req.addr.rowid, 0);
                dict[req.addr.rowid] += 1;
            }

            foreach (Req req in q) {
                req.transaction_length = dict[req.addr.rowid];
            }

            Req best_req = q[0];
            for (int i = 1; i < q.Count; i++) {
                best_req = better_req(best_req, q[i]);
            }
            return best_req;
        }

        public override Req better_req(Req req1, Req req2)
        {
            //first-ready
            bool hit1 = is_row_hit(req1);
            bool hit2 = is_row_hit(req2);
            if (hit1 ^ hit2) {
                if (hit1) return req1;
                else return req2;
            }

            //longest-transaction
            int transaction1 = req1.transaction_length;
            int transaction2 = req2.transaction_length;
            if (transaction1 != transaction2) {
                if (transaction1 > transaction2) return req1;
                else return req2;
            }

            //random tie-breaker
            if (rand.Next(2) == 0) return req1;
            else return req2;
        }
    }
}