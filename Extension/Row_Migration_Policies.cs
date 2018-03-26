using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MemMap
{
    public class Row_Migration_Policies
	{ 
		//public Req req;
		public static int Cycles=0;
		//Interval: # of cycles between each migration
		public static int Interval=1000000;
//                public static int Interval1=50000;
                public static Req target_req;
                public static bool target = false;
               
                public int tick()
		{//Clock in RBLA, make decision when Cycles is multiples of Interval
		 //If Migration 
                    
                        if (Cycles == 0)
                        {
                           if (Config.proc.cache_insertion_policy == "RBLA")
                               RBLA.initialize();
			   else if (Config.proc.cache_insertion_policy == "PFA")
			       PFA.initialize();
                        }
                        else
                        {
		            if(Cycles % Interval==0)
		               MigrationDecision();
                            if (target)
                            {
                               target = false;
                               if (Config.proc.cache_insertion_policy == "RBLA")
                                  RBLA.tick();
			       else  if (Config.proc.cache_insertion_policy == "PFA" && Cycles >= Interval)
			          PFA.tick();
                            }
                            if (Cycles % Interval ==0)
                               RowStat.ClearPerInterval();
                        }
			Migration.tick();
			Cycles++;
			return 0;
		}

		private void MigrationDecision()
		{//Determine which row should be migrated and send row numbers to Migration
			Migration.migrationlist.Clear();
                        Migration.migrationlistPID.Clear();
			RowStat.UpdateAccessPerInterval();
			switch (Config.proc.cache_insertion_policy)
			{
			case "RBLA":
				RBLA.decision();
			break;
			case "PFA":
			if(Cycles == Interval)
			  	PFA.assignE0();
                        else
			        PFA.decision();
			break;
			default:
				Console.WriteLine("Row Migration Policy Error");
			break;
			}
		}

	}
}
