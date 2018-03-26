using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MemMap
{
    public class DecoupledRDEmptyIdleServeN : MemWBMode
    {
        public uint serve_max;
        public uint[] serve_cnt;

        private bool rmpkis_valid = false;
        private double[] rmpkis;
        private bool[] is_low_rmpki;
        private uint low_rmpki_cnt;

        public DecoupledRDEmptyIdleServeN(MemCtrl[] mctrls)
            : base(mctrls)
        {
            serve_max = Config.mctrl.serve_max;
            serve_cnt = new uint[cmax];

            rmpkis = new double[Config.N];
            is_low_rmpki = new bool[Config.N];
        }

        public override void issued_write_cmd(Cmd cmd)
        {
            Dbg.Assert(cmd.type == Cmd.TypeEnum.WRITE);
            uint cid = cmd.addr.cid;
            serve_cnt[cid]++;
        }

        private void calculate_rmpki()
        {
            low_rmpki_cnt = 0;

            for (int pid = 0; pid < Config.N; pid++) {
                ulong read_cnt = Stat.procs[pid].read_req.Count;
                ulong inst_cnt = Stat.procs[pid].ipc.Count;
                double rmpki = 1000 * ((double)read_cnt) / inst_cnt;

                rmpkis[pid] = rmpki;
                is_low_rmpki[pid] = (rmpki < Config.mctrl.low_rmpki_threshold);
                if (is_low_rmpki[pid])
                    low_rmpki_cnt++;
            }

            if (!rmpkis_valid)
                rmpkis_valid = true;
        }

        private bool is_readq_idle(uint cid)
        {
            if (!rmpkis_valid)
                return false;

            if (low_rmpki_cnt == 0)
                return false;

            MemCtrl mctrl = mctrls[cid];
            for (int pid = 0; pid < Config.N; pid++) {
                if (!is_low_rmpki[pid])
                    continue;

                if (mctrl.rload_per_proc[pid] > 0)
                    return false;
            }

            return true;
        }

        public override void tick(uint cid)
        {
            if (cid != 0) return;

            cycles++;

            //calculate rmpki;
            if (cycles % 10000 == 0) {
                calculate_rmpki();
            }

            //check for end of wb_mode
            for (uint i = 0; i < cmax; i++) {
                if (!wb_mode[i])
                    continue;

                if (!is_writeq_empty(i) && serve_cnt[i] < serve_max)
                    continue;

                wb_mode[i] = false;
            }

            //check for start of wb_mode
            for (uint i = 0; i < cmax; i++) {
                if (wb_mode[i])
                    continue;

                if (!is_readq_empty(i) && !is_readq_idle(i))
                    continue;

                serve_cnt[i] = 0;
                wb_mode[i] = true;
            }
        }
    }
}
