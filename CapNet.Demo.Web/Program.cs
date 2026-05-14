using System;
using Microsoft.Owin.Hosting;

namespace CapNet.Demo.Web
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            string url = args.Length > 0 ? args[0] : "http://localhost:5500/";
            using (WebApp.Start<Startup>(url))
            {
                Console.WriteLine($"CapNet.Demo.Web listening on {url}");
                Console.WriteLine("Press Ctrl+C to stop.");
                var done = new System.Threading.ManualResetEventSlim(false);
                Console.CancelKeyPress += (s, e) => { e.Cancel = true; done.Set(); };
                done.Wait();
            }
        }
    }
}
