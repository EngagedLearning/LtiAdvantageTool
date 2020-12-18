﻿using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
namespace AdvantageTool
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureKestrel(serverOptions =>
                    {
                        // Set properties and call methods on options
                    })
                    .UseStartup<Startup>();
                });
        // WebHost.CreateDefaultBuilder(args)
        //     // .UseApplicationInsights()
        //     .UseStartup<Startup>();
    }
}
