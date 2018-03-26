using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace MemMap
{

    public class PFA
    {
	static uint migration_num = 0;
//        static uint prev_migration_num = 0;
//        static uint trapped_num = 10;
//	static double u_bar = 0;
        static ulong[] prev_core_stall_cycles;
//        static ulong[] prev_core_rpkc;
//        static ulong[] prev_core_wpkc;
        static double[] prev_interference;
        static ulong[] prev_memtime;
        static ulong diff_read_cost;
        static ulong diff_write_cost;
	static double u_total = 0;
	static double prev_u_total = 0;
	static bool eff_thresh_inc = false;
        static double Interval;
        static double eff_thresh = 0;
//        static double weight = 0.75;
//        static double beta = 0.8;
        static double alpha = 0.1;
        static double[] ThreadWeight; 
//        static double migration_cost;
//	static double[] r = new double[100];
//	static double[] r_temp = new double[100];
//	static double r_temp_bar = 0;

	public static void decision() {
        	//First, determine E0
        	prev_u_total = u_total;	
//        	u_total = 0;
//        	for(int i=0; i<Config.N; i++)   //sum up all utility in DRAM
//	        	u_total += RowStat.app_util_Dram[i];
//		u_bar = u_total/Config.N;

                u_total = 0;

                for (int i = 0; i < Config.N; i++)
                {
                    u_total += (double) (Measurement.core_stall_cycles[i] - prev_core_stall_cycles[i]);
                }

/*                double total_mpkc = 0;
                for (int i = 0; i < Config.N; i++)
                    total_mpkc += (double) ((Measurement.core_rpkc[i] + Measurement.core_wpkc[i]) - (prev_core_rpkc[i] + prev_core_wpkc[i]));
*/                
                for (int i = 0; i < Config.N; i++)
                {
//                    ThreadWeight[i] = 1.0 - beta * ((double) (Measurement.core_stall_cycles[i] - prev_core_stall_cycles[i])) / Interval * (1.0 - (Measurement.core_rpkc[i] + Measurement.core_wpkc[i] - prev_core_rpkc[i] - prev_core_wpkc[i]) / total_mpkc);
                      if (Measurement.memtime[i] != prev_memtime[i])
                      {
                          ThreadWeight[i] = 1.0 - (Measurement.interference[i] - prev_interference[i]) / ((double) (Measurement.memtime[i] - prev_memtime[i])) * ((double) (Measurement.core_stall_cycles[i] - prev_core_stall_cycles[i])) / Interval;
                      /*    Console.WriteLine(Sim.cycles);
                          if (Sim.cycles>600000000)
                          {
                              Console.WriteLine("ThreadWeight - {0}",ThreadWeight[i]);
                              Console.WriteLine("interference - {0}",Measurement.interference[i]);
                              Console.WriteLine("prev_interference - {0}",prev_interference[i]);
                              Console.WriteLine("memtime - {0}", Measurement.memtime[i]);
                              Console.WriteLine("prev_memtime - {0}", prev_memtime[i]);
                              Console.WriteLine("core_stall_cycles - {0}",Measurement.core_stall_cycles[i]);
                              Console.WriteLine("prev_core_stall_cycles - {0}",prev_core_stall_cycles[i]);
                              Console.WriteLine("Interval - {0}",Interval);
                          }*/
                      }
                      else
                          ThreadWeight[i] = 1.0;
                }

                for (int i = 0; i < Config.N; i++)
                {
//                    prev_core_rpkc[i] = Measurement.core_rpkc[i];
//                    prev_core_wpkc[i] = Measurement.core_wpkc[i];
                    prev_core_stall_cycles[i] = Measurement.core_stall_cycles[i];
                    prev_interference[i] = Measurement.interference[i];
                    prev_memtime[i] = Measurement.memtime[i];
                }



//                u_total = (RowStat.DramWeightedReadResponseTime + RowStat.NVMWeightedReadResponseTime);

//                Console.WriteLine ("migration number: {0}", migration_num);
/*                Console.WriteLine ("DramWeightedReadResponseTime: {0}", RowStat.DramWeightedReadResponseTime);
                Console.WriteLine ("NVMWeightedReadResponseTime: {0}", RowStat.NVMWeightedReadResponseTime);
                Console.WriteLine ("utotal: {0}", u_total);
  */
/*                if (migration_num < trapped_num && prev_migration_num < trapped_num)
                {
                        initialize();
                        migration_num = 0;
                        prev_migration_num = 0;
                        u_total = 0;
                        prev_u_total = 0;
                        eff_thresh_inc = false;
    //                    Console.WriteLine ("threshold: {0}", eff_thresh);
                        return;
                }	
*/
                double unit = RowStat.t_rd_miss_diff / 27.0;

                if (u_total < prev_u_total)
                {
                      if (eff_thresh_inc)
                      {
                           eff_thresh = eff_thresh + unit;
                      }
                      else
                      {
                           if (eff_thresh >= unit)
                               eff_thresh = eff_thresh - unit;
                      }
                }
                else
                {
                      if (eff_thresh_inc)
                      {
                          if (eff_thresh >= unit)
                                eff_thresh = eff_thresh - unit;
                          else
                                eff_thresh_inc = !eff_thresh_inc;
                      }
                      else
                      {
                          eff_thresh = eff_thresh + unit;
                      }
                      eff_thresh_inc  = !eff_thresh_inc;
                }

                migration_num = 0;
 

/*                else
                {
                        eff_thresh = 1/1.1 * eff_thresh;
                        eff_thresh_inc = false;
                }
 //               prev_migration_num = migration_num;
 //               migration_num = 0;
                Console.WriteLine ("threshold: {0}", eff_thresh);
                

/*		//Second, determine ri
		for(int i=0; i<Config.N; i++) {
		        if (RowStat.app_util_Dram[i] > u_bar)
				r_temp[i]= r[i] + 0.1;
			else if (RowStat.app_util_Dram[i] < u_bar)
				r_temp[i]= r[i] - 0.1;
		        else
		                r_temp[i] = r[i];
		}
	 
		r_temp_bar = 0;
		for(int i=0; i<Config.N; i++) {
			r_temp_bar += r[i]/Config.N;
		}

		for(int i=0; i<Config.N; i++) {
			r[i] = r_temp[i]/r_temp_bar;
		}

		//Last, determine Ei
		for(int i=0; i<Config.N; i++) {
			RowStat.eff[i] = RowStat.eff_thresh * r[i];
			}
*/
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
                     
//               double utility = (temp.ReadMiss * RowStat.t_rd_miss_diff * temp.ReadMLPnum + weight * temp.WriteMiss * RowStat.t_wr_miss_diff * temp.WriteMLPnum)  * ThreadWeight[temp.pid];

              double utility = (temp.ReadMiss * diff_read_cost * temp.ReadMLPnum + temp.WriteMiss * diff_write_cost * temp.WriteMLPnum)  * ThreadWeight[temp.pid];




//               if (temp.effectiveness > RowStat.eff[temp.pid])
               if ((utility > eff_thresh) && (!Migration.migrationlist.Contains(rowkey)))
               {
                    Migration.migrationlist.Add(rowkey);
                    Migration.migrationlistPID.Add(temp.pid);
                    migration_num++;
                    temp.addlist = true;
                    RowStat.NVMDict[rowkey] = temp;
               }
                
         }

	public static void initialize()
	{
                DDR3DRAM ddr3_temp1 = new DDR3DRAM (Config.mem.ddr3_type, Config.mem.clock_factor, 0, 0);   
                DDR3DRAM ddr3_temp2 = new DDR3DRAM (Config.mem2.ddr3_type, Config.mem2.clock_factor,0,0);   
                diff_read_cost = ddr3_temp1.timing.tRCD - ddr3_temp2.timing.tRCD;
                diff_write_cost = ddr3_temp1.timing.tWR - ddr3_temp2.timing.tWR;
//       	for(int i=0; i<Config.N ; i++)
//			r[i] = 1;
                Interval = Row_Migration_Policies.Interval;
                prev_core_stall_cycles = new ulong[Config.N];
//                prev_core_rpkc = new ulong[Config.N];
//                prev_core_wpkc = new ulong[Config.N];
                prev_interference = new double[Config.N];
                prev_memtime = new ulong[Config.N];

                for (int i=0; i<Config.N; i++)
                {
                    prev_core_stall_cycles[i] = 0;
//                    prev_core_rpkc[i] = 0;
//                    prev_core_wpkc[i] = 0;
                    prev_interference[i] = 0;
                    prev_memtime[i] = 0;
                }
                ThreadWeight = new double [Config.N];
                for (int i = 0; i < Config.N; i++)
                    ThreadWeight[i] = 1;
//                migration_cost = 8*1024/64;
//                eff_thresh = 8*1024/64 * 8/2 * 0.5;
	}

	public static void assignE0()
	{
                u_total = 0;

                for (int i = 0; i < Config.N; i++)
                {
                    u_total += (double) (Measurement.core_stall_cycles[i] - prev_core_stall_cycles[i]);
                }
	        
/*                double total_mpkc = 0;
                for (int i = 0; i < Config.N; i++)
                    total_mpkc += (double) (Measurement.core_rpkc[i] + Measurement.core_wpkc[i] - prev_core_rpkc[i] - prev_core_wpkc[i]);
*/                
                for (int i = 0; i < Config.N; i++)
                {
//                    ThreadWeight[i] = 1.0 - beta * ((double) (Measurement.core_stall_cycles[i] - prev_core_stall_cycles[i])) / Interval * (1.0 - (Measurement.core_rpkc[i] + Measurement.core_wpkc[i] - prev_core_rpkc[i] - prev_core_wpkc[i]) / total_mpkc);
                      if (Measurement.memtime[i] != prev_memtime[i])
                          ThreadWeight[i] = 1.0 - (Measurement.interference[i] - prev_interference[i]) / ((double) (Measurement.memtime[i] - prev_memtime[i])) * ((double) (Measurement.core_stall_cycles[i] - prev_core_stall_cycles[i])) / Interval;
                      else
                          ThreadWeight[i] = 1.0;
              }

                for (int i = 0; i < Config.N; i++)
                {
                    prev_core_stall_cycles[i] = Measurement.core_stall_cycles[i];
//                    prev_core_rpkc[i] = Measurement.core_rpkc[i];
//                    prev_core_wpkc[i] = Measurement.core_wpkc[i];
                    prev_interference[i] = Measurement.interference[i];
                    prev_memtime[i] = Measurement.memtime[i];
                }

                double Eff_max = 1;
		foreach (ulong rowkey in RowStat.NVMLookUp)
		{
                	RowStat.AccessInfo temp = RowStat.NVMDict[rowkey];
//                        double temp_utility = (temp.ReadMiss * RowStat.t_rd_miss_diff * temp.ReadMLPnum + weight * temp.WriteMiss * RowStat.t_wr_miss_diff * temp.WriteMLPnum) * ThreadWeight[temp.pid];	
                        double temp_utility = (temp.ReadMiss * diff_read_cost * temp.ReadMLPnum + temp.WriteMiss * diff_write_cost * temp.WriteMLPnum) * ThreadWeight[temp.pid];	       
                        if (temp_utility > Eff_max)
                               Eff_max = temp_utility;
                }
                eff_thresh = alpha * Eff_max;


/*                Console.WriteLine ("migration number: {0}", migration_num);
                Console.WriteLine ("DramWeightedReadResponseTime: {0}", RowStat.DramWeightedReadResponseTime);
                Console.WriteLine ("NVMWeightedReadResponseTime: {0}", RowStat.NVMWeightedReadResponseTime);
                Console.WriteLine ("utotal {0}", u_total);
                Console.WriteLine ("threshold: {0}", eff_thresh);*/
	}

    }
}
