using System;
using System.Collections.Generic;
using System.Linq;

namespace MemMap
{
    public class DRAMTrueLRU : Cache
    {
        protected TagEntry[][] data;

        private int sets;
        private int ways;
        private int latency;

        private long cycles;

        private Queue<Req> reqs;
        private Queue<Req> wbs;

        public static MemCtrl2[][] mctrls2;
        public static MemWBMode2 mwbmode;
        public static BLPTracker2 blptracker;

        //etc: outstanding requests
        public int out_read_req;

        public DRAMTrueLRU()
        {
            this.sets = Config.proc.cache_sets;
            this.ways = Config.proc.cache_ways;
            this.latency = Config.proc.cache_latency;
            data = new TagEntry[sets][];
            reqs = new Queue<Req>();
            wbs = new Queue<Req>();

            DDR3DRAM ddr3 = new DDR3DRAM(Config.mem2.ddr3_type, Config.mem2.clock_factor, Config.mem2.tWR, Config.mem2.tWTR);
            uint cmax = (uint)Config.mem2.channel_max;
            uint rmax = (uint)Config.mem2.rank_max;

            //memory controllers
            mctrls2 = new MemCtrl2[Config.mem2.mctrl_num][];
            for (int n = 0; n < Config.mem2.mctrl_num; n++) {
                mctrls2[n] = new MemCtrl2[cmax];
                for (int i = 0; i < mctrls2[n].Length; i++) {
                    mctrls2[n][i] = new MemCtrl2(rmax, ddr3);
                }
            }

            //memory schedulers and metamemory controllers
            if (!Config.sched.is_omniscient) {
                MemSched2[][] scheds = new MemSched2[Config.mem2.mctrl_num][];
                for (int n = 0; n < Config.mem2.mctrl_num; n++) {
                    scheds[n] = new MemSched2[cmax];
                    for (int i = 0; i < cmax; i++) {
                        scheds[n][i] = Activator.CreateInstance(Config.sched.typeof_sched_algo2) as MemSched2;
                    }
                }

                MemSched2[][] wbscheds = new MemSched2[Config.mem2.mctrl_num][];
                for (int n = 0; n < Config.mem2.mctrl_num; n++) {
                    wbscheds[n] = new MemSched2[cmax];
                    if (!Config.sched.same_sched_algo) {
                        for (int i = 0; i < cmax; i++) {
                            wbscheds[n][i] = Activator.CreateInstance(Config.sched.typeof_wbsched_algo2) as MemSched2;
                        }
                    }
                    else {
                        for (int i = 0; i < cmax; i++) {
                            wbscheds[n][i] = scheds[n][i];
                        }
                    }
                }

                MetaMemCtrl2[][] meta_mctrls2 = new MetaMemCtrl2[Config.mem2.mctrl_num][];
                for (int n = 0; n < Config.mem2.mctrl_num; n++) {
                    meta_mctrls2[n] = new MetaMemCtrl2[cmax];
                    for (int i = 0; i < cmax; i++) {
                        meta_mctrls2[n][i] = new MetaMemCtrl2(mctrls2[n][i], scheds[n][i], wbscheds[n][i]);
                        mctrls2[n][i].meta_mctrl = meta_mctrls2[n][i];
                        scheds[n][i].meta_mctrl = meta_mctrls2[n][i];
                        scheds[n][i].initialize();
                        wbscheds[n][i].meta_mctrl = meta_mctrls2[n][i];
                        wbscheds[n][i].initialize();
                    }
                }
            }
            else {
                MemSched2[] sched = new MemSched2[Config.mem2.mctrl_num];
                MemSched2[] wbsched = new MemSched2[Config.mem2.mctrl_num];
                for (int n = 0; n < Config.mem2.mctrl_num; n++) {
                    sched[n] = Activator.CreateInstance(Config.sched.typeof_sched_algo2) as MemSched2;
                    if (!Config.sched.same_sched_algo) {
                        wbsched[n] = Activator.CreateInstance(Config.sched.typeof_wbsched_algo2) as MemSched2;
                    }
                    else {
                        wbsched[n] = sched[n];
                    }
                }

                MetaMemCtrl2[] meta_mctrl = new MetaMemCtrl2[Config.mem2.mctrl_num];
                for (int n = 0; n < Config.mem2.mctrl_num; n++) {
                    meta_mctrl[n] = new MetaMemCtrl2(mctrls2[n], sched[n], wbsched[n]);
                    for (int i = 0; i < cmax; i++) {
                        mctrls2[n][i].meta_mctrl = meta_mctrl[n];
                    }
                    sched[n].meta_mctrl = meta_mctrl[n];
                    sched[n].initialize();
                    wbsched[n].meta_mctrl = meta_mctrl[n];
                    wbsched[n].initialize();
                }
            }

            //wbmode
            for (int n = 0; n < Config.mem2.mctrl_num; n++) {
                Console.WriteLine(Config.mctrl.typeof_wbmode_algo);
                mwbmode = Activator.CreateInstance(Config.mctrl.typeof_wbmode_algo2, new Object[] { mctrls2[n] }) as MemWBMode2;
                for (int i = 0; i < cmax; i++) {
                    mctrls2[n][i].mwbmode = mwbmode;
                }

                //blp tracker
                blptracker = new BLPTracker2(mctrls2[n]);
            }

            for (int set = 0; set < sets; set++) {
                data[set] = new TagEntry[ways];
                for (int way = 0; way < ways; way++) {
                    data[set][way] = new TagEntry();
                    data[set][way].valid = false;
                    data[set][way].addr = 0x0;
                    data[set][way].access = 0;
//                    data[set][way].dirty = false;
                    data[set][way].pid = -1;
                    data[set][way].block_valid = new bool[Config.proc.page_block_diff];
                    data[set][way].block_dirty = new bool[Config.proc.page_block_diff];
                    for (int block_id = 0; block_id < Config.proc.page_block_diff; block_id++)
                    {
                        data[set][way].block_valid[block_id] = false;
                        data[set][way].block_dirty[block_id] = false;
                    }
                }
            }
        }

