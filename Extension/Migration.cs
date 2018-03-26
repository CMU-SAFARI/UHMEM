using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MemMap
{
    public class Migration
	{
		static int Cycles = 0;
                static ulong current_block = 0;
		public static List<ulong> migrationlist = new List<ulong>();
                public static List<int> migrationlistPID = new List<int>();
		
		public static int tick ()
		{
			if (migrationlist.Any())
				migrate();
			Cycles++;
                        return 0;
		}


		public static void migrate()
	        {
                        ulong key = migrationlist[0];
                        ulong paddr = key * RowStat.page_size; 
                        ulong units =(ulong) 1 << ((int)Config.proc.block_size_bits);
                        ulong num_block =(ulong) 1 << RowStat.page_bits;
                       
			Req req1 = new Req();
                        int pid1 = migrationlistPID[0];
			req1.set(pid1, ReqType.RD, paddr + current_block*units, true);
                        req1.ts_arrival = Cycles;
                        req1.ts_departure = Cycles;
                        req1.migrated_request = true;
                        if (!Sim.mctrls[Sim.get_mctrl(pid1)][req1.addr.cid].is_q_full(pid1, req1.type, req1.addr.rid, req1.addr.bid))
	                    Sim.mctrls[Sim.get_mctrl(pid1)][req1.addr.cid].enqueue_req(req1);
                        else
                            return;
                                
                        Req req2 = new Req();
			req2.set(pid1, ReqType.WR, paddr + current_block*units, true);
                        req2.ts_arrival = Cycles;
                        req2.ts_departure = Cycles;
                        req2.migrated_request = true;
                        Sim.caches[0].insert(req2);
                        
                        current_block+=1;
                        if (current_block == num_block)
                        {
                           current_block = 0;
                           migrationlist.RemoveAt(0);
                           migrationlistPID.RemoveAt(0);
                           RowCache.NVMCache.evict(key);
                        }			
		}

	}
}
