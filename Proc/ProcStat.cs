using System;
using System.IO;
using System.Collections.Generic;
using System.Text;


namespace MemMap
{
    /**
     * Processor statistics
     */
    public class ProcStat : StatGroup
    {
        //trace
        public string trace_fname;

        //instructions
        public AccumStat cycle;
        public AccumRateStat ipc;  //total instructions, mem and non-mem, retired (executed) by instruction window

        //misses
        public AccumRateStat rmpc;
        public AccumRateStat wmpc;

        //stall
        public AccumStat stall_inst_wnd;
        public AccumStat stall_read_mctrl;
        public AccumStat stall_write_mctrl;
        public AccumStat stall_mshr;

        //memory request issued (sent to memory scheduler)
        public AccumStat req;           //total memory requests issued
        public AccumStat read_req;      //read (load) requests issued
        public AccumStat write_req;     //write (store) requests issued
        //public AccumStat req_wb;      //writeback requests issued
        //public AccumStat dropped_wb;  //writeback requests not issued due to instruction window stall

        //memory request served (result received by processor)
        public AccumStat read_req_served;
        public AccumStat write_req_served;

        //per-quantum stats
        public PerQuantumStat read_quantum;
        public PerQuantumStat write_quantum;

        //writeback hit
        public AccumStat wb_hit;

        //row-buffer
        public AccumStat row_hit_read;
        public AccumStat row_miss_read;
        public SamplePercentAvgStat row_hit_rate_read;

        public AccumStat row_hit_write;
        public AccumStat row_miss_write;
        public SamplePercentAvgStat row_hit_rate_write;

        //latency (time between when a request is issued and served)
        public SampleAvgStat read_avg_latency;
        public SampleAvgStat write_avg_latency; 

        //bank-level parallelism
        public SampleAvgStat service_blp;

        //idealized row-buffer stats
        public SamplePercentAvgStat rw_buddy_prob;
        public SamplePercentAvgStat rw_buddy_wprob;

        public SamplePercentAvgStat rr_buddy_prob;
        public SamplePercentAvgStat rr_buddy_wprob;

        //cache
        public AccumStat cache_insert;
        public AccumStat cache_read;
        public AccumStat cache_write;
        public AccumStat cache_wb_req;
        public SamplePercentAvgStat cache_hit_rate_read;
        public SamplePercentAvgStat cache_hit_rate_write;

        //etc
        //public double mem_wait_avg; //memory waiting time average

        public ProcStat()
        {
            Init();
        }

        public void Reset()
        {
            cycle.Reset();
            ipc.Reset();
            rmpc.Reset();
            wmpc.Reset();
            stall_inst_wnd.Reset();
            stall_read_mctrl.Reset();
            stall_write_mctrl.Reset();
            stall_mshr.Reset();
            req.Reset();
            read_req.Reset();
            write_req.Reset(); 
            read_req_served.Reset();
            write_req_served.Reset();
            read_quantum.Reset();
            write_quantum.Reset();
            wb_hit.Reset();
            row_hit_read.Reset();
            row_miss_read.Reset();
            row_hit_rate_read.Reset();
            row_hit_write.Reset();
            row_miss_write.Reset();
            row_hit_rate_write.Reset();
            read_avg_latency.Reset();
            write_avg_latency.Reset(); 
            service_blp.Reset();
            rw_buddy_prob.Reset();
            rw_buddy_wprob.Reset();
            rr_buddy_prob.Reset();
            rr_buddy_wprob.Reset();
            cache_insert.Reset();
            cache_read.Reset();
            cache_write.Reset();
            cache_wb_req.Reset();
            cache_hit_rate_read.Reset();
            cache_hit_rate_write.Reset();
        }
    }
}
