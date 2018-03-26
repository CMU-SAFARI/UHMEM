using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using System.Threading;
using System.Diagnostics;
using System.Reflection;

namespace MemMap
{
    static class Dbg
    {
        //*IMPORTANT* mono's default Assert() statement just keeps on trucking even if it fails
        public static void Assert(bool val){
            Debug.Assert(val);
            if (!val) {
                throw new System.Exception("YOONGU, WHAT HAVE YOU DONE?!");
            }
        }
    }

    class Sim
    {
        public static Proc[] procs;
        public static Xbar xbar;
        public static MemCtrl[][] mctrls;
        public static MemWBMode mwbmode;
        public static BLPTracker blptracker;

        public static Row_Migration_Policies rmp;
        public static Measurement mesur;

        public static Cache[] caches;
        public static Stat stat;
  
        public static Queue<string> task_queue;
        public static int task_num = 0;

        public static int PROC_MAX_LIMIT = 128;         //maximum number of processors supported
        public static ulong cycles = 0;                 //number of clock cycles past

        public static Random rand = new Random(0);

        public static ulong Dram_Utilization_size;
        public static ulong Dram_req_num;
        public static ulong NVM_req_num;

// Power Measurement
        public static bool[] processor_finished;
        public static ulong[] DRAM_processor_read_hit;
        public static ulong[] DRAM_processor_read_miss;
        public static ulong[] DRAM_processor_write_hit;
        public static ulong[] DRAM_processor_write_miss;
        public static ulong[] DRAM_migration_read_hit;
        public static ulong[] DRAM_migration_read_miss;
        public static ulong[] DRAM_migration_write_hit;
        public static ulong[] DRAM_migration_write_miss;
        public static ulong[] NVM_processor_read_hit;
        public static ulong[] NVM_processor_read_miss;
        public static ulong[] NVM_processor_write_hit;
        public static ulong[] NVM_processor_write_miss;
        public static ulong[] NVM_migration_read_hit;
        public static ulong[] NVM_migration_read_miss;
        public static ulong[] NVM_migration_write_hit;
        public static ulong[] NVM_migration_write_miss;
        public static ulong[] processor_cycles;

        public static bool[] proc_warmup;    // indicate whether the workload has been warmed up
        public static ulong[] proc_warmup_cycles;


        public static int get_cache(int pid) {
            return pid % Config.proc.cache_num;
        }

        public static int get_mctrl(int pid) {
            return pid % Config.mem.mctrl_num;
        }

//        public static Dictionary<ulong, int>[] reuse;

        static void Main(string[] args)
        {
//            MLPDEC = 0;
//           MLPINC = 0;
            //*IMPORTANT* without a trace listener, mono can't output Dbg.Assert() */
            Debug.Listeners.Add(new ConsoleTraceListener());

            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

            Config config = new Config();
            config.read(args);
            caches = new Cache[Config.proc.cache_num];
            for (int n = 0; n < Config.proc.cache_num; n++) {
                caches[n] = Activator.CreateInstance(Config.proc.typeof_cache_replacement_policy) as Cache;
            }
           
            stat = new Stat();

            task_queue = new Queue<string>();


          
            initialize();
            run();
            finish();

            //stats
            TextWriter tw = new StreamWriter(Config.output);
            stat.Report(tw);
            tw.Close();

            //out-of-band stats
            /*
            foreach (MemCtrl mctrl in mctrls) {
                mctrl.temporal_correlator.report();
            }
            */
/*            if (Config.collect_reuse == true) {
                for (int n = 0; n < Config.N; n++) {
                    TextWriter rw = new StreamWriter(Config.output + ".reuse-thread" + n);
                    foreach (ulong key in reuse[n].Keys)
                        rw.WriteLine(key + " " + reuse[n][key]);
                    rw.Close();
                }
            }*/
        }

