using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;

namespace MemMap
{
   	public class Measurement
       	{
                public static ulong cycles;
                public static ulong interval = 1000000;
//                public static double r=0.5;
                public static int[] mem_read_num;
                public static int[] mem_write_num;

//                public static int[] MLP_rd_bank_num; // number of banks in which there is read request from the same core
//                public static int[] MLP_wr_bank_num; // number of banks in which there is write request from the same core

                public static ulong[] core_stall_cycles; //the number of cycles that each core stall
                public static ulong[] warmup_core_stall_cycles; //the number of cycles that each core stall during the warmup phase;
                public static ulong[] core_rpkc;         //read request per kilo-cycles; In reality, it is just the number of read requests from start to now.
                public static ulong[] core_wpkc;         //write request per kilo-cycles; In reality, it is just the number of write requests from start to now.

                public static bool[] Dram_bus_conflict;
                public static bool[] NVM_bus_conflict;

                public static double Dram_Q_rd_miss;
                public static double Dram_Q_wr_miss;
                public static double Dram_Q_rd_hit;
                public static double Dram_Q_wr_hit;
                public static double NVM_Q_rd_miss;
                public static double NVM_Q_wr_miss;
                public static double NVM_Q_rd_hit;
                public static double NVM_Q_wr_hit;
              
                public static double Dram_Q_rd_miss_init;
                public static double Dram_Q_wr_miss_init;
                public static double Dram_Q_rd_hit_init;
                public static double Dram_Q_wr_hit_init;
                public static double NVM_Q_rd_miss_init;
                public static double NVM_Q_wr_miss_init;
                public static double NVM_Q_rd_hit_init;
                public static double NVM_Q_wr_hit_init;
                
                public static long Dram_rowbufferunit;
                public static long NVM_rowbufferunit;

/*                public static double Dram_Q_rd_miss_Acc;
                public static double Dram_Q_wr_miss_Acc;
                public static double Dram_Q_rd_hit_Acc;
                public static double Dram_Q_wr_hit_Acc;
                public static double NVM_Q_rd_miss_Acc;
                public static double NVM_Q_wr_miss_Acc;
                public static double NVM_Q_rd_hit_Acc;
                public static double NVM_Q_wr_hit_Acc;
              
                public static double Dram_Q_rd_miss_times;
                public static double Dram_Q_wr_miss_times;
                public static double Dram_Q_rd_hit_times;
                public static double Dram_Q_wr_hit_times;
                public static double NVM_Q_rd_miss_times;
                public static double NVM_Q_wr_miss_times;
                public static double NVM_Q_rd_hit_times;
                public static double NVM_Q_wr_hit_times;
*/
                public bool[] core_stall; //the bit indicating whether the core is stalled.
                public static double[] t_excess;
                public static double[] t_excess_Acc;
//                public static double[] t_excess_total;
                public static int[,] core_req_num; //the number of request for each core
//                public static int[,] core_rd_req_num; // the number of read request for each core
//                public static int[,] core_wr_req_num; // the number of write request for each core
                public static int[] bank_req_pid; // the pid of the current inflight request for each bank
                public bool[] bank_conflict; //the bit indicating whether each core is stalled due to bank conflict
                public static long[,] rowbuffer_change;
                public static ulong[,] core_prev_rowid;//the row id of previous read request of each core for each bank;
                            
                public static double[] interference;
                public static double[] interference_Acc;
                public static ulong[] memtime;
                public static ulong[] memtime_Acc;

                public static int Dram_bank_num;
                public static int NVM_bank_num;
                public static int Dram_BANK_MAX;
                public static int NVM_BANK_MAX;

