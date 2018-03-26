using System;
using System.Collections.Generic;
using System.Text;

using System.IO;

namespace MemMap {
    public delegate void Callback(Req req);

    public class MemCtrl {
        static uint cmax;
        public uint cid;
    //    public ulong DRAMRead;
    //    public ulong DRAMWrite;
    //    public ulong NVMRead;
    //    public ulong NVMWrite;
        //state
        public long cycles;
        public uint rmax;
        public uint bmax;


        //DDR3
        public DDR3DRAM.Timing timing;
        public uint col_max;
        public uint row_size;   //in bytes

        //components
        public Channel chan;
        public MetaMemCtrl meta_mctrl;

        //row-hit finder
        public RowHitFinder rh_finder;

        //waiting queues
        public List<Req>[,] readqs;
        public List<Req>[,] writeqs;
       
        //global waiting queue
        public int writeq_max;
        public List<Req> mctrl_writeq;

        //transitional queues
        public List<Req>[,] inflightqs;
        public List<Cmd>[,] cmdqs;
        static int INFLIGHTQ_MAX = 8;
        static int CMDQ_MAX = 3;

        //reserved bus transations (when request is serviced)
        public List<BusTransaction> bus_q;
        static uint BUS_TRANSACTIONS_MAX = 16;

        //writeback
        public bool wb_mode;
        public MemWBMode mwbmode;
        public uint reads_to_drain;
        public uint writes_to_drain;

        public long ts_start_wbmode = -1;
        public long ts_end_wbmode = -1;

        //stats
        public uint read_unloaded_time;
        public uint read_loaded_time;

        public uint rload;
        public uint[] rload_per_proc;
        public uint[, ,] rload_per_procrankbank;
        public ulong[, ,] shadow_rowid_per_procrankbank;

        public uint wload;
        public uint[] wload_per_proc;
        public uint[, ,] wload_per_procrankbank;

        //writeback throttle
        public WBThrottle wbthrottle;

        //writeback mode stats
        public uint rds_per_wb_mode;
        public uint wbs_per_wb_mode;

        //constructor for some subclasses
        public MemCtrl()
        { }

        //constructor
        public MemCtrl(uint rmax, DDR3DRAM ddr3)
        {
            this.cid = cmax;
            cmax++;

            //states
            this.rmax = rmax;
            this.bmax = ddr3.BANK_MAX;

            //DDR3
            timing = ddr3.timing;
            this.col_max = ddr3.COL_MAX;
            this.row_size = ddr3.COL_MAX * ddr3.CHANNEL_WIDTH;

            //components
            chan = new Channel(this, rmax, ddr3.BANK_MAX);

            //row-hit finder
            rh_finder = new RowHitFinder(this);

            //queues
//            int readq_max = Config.mctrl.readq_max_per_bank;
            int readq_max = (int)this.rmax * (int)this.bmax * Config.mctrl.readq_max_per_bank;
            writeq_max = (int)this.rmax * (int)this.bmax * Config.mctrl.writeq_max_per_bank;

            readqs = new List<Req>[rmax, bmax];
            writeqs = new List<Req>[rmax, bmax];
            mctrl_writeq = new List<Req>(writeq_max);
            inflightqs = new List<Req>[rmax, bmax];
            cmdqs = new List<Cmd>[rmax, bmax];
            for (uint r = 0; r < rmax; r++) {
                for (uint b = 0; b < bmax; b++) {
                    readqs[r, b] = new List<Req>(readq_max);
                    writeqs[r, b] = new List<Req>(writeq_max);
                    inflightqs[r, b] = new List<Req>(INFLIGHTQ_MAX);
                    cmdqs[r, b] = new List<Cmd>(CMDQ_MAX);
                }
            }
            bus_q = new List<BusTransaction>((int)BUS_TRANSACTIONS_MAX);

            //stats
            rload_per_proc = new uint[Config.N];
            rload_per_procrankbank = new uint[Config.N, rmax, bmax];
            shadow_rowid_per_procrankbank = new ulong[Config.N, rmax, bmax];

            wload_per_proc = new uint[Config.N];
            wload_per_procrankbank = new uint[Config.N, rmax, bmax];

            //writeback throttler
            wbthrottle = Activator.CreateInstance(Config.sched.typeof_wbthrottle_algo) as WBThrottle;
        }