        private int set_hash(ulong addr)
        {
            return (int)((addr >> Config.proc.page_block_diff_bits) % (ulong)sets);
        }

        private int set_hash_block(ulong addr)
        {
            return (int)(addr % (ulong)Config.proc.page_block_diff);
        }

        public override void tick()
        { 
            cycles++;

            //memory controllers
            for (int n = 0; n < Config.mem2.mctrl_num; n++) {
                for (int i = 0; i < Config.mem2.channel_max; i++) {
                    mctrls2[n][i].tick();
                }
            }

            while (reqs.Count > 0
                    && cycles - reqs.Peek().ts_arrival >= latency) {

//*********************************************************************************************************************************************
//yang
               Req req_temp = reqs.Peek();
               Dbg.Assert(req_temp != null);
               
               if (mctrls2[Sim.get_mctrl(req_temp.pid)][req_temp.addr.cid].is_q_full(req_temp.pid, req_temp.type, req_temp.addr.rid, req_temp.addr.bid))
                  break;

//*********************************************************************************************************************************************
                Req req = reqs.Dequeue();
                Dbg.Assert(req != null);

//                req.ts_departure = cycles;
//                Dbg.Assert(req.ts_departure - req.ts_arrival > 0);

                if (req.type == ReqType.RD) {
                    ////traverse crossbar
                    //Sim.xbar.enqueue(req);
                    //insert into mctrl
                    insert_mctrl(req);
                }
                else {
                    switch (Config.proc.cache_write_policy) {
                        case "WriteThrough":
          //                  throw new System.Exception("Cache: Trying to service a write in a write-through cache.");
                              break;
                        case "WriteBack":
                            //set_dirty(req);

                            //Callback cb = req.callback;
                            //Dbg.Assert(cb != null);
                            //cb(req);
                            insert_mctrl(req);
                            break;
                    }
                }
            }

            while (wbs.Count > 0) {
                Req wb_req = wbs.Peek();
                Dbg.Assert(wb_req != null);
                MemAddr addr = wb_req.addr;

/*              if (mctrls2[Sim.get_mctrl(wb_req.pid)][addr.cid].is_q_full(wb_req.pid, wb_req.type, addr.rid, addr.bid))
                    break;

                wbs.Dequeue();
                mctrls2[Sim.get_mctrl(wb_req.pid)][addr.cid].enqueue_req(wb_req);*/

//yang:
               if (Sim.mctrls[Sim.get_mctrl(wb_req.pid)][addr.cid].is_q_full(wb_req.pid, wb_req.type, addr.rid, addr.bid))
                    break;

                wbs.Dequeue();
                Sim.mctrls[Sim.get_mctrl(wb_req.pid)][addr.cid].enqueue_req(wb_req);

            
                Stat.procs[wb_req.pid].cache_wb_req.Collect();
            }

            if (Config.proc.cache_insertion_policy == "All")
                Migration.tick();
       }