                public Measurement ()
                {
                       mem_read_num = new int[Config.N];
                       mem_write_num = new int[Config.N];
                       for (int i = 0; i < Config.N; i++)
                       {
                           mem_read_num[i] = 0;
                           mem_write_num[i] = 0;
                       }

                       Dram_bus_conflict = new bool[Config.N];
                       NVM_bus_conflict = new bool[Config.N];
                       for (int i = 0; i < Config.N; i++)
                       {
                           Dram_bus_conflict[i] = false;
                           NVM_bus_conflict[i] = false;
                       }

                       interference = new double[Config.N];
                       interference_Acc = new double[Config.N];
                       memtime = new ulong[Config.N];
                       memtime_Acc = new ulong[Config.N];
                      
                       for (int i = 0; i < Config.N; i++)
                       {
                           interference[i] = 0;
                           interference_Acc[i] = 0;
                           memtime[i] = 0;
                           memtime_Acc[i] = 0;                           
                       }
//                       MLP_rd_bank_num = new int[Config.N];
//                       MLP_wr_bank_num = new int[Config.N];
                       core_stall_cycles = new ulong[Config.N];
                       warmup_core_stall_cycles = new ulong[Config.N];
                       core_rpkc = new ulong[Config.N];
                       core_wpkc = new ulong[Config.N];

                       for (int i = 0; i < Config.N; i++)
                       {
 //                          MLP_rd_bank_num[i] = 0;
 //                          MLP_wr_bank_num[i] = 0;
                           core_stall_cycles[i] = 0;
                           warmup_core_stall_cycles[i] = 0;
                           core_rpkc[i] = 0;
                           core_wpkc[i] = 0;
                       }
                       

                       DDR3DRAM ddr2 = new DDR3DRAM(Config.mem2.ddr3_type, Config.mem2.clock_factor, Config.mem2.tWR, Config.mem2.tWTR);
                       DDR3DRAM ddr = new DDR3DRAM(Config.mem.ddr3_type, Config.mem.clock_factor, Config.mem.tWR, Config.mem.tWTR);
                       Dram_Q_rd_miss = (double)(ddr2.timing.tRTP + ddr2.timing.tRP + ddr2.timing.tRCD + ddr2.timing.tBL);
                       Dram_Q_wr_miss = (double)(ddr2.timing.tWR + ddr2.timing.tRP + ddr2.timing.tRCD + ddr2.timing.tBL);
                       Dram_Q_rd_hit = (double)(ddr2.timing.tCL + ddr2.timing.tBL);
                       Dram_Q_wr_hit = (double)(ddr2.timing.tCL + ddr2.timing.tBL);
                       NVM_Q_rd_miss = (double) (ddr.timing.tRTP + ddr.timing.tRP + ddr.timing.tRCD + ddr.timing.tBL);
                       NVM_Q_wr_miss = (double) (ddr.timing.tWR + ddr.timing.tRP + ddr.timing.tRCD + ddr.timing.tBL);
                       NVM_Q_rd_hit = (double) (ddr.timing.tCL + ddr.timing.tBL);
                       NVM_Q_wr_hit = (double) (ddr.timing.tCL + ddr.timing.tBL);

                       Dram_rowbufferunit = (long)((ddr2.timing.tRP + ddr2.timing.tRCD)/Config.mem.clock_factor);
                       NVM_rowbufferunit = (long)((ddr.timing.tRP + ddr.timing.tRCD)/Config.mem.clock_factor);

                       Dram_Q_rd_miss_init = Dram_Q_rd_miss;
                       Dram_Q_wr_miss_init = Dram_Q_wr_miss;
                       Dram_Q_rd_hit_init = Dram_Q_rd_hit;
                       Dram_Q_wr_hit_init = Dram_Q_wr_hit;
                       
                       NVM_Q_rd_miss_init = NVM_Q_rd_miss;
                       NVM_Q_wr_miss_init = NVM_Q_wr_miss;
                       NVM_Q_rd_hit_init = NVM_Q_rd_hit;
                       NVM_Q_wr_hit_init = NVM_Q_wr_hit;                     
                      
/*                       Dram_Q_rd_miss_Acc = 0;
                       Dram_Q_wr_miss_Acc = 0;
                       Dram_Q_rd_hit_Acc = 0;
                       Dram_Q_wr_miss_Acc = 0;
                       NVM_Q_rd_miss_Acc = 0;
                       NVM_Q_wr_miss_Acc = 0;
                       NVM_Q_rd_hit_Acc = 0;
                       NVM_Q_wr_hit_Acc = 0;
                       
                       Dram_Q_rd_miss_times = 0;
                       Dram_Q_wr_miss_times = 0;
                       Dram_Q_rd_hit_times = 0;
                       Dram_Q_wr_miss_times = 0;
                       NVM_Q_rd_miss_times = 0;
                       NVM_Q_wr_miss_times = 0;
                       NVM_Q_rd_hit_times = 0;
                       NVM_Q_wr_hit_times = 0;
                 
*/
                       Dram_bank_num = (int) (Config.mem2.channel_max * Config.mem2.rank_max * ddr2.BANK_MAX);
                       NVM_bank_num = (int) (Config.mem.channel_max * Config.mem.rank_max * ddr.BANK_MAX);
                       Dram_BANK_MAX = (int) ddr2.BANK_MAX;
                       NVM_BANK_MAX = (int) ddr.BANK_MAX;

                       core_stall = new bool[Config.N];
                       bank_conflict = new bool[Config.N];
                       t_excess = new double[Config.N];
                       t_excess_Acc = new double[Config.N];
//                       t_excess_total = new double[Config.N];
                       core_req_num = new int[Config.N, Dram_bank_num + NVM_bank_num];
//                       core_rd_req_num = new int[Config.N, Dram_bank_num + NVM_bank_num];
//                       core_wr_req_num = new int[Config.N, Dram_bank_num + NVM_bank_num];
                       bank_req_pid = new int[Dram_bank_num + NVM_bank_num];
                       rowbuffer_change = new long[Config.N, Dram_bank_num + NVM_bank_num];
                       core_prev_rowid = new ulong[Config.N, Dram_bank_num + NVM_bank_num];
                       for (int i = 0; i < Config.N; i++)
                       {
                         core_stall[i] = false;
                         bank_conflict[i] = false;
                         t_excess[i] = 0;
                         t_excess_Acc[i] = 0;
//                         t_excess_total[i] = 0;
                         for (int j = 0; j < Dram_bank_num + NVM_bank_num; j++)
                         {
                            core_req_num[i,j]=0;
                            rowbuffer_change[i,j] = 0;
//                            core_rd_req_num[i,j] = 0;
//                            core_wr_req_num[i,j] = 0;
                            bank_req_pid[j]=Config.N;
                            core_prev_rowid[i,j] = 0;
                         }
                       }
                }