        public void tick()
        {

            //must be the very first thing that's done
            cycles++;


//            if (cycles % 1000000 == 0)
//              Console.WriteLine("{0} - {1} - {2}",Sim.MLPINC,Sim.MLPDEC,Sim.MLPINC - Sim.MLPDEC);


            meta_mctrl.tick(cid);
            wbthrottle.tick();
            mwbmode.tick(cid);
     //      if (cycles % 1000000 == 0)
     //       {
     //          Console.WriteLine("DRAM Read Access Times: {0}", DRAMRead);
     //          Console.WriteLine("DRAM Write Access Times:{0}",DRAMWrite);
     //          Console.WriteLine("NVM Read Access Times: {0}",NVMRead);
     //          Console.WriteLine("NVM Write Access Times:{0}",NVMWrite);
     //          DRAMRead=0;
     //          DRAMWrite=0;
     //          NVMRead =0;
     //          NVMWrite=0;
     //       }

            //load stats
            for (int p = 0; p < Config.N; p++) {
                //read load
                if (rload_per_proc[p] > 0)
                    Stat.mctrls[cid].rbinaryloadtick_per_proc[p].Collect();
                Stat.mctrls[cid].rloadtick_per_proc[p].Collect(rload_per_proc[p]);

                //write load
                if (wload_per_proc[p] > 0)
                    Stat.mctrls[cid].wbinaryloadtick_per_proc[p].Collect();
                Stat.mctrls[cid].wloadtick_per_proc[p].Collect(wload_per_proc[p]);
            }

            //busy/idle stats
            if (rload > 0) {
                read_loaded_time++;
                if (read_unloaded_time > 0) {
                    //Stat.mctrls[cid].read_unloaded_time.Collect(read_unloaded_time);
                }
                read_unloaded_time = 0;
            }
            else {
                read_unloaded_time++;
                if (read_loaded_time > 0) {
                    //Stat.mctrls[cid].read_loaded_time.Collect(read_loaded_time);
                }
                read_loaded_time = 0;
            }

            /*** writeback mode ***/
            update_wb_mode();
            /*
            if (wb_mode && cid == 0) {
                Console.WriteLine("==={0}==============================================", cycles);
                Console.WriteLine("Reads to Drain:  {0}", reads_to_drain);
                Console.WriteLine("Writes Serviced: {0}", ((DecoupledWBFullServeN) mwbmode).serve_cnt[0]);
                uint r = 0;
                for (uint b = 0; b < bmax; b++) {
                    Console.Write("{0}\t", b);
                    foreach (Cmd cmd in cmdqs[r, b]) {
                        Console.Write("{0} {1}\t", cmd.type.ToString(), can_schedule_cmd(cmd));
                    }
                    Console.WriteLine();
                }
            }
            */

            /*** clock factor ***/
            if (cycles % Config.mem.clock_factor != 0)
                return;

            if ((Config.proc.cache_insertion_policy == "PFA") && (cycles % (6*Config.mem.clock_factor) == 0))
            {
                int indexi, indexj;
                for (indexi = 0; indexi < rmax; indexi++)
                {
                    for (indexj = 0; indexj < bmax; indexj++)
                    {
                         Measurement.read_MLP_cal (ref readqs[indexi, indexj]); 
                         Measurement.write_MLP_cal (ref writeqs[indexi, indexj]);
                         Measurement.MLP_cal (ref inflightqs[indexi, indexj]);
                    }
                }
            }

            /*** serve completed request ***/
            if (bus_q.Count > 0 && bus_q[0].ts <= cycles) {
                MemAddr addr = bus_q[0].addr;
                bus_q.RemoveAt(0);

                List<Req> inflight_q = inflightqs[addr.rid, addr.bid];
                Dbg.Assert(inflight_q.Count > 0);
                Dbg.Assert(addr == inflight_q[0].addr);
                Req req = inflight_q[0];
                inflight_q.RemoveAt(0);
    
                if (Config.proc.cache_insertion_policy == "PFA")
                  Measurement.NVMBankPidDeUpdate(req);
               
                dequeue_req(req);
            }

            Cmd best_cmd = find_best_cmd();
            Req best_req = find_best_req();

            //nothing to issue
            if (best_cmd == null && best_req == null) {
          
                if (Config.proc.cache_insertion_policy == "PFA")
                    CheckBusConflict();
                return;
            }

            //arbitrate between command and request
            bool is_issue_req = false;
            if (best_req != null && best_cmd == null) {
                is_issue_req = true;
            }
            else if (best_req == null && best_cmd != null) {
                is_issue_req = false;
            }
            else {
                if (best_req == __better_req(best_cmd.req, best_req))
                    is_issue_req = true;
                else
                    is_issue_req = false;
            }

            //issue command or request
            if (is_issue_req)
            {
               if (!best_req.migrated_request)
               {
                  if (Config.proc.cache_insertion_policy == "RBLA") 
                  {
                     RowStat.UpdateDict(RowStat.NVMDict,best_req,this);
                  }
                  else if (Config.proc.cache_insertion_policy == "PFA") 
                  {
                     RowStat.UpdateDict(RowStat.NVMDict,best_req,this);
//                     Measurement.NVMBankPidEnUpdate(best_req);
                  }
               }
               if (Config.proc.cache_insertion_policy == "PFA")
                   Measurement.NVMBankPidEnUpdate(best_req);              
               issue_req(best_req);
            }
            else issue_cmd(best_cmd);
           
            if (Config.proc.cache_insertion_policy == "PFA")
                CheckBusConflict();                          
        }

        public void CheckBusConflict()
        {
            if (bus_q.Count == 0)
                return;

            BusTransaction last_trans = bus_q[bus_q.Count - 1];
            MemAddr addr = last_trans.addr;
        
            for (int i=0; i < rmax; i++)
            {
                for (int j=0; j < bmax; j++)
                {
                    if (cmdqs[i,j].Count == 0)
                        continue;                   
                    
                    if (inflightqs[i,j][0].pid == inflightqs[addr.rid, addr.bid][0].pid)
                        continue;
 
                    if (cmdqs[i,j][0].type == Cmd.TypeEnum.READ)
                    {
                        if (cycles + (timing.tCL + timing.tBL) - last_trans.ts < timing.tBL)
                        {
                            if (chan.can_read((uint)i, (uint)j))
                                Measurement.NVM_bus_conflict_set(cmdqs[i,j][0].pid);
                         }
                    }
                    else if (cmdqs[i,j][0].type == Cmd.TypeEnum.WRITE)
                    {
                         if (cycles + (timing.tCWL + timing.tBL) - last_trans.ts < timing.tBL)
                         {
                             if (chan.can_write((uint)i, (uint)j))
                                 Measurement.NVM_bus_conflict_set(cmdqs[i,j][0].pid);
                         }
                     }
                 }
             }
         }
                    

