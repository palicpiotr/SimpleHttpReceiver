using System;
using System.Net;
using System.Threading.Tasks;

namespace HttpReceiver
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Start");

            var client = new WebClient();          
            HttpReceiver rec = new HttpReceiver("http://localhost", 8080);
            Task.Run(async () =>
            {
                try
                {
                    await rec.Run();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            });
            Console.ReadKey();
            Console.WriteLine("Stopping...");
            rec.Stop();
            Console.WriteLine("Stopped");
            Console.ReadKey();
        }
    }
}
