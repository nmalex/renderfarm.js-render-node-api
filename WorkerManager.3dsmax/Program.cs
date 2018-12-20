using System;
using System.Threading;

namespace WorkerManager._3dsmax
{
    class Program
    {
        static void Main(string[] args)
        {
            int i = 0;
            foreach (var a in args)
            {
                Console.WriteLine($"args[{i++}] = {a}");
            }

            // ensure some cpu load and ram consumption
            ThreadPool.QueueUserWorkItem(o =>
            {
                var rnd = new Random((int)DateTime.Now.Ticks);
                while (true)
                {
                    {
                        var m = new int[1024 * 1024 * (1 + rnd.Next() % 10)]; // alloc some ram

                        long sum = 0;
                        for (int k = 0; k < int.MaxValue / 1000; k++)
                        {
                            sum += k;
                            sum = sum%1000;
                        }
                    }
                    GC.Collect();

                    Thread.Sleep(100);
                }
            });

            Console.WriteLine("Press any key to exit");
            Console.ReadLine();
        }
    }
}