        public void update_wb_mode()
        {
            bool prev_wb_mode = wb_mode;
            wb_mode = mwbmode.is_wb_mode(cid);
            if (wb_mode) {
                Stat.mctrls[cid].wbmode_fraction.Collect();
            }

            if (prev_wb_mode == false && wb_mode == true) {
                //stats
                ts_start_wbmode = cycles;
                if (ts_end_wbmode != -1) {
                    Stat.mctrls[cid].wbmode_distance.Collect((int) (ts_start_wbmode - ts_end_wbmode));
                }

                /*
                if (cid == 0) {
                    Console.WriteLine("=====Start: {0,8}======================================", cycles);
                    Console.Write("\t");
                    for (uint b = 0; b < bmax; b++) {
                        Console.Write("{0,4}", readqs[0, b].Count);
                    }
                    Console.WriteLine();

                    Console.Write("\t");
                    for (uint b = 0; b < bmax; b++) {
                        Console.Write("{0,4}", writeqs[0, b].Count);
                    }
                    Console.WriteLine();
                }
                */

                //stats: longest write transaction
                int longest_transaction = 0;
                for (uint r = 0; r < rmax; r++) {
                    for (uint b = 0; b < bmax; b++) {
                        List<Req> q = writeqs[r, b];
                        Dictionary<ulong, int> dict = new Dictionary<ulong, int>();
                        foreach(Req req in q) {
                            if (!dict.ContainsKey(req.addr.rowid))
                                dict.Add(req.addr.rowid, 0);
                            dict[req.addr.rowid] += 1;
                        }

                        foreach (int transaction in dict.Values)
                            if (transaction > longest_transaction)
                                longest_transaction = transaction;
                    }
                }
                Stat.mctrls[cid].wbmode_longest_transaction.Collect(longest_transaction);
                /*
                if (cid == 0)
                    Console.WriteLine("Longest Transaction: {0}", longest_transaction);
                */

                //flush/drain reads
                reads_to_drain = 0;
                for (uint r = 0; r < rmax; r++) {
                    for (uint b = 0; b < bmax; b++) {
                        List<Cmd> cmdq = cmdqs[r, b];
                        if (cmdq.Count == 0)
                            continue;

                        //only column command
                        if (cmdq.Count == 1) {
                            //increment the number of reads to drain during the first part of the writeback mode
                            Dbg.Assert(cmdq[0].type == Cmd.TypeEnum.READ || cmdq[0].type == Cmd.TypeEnum.WRITE);
                            if (cmdq[0].type == Cmd.TypeEnum.READ) {
                                reads_to_drain++;
                                cmdq[0].is_drain = true;
                            }
                            continue;
                        }

                        //activate+column command
                        Dbg.Assert(cmdq.Count == 2);
                        Dbg.Assert(cmdq[0].type == Cmd.TypeEnum.ACTIVATE);
                        Dbg.Assert(cmdq[1].type == Cmd.TypeEnum.READ || cmdq[1].type == Cmd.TypeEnum.WRITE);

                        //write requests don't matter
                        if (cmdq[1].type == Cmd.TypeEnum.WRITE)
                            continue;

                        //don't flush read request
                        if (Config.mctrl.read_bypass) {
                            if (writeqs[r, b].Count == 0)
                                continue;
                        }

                        //flush read request
                        Req req = cmdq[1].req;

                        List<Req> inflightq = get_inflight_q(req);
                        Req last_req = inflightq[inflightq.Count - 1];
                        Dbg.Assert(last_req.block_addr == req.block_addr);
                        inflightq.RemoveAt(inflightq.Count - 1);

                        if (Config.proc.cache_insertion_policy == "PFA")
                             Measurement.NVMResetRowBufferChange (req);
                        
                        List<Req> q = get_q(req);
               //         Dbg.Assert(q.Count <= q.Capacity);
                        q.Add(req);

                        //flush read command
                        cmdq.RemoveRange(0, 2);

                        if (Config.proc.cache_insertion_policy == "PFA")
                            Measurement.NVM_bus_conflict_reset(req.pid); 
                    }
                }
            }
            else if (prev_wb_mode == true && wb_mode == false) {
                //stats
                ts_end_wbmode = cycles;
                Stat.mctrls[cid].wbmode_length.Collect((int)(ts_end_wbmode - ts_start_wbmode));

                /*
                if (cid == 0) {
                    Console.WriteLine("Length: {0}", cycles-ts_start_wbmode);
                    Console.WriteLine("Rds: {0}", rds_per_wb_mode);
                    Console.WriteLine("Wrs: {0}", wbs_per_wb_mode);
                    Console.WriteLine("=====End: {0,8}======================================", cycles);
                }
                */

                Stat.mctrls[cid].rds_per_wb_mode.Collect(rds_per_wb_mode);
                Stat.mctrls[cid].wbs_per_wb_mode.Collect(wbs_per_wb_mode);
                rds_per_wb_mode = 0;
                wbs_per_wb_mode = 0;

                //flush/drain writes
                writes_to_drain = 0;
                foreach (List<Cmd> cmdq in cmdqs) {
                    if (cmdq.Count == 0)
                        continue;

                    //only column command
                    if (cmdq.Count == 1) {
                        //increment the number of reads to drain during the first part of the writeback mode
                        Dbg.Assert(cmdq[0].type == Cmd.TypeEnum.READ || cmdq[0].type == Cmd.TypeEnum.WRITE);
                        if (cmdq[0].type == Cmd.TypeEnum.WRITE) {
                            writes_to_drain++;
                            cmdq[0].is_drain = true;
                        }
                        continue;
                    }

                    //activate+column command
                    Dbg.Assert(cmdq.Count == 2);
                    Dbg.Assert(cmdq[0].type == Cmd.TypeEnum.ACTIVATE);
                    Dbg.Assert(cmdq[1].type == Cmd.TypeEnum.READ || cmdq[1].type == Cmd.TypeEnum.WRITE);

                    if (cmdq[1].type == Cmd.TypeEnum.READ)
                        continue;

                    //flush read request
                    Req req = cmdq[1].req;

                    List<Req> inflightq = get_inflight_q(req);
                    Req last_req = inflightq[inflightq.Count - 1];
                    Dbg.Assert(last_req.block_addr == req.block_addr);
                    inflightq.RemoveAt(inflightq.Count - 1);

                    if (Config.proc.cache_insertion_policy == "PFA")
                         Measurement.NVMResetRowBufferChange (req);
               
                    List<Req> q = get_q(req);
             //       Dbg.Assert(q.Count <= q.Capacity);
                    
                    q.Add(req);

                    //flush read command
                    cmdq.RemoveRange(0, 2);

                    if (Config.proc.cache_insertion_policy == "PFA")
                        Measurement.NVM_bus_conflict_reset(req.pid);
                }
            }
        }