                public void tick()
                {      

                       if (cycles % Config.mem.clock_factor == 0)
                       {
                          for (int i = 0; i < Config.N; i++)
                          {
                             double inc_num = 0;
                             if (Dram_bus_conflict[i] || NVM_bus_conflict[i])
                             {
                                 inc_num += 1;
                             }
                                                              
                             double temp1 = 0;
                             double temp2 = 0;
                             for (int j=0; j < Dram_bank_num + NVM_bank_num; j++)
                             {
                                 if (core_req_num[i,j] != 0)
                                 {
                                     if (bank_req_pid[j] != i)
                                     {
                                         temp2 += 1;
                                         temp1 += 1;
                                     }
                                
                                     if ((bank_req_pid[j] == i) && (core_req_num[i,j] > 1))
                                         temp1 += 1;
                                 }
                             }
                            
                             if (temp1 != 0)
                             {     
                                 inc_num += (temp2 / temp1);
                             }

                             temp1 = 0;

                             for (int j = 0; j < Dram_bank_num + NVM_bank_num; j++)
                             {
                                 if (bank_req_pid[j] == i)
                                     temp1 += 1;
                             }
                             if (temp1 != 0)
                             {
                                 for (int j=0; j < Dram_bank_num + NVM_bank_num; j++)
                                 {
                                     if (rowbuffer_change[i,j] > 0)
                                         inc_num += 1.0 / temp1;
                                     else if (rowbuffer_change[i,j] < 0)
                                         inc_num += -1.0 / temp1;
                                 }
                             }
                     
                             if (inc_num > 1)
                                 t_excess_Acc[i] += Config.mem.clock_factor;
                             else
                                 t_excess_Acc[i] += inc_num * Config.mem.clock_factor;
                          }
                    
                          for (int i = 0; i < Config.N; i++)
                          {
                              memtime_Acc[i] += (ulong)Config.mem.clock_factor;
                              double inc_num = 0;

                              if (NVM_bus_conflict[i] || Dram_bus_conflict[i])
                              {
                                  inc_num += 1;
                              }
                              
                              double temp_num1 = 0;
                              double temp_num2 = 0;
                              for (int j = 0; j < NVM_bank_num + Dram_bank_num; j++)
                              {
                                  if (core_req_num[i,j] != 0)
                                  {
                                      temp_num1 += 1;
                                      if (bank_req_pid[j] != i)
                                          temp_num2 += 1;
                                  }
                              }
                              if (temp_num1 != 0)
                              {
                                  inc_num += temp_num2 / temp_num1;
                              }
                              

                              for (int j=0; j < NVM_bank_num + Dram_bank_num; j++)
                              {  
                                  if (rowbuffer_change[i,j] > 0)
                                  {
                                      if(temp_num1 != 0)
                                          inc_num += 1.0 / temp_num1;
                                      rowbuffer_change[i,j] += -1;
                                  }
                                  else if (rowbuffer_change[i,j] < 0)
                                  {
                                      if(temp_num1 != 0)
                                          inc_num += -1.0 / temp_num1;
                                      rowbuffer_change[i,j] += 1;
                                  }
                              
                              }

                              if (inc_num > 1)
                                  interference_Acc[i] += Config.mem.clock_factor;
                              else
                                  interference_Acc[i] += inc_num * Config.mem.clock_factor;
                           }    
                       }         
                                
                       if (cycles % interval == 0)
                       {
                       
/*                     	 if (Dram_Q_rd_miss_times > 0)
                       	   Dram_Q_rd_miss = Dram_Q_rd_miss_Acc / Dram_Q_rd_miss_times;
                         else
                           Dram_Q_rd_miss = Dram_Q_rd_miss_init;
                   
                  	 if (Dram_Q_wr_miss_times > 0)
                       	   Dram_Q_wr_miss = Dram_Q_wr_miss_Acc / Dram_Q_wr_miss_times;
                         else
                           Dram_Q_wr_miss = Dram_Q_wr_miss_init;
                   
                         if (Dram_Q_rd_hit_times > 0)
                           Dram_Q_rd_hit = Dram_Q_rd_hit_Acc / Dram_Q_rd_hit_times;
                         else
                           Dram_Q_rd_hit = Dram_Q_rd_hit_init;

                         if (Dram_Q_wr_hit_times > 0)
                           Dram_Q_wr_hit = Dram_Q_wr_hit_Acc / Dram_Q_wr_hit_times;
                         else
                           Dram_Q_wr_hit = Dram_Q_wr_hit_init;

                     	 if (NVM_Q_rd_miss_times > 0)
                       	   NVM_Q_rd_miss = NVM_Q_rd_miss_Acc / NVM_Q_rd_miss_times;
                         else
                           NVM_Q_rd_miss = NVM_Q_rd_miss_init;
                   
                  	 if (NVM_Q_wr_miss_times > 0)
                       	   NVM_Q_wr_miss = NVM_Q_wr_miss_Acc / NVM_Q_wr_miss_times;
                         else
                           NVM_Q_wr_miss = NVM_Q_wr_miss_init;
                   
                         if (NVM_Q_rd_hit_times > 0)
                           NVM_Q_rd_hit = NVM_Q_rd_hit_Acc / NVM_Q_rd_hit_times;
                         else
                           NVM_Q_rd_hit = NVM_Q_rd_hit_init;

                         if (NVM_Q_wr_hit_times > 0)
                           NVM_Q_wr_hit = NVM_Q_wr_hit_Acc / NVM_Q_wr_hit_times;
                         else
                           NVM_Q_wr_hit = NVM_Q_wr_hit_init;
                         
                         Dram_Q_rd_miss_Acc = 0;
                         Dram_Q_wr_miss_Acc = 0;
                         Dram_Q_rd_hit_Acc = 0;
                         Dram_Q_wr_hit_Acc = 0;
                         NVM_Q_rd_miss_Acc = 0;
                         NVM_Q_wr_miss_Acc = 0;
                         NVM_Q_rd_hit_Acc = 0;
                         NVM_Q_wr_hit_Acc = 0;

                         Dram_Q_rd_miss_times = 0;
                         Dram_Q_wr_miss_times = 0;
                         Dram_Q_rd_hit_times = 0;
                         Dram_Q_wr_hit_times = 0;
                         NVM_Q_rd_miss_times = 0;
                         NVM_Q_wr_miss_times = 0;
                         NVM_Q_rd_miss_times = 0;
                         NVM_Q_wr_miss_times = 0;
*/
                         for (int i = 0; i < Config.N; i++)
                         {
                           t_excess[i] = t_excess_Acc[i];
                           interference[i] = interference_Acc[i];
                           memtime[i] = memtime_Acc[i];
                         }
                       }
                       cycles += 1;
/*                       if (cycles == 1000000000)
                       {
                          for (int i = 0; i < Config.N; i++)
                             Console.WriteLine ("t_excess_total is: {0}", t_excess_total[i]);
                       }
*/
/*                       if (cycles % 1000000 == 0)
                       {
                          Console.WriteLine ("cycles: {0}", cycles);
                       }
*/

                }
               
