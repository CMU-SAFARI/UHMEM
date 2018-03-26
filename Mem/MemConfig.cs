using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MemMap
{
    public class MemConfig : ConfigGroup
    {
        //ddr3
       // public DDR3DRAM.DDR3Enum ddr3_type = DDR3DRAM.DDR3Enum.DDR3_2Gb_x8_1066_8_8_8;
       
       //yang
        public DDR3DRAM.DDR3Enum ddr3_type = DDR3DRAM.DDR3Enum.PCM_2Gb_x8_1066_36_8_8;
///////////////////////////////////////////////////////////////////////////////////////////////////////////
        public uint tWR = 0;
        public uint tWTR = 0;
        public uint tCCD = 0;
        public uint tBL = 0;

        //mapping
//        public MemMap.MapEnum map_type = MemMap.MapEnum.ROW_RANK_BANK_CHAN_COL;
        public MemMap.MapEnum map_type = MemMap.MapEnum.ROW_COL_RANK_BANK_CHAN;
        public uint col_per_subrow = 0;

        //scale time
        public uint clock_factor = 5;

        //physical configuration
        public uint channel_max;
        public uint rank_max;

        //isolation
        public int mctrl_num = 1;
        public bool bank_spreading = false;
        public int chan_per_mctrl_num = 1;

        protected override bool set_special_param(string param, string val)
        {
            return false;
        }

        public override void finalize() { }
    }
}