        public Req find_best_req()
        {
            Req best_req = null;
            for (int r = 0; r < rmax; r++) {
                for (int b = 0; b < bmax; b++) {
                    Req req = null;

                    //find best request from a bank
                    req = __find_best_req(r, b);

                    //no request
                    if (req == null)
                        continue;

                    //update best request
                    if (best_req == null) {
                        best_req = req;
                        continue;
                    }

                    //arbitrate between requests from different banks
                    best_req = __better_req(best_req, req);
                }
            }
            return best_req;
        }

        private Req __find_best_req(int r, int b)
        {
            //no need to search for request, already outstanding commands
            if (cmdqs[r, b].Count > 0)
                return null;

            /*** find best request ***/
            List<Req> rq = readqs[r, b];
            List<Req> wq = writeqs[r, b];

            if (rq.Count == 0 && wq.Count == 0)
                return null;

            Req best_req = null;
            Cmd cmd = null;

            //find best writeback request
            if (wb_mode) {
                best_req = meta_mctrl.find_best_wb_req(wq);
                if (best_req != null) {
                    //check if best writeback request is schedulable
                    cmd = decode_req(best_req)[0];

                    if (!can_schedule_cmd(cmd))
                        return null;

                    return best_req;
                }

                //writeq is empty: should we let reads bypass?
                if (!Config.mctrl.read_bypass)
                    return null;
            }

            //find best read request
            best_req = meta_mctrl.find_best_rd_req(rq);

            /*** row-hit bypass ***/
            if (Config.mctrl.row_hit_bypass) {
                Req hit_req = rh_finder.find_best_req(rq);
                if (!meta_mctrl.is_row_hit(best_req) && hit_req != null) {
                    Bank bank = chan.ranks[r].banks[b];
                    Dbg.Assert(bank.ts_act != -1);

                    long ts_pre = bank.ts_act + timing.tRAS;
                    long speculative_ts_pre = cycles + timing.tRTP;
                    if (speculative_ts_pre <= ts_pre) {
                        best_req = hit_req;
                    }
                }
            }

            if (best_req == null)
                return null;

            //check if best request is schedulable
            cmd = decode_req(best_req)[0];
            if (!can_schedule_cmd(cmd))
                return null;

            return best_req;
        }

        private Req __better_req(Req req1, Req req2)
        {
            bool is_wr1 = req1.type == ReqType.WR;
            bool is_wr2 = req2.type == ReqType.WR;

            if (is_wr1 && is_wr2) {
                return meta_mctrl.better_wb_req(req1, req2);
            }

            if (is_wr1 ^ is_wr2) {
                if (is_wr1) return req1;
                else return req2;
            }

            //two reads
            return meta_mctrl.better_req(req1, req2);
        }

        public Cmd find_best_cmd()
        {
            Cmd best_cmd = null;
            for (int r = 0; r < rmax; r++) {
                for (int b = 0; b < bmax; b++) {
                    if (cmdqs[r, b].Count == 0)
                        continue;

                    Cmd cmd = cmdqs[r, b][0];

                    //check if best command is schedulable
                    if (!can_schedule_cmd(cmd))
                        continue;

                    //update best command for this bank
                    if (best_cmd == null) {
                        best_cmd = cmd;
                        continue;
                    }

                    //arbitrate between commands from different banks
                    Req best_req = __better_req(best_cmd.req, cmd.req);
                    if (best_req == cmd.req) best_cmd = cmd;
                }
            }
            return best_cmd;
        }