                public static void DramSetCorePrevRowid (Req req)
                {
                   core_prev_rowid[req.pid, NVM_bank_num + (req.addr.cid * Config.mem2.rank_max + req.addr.rid) * Dram_BANK_MAX + req.addr.bid] = req.addr.rowid;
                }

                public static void NVMSetCorePrevRowid (Req req)
                {
                   core_prev_rowid[req.pid, (req.addr.cid * Config.mem.rank_max + req.addr.rid) * NVM_BANK_MAX + req.addr.bid] = req.addr.rowid;
                }

                public static void DramMissSetRowBufferChange (Req req)
                {
                   if (req.addr.rowid == core_prev_rowid[req.pid, NVM_bank_num + req.addr.bid])
                   {
                         rowbuffer_change[req.pid, NVM_bank_num + req.addr.bid] += Dram_rowbufferunit;

//                         Console.WriteLine ("Cycles: {0}  DRAM: {1}", cycles, rowbuffer_change[req.pid]);

//                         Console.WriteLine ("dram yes");
                   }
//                   else
//                         Console.WriteLine ("darm no");
                }

                public static void NVMMissSetRowBufferChange (Req req)
                {
                   if (req.addr.rowid == core_prev_rowid[req.pid, req.addr.bid])
                   {
                  	    rowbuffer_change[req.pid, req.addr.bid] += NVM_rowbufferunit;
//                         Console.WriteLine ("Cycles: {0}   NVM: {1}", cycles, rowbuffer_change[req.pid]);
//                         Console.WriteLine ("nvm yes");
                   }
//                   else
//                         Console.WriteLine ("nvm no");
                }

