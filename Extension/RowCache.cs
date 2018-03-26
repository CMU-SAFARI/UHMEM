using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;

namespace MemMap
{
        public class RowCache
        {
		public class RCache
                {
			private List<ulong>[] keys;
			private List<int>[] values;
		
			private int sets;
			private int ways;
			
			public RCache()
			{
				sets = 64;
				ways = 32;
				keys = new List<ulong>[sets];
				values = new List<int>[sets];
		
				for(int i=0; i<sets; i++)
				{
					keys[i] = new List<ulong>();
					values[i] = new List<int>();
				}
			}
	
			public void insert(ulong key)
			{
				int setNum = (int)key % sets;
				for(int i=0; i<values[setNum].Count; i++)
				{
					values[setNum][i]++;
				} 
				
				if (!keys[setNum].Contains(key))
				{	
					keys[setNum].Add(key);
					values[setNum].Add(0);
				}
				else
				{
					values[setNum][keys[setNum].IndexOf(key)] = 0;
				}
			// Evict:	
				if (keys[setNum].Count > ways)
				{
					int index = values[setNum].IndexOf(values[setNum].Max());
					ulong rmkey = keys[setNum][index];
					evict(rmkey);                              
				} 
				
			}

			public void evict(ulong key)
			{
				int setNum = (int)key % sets;
				
				int index = keys[setNum].IndexOf(key);
				if (index == -1)
				{
		//			Console.WriteLine("index is -1");
					return;
				}	
			
                                keys[setNum].RemoveAt(index);
                                values[setNum].RemoveAt(index);
                                        
                                if (RowStat.NVMDict.ContainsKey(key))
                                	RowStat.NVMDict.Remove(key);
                                if (RowStat.NVMLookUp.Contains(key))
                                	RowStat.NVMLookUp.Remove(key);
			}

			public void clear()
			{
				for (int i = 0; i < sets; i++)
				{
					keys[i].Clear();
					values[i].Clear();
				}
			}		
		}
		
		public static RCache NVMCache = new RCache();
	}	
}