        static void initialize()
        {
              if (Config.task_based == true) {
                string task_fname = Config.traceFileNames[0];
                if (Config.sim_type == Config.SIM_TYPE.GROUPED)
                    task_fname = Config.traceFileNames[Config.group_boundary];
                foreach (string dir in Config.TraceDirs.Split(',', ' ')) {
                    if (File.Exists(dir + "/" + task_fname)) {
                        task_fname = dir + "/" + task_fname;
                    }
                }
                Dbg.Assert(File.Exists(task_fname));
                StreamReader tasks = new StreamReader(File.OpenRead(task_fname));
                while (true) {
                    string line = tasks.ReadLine();
                    if (line == null)
                        break;
                    task_queue.Enqueue(line);
                }
                tasks.Close();
            }

            Dram_Utilization_size = 0;
            Dram_req_num = 0;
            NVM_req_num = 0;
          
             //randomized page table
            ulong page_size = 4 * 1024;
            PageRandomizer prand = new PageRandomizer(page_size);
            Req.prand = prand;

            //processors
            procs = new Proc[Config.N];
            for (int p = 0; p < Config.N; p++) {
                if ((Config.task_based == true && Config.sim_type != Config.SIM_TYPE.GROUPED)
                        || (Config.task_based == true && Config.sim_type == Config.SIM_TYPE.GROUPED && p >= Config.group_boundary && p < Config.N)) {
                    procs[p] = new Proc(task_queue.Dequeue());
                } else {
                    procs[p] = new Proc(Config.traceFileNames[p]);
                }
            }

            //crossbar
            xbar = new Xbar();

// warmup phase
            proc_warmup = new bool[Config.N];
            proc_warmup_cycles = new ulong[Config.N];
            for (int p = 0; p < Config.N; p++)
            {
                proc_warmup[p] = false;
                proc_warmup_cycles[p] = 0;
            }

// Power Measurement:
            processor_finished = new bool[Config.N];
            DRAM_processor_read_hit = new ulong[Config.N];
            DRAM_processor_read_miss = new ulong[Config.N];
            DRAM_processor_write_hit = new ulong[Config.N];
            DRAM_processor_write_miss = new ulong[Config.N];
            DRAM_migration_read_hit = new ulong[Config.N];
            DRAM_migration_read_miss = new ulong[Config.N];
            DRAM_migration_write_hit = new ulong[Config.N];
            DRAM_migration_write_miss = new ulong[Config.N];
            NVM_processor_read_hit = new ulong[Config.N];
            NVM_processor_read_miss = new ulong[Config.N];
            NVM_processor_write_hit = new ulong[Config.N];
            NVM_processor_write_miss = new ulong[Config.N];
            NVM_migration_read_hit = new ulong[Config.N];
            NVM_migration_read_miss = new ulong[Config.N];
            NVM_migration_write_hit = new ulong[Config.N];
            NVM_migration_write_miss = new ulong[Config.N];
            processor_cycles = new ulong[Config.N];

            for (int p = 0; p < Config.N; p++)
            {
                processor_finished[p] = false;
                DRAM_processor_read_hit[p] = 0;
                DRAM_processor_read_miss[p] = 0;
                DRAM_processor_write_hit[p] = 0;
                DRAM_processor_write_miss[p] = 0;
                DRAM_migration_read_hit[p] = 0;
                DRAM_migration_read_miss[p] = 0;
                DRAM_migration_write_hit[p] = 0;
                DRAM_migration_write_miss[p] = 0;
                NVM_processor_read_hit[p] = 0;
                NVM_processor_read_miss[p] = 0;
                NVM_processor_write_hit[p] = 0;
                NVM_processor_write_miss[p] = 0;
                NVM_migration_read_hit[p] = 0;
                NVM_migration_read_miss[p] = 0;
                NVM_migration_write_hit[p] = 0;
                NVM_migration_write_miss[p] = 0;
                processor_cycles[p] = 0;
            }           
//

	    //Jin: Row Migration Policies
	    rmp = new Row_Migration_Policies();
            mesur = new Measurement();

            //ddr3
            DDR3DRAM ddr3 = new DDR3DRAM(Config.mem.ddr3_type, Config.mem.clock_factor, Config.mem.tWR, Config.mem.tWTR);
            uint cmax = (uint)Config.mem.channel_max;
            uint rmax = (uint)Config.mem.rank_max;

            //sequential page table
            PageSequencer pseq = new PageSequencer(page_size, cmax, rmax, ddr3.BANK_MAX);
            Req.pseq = pseq;
        
 
            //memory mapping
            MemMap.init(Config.mem.map_type, Config.mem.channel_max, Config.mem.rank_max, Config.mem.col_per_subrow, ddr3);

            //memory controllers
            mctrls = new MemCtrl[Config.mem.mctrl_num][];
            for (int n = 0; n < Config.mem.mctrl_num; n++) {
                mctrls[n] = new MemCtrl[cmax];
                for (int i = 0; i < mctrls[n].Length; i++) {
                    mctrls[n][i] = new MemCtrl(rmax, ddr3);
                }
            }

            //memory schedulers and metamemory controllers
            if (!Config.sched.is_omniscient) {
                MemSched[][] scheds = new MemSched[Config.mem.mctrl_num][];
                for (int n = 0; n < Config.mem.mctrl_num; n++) {
                    scheds[n] = new MemSched[cmax];
                    for (int i = 0; i < cmax; i++) {
                        scheds[n][i] = Activator.CreateInstance(Config.sched.typeof_sched_algo) as MemSched;
                    }
                }

                MemSched[][] wbscheds = new MemSched[Config.mem.mctrl_num][];
                for (int n = 0; n < Config.mem.mctrl_num; n++) {
                    wbscheds[n] = new MemSched[cmax];
                    if (!Config.sched.same_sched_algo) {
                        for (int i = 0; i < cmax; i++) {
                            wbscheds[n][i] = Activator.CreateInstance(Config.sched.typeof_wbsched_algo) as MemSched;
                        }
                    }
                    else {
                        for (int i = 0; i < cmax; i++) {
                            wbscheds[n][i] = scheds[n][i];
                        }
                    }
                }

                MetaMemCtrl[][] meta_mctrls = new MetaMemCtrl[Config.mem.mctrl_num][];
                for (int n = 0; n < Config.mem.mctrl_num; n++) {
                    meta_mctrls[n] = new MetaMemCtrl[cmax];
                    for (int i = 0; i < cmax; i++) {
                        meta_mctrls[n][i] = new MetaMemCtrl(mctrls[n][i], scheds[n][i], wbscheds[n][i]);
                        mctrls[n][i].meta_mctrl = meta_mctrls[n][i];
                        scheds[n][i].meta_mctrl = meta_mctrls[n][i];
                        scheds[n][i].initialize();
                        wbscheds[n][i].meta_mctrl = meta_mctrls[n][i];
                        wbscheds[n][i].initialize();
                    }
                }
            }
            else {
                MemSched[] sched = new MemSched[Config.mem.mctrl_num];
   		MemSched[] wbsched = new MemSched[Config.mem.mctrl_num];
                for (int n = 0; n < Config.mem.mctrl_num; n++) {
                    sched[n] = Activator.CreateInstance(Config.sched.typeof_sched_algo) as MemSched;
                    if (!Config.sched.same_sched_algo) {
                        wbsched[n] = Activator.CreateInstance(Config.sched.typeof_wbsched_algo) as MemSched;
                    }
                    else {
                        wbsched[n] = sched[n];
                    }
                }

                MetaMemCtrl[] meta_mctrl = new MetaMemCtrl[Config.mem.mctrl_num];
                for (int n = 0; n < Config.mem.mctrl_num; n++) {
                    meta_mctrl[n] = new MetaMemCtrl(mctrls[n], sched[n], wbsched[n]);
                    for (int i = 0; i < cmax; i++) {
                        mctrls[n][i].meta_mctrl = meta_mctrl[n];
                    }
                    sched[n].meta_mctrl = meta_mctrl[n];
                    sched[n].initialize();
                    wbsched[n].meta_mctrl = meta_mctrl[n];
                    wbsched[n].initialize();
                }
            }

            //wbmode
            for (int n = 0; n < Config.mem.mctrl_num; n++) {
                mwbmode = Activator.CreateInstance(Config.mctrl.typeof_wbmode_algo, new Object[] { mctrls[n] }) as MemWBMode;
                for (int i = 0; i < cmax; i++) {
                    mctrls[n][i].mwbmode = mwbmode;
                }

                //blp tracker
                blptracker = new BLPTracker(mctrls[n]);
            }
        }

