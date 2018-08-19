using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace zoro.one.http
{
    class httpserver
    {
        private IWebHost host;
        public void Start(int port, int portForHttps = 0, string pfxpath = null, string password = null)
        {
            host = new WebHostBuilder().UseKestrel((options) =>
            {
                options.Listen(IPAddress.Any, port, listenOptions =>
                  {

                  });
                if (portForHttps != 0)
                {
                    options.Listen(IPAddress.Any, portForHttps, listenOptions =>
                      {
                      //if (!string.IsNullOrEmpty(sslCert))
                      //if (useHttps)
                      listenOptions.UseHttps(pfxpath, password);
                      //sslCert, password);
                  });
                }
            }
            )
            .Configure(app =>
            {
                //app.UseResponseCompression();
                app.Run(ProcessAsync);
            })
            //.ConfigureServices(services =>
            //{
            //    services.AddResponseCompression(options =>
            //    {
            //        // options.EnableForHttps = false;
            //        options.Providers.Add<GzipCompressionProvider>();
            //        options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "application/json-rpc" });
            //    });

            //    services.Configure<GzipCompressionProviderOptions>(options =>
            //    {
            //        options.Level = CompressionLevel.Fastest;
            //    });
            //})
            .Build();

            host.Start();
        }
        private async Task ProcessAsync(HttpContext context)
        {
        }
    }
}
