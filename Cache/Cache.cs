using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MemMap
{
/*    public class TagEntry
    {
        public ulong addr;
        public long access;
        public int pid;
        public bool valid;
        public bool dirty;

        public TagEntry()
        {
            valid = false;
            addr = 0x0;
            access = 0;
            dirty = false;
            pid = -1;
        }
    } */


    public struct TagEntry
    {
        public ulong addr;
        public long access;
        public int pid;
        public bool valid;
//        public bool dirty;
        public bool[] block_valid;
        public bool[] block_dirty;
    }

    public abstract class Cache
    {
        public string ins;
        private Random random;

        public Cache() {
            this.ins = Config.proc.cache_insertion_policy;
            this.random = new Random(0);
        }

        public bool meta_insert(Req req) {
            if (ins == "All")
            {
                ulong rowkey = req.paddr >> Config.proc.page_size_bits;
                if ((!Sim.caches[Sim.get_cache(req.pid)].is_cached(req))&&(!Migration.migrationlist.Contains(rowkey)))
                {
                    Migration.migrationlist.Add(rowkey);
                    Migration.migrationlistPID.Add(req.pid);
                }
                return true;
  //              return insert(req);
            }
            else if ((ins == "RBLA") || (ins == "RBLAMLP") || (ins == "PFA")) {
		return false;
	    }	
            else if (ins == "ACTS"/* && Sim.is_critical_thread(req.pid)*/) {
                return insert(req);
            }
            else if (ins == "NOT_GROUP" && req.pid < Config.group_boundary) {
                return insert(req);
            }
            else if (ins == "PROB") {
                if (Config.proc.cache_insert_prob > random.NextDouble())
                    return insert(req);
                return false;
            }
            else if (ins == "PROB_RBHR") {
                double rbhr = ((double)Stat.procs[req.pid].row_hit_rate_read.Count)
                        / Stat.procs[req.pid].row_hit_rate_read.Sample;
                if (random.NextDouble() > rbhr)
                    return insert(req);
                return false;
            }
            else if (ins == "NOT_GROUP_PROB_RBHR") {
                double rbhr = ((double)Stat.procs[req.pid].row_hit_rate_read.Count)
                        / Stat.procs[req.pid].row_hit_rate_read.Sample;
                if ((req.pid < Config.group_boundary)
                        || (req.pid >= Config.group_boundary && random.NextDouble() > rbhr))
                    return insert(req);
                return false;
            }
            else if (ins == "NOT_GROUP_PROB_PROB_RBHR") {
                double rbhr = ((double)Stat.procs[req.pid].row_hit_rate_read.Count)
                        / Stat.procs[req.pid].row_hit_rate_read.Sample;
                if ((req.pid < Config.group_boundary)
                        || (req.pid >= Config.group_boundary && random.NextDouble() < Config.proc.cache_insert_prob && random.NextDouble() > rbhr))
                    return insert(req);
                return false;
            }

            return false;
        }

        public abstract bool insert(Req req);
        public abstract bool is_cached(Req req);
        public abstract void promote(Req req);
        public abstract bool displace(Req req);
        public abstract bool service(Req req);
        public abstract void tick();
    }
}
