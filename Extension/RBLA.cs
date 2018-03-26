using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MemMap
{
    public class RBLA
        {
		public static ulong MissThresh = 2; 
		static int NB=0,PNB=0;   //Net Benefit, Previous Net Benefit
		static bool is_incremented = true; //true for previously incremented, false for previously decremented 
		static ulong migration_num = 0;      //Number of migration last time
//                static ulong prev_migration_num = 0;
//                static ulong trapped_thresh = 10;

		//These parameters might need to be changed later
		static ulong migration_cost;
		static ulong diff_read_cost;
                static ulong diff_write_cost;   //cost differenct between NVM write and DRAM write
		static ulong weight; 
		//ratio of write miss cost and read miss cost used for comparing with MissTresh
                
 //               static ulong ExpectedDramReadMiss;
 //               static ulong ExpectedDramWriteMiss;

                public static void initialize()
                {
                    DDR3DRAM ddr3_temp1 = new DDR3DRAM (Config.mem.ddr3_type, Config.mem.clock_factor, 0, 0);
                    DDR3DRAM ddr3_temp2 = new DDR3DRAM (Config.mem2.ddr3_type, Config.mem2.clock_factor,0,0);
                    migration_cost = (ulong) Config.mem.clock_factor * ddr3_temp1.COL_MAX * (ulong)(1<<Config.proc.block_size_bits) / ddr3_temp1.CHANNEL_WIDTH * 8 / 2;      //8: byte size; 2: each edge transfers one data
                    diff_read_cost = ddr3_temp1.timing.tRCD - ddr3_temp2.timing.tRCD;
                    diff_write_cost = ddr3_temp1.timing.tWR - ddr3_temp2.timing.tWR;
                    weight = diff_write_cost / diff_read_cost;
                    if ((migration_cost <= 0) ||( diff_read_cost <= 0) ||( diff_write_cost <= 0) ||( weight <= 0))
                            Console.WriteLine("RBLA Big Mistake");
                    MissThresh =  migration_cost / diff_read_cost;
		
                }
           


                public static void decision()
                {      
			//Migration.migrationlist is already cleared in Policy.csi
			NB = (int)(RowStat.DramReadMissPerInterval * diff_read_cost + RowStat.DramWriteMissPerInterval * diff_write_cost - migration_cost * migration_num); 
  /*                      Console.WriteLine("Benifit {0}",NB);
                        Console.WriteLine("DRAMReadMissPerInterval: {0}",RowStat.DramReadMissPerInterval);
                        Console.WriteLine("DRAMWriteMissPerInterval: {0}",RowStat.DramWriteMissPerInterval);
//                        Console.WriteLine("DramHitPerInterval:{0}",RowStat.DramHitPerInterval);
                        Console.WriteLine("MigrationNumber:{0}",migration_num);
//                        Console.WriteLine("NVMReadMissPerInterval: {0}",RowStat.NVMReadMissPerInterval);
//                        Console.WriteLine("NVMWriteMissPerInterval: {0}",RowStat.NVMWriteMissPerInterval);*/

    /*                    if (migration_num <= trapped_thresh && prev_migration_num <= trapped_thresh)
                        {
                            initialize();
                            migration_num = 0;
                            prev_migration_num = 0;
                            is_incremented = true;
                            NB=0;
                            PNB=0;
                            Console.WriteLine("Threshold {0}",MissThresh);
                            return;
                        }
*/
			if(NB < 0)
                        {
				MissThresh = MissThresh + 1;
                                is_incremented = true;
                        }
			else if (NB > PNB)
			{
				if (is_incremented)
                                {
					MissThresh = MissThresh + 1;
                                }
				else
                                {
                                        if (MissThresh != 0)
						MissThresh = MissThresh - 1;
                                }
			}
			else
			{
				if (is_incremented)
                                {
                                        if (MissThresh != 0)
						MissThresh = MissThresh - 1;
                                        else
                                                is_incremented = !is_incremented;
                                }
				else
                                { 
					MissThresh = MissThresh + 1;
                                }

				is_incremented = !is_incremented; 
			}
/*                        else if (MissThresh != 0)
                        {
                                MissThresh = MissThresh - 1;
                                is_incremented = false;
                        }
*/
//			Console.WriteLine("Threshold {0}",MissThresh);

			PNB = NB;          //
                        
//                        prev_migration_num = migration_num;			
			migration_num = 0; //reset migration counter
                }


               public static void tick()
               {
                        RowStat.AccessInfo temp;
                        ulong rowkey;

                        rowkey = RowStat.KeyGen(Row_Migration_Policies.target_req);
                    
                        if (!RowStat.NVMDict.ContainsKey(rowkey))
                            return;
               
                        temp = RowStat.NVMDict[rowkey];
                     
                        if (temp.addlist)
                            return;

                        if ((temp.ReadMiss+temp.WriteMiss*weight > MissThresh) && (!Migration.migrationlist.Contains(rowkey)))
			{
			    Migration.migrationlist.Add(rowkey);
                            Migration.migrationlistPID.Add(temp.pid);
			    migration_num++;
                            temp.addlist = true; 
                            RowStat.NVMDict[rowkey] = temp;
		        }	
  
               }
	}
      
}
