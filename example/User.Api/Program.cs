using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace User.Api
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                CreateHostBuilder(args)
                    .Build()
                    .Run();
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message, "An unhandled exception occured during User API bootstrapping....");
                return 1;
            }
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((_, config) => { })
                .ConfigureWebHostDefaults(builder => { builder.UseStartup<Startup>(); });
    }
}