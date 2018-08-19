using System;

namespace zoro.one
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            zoro.one.http.httpserver http = new one.http.httpserver();

            var pfxpath = "http" + System.IO.Path.DirectorySeparatorChar + "214541951070440.pfx";
            var password = "214541951070440";

            http.Start(80, 443, pfxpath, password);

            var curlibVersion = typeof(Program).Assembly.GetName().Version;
            Console.WriteLine("zoro.one V" + curlibVersion);
            while (true)
            {
                Console.ReadLine();
            }
        }
    }
}