                 public static void DramHitSetRowBufferChange (Req req)
                {
                   if (req.addr.rowid != core_prev_rowid[req.pid, NVM_bank_num + req.addr.bid])
                   {
                         rowbuffer_change[req.pid, NVM_bank_num + req.addr.bid] += -Dram_rowbufferunit;

//                         Console.WriteLine ("Cycles: {0}  DRAM: {1}", cycles, rowbuffer_change[req.pid]);

//                         Console.WriteLine ("dram yes");
                   }
//                   else
//                         Console.WriteLine ("darm no");
                }

                public static void NVMHitSetRowBufferChange (Req req)
                {
                   if (req.addr.rowid != core_prev_rowid[req.pid, req.addr.bid])
                   {
                  	    rowbuffer_change[req.pid, req.addr.bid] += -NVM_rowbufferunit;
//                         Console.WriteLine ("Cycles: {0}   NVM: {1}", cycles, rowbuffer_change[req.pid]);
//                         Console.WriteLine ("nvm yes");
                   }
//                   else
//                         Console.WriteLine ("nvm no");
                }

                
                public static void DramResetRowBufferChange (Req req)
                {
                    core_prev_rowid[req.pid, NVM_bank_num + (req.addr.cid * Config.mem2.rank_max + req.addr.rid) * Dram_BANK_MAX + req.addr.bid] = 0;      
                }

