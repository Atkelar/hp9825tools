using System;
using System.Security.Cryptography;
using System.Text;

namespace HP9825CPU
{
    public static class KnwonHPLCallbacks
    {
        public static void AdvancedProgrammingCompileCallback(int infoFromGuide, byte opCode, HPLCompilerTables.CompileCallbackContext ctx)
        {
            if ((infoFromGuide | opCode) != 256)
            {
                ctx.SetFromAuxilaryTable("def", opCode);
            }
            else
            {
                // adv. prog. rom: we got a "'" label for a function...
                int len = ctx.GetNextByte() ?? throw new InvalidOperationException("Unexpeced end of byte stream in advanced programming ROM");
                StringBuilder sb = new StringBuilder();
                for(int i = 0;i<len;i++)
                    sb.Append(HP9825Charset.FromHP9825(ctx.GetNextByte() ?? throw new InvalidOperationException("EOF in function label!")));
                ctx.SetEntry(opCode, sb.ToString());
            }
        }
        public static void StringsReverseCallback(int infoFromGuide, byte byteCode, HPLCompilerTables.ReverseCallbackContext ctx)
        {
            if (byteCode <=64)
                ctx.SetResultFromAux("def", byteCode-1);
            else
            {
                if (byteCode >= 97)
                {
                    byteCode-=32;
                    ctx.SetResultEntry(MnemonicClass.UnaryOperator, 15, $"{HP9825Charset.FromHP9825((byte)byteCode)}$");
                    // rev. tab. ent; 171002 - string length 2, ...
                }
                else
                {
                    ctx.SetResultEntry(MnemonicClass.Operand, 0, $"{HP9825Charset.FromHP9825((byte)byteCode)}$");
                    // rev. tab. ent: 402 - string length 2, ...
                }
            }            
        }
    }
}