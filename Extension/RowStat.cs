using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;

namespace MemMap
{
   	 public class RowStat
       	{

		public  struct AccessInfo 
	       	{
         	       public ulong ReadMiss;           //row buffer read miss 
         	       public ulong WriteMiss;		//row buffer write miss
         	       public ulong ReadHit;		//row buffer read hit
                       public ulong WriteHit;           //row buffer write hit
         	       public ulong Access;		//total access
                       public double ReadMLPnum;        //MLP number for Read
                       public double WriteMLPnum;       //MLP number for Write
                       public double ReadMLPAcc;
                       public double ReadMLPTimes;
                       public double WriteMLPAcc;
                       public double WriteMLPTimes;  
                       public int pid;                  //process that the row belongs to
                       public bool addlist;             //whether the page has been added to the migration list
	        }

                public static ulong page_size = (ulong)Config.proc.page_size;
                public static int page_bits = Config.proc.page_block_diff_bits;
		
		public static Dictionary<ulong,AccessInfo> DramDict = CreateDict();
		public static Dictionary<ulong,AccessInfo> NVMDict = CreateDict();
		
		public static List<ulong> DramLookUp = new List<ulong>(); 
		public static List<ulong> NVMLookUp = new List<ulong>();		

		public static ulong DramReadMissPerInterval = 0;
		public static ulong DramWriteMissPerInterval = 0;
                public static ulong DramReadHitPerInterval = 0; 

	        public static ulong NVMReadMissPerInterval = 0;
                public static ulong NVMWriteMissPerInterval = 0;
                public static ulong NVMReadHitPerInterval = 0; 	
               
                public static double DramWeightedReadMissPerInterval = 0;
                public static double DramWeightedWriteMissPerInterval = 0;

                public static double NVMWeightedReadMissPerInterval = 0;
                public static double NVMWeightedWriteMissPerInterval = 0;

                public static double DramWeightedReadResponseTime = 0;
                public static double NVMWeightedReadResponseTime = 0;

		//new:      
		public static double Interval = Row_Migration_Policies.Interval;      //interval length 
		public static double[] t_excess = new double[100];                    // Texcess
//		public static double[] app_util_Dram = new double[100];  // Total utility of an app
//		public static double[] app_util_NVM = new double[100];

//              migration_cost = ddr3_temp1.COL_MAX * (ulong)(1<<Config.proc.block_size_bits) / ddr3_temp1.CHANNEL_WIDTH * ddr3_temp1.timing.tBL;
//              public static double migration_cost = (double) (8*1024/64 * (ulong)(1<<Config.proc.block_size_bits) / 64 * 4 * 5);

 		public static double Dram_Q_rd_miss_init;           //Dram initial queuing time 
		public static double Dram_Q_wr_miss_init;
                public static double Dram_Q_rd_hit_init;
                public static double Dram_Q_wr_hit_init;
		public static double NVM_Q_rd_miss_init;            //NVM initial queuing time
		public static double NVM_Q_wr_miss_init;
                public static double NVM_Q_rd_hit_init;
                public static double NVM_Q_wr_hit_init;

                public static double Dram_Q_rd_miss;           //Dram queuing time 
                public static double Dram_Q_wr_miss;
                public static double Dram_Q_rd_hit;
                public static double Dram_Q_wr_hit;
                public static double NVM_Q_rd_miss;            //NVM queuing time
                public static double NVM_Q_wr_miss;
                public static double NVM_Q_rd_hit;
                public static double NVM_Q_wr_hit;


                public static double t_rd_miss_diff;
                public static double t_wr_miss_diff;
                public static double t_rd_hit_diff;
                public static double t_wr_hit_diff; 	

        	static Dictionary<ulong,AccessInfo> CreateDict()
		{//Create Dictionary
			Dictionary<ulong,AccessInfo> dict = new Dictionary<ulong, AccessInfo>();
			return dict;
		}

		public static ulong KeyGen(Req req)
		{//Generate a key
 
			ulong key = req.paddr / page_size;
			return key;
                        
		}		

