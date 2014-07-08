using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PythonShell;

namespace Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            PythonShell.PythonShell i = new PythonShell.PythonShell();
            string stdout = i.Eval(@"print(""hello"");print(""world"")");
            Trace.Assert(stdout == "hello\r\nworld\r\n");

            stdout = i.Eval(@"print(""hello"");print(""world"");print('');print('')");
            Trace.Assert(stdout == "hello\r\nworld\r\n\r\n\r\n");


            stdout = i.Eval(@"
import sys
print('stdin:'+sys.stdin.read())
", stdinString: "hello");
            Trace.Assert(stdout == "stdin:hello\r\n");


            // Big stdout
            stdout = i.Eval(@"
for i in range(300):
 print('This is big text!')
");
            Trace.Assert(stdout.Length >= 100);


            try
            {
                i.Eval(@"
import sys
sys.stderr.write('myerror')
");
                throw new Exception("should not get here");
            }
            catch (PythonShell.Exceptions.PythonException ex)
            {
                Trace.Assert(ex.StandardError == "myerror\r\n");
            }


            // Slow script
            stdout = i.Eval(@"import time
print(0);time.sleep(1)
print(1);time.sleep(1)
print(2);time.sleep(1)
print(3);time.sleep(1)
print(4);time.sleep(1)
print(5)
");
            Trace.Assert(stdout == "0\r\n1\r\n2\r\n3\r\n4\r\n5\r\n");

            Console.WriteLine("All tests passed!");
        }
    }
}