        private List<Cmd> decode_req(Req req)
        {
            MemAddr addr = req.addr;
            List<Cmd> cmd_q = cmdqs[addr.rid, addr.bid];
            int pid = req.pid;
            Bank b = chan.ranks[addr.rid].banks[addr.bid];

            List<Cmd> decode_cmd_q = new List<Cmd>(CMDQ_MAX);
            if (b.curr_rowid == -1) {
                //row-closed
                decode_cmd_q.Add(new Cmd(Cmd.TypeEnum.ACTIVATE, addr, pid, req, cmd_q));
            }
            else if (b.curr_rowid != (long)addr.rowid) {
                //row-conflict
                decode_cmd_q.Add(new Cmd(Cmd.TypeEnum.PRECHARGE, addr, pid, req, cmd_q));
                decode_cmd_q.Add(new Cmd(Cmd.TypeEnum.ACTIVATE, addr, pid, req, cmd_q));
            }

            Cmd.TypeEnum RW = (req.type == ReqType.WR ? Cmd.TypeEnum.WRITE : Cmd.TypeEnum.READ);
            decode_cmd_q.Add(new Cmd(RW, addr, pid, req, cmd_q));

            return decode_cmd_q;
        }

        public void __dequeue_req(Req req)
        {
            req.ts_departure = cycles;
            Dbg.Assert(req.ts_departure - req.ts_arrival > 0);


            if (!req.migrated_request)
            {
                if (Config.proc.cache_insertion_policy == "PFA")
                {   
                    RowStat.UpdateMLP(RowStat.NVMDict,req); 
                    Measurement.mem_num_dec (req);
//                    Measurement.NVMServiceTimeUpdate (req);
//                    Measurement.NVMCoreReqNumDec (req);
                    Row_Migration_Policies.target = true;
                    Row_Migration_Policies.target_req = req;                 
                }  
                else if (Config.proc.cache_insertion_policy == "RBLA")
                {
                    Row_Migration_Policies.target = true;
                    Row_Migration_Policies.target_req = req;  
                }
            }
            if (Config.proc.cache_insertion_policy == "PFA")
                Measurement.NVMCoreReqNumDec (req);
            
/*            if (Config.proc.cache_insertion_policy == "PFA")
            {
                   Measurement.NVMSetCorePrevRowid (req);
            }
*/
            //sched
            meta_mctrl.dequeue_req(req);

            //load stat management
            if (!req.cache_wb){
               if (req.type == ReqType.RD) {
                   rload--;
                   rload_per_proc[req.pid]--;
                   rload_per_procrankbank[req.pid, req.addr.rid, req.addr.bid]--;
                   Dbg.Assert(rload >= 0);
                   Dbg.Assert(rload_per_proc[req.pid] >= 0);
                   Dbg.Assert(rload_per_procrankbank[req.pid, req.addr.rid, req.addr.bid] >= 0);
               }
               else {
                   wload--;
                   wload_per_proc[req.pid]--;
                   wload_per_procrankbank[req.pid, req.addr.rid, req.addr.bid]--;
                   Dbg.Assert(wload >= 0);
                   Dbg.Assert(wload_per_proc[req.pid] >= 0);
                   Dbg.Assert(wload_per_procrankbank[req.pid, req.addr.rid, req.addr.bid] >= 0);
               }
            }
            else{
                if (req.type == ReqType.RD)
                   rload--;
                else
                   wload--;
            }

/*           //dequeue proper
           if (req.type == ReqType.RD) {
                //traverse crossbar
              Sim.xbar.enqueue(req);

                //cache
               Sim.caches[Sim.get_cache(req.pid)].meta_insert(req);
            }
            else {
                bool removeok = mctrl_writeq.Remove(req);
                Dbg.Assert(removeok);
                req.latency = (int)(req.ts_departure - req.ts_arrival);

                Callback cb = req.callback;              
                cb(req);

                if (!req.cache_wb) {
                    //cache
                    switch (Config.proc.cache_write_policy) {
                        case "WriteThrough":
                            // do nothing
                            break;
                        case "WriteBack":
                              Sim.caches[Sim.get_cache(req.pid)].meta_insert(req);
                            break;
                    }
                }
                else
                    RequestPool.enpool(req);
            }
*/


            if (req.type == ReqType.RD) {
                if (!Sim.caches[Sim.get_cache(req.pid)].is_cached(req))
               	   Sim.caches[Sim.get_cache(req.pid)].meta_insert(req);
          //      if (req.callback != null)
                     Sim.xbar.enqueue(req);
          //      else
          //           RequestPool.enpool(req);
            }
            else {
                bool removeok = mctrl_writeq.Remove(req);
                Dbg.Assert(removeok);
                req.latency = (int)(req.ts_departure - req.ts_arrival);
                Callback cb = req.callback;
                if (!req.cache_wb) {
                   switch (Config.proc.cache_write_policy) {
                       case "WriteThrough":
                          break;
                       case "WriteBack":
                          if (!Sim.caches[Sim.get_cache(req.pid)].is_cached(req))
                             Sim.caches[Sim.get_cache(req.pid)].meta_insert(req);
                          break;
                   }
                   if (cb != null)
                     cb(req);
        //           else
        //             RequestPool.enpool(req);
                }
                else
                {
                    RequestPool.enpool(req);
                }
            }
        }

        public void dequeue_req(Req req)
        {
            __dequeue_req(req);
        }

