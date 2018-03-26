using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Globalization;


using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.GZip;

namespace MemMap
{
    public class Trace
    {
        public int pid;
        public bool finished;

        //trace file
        public string trace_fname;   //name of trace file
        public int line_num;
        public ulong total_cpu_inst_count;

        //reader
        Stream gzip_reader;         //gzip trace file reader

        //size of temporary buffer
        public const int BUF_MAX = 1000;    //buffer length for reading from trace files

        public Trace(int pid, string trace_fname)
        {
            this.pid = pid;
            total_cpu_inst_count = 0;
            foreach (string dir in Config.TraceDirs.Split(',', ' ')) {
                if (File.Exists(dir + "/" + trace_fname)) {
                    trace_fname = dir + "/" + trace_fname;
                }
            }

            //trace file
            Console.WriteLine(trace_fname);
            Dbg.Assert(File.Exists(trace_fname));
            this.trace_fname = trace_fname;

            //gzip_reader
            gzip_reader = new GZipInputStream(File.OpenRead(this.trace_fname));
        }

        private string read_gzip_trace()
        {
            byte[] single_buf = new byte[1];

            bool copied = StreamUtils.Copy(gzip_reader, null, single_buf);
            if (!copied) {
                return null;
            }

            byte[] buf = new byte[BUF_MAX];
            int n = 0;
            while (single_buf[0] != (byte)'\n') {
                buf[n++] = single_buf[0];
                copied = StreamUtils.Copy(gzip_reader, null, single_buf);
            }
            return Encoding.ASCII.GetString(buf, 0, n);
        }

        public void get_req(ref int cpu_inst_cnt, out Req rd_req, out Req wb_req)
        {
            string line = read_trace();

            Char[] delim = new Char[] { ' ' };
            string[] tokens = line.Split(delim);

            cpu_inst_cnt = int.Parse(tokens[0]);
            total_cpu_inst_count += (ulong) cpu_inst_cnt;
            ulong rd_addr = ulong.Parse(tokens[1]);
            rd_addr = rd_addr | (((ulong)pid) << 56);

            rd_req = RequestPool.depool();
     //       RequestPool.RD_Count++;
            rd_req.set(pid, ReqType.RD, rd_addr);

            if (!Config.proc.wb || tokens.Length == 2) {
                wb_req = null;
                return;
            }

            Dbg.Assert(tokens.Length == 3);
            ulong wb_addr = ulong.Parse(tokens[2]);
            wb_addr = wb_addr | (((ulong)pid) << 56);
            wb_req = RequestPool.depool();
            wb_req.set(pid, ReqType.WR, wb_addr);
//            Console.WriteLine("{0}",rd_req.paddr);
        }

        public string read_trace()
        {
            line_num++;
     
            if (total_cpu_inst_count > (Config.sim_inst_max + Config.warmup_inst_max) && Config.sim_type == Config.SIM_TYPE.INST)
            {
                gzip_reader.Close();
                gzip_reader = new GZipInputStream(File.OpenRead(this.trace_fname));
                total_cpu_inst_count = 0;
//                Console.WriteLine("open once");
//                Console.WriteLine(this.trace_fname);
            }

            string line = read_gzip_trace();
            if(line != null)
            {
 //               Console.WriteLine("{0}",line);
                return line;
            }
            //reached EOF; reopen trace file or open next trace file
            gzip_reader.Close();
            if ((Config.task_based == true && Config.sim_type != Config.SIM_TYPE.GROUPED && Sim.task_queue.Count > 0)
                    || (Config.task_based == true && Config.sim_type == Config.SIM_TYPE.GROUPED && Sim.task_queue.Count > 0 && pid >= Config.group_boundary && pid < Config.N)) {
                string trace_fname = Sim.task_queue.Dequeue();
                foreach (string dir in Config.TraceDirs.Split(',', ' ')) {
                    if (File.Exists(dir + "/" + trace_fname)) {
                        trace_fname = dir + "/" + trace_fname;
                    }
                }

                //trace file
                Dbg.Assert(File.Exists(trace_fname));
                this.trace_fname = trace_fname;
            } else {
                finished = true;
                  
            }
            gzip_reader = new GZipInputStream(File.OpenRead(this.trace_fname));
            line_num = 0;
            line = read_trace();
            Dbg.Assert(line != null);
            return line;
        }
    }
}