        static void run()
        {
            //DateTime start_time = DateTime.Now;

            bool[] is_done = new bool[Config.N];
            for (int i = 0; i < Config.N; i++) {
                is_done[i] = false;
            }

            bool finished = false;
            string OutputFileName = Config.output + ".csv";
          
            while (!finished) {
                finished = true;

                if (cycles % 1000000 == 0){
                  if (cycles != 0)
                  {
                      string OutputInformation = cycles.ToString() + " " + ((double) Dram_Utilization_size * 64 / 1073741824).ToString() + " " + ((double) Dram_req_num / (Dram_req_num + NVM_req_num + 0.001)).ToString();
                      using (StreamWriter sw = File.AppendText(OutputFileName))
                      {      
                         sw.WriteLine(OutputInformation);
                      }
                      Dram_req_num = 0;
                      NVM_req_num = 0;
                  }
                  
                }

                //processors
                int pid = rand.Next(Config.N);
                for (int i = 0; i < Config.N; i++) {
                    Proc curr_proc = procs[pid];
//yang:
                    if (Config.sim_type == Config.SIM_TYPE.GROUPED && pid >= Config.group_boundary && pid < Config.N && is_done[pid] == false) {
                        curr_proc.tick();
                    }
                    else if (Config.sim_type != Config.SIM_TYPE.PARALLEL || is_done[pid] == false) {
                        curr_proc.tick();
                    }
                    pid = (pid + 1) % Config.N;
                }


                  //memory controllers
                for (int n = 0; n < Config.mem.mctrl_num; n++) {
                    for (int i = 0; i < Config.mem.channel_max; i++) {
                        mctrls[n][i].tick();
                    }
                }


                //blp tracker
                blptracker.tick();

                //xbar
                xbar.tick();

                //cache
                foreach (Cache c in caches)
                    c.tick();
 
	
                if (Config.proc.cache_insertion_policy == "PFA")
                     mesur.tick();  

	
		//Jin: Row Migration Policies
		if (Config.proc.cache_insertion_policy == "RBLA" || Config.proc.cache_insertion_policy == "PFA")
       	             rmp.tick();

 
                //progress simulation time
                cycles++;

                //warmup phase
                for (int p = 0; p < Config.N; p++)
                {
                    if (!proc_warmup[p])
                    {
                        if (Stat.procs[p].ipc.Count >= Config.warmup_inst_max)
                        {
                            proc_warmup[p] = true;
                            Stat.procs[p].Reset();
                            foreach (MemCtrlStat mctrl in Stat.mctrls) {
                                    mctrl.Reset(pid);
                            }              
                            foreach (BankStat bank in Stat.banks) {
                                    bank.Reset(pid);
                            }
                            proc_warmup_cycles[p] = cycles;
                            Measurement.warmup_core_stall_cycles[p] = Measurement.core_stall_cycles[p];
                            processor_finished[p] = false;
                            DRAM_processor_read_hit[p] = 0;
                            DRAM_processor_read_miss[p] = 0;
                            DRAM_processor_write_hit[p] = 0;
                            DRAM_processor_write_miss[p] = 0;
                            DRAM_migration_read_hit[p] = 0;
                            DRAM_migration_read_miss[p] = 0;
                            DRAM_migration_write_hit[p] = 0;
                            DRAM_migration_write_miss[p] = 0;
                            NVM_processor_read_hit[p] = 0;
                            NVM_processor_read_miss[p] = 0;
                            NVM_processor_write_hit[p] = 0;
                            NVM_processor_write_miss[p] = 0;
                            NVM_migration_read_hit[p] = 0;
                            NVM_migration_read_miss[p] = 0;
                            NVM_migration_write_hit[p] = 0;
                            NVM_migration_write_miss[p] = 0;
                            processor_cycles[p] = 0;
                        }
                    }
                }
 


                    //case #1: instruction constrained simulation
                    if (Config.sim_type == Config.SIM_TYPE.INST) {
                        for (int p = 0; p < Config.N; p++) {
                            if (is_done[p]) continue;

                            if (proc_warmup[p] && (Stat.procs[p].ipc.Count >= Config.sim_inst_max)) {
                                //simulation is now finished for this processor
                                finish_proc(p);
                                is_done[p] = true;

                                //Power Measurement:
			        processor_finished[p] = true;
                                processor_cycles[p] = cycles - proc_warmup_cycles[p];
                            
                                information_output (p);

                            }   
                            else {
                                //simulation is still unfinished for this processor
                                finished = false;
                            }
                        }
                    }

                    //case #2: cycle constrained simulation  // default case
                    else if (Config.sim_type == Config.SIM_TYPE.CYCLE) {
                        if (cycles >= Config.sim_cycle_max) {
                            finish_proc();
                            finished = true;
                        }
                        else {
                            finished = false;
                        }
                    }

                    //case #3: run to completion
                    else if (Config.sim_type == Config.SIM_TYPE.COMPLETION || Config.sim_type == Config.SIM_TYPE.PARALLEL) {
                        for (int p = 0; p < Config.N; p++) {
                            if (is_done[p]) continue;

                            if (procs[p].trace.finished) {
                                //simulation is now finished for this processor
                                finish_proc(p);
                                is_done[p] = true;
                            }
                            else {
                                //simulation is still unfinished for this processor
                                finished = false;
                            }
                        }
                    }

                    //case #4: run to completion for the parallel group
                    else if (Config.sim_type == Config.SIM_TYPE.GROUPED) {
                        for (int p = Config.group_boundary; p < Config.N; p++) {
                            if (is_done[p]) continue;

                            if (procs[p].trace.finished) {
                                //simulation is now finished for this processor
                                finish_proc(p);
                                is_done[p] = true;
                            }
                            else {
                                //simulation is still unfinished for this processor
                                finished = false;
                            }
                        }
                        if (finished == true) {
                            for (int p = 0; p < Config.group_boundary; p++) {
                                finish_proc(p);
                            }
                        }
                    }
                
            }
        }