        public void enqueue_req(Req req)
        {
            //check if writeback hit
            List<Req> q = get_q(req);
            MemAddr addr = req.addr;
            if (req.type == ReqType.RD) {
                List<Req> wq = writeqs[addr.rid, addr.bid];

                int idx = wq.FindIndex(delegate(Req w) { return w.block_addr == req.block_addr; });
                if (idx != -1) {
                    //writeback hit
                    Sim.xbar.enqueue(req);
                    Stat.procs[req.pid].wb_hit.Collect();
                    return;
                }
            }

            //writeback dumpster
            if (req.type == ReqType.WR && Config.mctrl.wb_dump) {
                req.addr.rowid = 0;
            }


            Dbg.Assert(q.Count < q.Capacity);
            __enqueue_req(req, q);
        }

        public void __enqueue_req(Req req, List<Req> q)
        {
            //timestamp
            req.ts_arrival = cycles;

/*            // do any analysis
            if (Config.collect_reuse == true) {
                if (Sim.reuse[req.pid].ContainsKey(req.block_addr))
                    Sim.reuse[req.pid][req.block_addr] = Sim.reuse[req.pid][req.block_addr] + 1;
                else
                    Sim.reuse[req.pid].Add(req.block_addr, 1);
            }
*/          
           // check if cache hit
            bool cache_serviced = false;


            // don't allow cache writeback requests to be re-cached
            if (Config.proc.cache && Sim.caches[Sim.get_cache(req.pid)].is_cached(req) && (!req.migrated_request)) {
                 if (Config.proc.cache_insertion_policy == "PFA")
                 { 
                     Measurement.mem_num_inc (req);
                 }   

                 Sim.caches[Sim.get_cache(req.pid)].promote(req);
                //stats
                if (req.type == ReqType.RD) {
                    Stat.procs[req.pid].cache_read.Collect();
                    Stat.procs[req.pid].cache_hit_rate_read.Collect(1);
                    Sim.caches[Sim.get_cache(req.pid)].service(req);
                    cache_serviced = true;
                }
                else {
                    switch (Config.proc.cache_write_policy) {
                        case "WriteThrough":
                            // displace entry
                            Sim.caches[Sim.get_cache(req.pid)].displace(req);
                            break;
                        case "WriteBack":
                            Stat.procs[req.pid].cache_write.Collect();
                            Stat.procs[req.pid].cache_hit_rate_write.Collect(1);
                            Sim.caches[Sim.get_cache(req.pid)].service(req);
                            cache_serviced = true;
                            break;
                    }
                }

            }

            if (!cache_serviced) {
                if (!req.migrated_request)
                   Sim.NVM_req_num = Sim.NVM_req_num + 1;

                if ((!req.migrated_request) && (Config.proc.cache_insertion_policy == "PFA"))
                {
                    Measurement.mem_num_inc (req);
                }  
                if (Config.proc.cache_insertion_policy == "PFA")
                    Measurement.NVMCoreReqNumInc(req); 
                q.Add(req);
                if (req.type == ReqType.WR) {
                    Dbg.Assert(mctrl_writeq.Count < mctrl_writeq.Capacity);
                    mctrl_writeq.Add(req);
                }
                //sched
                meta_mctrl.enqueue_req(req);    //does nothing for now

                //stats
                if (!req.cache_wb){
                   if (req.type == ReqType.RD) {
                       rload++;
                       rload_per_proc[req.pid]++;
                       rload_per_procrankbank[req.pid, req.addr.rid, req.addr.bid]++;
                       Stat.procs[req.pid].cache_hit_rate_read.Collect(0);
                   }
                   else {
                        wload++;
                        wload_per_proc[req.pid]++;
                        wload_per_procrankbank[req.pid, req.addr.rid, req.addr.bid]++;
                        Stat.procs[req.pid].cache_hit_rate_write.Collect(0);
              	   }
                }
                else{
                    if (req.type == ReqType.RD)
                    {
                        rload++;
                    }
                    else
                    {
                        wload++;
                    }
                 }
            }
        }

        public bool can_schedule_cmd(Cmd cmd)
        {
            //drain reads during the first part of the writeback mode
            if (wb_mode && reads_to_drain > 0) {
                if (cmd.type == Cmd.TypeEnum.WRITE)
                    return false;
            }

            //drain writes right after the writeback mode
            if (!wb_mode && writes_to_drain > 0) {
                if (cmd.type == Cmd.TypeEnum.READ)
                    return false;
            }

            //DRAM timing
            MemAddr addr = cmd.addr;
            switch (cmd.type) {
                case Cmd.TypeEnum.ACTIVATE:
                    return can_activate(addr);
                case Cmd.TypeEnum.PRECHARGE:
                    return can_precharge(addr);
                case Cmd.TypeEnum.READ:
                    return can_read(addr);
                case Cmd.TypeEnum.WRITE:
                    return can_write(addr);
            }
            //should never get here
            throw new System.Exception("DRAM: Invalid Cmd.");
        }