                public static void NVMResetRowBufferChange (Req req)
                {
                    core_prev_rowid[req.pid, (req.addr.cid * Config.mem.rank_max + req.addr.rid) * NVM_BANK_MAX + req.addr.bid] = 0;
                }

                public static void DramCoreReqNumInc (Req req)
                {
                   core_req_num[req.pid, NVM_bank_num + (req.addr.cid * Config.mem2.rank_max + req.addr.rid) * Dram_BANK_MAX + req.addr.bid] += 1;
 /*                  if (req.type == ReqType.RD)
                   {
                       core_rd_req_num[req.pid, NVM_bank_num + (req.addr.cid * Config.mem2.rank_max + req.addr.rid) * Dram_BANK_MAX + req.addr.bid] += 1;
                       if (core_rd_req_num[req.pid, NVM_bank_num + (req.addr.cid * Config.mem2.rank_max + req.addr.rid) * Dram_BANK_MAX + req.addr.bid] == 1)
                           MLP_rd_bank_num[req.pid] += 1;
                   }
                   else
                   {
                       core_wr_req_num[req.pid, NVM_bank_num + (req.addr.cid * Config.mem2.rank_max + req.addr.rid) * Dram_BANK_MAX + req.addr.bid] += 1;
                       if (core_wr_req_num[req.pid, NVM_bank_num + (req.addr.cid * Config.mem2.rank_max + req.addr.rid) * Dram_BANK_MAX + req.addr.bid] == 1)
                           MLP_wr_bank_num[req.pid] += 1;
                   }*/
                }
                
                
                public static void DramCoreReqNumDec (Req req)
                {
                   core_req_num[req.pid, NVM_bank_num + (req.addr.cid * Config.mem2.rank_max + req.addr.rid) * Dram_BANK_MAX + req.addr.bid] -= 1;
/*                   if (req.type == ReqType.RD)
                   {
                       core_rd_req_num[req.pid, NVM_bank_num + (req.addr.cid * Config.mem2.rank_max + req.addr.rid) * Dram_BANK_MAX + req.addr.bid] -= 1;
                       if (core_rd_req_num[req.pid, NVM_bank_num + (req.addr.cid * Config.mem2.rank_max + req.addr.rid) * Dram_BANK_MAX + req.addr.bid] == 0)
                           MLP_rd_bank_num[req.pid] -= 1;
                   } 
                   else
                   {
                       core_wr_req_num[req.pid, NVM_bank_num + (req.addr.cid * Config.mem2.rank_max + req.addr.rid) * Dram_BANK_MAX + req.addr.bid] -= 1;
                       if (core_wr_req_num[req.pid, NVM_bank_num + (req.addr.cid * Config.mem2.rank_max + req.addr.rid) * Dram_BANK_MAX + req.addr.bid] == 0)
                           MLP_wr_bank_num[req.pid] -= 1; 
                   }*/
                }
       
