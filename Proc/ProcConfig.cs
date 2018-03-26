using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MemMap
{
    public class ProcConfig : ConfigGroup
    {
        public int ipc = 3;

        //writebacks
 //     public bool wb = false;
//yang:
        public bool wb = true;

        //cache
        public int cache_size;
//        public int cache_size_bits = 19;    //power of two
//yang:
        public int cache_size_bits = 24;

        public int cache_assoc;
        public int cache_assoc_bits = 4;    //power of two

        public int block_size;
        public int block_size_bits = 6;     //power of two

        public int page_size;
        public int page_size_bits = 12;

        public int page_block_diff;
        public int page_block_diff_bits;

        //instruction window
        public int inst_wnd_max = 128;

        //mshr
//        public int mshr_max = 128;
//yang:     
        public int mshr_max = 32;

        //writeback queue
        public int wb_q_max = 128;

        //cache (other)
//      public bool cache = false;
//yang:
        public bool cache = true;

        public int cache_num = 1;

        public string cache_replacement_policy = "DRAMTrueLRU";
        public Type typeof_cache_replacement_policy;

//yang:
//      public string cache_write_policy = "WriteThrough";
        public string cache_write_policy = "WriteBack";

        public string cache_insertion_policy = "All";

        // size, associativity, and latency modeled after Power 7 L3 eDRAM cache
//        public int cache_sets = 65536; // with 8 ways => 32MB
//        public int cache_sets = 4194304;
//        public int cache_ways = 8;

//        public int cache_sets = 1048576;
//        public int cache_sets = 2097152;
//        public int cache_sets = 262144;

//        public int cache_sets = 524288;
//        public int cache_ways = 16;

//DRAM Cache with Page Granularity
          public int cache_sets = 8192;
          public int cache_ways = 16;

        public int cache_latency = 6;

        public double bip_epsilon = 0.03125; // 1/32
        public double cache_insert_prob = 0.5;


        protected override bool set_special_param(string param, string val)
        {
            return false;
        }

        public override void finalize()
        {
            //cache replacement policy
            string replacement_type_name = typeof(Sim).Namespace + "." + Config.proc.cache_replacement_policy;
            try{
                typeof_cache_replacement_policy = Type.GetType(replacement_type_name);
            }
            catch{
                throw new Exception(String.Format("Replacement policy not found {0}", Config.proc.cache_replacement_policy));
            }
            cache_size = 1 << cache_size_bits;
            cache_assoc = 1 << cache_assoc_bits;
            block_size = 1 << block_size_bits;
            page_size = 1 << page_size_bits;
            page_block_diff_bits = page_size_bits - block_size_bits;
            page_block_diff = 1 << page_block_diff_bits;
        }
    }
}