        static void information_output(int p)
        {
/*            double total_mpkc = 0;
            for (int i = 0; i < Config.N; i++)
                total_mpkc += (double) (Measurement.core_rpkc[i] + Measurement.core_wpkc[i]);

            string OutputFileName = Config.output + ".txt";
            string OutputInformation = p.ToString() + " " + (((double) Measurement.t_excess_Acc[p]) / 1000000000).ToString() + " " + (((double)Measurement.cycles) / 1000000000).ToString() + " " + (((double) Measurement.uore_stall_cycles[p])/1000000000).ToString() + " " + (Measurement.interference[p] / 1000000000).ToString() + " " + (((double)Measurement.memtime[p]) / 1000000000).ToString();

            using(StreamWriter sw = File.AppendText(OutputFileName))
            {
                sw.WriteLine(OutputInformation);
            }
*/

// Power Measurement:
            string OutputFileName = Config.output + ".txt";
            string OutputInformation = p.ToString() + " " + (((double) processor_cycles[p]) / 1000000000).ToString() + " " + (((double) DRAM_processor_read_hit[p]) / 10000000).ToString() + " " + (((double) DRAM_processor_read_miss[p]) / 10000000).ToString() + " " + (((double) DRAM_processor_write_hit[p]) / 10000000).ToString() + " " + (((double) DRAM_processor_write_miss[p]) / 10000000).ToString() + " " + (((double) DRAM_migration_read_hit[p]) / 10000000).ToString() + " " + (((double) DRAM_migration_read_miss[p]) / 10000000).ToString() + " " + (((double) DRAM_migration_write_hit[p]) / 10000000).ToString() + " " + (((double) DRAM_migration_write_miss[p]) / 10000000).ToString() + " " + (((double) NVM_processor_read_hit[p]) / 10000000).ToString() + " " + (((double) NVM_processor_read_miss[p]) / 10000000).ToString() + " " + (((double) NVM_processor_write_hit[p]) / 10000000).ToString() + " " + (((double) NVM_processor_write_miss[p]) / 10000000).ToString() + " " + (((double) NVM_migration_read_hit[p]) / 10000000).ToString() + " " + (((double) NVM_migration_read_miss[p]) / 10000000).ToString() + " " + (((double) NVM_migration_write_hit[p]) / 10000000).ToString() + " " + (((double) NVM_migration_write_miss[p]) / 10000000).ToString() + " " + ((double) (Measurement.core_stall_cycles[p] - Measurement.warmup_core_stall_cycles[p]) / 1000000000).ToString();
            using(StreamWriter sw = File.AppendText(OutputFileName))
            {
                sw.WriteLine(OutputInformation);
            }
        }

