using System;
using System.Collections.Generic;
using System.Text;

using System.IO;

namespace MemMap
{
    public abstract class MemSched2
    {
        //memory controller
        public MetaMemCtrl2 meta_mctrl;

        public virtual void initialize() { }
        public virtual void issue_req(Req req) { }
        public abstract void dequeue_req(Req req);
        public abstract void enqueue_req(Req req);

        //scheduler-specific overridden method
        public abstract Req better_req(Req req1, Req req2);

        public virtual void tick() { }

        protected bool is_row_hit(Req req)
        {
            return meta_mctrl.is_row_hit(req);
        }

        public virtual Req find_best_req(List<Req> q)
        {
            if (q.Count == 0)
                return null;

            Req best_req = q[0];
            for (int i = 1; i < q.Count; i++) {
                best_req = better_req(best_req, q[i]);
            }
            return best_req;
        }
    }
}