        private void issue_req(Req req)
        {
            //remove request from waiting queue
            List<Req> q = get_q(req);
            Dbg.Assert(q.Contains(req));
            q.Remove(req);

            //add to inflight queue
            MemAddr addr = req.addr;
            List<Req> inflight_q = inflightqs[addr.rid, addr.bid];
            Dbg.Assert(inflight_q.Count < inflight_q.Capacity);
            inflight_q.Add(req);
 
 //           if (inflight_q.Count != 1)
 //           Console.WriteLine("{0}",inflight_q.Count);

            //add to command queue
            List<Cmd> cmd_q = cmdqs[addr.rid, addr.bid];
            Dbg.Assert(cmd_q.Count == 0);
            List<Cmd> new_cmd_q = decode_req(req);
            Dbg.Assert(new_cmd_q.Count > 0);
            cmd_q.AddRange(new_cmd_q);

            Cmd cmd = cmd_q[0];
           
            Dbg.Assert (cmd == new_cmd_q[0]);
              
            //meta_mctrl
            meta_mctrl.issue_req(req);

            Dbg.Assert(cmd.req.addr.rowid == req.addr.rowid);

            //stats
            BankStat bstat = Stat.banks[addr.cid, addr.rid, addr.bid];
            bstat.access.Collect();
            if (cmd.type == Cmd.TypeEnum.PRECHARGE || cmd.type == Cmd.TypeEnum.ACTIVATE) {
                //bank stat
                bstat.row_miss.Collect();
                bstat.row_miss_perproc[req.pid].Collect();

                //proc stat
                if (cmd.req.type == ReqType.RD) {
                    Stat.procs[req.pid].row_hit_rate_read.Collect(0);
                    Stat.procs[req.pid].row_miss_read.Collect();
                }
                else {
                    Stat.procs[req.pid].row_hit_rate_write.Collect(0);
                    Stat.procs[req.pid].row_miss_write.Collect();
                }


                req.hit = 2;

// Power Measurement:
                Sim.NVM_power_statistics (req.pid, req.migrated_request, req.type, false);
//




                if (Config.proc.cache_insertion_policy == "PFA")
                {
//                   if ((!req.migrated_request) && (req.type == ReqType.RD))
                       Measurement.NVMMissSetRowBufferChange (req);
                }
            }
            else {
                //bank stat
                bstat.row_hit.Collect();
                bstat.row_hit_perproc[req.pid].Collect();

                //proc stat
                if (cmd.req.type == ReqType.RD) {
                    Stat.procs[req.pid].row_hit_rate_read.Collect(1);
                    Stat.procs[req.pid].row_hit_read.Collect();
                }
                else {
                    Stat.procs[req.pid].row_hit_rate_write.Collect(1);
                    Stat.procs[req.pid].row_hit_write.Collect();
                }


                req.hit = 1;

// Power Measurement:
               Sim.NVM_power_statistics (req.pid, req.migrated_request, req.type, true);

//


                if (Config.proc.cache_insertion_policy == "PFA")
                    Measurement.NVMHitSetRowBufferChange (req);
            }

    //        if (req.type == ReqType.RD && (!req.migrated_request) )
    //             Dbg.Assert(cmd.type == Cmd.TypeEnum.READ);
            //issue command

            if (Config.proc.cache_insertion_policy == "PFA")
               Measurement.NVMSetCorePrevRowid (req);

            issue_cmd(cmd);
        }

        private void issue_cmd(Cmd cmd)
        {
            MemAddr addr = cmd.addr;

            /*
            if (cid == 0 && wb_mode) {
                Console.Write("@{0}\t", cycles - ts_start_wbmode);
                for (uint b = 0; b < addr.bid; b++) {
                    Console.Write("{0,4}", "-");
                }
                Console.Write("{0,4}", cmd.type.ToString()[0]);
                for (uint b = addr.bid; b < bmax; b++) {
                    Console.Write("{0,4}", "-");
                }
                Console.WriteLine();
            }
            */

            List<Cmd> cmd_q = cmdqs[addr.rid, addr.bid];
            Dbg.Assert(cmd == cmd_q[0]);
            cmd_q.RemoveAt(0);
            BankStat bank_stat = Stat.banks[addr.cid, addr.rid, addr.bid];
            BusStat bus_stat = Stat.busses[addr.cid];

            //writeback mode stats
            if (wb_mode) {
                if (cmd.type == Cmd.TypeEnum.READ)
                    rds_per_wb_mode++;
                else if (cmd.type == Cmd.TypeEnum.WRITE)
                    wbs_per_wb_mode++;
            }

            //string dbg;
            switch (cmd.type) {
                case Cmd.TypeEnum.ACTIVATE:
                    activate(addr);
                    /*dbg = String.Format("@{0,6} DRAM ACTI: Channel {1}, Rank {2}, Bank {3}, Row {4}, Col {5}",
                        cycles, cid, addr.rid, addr.bid, addr.rowid, addr.colid);*/
                    //stats                    
                    bank_stat.cmd_activate.Collect();
                    bank_stat.utilization.Collect(timing.tRCD);

                    //shadow row-buffer id
                    shadow_rowid_per_procrankbank[cmd.pid, addr.rid, addr.bid] = addr.rowid;
                    break;
                case Cmd.TypeEnum.PRECHARGE:
                    precharge(addr);
                    /*dbg = String.Format("@{0,6} DRAM PREC: Channel {1}, Rank {2}, Bank {3}, Row {4}, Col {5}",
                        cycles, cid, addr.rid, addr.bid, addr.rowid, addr.colid);*/
                    //stats
                    bank_stat.cmd_precharge.Collect();
                    bank_stat.utilization.Collect(timing.tRP);
                    break;
                case Cmd.TypeEnum.READ:
                    read(addr);
           
                    if (Config.proc.cache_insertion_policy == "PFA")
                        Measurement.NVM_bus_conflict_reset(cmd.req.pid);
                    /*dbg = String.Format("@{0,6} DRAM READ: Channel {1}, Rank {2}, Bank {3}, Row {4}, Col {5}",
                        cycles, cid, addr.rid, addr.bid, addr.rowid, addr.colid);*/

                    //writeback mode
                    if (wb_mode && cmd.is_drain) {
                        Dbg.Assert(reads_to_drain > 0);
                        reads_to_drain--;
                    }

                    //stats
                    bank_stat.cmd_read.Collect();
                    bank_stat.utilization.Collect(timing.tCL);
                    bus_stat.access.Collect();
                    bus_stat.utilization.Collect(timing.tBL);
                    break;
                case Cmd.TypeEnum.WRITE:
                    write(addr);

                    if (Config.proc.cache_insertion_policy == "PFA")
                        Measurement.NVM_bus_conflict_reset(cmd.req.pid);
                    /*dbg = String.Format("@{0,6} DRAM WRTE: Channel {1}, Rank {2}, Bank {3}, Row {4}, Col {5}",
                        cycles, cid, addr.rid, addr.bid, addr.rowid, addr.colid);*/

                    //writeback mode
                    if (!wb_mode && cmd.is_drain) {
                        Dbg.Assert(writes_to_drain > 0);
                        writes_to_drain--;
                    }
                    else {
                        mwbmode.issued_write_cmd(cmd);
                    }

                    //stats
                    bank_stat.cmd_write.Collect();
                    bank_stat.utilization.Collect(timing.tCL);
                    bus_stat.access.Collect();
                    bus_stat.utilization.Collect(timing.tBL);
                    break;

                default:
                    //should never get here
                    throw new System.Exception("DRAM: Invalid Cmd.");
            }
            //Debug.WriteLine(dbg);
        }