        public static void DRAM_power_statistics (int p, bool migration, ReqType type, bool hit)
        {
             if (processor_finished[p])
                 return;

             if (migration)
             {
                  if (type == ReqType.RD)
                  {
                       if (hit)
                           DRAM_migration_read_hit[p] = DRAM_migration_read_hit[p] + 1;
                       else
                           DRAM_migration_read_miss[p] = DRAM_migration_read_miss[p] + 1;
                  }
                  else
                  {
                       if (hit)
                           DRAM_migration_write_hit[p] = DRAM_migration_write_hit[p] + 1;
                       else
                           DRAM_migration_write_miss[p] = DRAM_migration_write_miss[p] + 1;
                  }
             }
             else
             {
                  if (type == ReqType.RD)
                  {
                       if (hit)
                           DRAM_processor_read_hit[p] = DRAM_processor_read_hit[p] + 1;
                       else
                           DRAM_processor_read_miss[p] = DRAM_processor_read_miss[p] + 1;
                  }
                  else
                  {
                       if (hit)
                           DRAM_processor_write_hit[p] = DRAM_processor_write_hit[p] + 1;
                       else
                           DRAM_processor_write_miss[p] = DRAM_processor_write_miss[p] + 1;
                  }
             }
        }

        
        
        public static void NVM_power_statistics (int p, bool migration, ReqType type, bool hit)
        {
             if (processor_finished[p])
                 return;

             if (migration)
             {
                  if (type == ReqType.RD)
                  {
                       if (hit)
                           NVM_migration_read_hit[p] = NVM_migration_read_hit[p] + 1;
                       else
                           NVM_migration_read_miss[p] = NVM_migration_read_miss[p] + 1;
                  }
                  else
                  {
                       if (hit)
                           NVM_migration_write_hit[p] = NVM_migration_write_hit[p] + 1;
                       else
                           NVM_migration_write_miss[p] = NVM_migration_write_miss[p] + 1;
                  }
             }
             else
             {
                  if (type == ReqType.RD)
                  {
                       if (hit)
                           NVM_processor_read_hit[p] = NVM_processor_read_hit[p] + 1;
                       else
                           NVM_processor_read_miss[p] = NVM_processor_read_miss[p] + 1;
                  }
                  else
                  {
                       if (hit)
                           NVM_processor_write_hit[p] = NVM_processor_write_hit[p] + 1;
                       else
                           NVM_processor_write_miss[p] = NVM_processor_write_miss[p] + 1;
                  }
             }
        }  

