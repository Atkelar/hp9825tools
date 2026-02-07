using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using CommandLineUtils;
using HP9825CPU;

namespace HP9825Assembler
{

    class Program
    {
        static async System.Threading.Tasks.Task<int> Main(string[] args)
        {
            using(var host = new SingleCommandHost<AssemberCommand>("hp9825asm", true))
            {
                host.SetupBanner<Program>("9825 CPU Assembler", "Atkelar", 2026);
                return await host.Run(args);
            }
        }
    }
}