        public void set_dirty(Req req)
        {
            // get the set
            int temp = set_hash(req.block_addr);
            int temp1 = set_hash_block(req.block_addr);

            // search for the entry
            for (int n = 0; n < ways; n++) {
                if (data[temp][n].valid && data[temp][n].block_valid[temp1] && data[temp][n].addr == (req.block_addr >> Config.proc.page_block_diff_bits)) {
//                    data[temp][n].dirty = true;
                    data[temp][n].block_dirty[temp1] = true;
                    return;
                }
            }
        }

        public override bool is_cached(Req req)
        {
            // get the set
            int temp = set_hash(req.block_addr);
            int temp1 = set_hash_block(req.block_addr);

            // search for the entry
            for (int n = 0; n < ways; n++) {
                if (data[temp][n].valid && data[temp][n].block_valid[temp1] && data[temp][n].addr == (req.block_addr >> Config.proc.page_block_diff_bits))
                    return true;
            }

            return false;
        }

        public override void promote(Req req)
        {
            // get the set
            int temp = set_hash(req.block_addr);
            int temp1 = set_hash_block(req.block_addr);

            // search for the entry
            for (int n = 0; n < ways; n++) {
                if (data[temp][n].valid && data[temp][n].block_valid[temp1] && data[temp][n].addr == (req.block_addr >> Config.proc.page_block_diff_bits))
                    data[temp][n].access = cycles;
            }
        }

        public override bool displace(Req req)
        {
            // get the set
            int temp = set_hash(req.block_addr);
            int temp1 = set_hash_block(req.block_addr);

            // search for the entry
            for (int n = 0; n < ways; n++) {
                if (data[temp][n].valid && data[temp][n].block_valid[temp1] && data[temp][n].addr == (req.block_addr >> Config.proc.page_block_diff_bits)) {
                    // displace and write back if necessary
                    data[temp][n].valid = false;
  
                    Sim.Dram_Utilization_size = Sim.Dram_Utilization_size - (ulong)Config.proc.page_block_diff;

                    for (int block_id = 0; block_id < Config.proc.page_block_diff; block_id++)
                        data[temp][n].block_valid[block_id] = false;

                   
                    for (int block_id = 0; block_id < Config.proc.page_block_diff; block_id++)
                    {
                        if (data[temp][n].block_dirty[block_id])
                        {
                            Req req_insert2 = new Req();
//                            req_insert2.set(data[temp][n].pid, ReqType.RD, (data[temp][n].addr << Config.proc.page_size_bits) + (ulong)(block_id * Config.proc.block_size), true);
//new dram mapping
                            req_insert2.set(data[temp][n].pid, ReqType.RD, (ulong)((n*sets+temp) << Config.proc.page_size_bits) + (ulong)(block_id << Config.proc.block_size_bits), true);
//end new dram mapping                          
                            req_insert2.ts_arrival = cycles;
                            req_insert2.migrated_request = true;
                            reqs.Enqueue (req_insert2);
                      
                        // write data back
                            Req wb_req = RequestPool.depool();
//*************************************************************************************************************************
//yang:
//                      wb_req.set(way.req.pid, ReqType.WR, way.req.paddr);
//                        wb_req.set(way.req.pid, ReqType.WR, way.req.paddr,true);
//                        wb_req.set(data[temp][n].pid, ReqType.WR, data[temp][n].addr << Config.proc.block_size_bits, true);
                            wb_req.set(data[temp][n].pid, ReqType.WR, (data[temp][n].addr << Config.proc.page_size_bits) + (ulong)(block_id * Config.proc.block_size), true);
                            wb_req.cache_wb = true;
                            wb_req.migrated_request = true;
                            wbs.Enqueue(wb_req);
                        }
                    }
                    
                    return true;
                }
            }

            return false;
        }