        static void finish()
        {
            foreach (MemCtrlStat mctrl in Stat.mctrls) {
                mctrl.Finish(Sim.cycles);
            }
            foreach (BusStat bus in Stat.busses) {
                bus.Finish(Sim.cycles);
            }
            foreach (BankStat bank in Stat.banks) {
                bank.Finish(Sim.cycles);
            }
//yang:
            foreach (MemCtrlStat mctrl2 in Stat.mctrls2) {
                mctrl2.Finish(Sim.cycles);
            }
            foreach (BusStat bus2 in Stat.busses2) {
                bus2.Finish(Sim.cycles);
            }
            foreach (BankStat bank2 in Stat.banks2) {
                bank2.Finish(Sim.cycles);
            }
        }

        static void finish_proc()
        {
            for (int pid = 0; pid < Config.N; pid++) {
                finish_proc(pid);
            }
        }

        static void finish_proc(int pid)
        {
            Stat.procs[pid].Finish(Sim.cycles - Sim.proc_warmup_cycles[pid]);
            foreach (MemCtrlStat mctrl in Stat.mctrls) {
                mctrl.Finish(Sim.cycles - Sim.proc_warmup_cycles[pid],pid);
            }
            foreach (BankStat bank in Stat.banks) {
                bank.Finish(Sim.cycles - Sim.proc_warmup_cycles[pid], pid);
            }
        }
    }
}