                public static void UpdateDict(Dictionary<ulong,AccessInfo> dict, Req req, MemCtrl2 mctrl)
                {//Need to update NVM dictionary when a request comes out of L2
		 //Need to update both dictionary when a request 
		        AccessInfo temp;
                        if(!dict.ContainsKey(KeyGen(req)))
                        {//If dictionary does not have this record
				temp.ReadMiss = 0;
	                        temp.WriteMiss = 0;
                                temp.ReadHit = 0;
                                temp.WriteHit = 0;
                	        temp.Access = 1;
                                temp.ReadMLPnum = 1;
                                temp.WriteMLPnum = 1;
                                temp.ReadMLPAcc = 0;
                                temp.ReadMLPTimes = 0;
                                temp.WriteMLPAcc = 0;
                                temp.WriteMLPTimes = 0;
                                temp.pid = req.pid;
                                temp.addlist = false;

				//not in the dictionary means a cold miss
				if (req.type == ReqType.RD)
					temp.ReadMiss++;
				else
					temp.WriteMiss++;

                 	      	dict.Add(KeyGen(req), temp);
				DramLookUp.Add(KeyGen(req));
                        }
			else
			{//If dictionary has this record
				temp = dict[KeyGen(req)];
				temp.Access++;
				RowHitFinder2 rhf = new RowHitFinder2(mctrl);
				if (rhf.is_row_hit(req))        // a hit
                                {
                                     if (req.type == ReqType.RD)                                
				        temp.ReadHit++;
                                     else
                                        temp.WriteHit++;
                                }
                                else
                                {
				     if (req.type == ReqType.RD)
					temp.ReadMiss++;
                                     else
				        temp.WriteMiss++;
                                }
                                dict[KeyGen(req)] = temp;  						
			}
		}



                public static void UpdateDict(Dictionary<ulong,AccessInfo> dict, Req req, MemCtrl mctrl)
                {//Need to update NVM dictionary when a request comes out of L2
		 //Need to update both dictionary when a request 
		        AccessInfo temp;
                        if(!dict.ContainsKey(KeyGen(req)))
                        {//If dictionary does not have this record
				temp.ReadMiss = 0;
	                        temp.WriteMiss = 0;
        	                temp.ReadHit = 0;
                                temp.WriteHit = 0;
                	        temp.Access = 1;
                                temp.ReadMLPnum = 1;
                                temp.WriteMLPnum= 1;
                                temp.ReadMLPAcc = 0;
                                temp.ReadMLPTimes = 0;
                                temp.WriteMLPAcc = 0;
                                temp.WriteMLPTimes = 0;
                                temp.pid = req.pid;
                                temp.addlist = false;

				//not in the dictionary means a cold miss
				if (req.type == ReqType.RD)
					temp.ReadMiss++;
				else
					temp.WriteMiss++;

                 	      	dict.Add(KeyGen(req), temp);
				NVMLookUp.Add(KeyGen(req));
                        }
			else
			{//If dictionary has this record
				temp = dict[KeyGen(req)];
				temp.Access++;

				RowHitFinder rhf = new RowHitFinder(mctrl);
	                        if (rhf.is_row_hit(req))          // a hit
                                {
                                     if (req.type == ReqType.RD)                                
				        temp.ReadHit++;
                                     else
                                        temp.WriteHit++;
                                }
                                else
                                {
				     if (req.type == ReqType.RD)
					temp.ReadMiss++;
                                     else
				        temp.WriteMiss++;
                                }
				dict[KeyGen(req)] = temp;  						
			}
                        RowCache.NVMCache.insert(KeyGen(req));
		}

                public static void UpdateMLP(Dictionary<ulong,AccessInfo> dict, Req req)
                {
		        AccessInfo temp;

                        if (!dict.ContainsKey(KeyGen(req)))
                           return;
                        
                        temp = dict[KeyGen(req)];
                        if (req.type == ReqType.RD)
                        {
                             temp.ReadMLPAcc += req.MLPAcc;
                             temp.ReadMLPTimes += req.MLPTimes;
                             temp.ReadMLPnum = temp.ReadMLPAcc / temp.ReadMLPTimes;
                        }
                        else
                        {   
                             temp.WriteMLPAcc += req.MLPAcc;
                             temp.WriteMLPTimes += req.MLPTimes;
                             temp.WriteMLPnum = temp.WriteMLPAcc / temp.WriteMLPTimes;
                        }

                        dict[KeyGen(req)] = temp;
		}