                public static void NVMCoreReqNumInc (Req req)
                {
                   core_req_num[req.pid, (req.addr.cid * Config.mem.rank_max + req.addr.rid) * NVM_BANK_MAX + req.addr.bid] += 1;
/*                   if (req.type == ReqType.RD)
                   {
                      core_rd_req_num[req.pid, (req.addr.cid * Config.mem.rank_max + req.addr.rid) * NVM_BANK_MAX + req.addr.bid] += 1;
                      if (core_rd_req_num[req.pid, (req.addr.cid * Config.mem.rank_max + req.addr.rid) * NVM_BANK_MAX + req.addr.bid] == 1)
                          MLP_rd_bank_num[req.pid] += 1;
                   }
                   else
                   {
                      core_wr_req_num[req.pid, (req.addr.cid * Config.mem.rank_max + req.addr.rid) * NVM_BANK_MAX + req.addr.bid] += 1;
                      if (core_wr_req_num[req.pid, (req.addr.cid * Config.mem.rank_max + req.addr.rid) * NVM_BANK_MAX + req.addr.bid] == 1) 
                          MLP_wr_bank_num[req.pid] += 1;
                   }*/
                }

                
                
                public static void NVMCoreReqNumDec (Req req)
                {
                   core_req_num[req.pid, (req.addr.cid * Config.mem.rank_max + req.addr.rid) * NVM_BANK_MAX + req.addr.bid] -= 1;
/*                   if (req.type == ReqType.RD)
                   {
                       core_rd_req_num[req.pid, (req.addr.cid * Config.mem.rank_max + req.addr.rid) * NVM_BANK_MAX + req.addr.bid] -= 1;
                       if (core_rd_req_num[req.pid, (req.addr.cid * Config.mem.rank_max + req.addr.rid) * NVM_BANK_MAX + req.addr.bid] == 0)
                           MLP_rd_bank_num[req.pid] -= 1; 
                   }
                   else
                   {
                       core_wr_req_num[req.pid, (req.addr.cid * Config.mem.rank_max + req.addr.rid) * NVM_BANK_MAX + req.addr.bid] -= 1;
                       if (core_wr_req_num[req.pid, (req.addr.cid * Config.mem.rank_max + req.addr.rid) * NVM_BANK_MAX + req.addr.bid] == 0)
                           MLP_wr_bank_num[req.pid] -= 1;
                   } */
                }

                   
                public static void DramBankPidEnUpdate (Req req)
                {
                   bank_req_pid[NVM_bank_num + (req.addr.cid * Config.mem2.rank_max + req.addr.rid) * Dram_BANK_MAX + req.addr.bid] = req.pid;
                }

                public static void DramBankPidDeUpdate (Req req)
                {
                   bank_req_pid[NVM_bank_num + (req.addr.cid * Config.mem2.rank_max + req.addr.rid) * Dram_BANK_MAX + req.addr.bid] = Config.N;
                }

                 public static void NVMBankPidEnUpdate (Req req)
                {
                   bank_req_pid[(req.addr.cid * Config.mem.rank_max + req.addr.rid) * NVM_BANK_MAX + req.addr.bid] = req.pid;
                }

