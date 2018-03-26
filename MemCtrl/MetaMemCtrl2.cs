using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace MemMap
{
    public class MetaMemCtrl2 : MetaMemCtrl
    {
        new public bool is_omniscient;
        new public MemCtrl2 mctrl;
        new public MemCtrl2[] mctrls;
        new public MemSched2 sched;
        new public MemSched2 wbsched;
        new public List<Bank2> banks;

        //non-omniscient
        public MetaMemCtrl2(MemCtrl2 mctrl, MemSched2 sched, MemSched2 wbsched)
        {
            is_omniscient = false;
            this.mctrl = mctrl;
            this.mctrls = new MemCtrl2[] { mctrl };
            this.sched = sched;
            this.wbsched = wbsched;

            set_banks();
        }

        //omniscient
        public MetaMemCtrl2(MemCtrl2[] mctrls, MemSched2 sched, MemSched2 wbsched)
        {
            is_omniscient = true;
            this.mctrl = null;
            this.mctrls = mctrls;
            this.sched = sched;
            this.wbsched = wbsched;

            set_banks();
        }

        private void set_banks()
        {
            banks = new List<Bank2>();
            foreach (MemCtrl2 mc in this.mctrls) {
                Channel2 chan = mc.chan;
                for (uint r = 0; r < chan.rmax; r++) {
                    Rank2 rank = chan.ranks[r];
                    for (uint b = 0; b < rank.bmax; b++) {
                        Bank2 bank = rank.banks[b];
                        banks.Add(bank);
                    }
                }
            }
        }

        new public void issue_req(Req req)
        {
            sched.issue_req(req);
            wbsched.issue_req(req);
        }

        new public void enqueue_req(Req req)
        {
            sched.enqueue_req(req);
            wbsched.issue_req(req);
        }

        new public void dequeue_req(Req req)
        {
            sched.dequeue_req(req);
            wbsched.issue_req(req);
        }

        new public bool is_row_hit(Req req)
        {
            MemCtrl2 mctrl = get_mctrl(req);

            Bank2 bank = mctrl.chan.ranks[req.addr.rid].banks[req.addr.bid];
            return bank.curr_rowid == (long)req.addr.rowid;
        }

        new public Req find_best_rd_req(List<Req> q)
        {
            return sched.find_best_req(q);
        }

        new public Req find_best_wb_req(List<Req> wq)
        {
            return wbsched.find_best_req(wq);
        }

        new public Req better_req(Req req1, Req req2)
        {
            return sched.better_req(req1, req2);
        }

        new public Req better_wb_req(Req req1, Req req2)
        {
            return wbsched.better_req(req1, req2);

        }

        new public virtual void tick(uint cid)
        {
            /* non-omniscient */
            if (!is_omniscient) {
                sched.tick();
                if (!Config.sched.same_sched_algo) {
                    wbsched.tick();
                }
                return;
            }

            /* omniscient */
            if (cid == 0) {
                sched.tick();
                if (!Config.sched.same_sched_algo) {
                    wbsched.tick();
                }

    /*            foreach (Bank2 b in banks) {
                    foreach (Req r in b.mc.inflightqs[b.rid, b.bid]) {
                        Sim.region_contention[r.addr.cid,r.addr.rid,r.addr.bid]++;
                    }
                }

                if (get_cycles() % Config.sched.acts_quantum_cycles == 0) {
                    int hotness = 0;
                    for (int chan = 0; chan < Config.mem2.channel_max; chan++) {
                        //Console.Write("Channel " + chan + ": ");
                        for (int rank = 0; rank < Config.mem2.rank_max; rank++) {
                            //Console.Write("Rank " + rank + ": ");
                            for (int bank = 0; bank < 8; bank++) {
                                //Console.Write(Sim.region_contention[chan,rank,bank] + " ");
                                if (Sim.region_contention[chan,rank,bank] > hotness || hotness == -1) {
                                    Sim.hot_region_chan = chan;
                                    Sim.hot_region_rank = rank;
                                    Sim.hot_region_bank = bank;
                                    hotness = Sim.region_contention[chan,rank,bank];
                                }
                                Sim.region_contention[chan,rank,bank] = 0;
                            }
                        }
                    }
                    //Console.WriteLine();
                    //Console.Write("Threads: ");
                    int criticality = 0;
                    Sim.critical_thread = -1;
                    for (int n = 0; n < Config.N; n++) {
                        //Console.Write(Sim.thread_criticality[n] + " ");
                        if (Sim.thread_criticality[n] > criticality) {
                            Sim.critical_thread = n;
                            criticality = Sim.thread_criticality[n];
                        }
                        Sim.thread_criticality[n] = 0;
                    }
                    //Console.WriteLine();
                    //Console.WriteLine("Critical threads: " + Sim.critical_thread);
                }
      */
          }
       }

        new public long get_cycles()
        {
            if (!is_omniscient) {
                return mctrl.cycles;
            }

            return mctrls[0].cycles;
        }

        public Req get_curr_req(Bank2 bank)
        {
            MemCtrl2 mc = bank.mc;
            List<Req> inflight_q = mc.inflightqs[bank.rid, bank.bid];
            if (inflight_q.Count == 0)
                return null;

            return inflight_q[inflight_q.Count - 1];
        }

        new public MemCtrl2 get_mctrl(Req req)
        {
            if (!is_omniscient) {
                Dbg.Assert(mctrl.cid == req.addr.cid);
                return mctrl;
            }
            return mctrls[req.addr.cid];
        }

        public MemCtrl2 get_mctrl(Bank2 bank)
        {
            if (!is_omniscient) {
                Dbg.Assert(mctrl.cid == bank.cid);
                return mctrl;
            }
            return mctrls[bank.cid];
        }

        new public uint get_bmax()
        {
            return (uint) banks.Count;
        }

        new public uint get_bid(Req req)
        {
            uint cid = req.addr.cid;
            uint rid = req.addr.rid;
            uint bid = req.addr.bid;

            uint global_bid = 0;
            if (is_omniscient && cid > 0) {
                global_bid += (cid - 1) * mctrls[0].rmax * mctrls[0].bmax;
            }
            if(rid > 0){
                global_bid += (rid - 1) * mctrls[0].bmax;
            }
            global_bid += bid;
            return global_bid;
        }

        new public uint get_rload()
        {
            return get_load(true);
        }

        new public uint get_wload()
        {
            return get_load(false);
        }

        private uint get_load(bool read)
        {
            if (!is_omniscient) {
                if(read) return mctrl.rload;
                return mctrl.wload;
            }

            uint load = 0;
            foreach (MemCtrl2 mc in mctrls) {
                if (read) load += mc.rload;
                else load += mc.wload;
            }
            return load;
        }

        new public bool is_writeq_full()
        {
            return get_writeq_max() == get_wload();
        }

        new public uint get_writeq_max()
        {
            if (!is_omniscient) return (uint) mctrl.mctrl_writeq.Capacity;

            uint writeq_max = 0;
            foreach (MemCtrl2 mc in mctrls) {
                writeq_max += (uint) mc.mctrl_writeq.Capacity;
            }
            return writeq_max;
        }

        new public uint get_load_per_proc(uint pid)
        {
            if (!is_omniscient)
                return mctrl.rload_per_proc[pid];

            uint load = 0;
            foreach (MemCtrl2 m in mctrls) {
                load += m.rload_per_proc[pid];
            }
            return load;
        }

        new public uint get_load_per_procbank(uint pid, uint bid)
        {
            uint banks_per_mctrl = mctrls[0].rmax * mctrls[0].bmax;
            uint banks_per_rank = mctrls[0].bmax;

            uint rid = (bid % banks_per_mctrl) / banks_per_rank;
            uint local_bid = bid % mctrls[0].bmax;

            if (!is_omniscient) {
                return mctrl.rload_per_procrankbank[pid, rid, local_bid];
            }

            uint mid = bid / banks_per_mctrl;
            return mctrls[(int) mid].rload_per_procrankbank[pid, rid, local_bid];
        }

        public List<Req> get_readq(Bank2 bank)
        {
            MemCtrl2 mc = get_mctrl(bank);
            return mc.readqs[bank.rid, bank.bid];
        }

        public List<Req> get_writeq(Bank2 bank)
        {
            MemCtrl2 mc = get_mctrl(bank);
            return mc.writeqs[bank.rid, bank.bid];
        }
    }
}
