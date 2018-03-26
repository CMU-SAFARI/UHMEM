using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace MemMap
{
    public class MemSchedConfig : ConfigGroup
    {
        public bool is_omniscient = false;    //whether all memory share the same controller

        //read scheduling algorithm
        public string sched_algo = "FRFCFS";
        public string sched_algo2 = "FRFCFS2";
        public Type typeof_sched_algo;
        public Type typeof_sched_algo2;

        //write scheduling algorithm
        public bool same_sched_algo = false;
        public string wbsched_algo = "FRFCFS";
        public string wbsched_algo2 = "FRFCFS2";
        public Type typeof_wbsched_algo;
        public Type typeof_wbsched_algo2;
        public double preempt_fraction = 0.75;
        public bool tcm_only_rmpki = false;

        //writeback throttle
        public string wbthrottle_algo = "Tax";
        public Type typeof_wbthrottle_algo;
        public uint wbthrottle_cycles = 0;
        public double wbthrottle_rmpki_threshold = 8;

        public double wbthrottle_wmpki_threshold = 8;
        public double wbthrottle_fraction = 0.5;

        //prioritize row-hits
        public bool prioritize_row_hits = false;

        /*************************
         * FRFCFS Scheduler
         *************************/
        public int row_hit_cap = 4;

        /*************************
         * STFM Scheduler
         *************************/
        public double alpha = 1.1;
        public ulong beta = 1048576;
        public double gamma = 0.5;
        public int ignore_gamma = 0;

        /*************************
         * ATLAS Scheduler
         *************************/
        public int quantum_cycles = 1000000;
        public int threshold_cycles = 100000;
        public double history_weight = 0.875;
        public bool service_overlap = false;

        /*************************
         * PAR-BS Scheduler
         *************************/
        public int batch_cap = 5;
        public int prio_max = 11;   //0~9 are real priorities, 10 is no-priority

        /*************************
         * ACTS Scheduler
         *************************/
        public long acts_quantum_cycles = 100000;
        public bool acts_prioritize_fr = false;

        /*************************
         * Phason Scheduler
         *************************/
        public long interval_cycles = 100000;
        public bool phason_prioritize_fr = false;

        //schedulers: FR_FCFS_Cap, NesbitFull
        public ulong prio_inv_thresh = 0;        //FRFCFS_Cap, NesbitFull schedulers; in memory cycles

        //schedulers: STFM, Nesbit{Basic, Full}
        public int use_weights = 0;
        public double[] weights = new double[128];

        /*************************
         * TCM Scheduler
         *************************/
        public double AS_cluster_factor = 0.10;

        //shuffle
        public TCM.ShuffleAlgo shuffle_algo = TCM.ShuffleAlgo.Hanoi;
        public int shuffle_cycles = 800;
        public bool is_adaptive_shuffle = true;
        public double adaptive_threshold = 0.1;

        protected override bool set_special_param(string param, string val)
        {
            return false;
        }

        public override void finalize()
        {
            //memory scheduling algo
            string type_name = typeof(Sim).Namespace + "." + Config.sched.sched_algo;
            string type_name2 = typeof(Sim).Namespace + "." + Config.sched.sched_algo2;
            try{
                typeof_sched_algo = Type.GetType(type_name);
                typeof_sched_algo2 = Type.GetType(type_name2);
            }
            catch{
                throw new Exception(String.Format("Scheduler not found {0}", Config.sched.sched_algo));
            }

            type_name = typeof(Sim).Namespace + "." + Config.sched.wbsched_algo;
            type_name2 = typeof(Sim).Namespace + "." + Config.sched.wbsched_algo2;
            try {
                typeof_wbsched_algo = Type.GetType(type_name);
                typeof_wbsched_algo2 = Type.GetType(type_name2);
            }
            catch {
                throw new Exception(String.Format("Writeback scheduler not found {0}", Config.sched.wbsched_algo));
            }

            type_name = typeof(Sim).Namespace + "." + Config.sched.wbthrottle_algo;
            try {
                typeof_wbthrottle_algo = Type.GetType(type_name);
            }
            catch {
                throw new Exception(String.Format("Writeback throttler not found {0}", Config.sched.wbthrottle_algo));
            }


            /*
            //normalize weights
            if (use_weights != 0)
            {
                MemSchedAlgo algo = Config.sched.mem_sched_algo;
                if (algo == MemorySchedulingAlgorithm.FQM_BASIC || algo == MemorySchedulingAlgorithm.FQM_FULL)
                {
                    double total_weight = 0;
                    foreach (int i in Config.sched.weights)
                        total_weight += i;
                    for (int i = 0; i < Config.N; i++)
                        Config.sched.weights[i] /= total_weight;
                }
            }
            */
        }
    }
}