		public static void UpdateAccessPerInterval()
		{//Calculate all values when cycles=multiples of interval	
			AccessInfo temp;
			DramReadMissPerInterval = 0;
			DramWriteMissPerInterval = 0;
                        DramReadHitPerInterval = 0;
                        NVMReadMissPerInterval = 0;
                        NVMWriteMissPerInterval = 0;
                        NVMReadHitPerInterval = 0;
                        DramWeightedReadMissPerInterval = 0;
			DramWeightedWriteMissPerInterval = 0;
                        NVMWeightedReadMissPerInterval = 0;
                        NVMWeightedWriteMissPerInterval = 0;
                        DramWeightedReadResponseTime = 0;
                        NVMWeightedReadResponseTime = 0;

                        Dram_Q_rd_miss_init = Measurement.Dram_Q_rd_miss_init;
                        Dram_Q_wr_miss_init = Measurement.Dram_Q_wr_miss_init;
                        Dram_Q_rd_hit_init = Measurement.Dram_Q_rd_hit_init;
                        Dram_Q_wr_hit_init = Measurement.Dram_Q_wr_hit_init;
                        NVM_Q_rd_miss_init = Measurement.NVM_Q_rd_miss_init;
                        NVM_Q_wr_miss_init = Measurement.NVM_Q_wr_miss_init;
                        NVM_Q_rd_hit_init = Measurement.NVM_Q_rd_hit_init;
                        NVM_Q_wr_hit_init = Measurement.NVM_Q_wr_hit_init;
                      
                        Dram_Q_rd_miss = Measurement.Dram_Q_rd_miss;
                        Dram_Q_wr_miss = Measurement.Dram_Q_wr_miss;
                        Dram_Q_rd_hit = Measurement.Dram_Q_rd_hit;
                        Dram_Q_wr_hit = Measurement.Dram_Q_wr_hit;
                        NVM_Q_rd_miss = Measurement.NVM_Q_rd_miss;
                        NVM_Q_wr_miss = Measurement.NVM_Q_wr_miss;
                        NVM_Q_rd_hit = Measurement.NVM_Q_rd_hit;
                        NVM_Q_wr_hit = Measurement.NVM_Q_wr_hit;

                        for (int i = 0; i < Config.N; i++)
                          t_excess[i] = Measurement.t_excess[i];
		
			t_rd_miss_diff = NVM_Q_rd_miss_init - Dram_Q_rd_miss_init;
			t_wr_miss_diff = NVM_Q_wr_miss_init - Dram_Q_wr_miss_init;
                        t_rd_hit_diff = NVM_Q_rd_hit_init - Dram_Q_rd_hit_init;
                        t_wr_hit_diff = NVM_Q_wr_hit_init - Dram_Q_wr_hit_init;
                         
			foreach (ulong rowkey in NVMLookUp)
			{
                		temp = RowStat.NVMDict[rowkey];
                                NVMReadMissPerInterval += temp.ReadMiss;
                                NVMWriteMissPerInterval += temp.WriteMiss;
                                NVMReadHitPerInterval += temp.ReadHit;
                                NVMWeightedReadMissPerInterval += (double) temp.ReadMiss * temp.ReadMLPnum;
                                NVMWeightedWriteMissPerInterval += (double) temp.WriteMiss * temp.WriteMLPnum;
                                NVMWeightedReadResponseTime += (double) (temp.ReadMiss * NVM_Q_rd_miss_init + temp.ReadHit * NVM_Q_rd_hit_init) * temp.ReadMLPnum;
                        }
			
			foreach (ulong rowkey in DramLookUp)
                        {
                                temp = RowStat.DramDict[rowkey];
		          	DramReadMissPerInterval += temp.ReadMiss;
                                DramWriteMissPerInterval += temp.WriteMiss;
                                DramReadHitPerInterval += temp.ReadHit;
                                DramWeightedReadMissPerInterval += (double)temp.ReadMiss * temp.ReadMLPnum;
                                DramWeightedWriteMissPerInterval += (double)temp.WriteMiss * temp.WriteMLPnum;
                                DramWeightedReadResponseTime += (double) (temp.ReadMiss * Dram_Q_rd_miss_init + temp.ReadHit * Dram_Q_rd_hit_init) * temp.ReadMLPnum;
            		}
					
		}                
        
                public static void ClearPerInterval()
                {
                       NVMDict.Clear();
                       DramDict.Clear();
                       NVMLookUp.Clear();
                       DramLookUp.Clear();
//                     Array.Clear(app_util_Dram, 0 , 99);
//	               Array.Clear(app_util_NVM, 0 , 99);
                       RowCache.NVMCache.clear();
                }
                    
        }
}
