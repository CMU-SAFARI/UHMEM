using System;
using System.Collections.Generic;
using System.Text;


namespace MemMap
{
    public enum ReqType
    {
        RD,
        WR
    };

    public class Req
    {
        //page mapping
        public static PageRandomizer prand;
        public static PageSequencer pseq;

        //state
        public int pid;
        public ReqType type;

        //address
        public ulong paddr;
        public ulong block_addr;
        public MemAddr addr;

        //timestamp
        public long ts_arrival;
        public long ts_departure;
        public int latency;

        //associated write-back request
        public Req wb_req;

        //callback
        public Callback callback;
        public Callback cache_callback;

        //scheduling-related
        public bool marked;

        //writeback-related
        public bool wb_marked;
        public int transaction_length;
        public bool cache_wb;

        //migration related write
        public bool migrated_request;

        //MLP related;
        public double MLPAcc;
        public double MLPTimes;
        
        //whether hit or miss
        public int hit;

        //constructor
        public Req() { }

        public void set(int pid, ReqType type, ulong paddr)
        {
            //state
            this.pid = pid;
            this.type = type;

            //address
            if (Config.mctrl.page_randomize) {
                this.paddr = prand.get_paddr(paddr);
            }
            else if (Config.mctrl.page_sequence){
                this.paddr = pseq.get_paddr(paddr);
            }
//		else if (Config.mctrl.remapping) {
//			if (mapping_table.contains(paddr >> PAGE_SIZE)) {
//				this.paddr = mapping_table[paddr >> PAGE_SIZE];
//			}
//		}
            else {
                this.paddr = paddr;
            }
            this.block_addr = this.paddr >> Config.proc.block_size_bits;
            this.addr = MemMap.translate(this.paddr, pid);

            //misc
            this.reset();
        }
        
        public void set(int pid, ReqType type, ulong paddr,bool flag)
        {
            if (flag == false)
            Console.Write ("Mistakenly call request set\n");

            //state
            this.pid = pid;
            this.type = type;

            this.paddr = paddr;
            this.block_addr = this.paddr >> Config.proc.block_size_bits;
            this.addr = MemMap.translate(this.paddr, pid);

            //misc
            this.reset();
        }

        public void paddr_set(ulong paddr, int pid)
        {
//            this.paddr = paddr;
//            this.block_addr = this.paddr >> Config.proc.block_size_bits;
            this.addr = MemMap.translate(paddr, pid);
        }

        public void reset()
        {
            //timestamp
            ts_arrival = -1;
            ts_departure = -1;
            latency = -1;

            //other
            wb_req = null;
            callback = null;
            marked = false;
            wb_marked = false;
            transaction_length = 0;
            cache_wb = false;
            migrated_request = false;
            MLPAcc=0;
            MLPTimes=0;
            hit = 0;      //un-processed: hit=0; hit: hit=1; miss hit=2.
        }
    }

    public class RequestPool
    {
        private const int RECYCLE_MAX = 100000;
        private static LinkedList<Req> req_pool = new LinkedList<Req>();

/*        public static int count;
        public static int RD_Count;
        public static int WR_Count;
        public static int DRAM_TO_PCM_Count;
        public static int CacheWrite;
        public static int PCMWrite;
*/
        static RequestPool()
        {
            for (int i = 0; i < RECYCLE_MAX; i++)
                req_pool.AddFirst(new Req());
        }

        public static void enpool(Req req)
        {
            req.reset();
            req_pool.AddLast(req);
        }

        public static Req depool()
        {
//            Dbg.Assert(req_pool.First != null);
            if (req_pool.First == null)
            {
               Req req_temp=new Req();
               req_pool.AddLast(req_temp);
            }
               
            Req req = req_pool.First.Value;
            req_pool.RemoveFirst();
            return req;
        }

        public bool is_empty()
        {
            return req_pool.Count == 0;
        }
    }
}
