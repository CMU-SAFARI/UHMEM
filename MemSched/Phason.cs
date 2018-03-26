using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace MemMap
{
    public class Phason : MemSched
    {
        int[,,,] stall_shared_prev;
        int[,,,] stall_shared;

        public override void enqueue_req(Req req) { }
        public override void dequeue_req(Req req) { }

        public Phason()
        {
            stall_shared_prev = new int[Config.N,Config.mem.channel_max,Config.mem.rank_max,8];
            stall_shared = new int[Config.N,Config.mem.channel_max,Config.mem.rank_max,8];
        }

        public override Req better_req(Req req1, Req req2)
        {
            Req stall = null;
            Req fr = null;

            int stall1 = stall_shared_prev[req1.pid,req1.addr.cid,req1.addr.rid,req1.addr.bid];
            int stall2 = stall_shared_prev[req2.pid,req2.addr.cid,req1.addr.rid,req2.addr.bid];

            if (stall1 > stall2) {
                stall = req1;
            }
            else if (stall1 < stall2) {
                stall = req2;
            }

            // otherwise, fall back to FR-FCFS
            bool hit1 = is_row_hit(req1);
            bool hit2 = is_row_hit(req2);
            if (hit1 ^ hit2) {
                if (hit1) fr = req1;
                else fr = req2;
            }

            if (Config.sched.phason_prioritize_fr && fr != null) {
                return fr;
            }
            else if (stall != null) {
                return stall;
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

            foreach (Bank b in meta_mctrl.banks) {
                Req curr_req = meta_mctrl.get_curr_req(b);
                if (curr_req == null) continue;
                int curr_req_proc = curr_req.pid;
                for (int p = 0; p < Config.N; p ++)
                {
                    // NOTE:  this first line is if the window is full also
                    //if (curr_req_proc == p && Sim.procs[p].inst_wnd.is_full() && Sim.procs[p].inst_wnd.is_mem[Sim.procs[p].inst_wnd.oldest] && Sim.procs[p].inst_wnd.addr[Sim.procs[p].inst_wnd.oldest] == curr_req.block_addr)
                    if (curr_req_proc == p && Sim.procs[p].inst_wnd.is_mem[Sim.procs[p].inst_wnd.oldest] && Sim.procs[p].inst_wnd.addr[Sim.procs[p].inst_wnd.oldest] == curr_req.block_addr)
                    {
                        stall_shared[p,curr_req.addr.cid,curr_req.addr.rid,curr_req.addr.bid]++;
                    }
                }
            }

            if (meta_mctrl.get_cycles() % Config.sched.interval_cycles == 0) {
                for (int n = 0; n < Config.N; n++) {
                    //Console.Write("Core " + n + ": ");
                    for (int chan = 0; chan < Config.mem.channel_max; chan++) {
                        //Console.Write("Channel " + chan + ": ");
                        for (int rank = 0; rank < Config.mem.rank_max; rank++) {
                            //Console.Write("Rank " + rank + ": ");
                            for (int bank = 0; bank < 8; bank++) {
                                stall_shared_prev[n,chan,rank,bank] = stall_shared[n,chan,rank,bank];
                                //Console.Write(stall_shared_prev[n,chan,rank,bank] + " ");
                                stall_shared[n,chan,rank,bank] = 0;
                            }
                        }
                    }
                    //Console.WriteLine();
                }
            }
        }
    }
}