        public override bool service(Req req)
        {

//new dram mapping
            int temp = set_hash(req.block_addr);
            int temp1 = set_hash_block(req.block_addr);
            int num = ways;
            ulong addr_target = req.block_addr >> Config.proc.page_block_diff_bits;
            // search for the entry
            for (int n = 0; n < ways; n++) {
//                if (data[temp][n].valid && data[temp][n].block_valid[temp1] && data[temp][n].addr == (req.block_addr >> Config.proc.page_block_diff_bits))
                if (data[temp][n].addr == addr_target)
                {   
                    num = n;
                    break;
                }
            }

           req.paddr_set((ulong)((num*sets+temp) << Config.proc.page_size_bits) + (ulong)(temp1 << Config.proc.block_size_bits), req.pid);
//new dram mapping end

            reqs.Enqueue(req);
            return true;
        }

        public override bool insert(Req req)
        {
            if (is_cached(req))
                return false;

            // get the set
            int temp = set_hash(req.block_addr);
            int temp1 = set_hash_block(req.block_addr);

            for (int n = 0; n < ways; n++)
            {
                if (data[temp][n].valid && (!data[temp][n].block_valid[temp1]) && (data[temp][n].addr == (req.block_addr >> Config.proc.page_block_diff_bits)))
                {  
                    Req req_insert1 = new Req();
//                    req_insert1.set(req.pid, ReqType.WR, req.paddr, true);
//new dram mapping
                    req_insert1.set(req.pid, ReqType.WR, (ulong)((n*sets+temp) << Config.proc.page_size_bits) + (ulong)(temp1 << Config.proc.block_size_bits), true);
//end new dram mapping

                    req_insert1.ts_arrival = cycles;
                    req_insert1.migrated_request = true;
                    Sim.Dram_Utilization_size = Sim.Dram_Utilization_size + 1;
                    reqs.Enqueue (req_insert1);
                    data[temp][n].access = cycles;
                    data[temp][n].block_valid[temp1] = true;
                    data[temp][n].block_dirty[temp1] = false;
                    return true;
                }
            }

            // find a candidate for replacement
            int victim = 0;
            bool victim_status = false;

/*            for (int n = 0; n < ways; n++) {
                if (data[temp][n].valid == false || data[temp][n].access < data[temp][victim].access)
                    victim = n;
            }
*/
//new dram mapping
            for (int n = 0; n < ways; n++)
            {
                if (!data[temp][n].valid)
                {
                    victim = n;
                    victim_status = true;
                    break;
                }
            }
            if (!victim_status) 
            {
                for (int n=0; n < ways; n++)
                {
                    if (data[temp][n].access < data[temp][victim].access)
                        victim = n;
                }
            }    
                  
            
            Dbg.Assert(victim != null);

            if (data[temp][victim].valid == true)
                Sim.Dram_Utilization_size = Sim.Dram_Utilization_size - (ulong)Config.proc.page_block_diff;

            for (int block_id = 0; block_id < Config.proc.page_block_diff; block_id++)
                data[temp][victim].block_valid[block_id] = false;

                // do writeback
            switch (Config.proc.cache_write_policy) {
                case "WriteThrough":
                    throw new System.Exception("Cache: Dirty data in a write-through cache.");
                case "WriteBack":
                    // write data back
                    for (int block_id = 0; block_id < Config.proc.page_block_diff; block_id++)
                    {
                        if (data[temp][victim].block_dirty[block_id])
                        {
                            Req req_insert2 = new Req();
//                            req_insert2.set(data[temp][victim].pid, ReqType.RD, (data[temp][victim].addr << Config.proc.page_size_bits) + (ulong)(block_id * Config.proc.block_size), true);
//new dram mapping
                            req_insert2.set(data[temp][victim].pid, ReqType.RD, (ulong)((victim*sets+temp) << Config.proc.page_size_bits) + (ulong)(block_id << Config.proc.block_size_bits), true); 
//end new dram mapping

                            req_insert2.ts_arrival = cycles;
                            req_insert2.migrated_request = true;
                            reqs.Enqueue (req_insert2);
                             
                            Req wb_req = RequestPool.depool();
//                        RequestPool.DRAM_TO_PCM_Count++;

//********************************************************************************************************************************
//yang:
//                      wb_req.set(victim.req.pid, ReqType.WR, victim.req.paddr);
//                        wb_req.set(victim.req.pid, ReqType.WR, victim.req.paddr, true);
//                            wb_req.set(data[temp][victim].pid, ReqType.WR, data[temp][victim].addr << Config.proc.block_size_bits, true);
                            wb_req.set(data[temp][victim].pid, ReqType.WR, (data[temp][victim].addr << Config.proc.page_size_bits) + (ulong)(block_id * Config.proc.block_size), true);
                            wb_req.cache_wb = true;
                            wb_req.migrated_request = true;
                            wbs.Enqueue(wb_req);
                        }
                    }
                    break;
            }
            

/*          victim.valid = true;
            victim.addr = req.block_addr;
            victim.access = cycles;
            victim.dirty = false;
            victim.req = req;
            Stat.procs[req.pid].cache_insert.Collect();*/
//*************************************************************************************
//yang:
            Req req_insert = new Req();

//            req_insert.set(req.pid, ReqType.WR, req.paddr, true);
//new dram mapping
            req_insert.set(req.pid, ReqType.WR, (ulong)((victim*sets+temp) << Config.proc.page_size_bits) + (ulong)(temp1 << Config.proc.block_size_bits), true);
//end new dram mapping

            req_insert.ts_arrival = cycles;
            req_insert.migrated_request = true;

            Sim.Dram_Utilization_size = Sim.Dram_Utilization_size + 1;

            reqs.Enqueue (req_insert);
            data[temp][victim].valid = true;
//            data[temp][victim].addr = req_insert.block_addr >> Config.proc.page_block_diff_bits;
//new dram mapping
            data[temp][victim].addr = req.block_addr >> Config.proc.page_block_diff_bits; 
//end new dram mapping

            data[temp][victim].access = cycles;
//            data[temp][victim].dirty = false;
            data[temp][victim].block_dirty[temp1] = false;
//            victim.req = req_insert;
            data[temp][victim].pid = req_insert.pid;
            data[temp][victim].block_valid[temp1] = true;
//**************************************************************************************
            return true;
        }