                public static void NVMBankPidDeUpdate (Req req)
                {
                   bank_req_pid[(req.addr.cid * Config.mem.rank_max + req.addr.rid) * NVM_BANK_MAX + req.addr.bid] = Config.N;
                }

              
/*                public static void DramServiceTimeUpdate(Req req)
                {
                     Dbg.Assert(req.hit > 0);

                     if (req.hit == 1)
                     {
                          if (req.type == ReqType.RD)
                          {
                             Dram_Q_rd_hit_Acc += (double) (req.ts_departure - req.ts_arrival);
                             Dram_Q_rd_hit_times += 1;
                          }
                          else
                          {
                             Dram_Q_wr_hit_Acc += (double) (req.ts_departure - req.ts_arrival);
                             Dram_Q_wr_hit_times += 1;
                          }
                     }
                     else
                     {
                          if (req.type == ReqType.RD)
                          {
                             Dram_Q_rd_miss_Acc += (double) (req.ts_departure - req.ts_arrival);
                             Dram_Q_rd_miss_times += 1;
                          }
                          else
                          {
                             Dram_Q_wr_miss_Acc += (double) (req.ts_departure - req.ts_arrival);
                             Dram_Q_wr_miss_times += 1;
                          }
                     }
                 }
                 
                public static void NVMServiceTimeUpdate(Req req)
                {
                     Dbg.Assert(req.hit > 0);
 
                     if (req.hit == 1)
                     {
                          if (req.type == ReqType.RD)
                          {
                             NVM_Q_rd_hit_Acc += (double) (req.ts_departure - req.ts_arrival);
                             NVM_Q_rd_hit_times += 1;
                          }
                          else
                          {
                             NVM_Q_wr_hit_Acc += (double) (req.ts_departure - req.ts_arrival);
                             NVM_Q_wr_hit_times += 1;
                          }
                     }
                     else
                     {
                          if (req.type == ReqType.RD)
                          {
                             NVM_Q_rd_miss_Acc += (double) (req.ts_departure - req.ts_arrival);
                             NVM_Q_rd_miss_times += 1;
                          }
                          else
                          {
                             NVM_Q_wr_miss_Acc += (double) (req.ts_departure - req.ts_arrival);
                             NVM_Q_wr_miss_times += 1;
                          }
                       } 
                 }
*/
                public static void mem_num_inc (Req req)
                {
                       if (req.type == ReqType.RD)
                           mem_read_num[req.pid] += 1;
                       else
                           mem_write_num[req.pid] += 1;
                }
                    
                public static void mem_num_dec (Req req)
                {
                       if (req.type == ReqType.RD)
                           mem_read_num[req.pid] -= 1;
                       else
                           mem_write_num[req.pid] -= 1;
                }

                public static void MLP_cal (ref List<Req> q)
                {
                       for (int i = 0; i < q.Count; i++)
                       {
                           if (q[i].migrated_request)
                              continue;
                   	   if (q[i].type == ReqType.RD)
                      	   {
                          	 q[i].MLPAcc += 1.0 / (double) mem_read_num[q[i].pid];
                       	         q[i].MLPTimes += 1.0;
 //                                Console.WriteLine ("mem_read_num: {0}", mem_read_num[q[i].pid]);
                      	   }
                      	   else
                      	   {
                          	 q[i].MLPAcc += 1.0 / (double) mem_write_num[q[i].pid];
                                 q[i].MLPTimes += 1.0;
 //                                           Console.WriteLine ("mem_write_num: {0}", mem_write_num[q[i].pid]);
                      	   }
                       }
                }
              
                public static void read_MLP_cal (ref List<Req> q)
                {
                       for (int i = 0; i < q.Count; i++)
                       {
                           if (q[i].migrated_request)
                              continue;
		           q[i].MLPAcc += 1.0 / (double) mem_read_num[q[i].pid];
                           q[i].MLPTimes += 1.0;
                       }
                }


                public static void write_MLP_cal (ref List<Req> q)
                {
                       for (int i = 0; i < q.Count; i++)
                       {
                           if (q[i].migrated_request)
                              continue;
		           q[i].MLPAcc += 1.0 / (double) mem_write_num[q[i].pid];
                           q[i].MLPTimes += 1.0;
                       }
                }

                 public static void Dram_bus_conflict_set (int pid)
                 {
                     Dram_bus_conflict[pid] = true;
                 }
                 
                 public static void NVM_bus_conflict_set (int pid)
                 {
                     NVM_bus_conflict[pid] = true;
                 }
                 
                  public static void Dram_bus_conflict_reset (int pid)
                  {
                      Dram_bus_conflict[pid] = false;
                  }
     
                  public static void NVM_bus_conflict_reset (int pid)
                  {
                      NVM_bus_conflict[pid] = false;
                  }
        }
}
