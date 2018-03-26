using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace MemMap
{
    public class ACTS : MemSched
    {
        public override void enqueue_req(Req req) { }
        public override void dequeue_req(Req req) { }

        public override Req better_req(Req req1, Req req2)
        {
            Req crit = null;
            Req fr = null;

 /*           if (req1.pid == Sim.critical_thread && req2.pid != Sim.critical_thread) {
                crit = req1;
            }
            else if (req1.pid != Sim.critical_thread && req2.pid == Sim.critical_thread) {
                crit = req2;
            }
*/
            crit = req1;

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

            /*
            foreach (Bank b in meta_mctrl.banks) {
                foreach (Req r in b.mc.inflightqs[b.rid, b.bid]) {
                    Sim.region_contention[r.addr.cid,r.addr.bid]++;
                }
            }

            if (meta_mctrl.get_cycles() % Config.sched.acts_quantum_cycles == 0) {
                int hotness = 0;
                for (int chan = 0; chan < Config.mem.channel_max; chan++) {
                    //Console.Write("Channel " + chan + ": ");
                    for (int bank = 0; bank < 8; bank++) {
                        //Console.Write(region_contention[chan,bank] + " ");
                        if (Sim.region_contention[chan,bank] > hotness || hotness == -1) {
                            Sim.hot_region_chan = chan;
                            Sim.hot_region_bank = bank;
                            hotness = Sim.region_contention[chan,bank];
                        }
                        Sim.region_contention[chan,bank] = 0;
                    }
                }
                //Console.WriteLine();
                //Console.Write("Threads: ");
                int criticality = 0;
                for (int n = 0; n < Config.N; n++) {
                    //Console.Write(thread_criticality[n] + " ");
                    if (Sim.thread_criticality[n] > criticality) {
                        Sim.critical_thread = n;
                        criticality = Sim.thread_criticality[n];
                    }
                    Sim.thread_criticality[n] = 0;
                }
                //Console.WriteLine();
                //Console.WriteLine("Critical threads: " + critical_thread);
            }
            */
        }
    }
}