        public bool is_q_full(int pid, ReqType rw, uint rid, uint bid)
        {
            /* read queue */
            if (rw == ReqType.RD) {
//yang modified:
                int temp = 0;
                for (int i = 0; i < rmax; i++)
                {
                   for (int j = 0; j < bmax; j++)
                   {
                      List<Req> q = readqs[i, j];
                      temp = temp + q.Count;
                   }
                }
                return temp >= readqs[rid,bid].Capacity;
            }

            /* write queue */
            if (mctrl_writeq.Count >= mctrl_writeq.Capacity)
                return true;

            //writeback throttle
              bool is_throttle = wbthrottle.is_throttle(pid);
              return is_throttle;
        }

        public List<Req> get_q(Req req)
        {
            List<Req>[,] rw_qs = (req.type == ReqType.RD ? readqs : writeqs);
            List<Req> q = rw_qs[req.addr.rid, req.addr.bid];
            return q;
        }

        public List<Req> get_inflight_q(Req req)
        {
            List<Req> q = inflightqs[req.addr.rid, req.addr.bid];
            return q;
        }

        public void freeze_stat(int pid)
        {
            Stat.mctrls[cid].Finish(Sim.cycles, pid);
        }

        private void activate(MemAddr addr)
        {
            chan.activate(addr.rid, addr.bid, addr.rowid);
        }
        private void precharge(MemAddr addr)
        {
            chan.precharge(addr.rid, addr.bid);
        }
        private void read(MemAddr addr)
        {
            chan.read(addr.rid, addr.bid);
            Dbg.Assert(bus_q.Count < bus_q.Capacity);
            BusTransaction trans = new BusTransaction(addr, cycles + (timing.tCL + timing.tBL));
            //check for bus conflict
            if (bus_q.Count > 0) {
                BusTransaction last_trans = bus_q[bus_q.Count - 1];
                Dbg.Assert(trans.ts - last_trans.ts >= timing.tBL);
            }
            bus_q.Add(trans);
        }
        private void write(MemAddr addr)
        {
            chan.write(addr.rid, addr.bid);
            Dbg.Assert(bus_q.Count < bus_q.Capacity);
            BusTransaction trans = new BusTransaction(addr, cycles + (timing.tCWL + timing.tBL));
            //check for bus conflict
            if (bus_q.Count > 0) {
                BusTransaction last_trans = bus_q[bus_q.Count - 1];
                Dbg.Assert(trans.ts - last_trans.ts >= timing.tBL);
            }
            bus_q.Add(trans);
        }
        private bool can_activate(MemAddr addr)
        {
            return chan.can_activate(addr.rid, addr.bid);
        }
        private bool can_precharge(MemAddr addr)
        {
            return chan.can_precharge(addr.rid, addr.bid);
        }
        private bool can_read(MemAddr addr)
        {
            return chan.can_read(addr.rid, addr.bid);
        }
        private bool can_write(MemAddr addr)
        {
            Dbg.Assert(bus_q.Count < bus_q.Capacity);
            BusTransaction trans = new BusTransaction(addr, cycles + (timing.tCWL + timing.tBL));
            if (bus_q.Count > 0) {
                BusTransaction last_trans = bus_q[bus_q.Count - 1];
                return chan.can_write(addr.rid, addr.bid) && (trans.ts - last_trans.ts >= timing.tBL);
            }
            return chan.can_write(addr.rid, addr.bid);
        }

        public void reset()
        {
            chan.reset();
        }
   }
}