        private bool insert_mctrl(Req req)
        {
            //MemAddr addr = req.addr;
            send_req(req);
            return true;
        }

        private void send_req(Req req)
        {
            switch (req.type) {
                case ReqType.RD:
                    //Stat.procs[pid].rmpc.Collect();
                    //Stat.procs[pid].read_req.Collect();
                    out_read_req++;
                    req.cache_callback = new Callback(recv_req);
                    break;
                case ReqType.WR:
                    //Stat.procs[pid].wmpc.Collect();
                    //Stat.procs[pid].write_req.Collect();
                    req.cache_callback = new Callback(recv_wb_req);
                    break;
            }

            mctrls2[Sim.get_mctrl(req.pid)][req.addr.cid].enqueue_req(req);
        }

        public void recv_req(Req req)
        {
            /*
            //stats
            Stat.procs[pid].read_req_served.Collect();
            Stat.procs[pid].read_avg_latency.Collect(req.latency);

            //free up instruction window and mshr
            inst_wnd.set_ready(req.block_addr);
            mshr.RemoveAll(x => x == req.block_addr);

            //writeback
            Req wb_req = req.wb_req;
            if (wb_req != null) {
                bool wb_merge = wb_q.Exists(x => x.block_addr == wb_req.block_addr);
                if (!wb_merge) {
                    wb_q.Add(wb_req);
                }
                else {
                    RequestPool.enpool(wb_req);
                }
            }

            //destory req
            RequestPool.enpool(req);
            */
              out_read_req--;

            //traverse crossbar
            Callback cb = req.callback;
            if (cb != null)
           	 Sim.xbar.enqueue(req);
 //           else
 //                RequestPool.enpool(req);
        }

        public void recv_wb_req(Req req)
        {
            //stats
            //Stat.procs[pid].write_req_served.Collect();
            //Stat.procs[pid].write_avg_latency.Collect(req.latency);

            //destroy req
            //RequestPool.enpool(req);

            if (!req.migrated_request) 
         	   set_dirty(req);

            Callback cb = req.callback;
            //Dbg.Assert(cb != null);
 ////////////////////////////////////////////////////////////////////////////////////////////////
            if (cb != null)
                cb(req);
//            else
//                RequestPool.enpool(req);
////////////////////////////////////////////////////////////////////////////////////////////////

        }
    }
}
