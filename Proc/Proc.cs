using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Globalization;


using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.GZip;

namespace MemMap
{
    class Proc
    {
        //throttle
        public static Random rand = new Random(0);
        public double throttle_fraction = 0;

        //processor id
        private static int pmax = 0;
        public int pid;

        //components
        public InstWnd inst_wnd;
        public List<ulong> mshr;
        public List<Req> wb_q;

        //other components
        public Trace trace;

        //current status
        public ulong cycles;
        public int curr_cpu_inst_cnt;
        public Req curr_rd_req;

        //retry memory request
        private bool mctrl_retry = false;
        private bool mshr_retry = false;

        //etc: outstanding requests
        public int out_read_req;

        //etc: stats
        ulong curr_quantum;
        ulong prev_read_req;
        ulong prev_write_req;


        public Proc(string trace_fname)
        {
            pid = pmax;
            pmax++;
            //components
            inst_wnd = new InstWnd(Config.proc.inst_wnd_max);
            mshr = new List<ulong>(Config.proc.mshr_max);
            wb_q = new List<Req>(2 * Config.proc.wb_q_max);

            //other components
            Stat.procs[pid].trace_fname = trace_fname;
            trace = new Trace(pid, trace_fname);

            //initialize
            curr_rd_req = get_req();
        }

        public void recv_req(Req req)
        {
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
            out_read_req--;
        }

        public void recv_wb_req(Req req)
        {
            //stats
            Stat.procs[pid].write_req_served.Collect();
            Stat.procs[pid].write_avg_latency.Collect(req.latency);

            //destroy req
            RequestPool.enpool(req);
        }

        public Req get_req()
        {
            Dbg.Assert(curr_cpu_inst_cnt == 0);

            Req wb_req = null;
            trace.get_req(ref curr_cpu_inst_cnt, out curr_rd_req, out wb_req);
            curr_rd_req.wb_req = wb_req;

            return curr_rd_req;
        }

        public bool issue_wb_req(Req wb_req)
        {
            bool mctrl_ok = insert_mctrl(wb_req);
            return mctrl_ok;
        }

        public bool reissue_rd_req()
        {
            //retry mshr
            if (mshr_retry) {
                Dbg.Assert(!mctrl_retry);

                //retry mshr
                bool mshr_ok = insert_mshr(curr_rd_req);
                if (!mshr_ok) 
                    return false;
                
                //success
                mshr_retry = false;

                //check if true miss
                bool false_miss = inst_wnd.is_duplicate(curr_rd_req.block_addr);
                Dbg.Assert(!false_miss);

                //retry mctrl
                mctrl_retry = true;
            }

            //retry mctrl
            if (mctrl_retry) {
                Dbg.Assert(!mshr_retry);

                //retry mctrl
                bool mctrl_ok = insert_mctrl(curr_rd_req);
                if (!mctrl_ok) 
                    return false;
                
                //success
                mctrl_retry = false;
                return true;
            }

            //should never get here
            throw new System.Exception("Processor: Reissue Request");
        }

        public void issue_insts(bool issued_rd_req)
        {
            //issue instructions
            for (int i = 0; i < Config.proc.ipc; i++) {
                if (inst_wnd.is_full()) {
                    if (i == 0) 
                    {
                        Stat.procs[pid].stall_inst_wnd.Collect();
//                        Measurement.core_stall_cycles[pid] += 1;
                    }
                    return;
                }
                
                //cpu instructions
                if (curr_cpu_inst_cnt > 0) {
                    curr_cpu_inst_cnt--;
                    inst_wnd.add(0, false, true);
                    continue;
                }

                //only one memory instruction can be issued per cycle
                if (issued_rd_req)
                    return;

                //memory instruction (only AFTER checking for one memory instruction per cycle)
                inst_wnd.add(curr_rd_req.block_addr, true, false);

                //check if true miss
                bool false_miss = inst_wnd.is_duplicate(curr_rd_req.block_addr);
                if (false_miss) {
                    Dbg.Assert(curr_rd_req.wb_req == null);
                    RequestPool.enpool(curr_rd_req);
                    curr_rd_req = get_req();
                    continue;
                }

                //try mshr
                bool mshr_ok = insert_mshr(curr_rd_req);
                if (!mshr_ok) {
                    mshr_retry = true;
                    return;
                }

                //try memory controller
                bool mctrl_ok = insert_mctrl(curr_rd_req);
                if (!mctrl_ok) {
                    mctrl_retry = true;
                    return;
                }

                //issued memory request
                issued_rd_req = true;

                //get new read request
                curr_rd_req = get_req();
            }
        }

