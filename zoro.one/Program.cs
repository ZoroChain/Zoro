using System;

namespace zoro.one
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            zoro.one.http.httpserver http = new one.http.httpserver();
            http.Start(80,false);
            while(true)
            {
                Console.ReadLine();
            }
        }
    }
}
