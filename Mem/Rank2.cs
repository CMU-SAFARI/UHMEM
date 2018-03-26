using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MemMap
{
    public class Rank2
    {
        public uint cid;
        public uint rid;

        //state
        public MemCtrl2 mc;

        //components
        public uint bmax;
        public Bank2[] banks;

        //timestamps
        long ts_act = -1;
        long ts_pre = -1;
        public long ts_read = -1;
        public long ts_write = -1;

        //constructor
        public Rank2(MemCtrl2 mc, Channel2 chan, uint rid, uint bmax)
        {
            this.cid = mc.cid;
            this.rid = rid;

            this.mc = mc;
            this.bmax = bmax;
            banks = new Bank2[bmax];
            for (uint i = 0; i < banks.Length; i++) {
                banks[i] = new Bank2(mc, this, i);
            }
        }

        //action methods
        public void activate(uint bid, ulong row_idx) {
            ts_act = mc.cycles;
            banks[bid].activate(row_idx);
        }

        public void precharge(uint bid) {
            ts_pre = mc.cycles;
            banks[bid].precharge();
        }

        public void read(uint bid) {
            ts_read = mc.cycles;
            banks[bid].read();
        }

        public void write(uint bid) {
            ts_write = mc.cycles;
            banks[bid].write();
        }

        //test methods
        public bool can_activate(uint bid) {
            if (!banks[bid].can_activate()) 
                return false;
            if (ts_act != -1 && mc.cycles - ts_act < mc.timing.tRRD) 
                return false;

            return true;
        }

        public bool can_precharge(uint bid) {
            return banks[bid].can_precharge();
        }

        public bool can_read(uint bid) {
            if (!banks[bid].can_read()) 
                return false;
            if (ts_read != -1 && mc.cycles - ts_read < mc.timing.tCCD) 
                return false;
            if (ts_write != -1 && mc.cycles - ts_write < mc.timing.tCWL + mc.timing.tBL + mc.timing.tWTR) 
                return false;

            return true;
        }

        public bool can_write(uint bid) {
            if (!banks[bid].can_write()) 
                return false;
            if (ts_read != -1 && mc.cycles - ts_read < mc.timing.tRTW) 
                return false;
            if (ts_write != -1 && mc.cycles - ts_write < mc.timing.tCCD) 
                return false;

            return true;
        }

        public void reset() {
            ts_act = -1;
            ts_pre = -1;
            ts_read = -1;
            ts_write = -1;

            foreach (Bank2 b in banks) {
                b.reset();
            }
        }
    }
}