        public void tick()
        {   
            /*** Preamble ***/
            cycles++;
            Stat.procs[pid].cycle.Collect();
            ulong inst_cnt = Stat.procs[pid].ipc.Count;
            if (inst_cnt != 0 && inst_cnt % 1000000 == 0) {
                ulong quantum = inst_cnt / 1000000;
                if (quantum > curr_quantum) {
                    curr_quantum = quantum;

                    ulong read_req = Stat.procs[pid].read_req.Count;
                    Stat.procs[pid].read_quantum.EndQuantum(read_req - prev_read_req);

                    prev_read_req = read_req;

                    ulong write_req = Stat.procs[pid].write_req.Count;
                    Stat.procs[pid].write_quantum.EndQuantum(write_req - prev_write_req);

                    prev_write_req = write_req;
                }
            }

            /*** Throttle ***/
            if (throttle_fraction > 0) {
                if (rand.NextDouble() < throttle_fraction)
                    return;
            }


            /*** Retire ***/
            int retired = inst_wnd.retire(Config.proc.ipc);
            Stat.procs[pid].ipc.Collect(retired);
            if (retired<0.5*Config.proc.ipc)
                Measurement.core_stall_cycles[pid]+=1;


            /*** Issue writeback request ***/
            if (Config.proc.wb && wb_q.Count > 0) {
                bool wb_ok = issue_wb_req(wb_q[0]);
//                Console.WriteLine("Issue Write {0}",wb_ok);
                if (wb_ok) {
                    wb_q.RemoveAt(0);
                }

                //writeback stall
                bool stalled_wb = wb_q.Count > Config.proc.wb_q_max;
                if (stalled_wb)
                    return;
            }

            /*** Reissue previous read request ***/
            bool issued_rd_req = false;
            if (mshr_retry || mctrl_retry) {
                Dbg.Assert(curr_rd_req != null && curr_cpu_inst_cnt == 0);

                //mshr/mctrl stall
                bool reissue_ok = reissue_rd_req();
//                Console.Write("Reissue read {0}",reissue_ok);
                if (!reissue_ok) 
                    return;

                //reissue success
                Dbg.Assert(!mshr_retry && !mctrl_retry);
                issued_rd_req = true;
                curr_rd_req = get_req();
            }

            /*** Issue instructions ***/
            Dbg.Assert(curr_rd_req != null);

            issue_insts(issued_rd_req);
        }

        private bool insert_mshr(Req req)
        {
            if (mshr.Count == mshr.Capacity) {
                Stat.procs[pid].stall_mshr.Collect();
                return false;
            }
            mshr.Add(req.block_addr);
            return true;
        }

        private bool insert_mctrl(Req req)
        {
            MemAddr addr = req.addr;

            //failure
            if (Sim.mctrls[Sim.get_mctrl(req.pid)][addr.cid].is_q_full(req.pid, req.type, addr.rid, addr.bid)) {
                if (req.type == ReqType.RD) {
                    Stat.procs[req.pid].stall_read_mctrl.Collect();
                }
                else {
                    Stat.procs[req.pid].stall_write_mctrl.Collect();
                }
                return false;
            }
            
            //success
            send_req(req);
            return true;
        }

        private void send_req(Req req)
        {
            switch (req.type) {
                case ReqType.RD:
                    Stat.procs[pid].rmpc.Collect();
                    Stat.procs[pid].read_req.Collect();
                    Measurement.core_rpkc[pid] += 1;
                    req.callback = new Callback(recv_req);
                    out_read_req++;
                    break;
                case ReqType.WR:
                    Stat.procs[pid].wmpc.Collect();
                    Stat.procs[pid].write_req.Collect();
                    Measurement.core_wpkc[pid] += 1;
                    req.callback = new Callback(recv_wb_req);
                    break;
            }

            Stat.procs[pid].req.Collect();
            Sim.mctrls[Sim.get_mctrl(req.pid)][req.addr.cid].enqueue_req(req);
        }

        public override string ToString()
        {
            return "Processor " + pid;
        }
    }
}
