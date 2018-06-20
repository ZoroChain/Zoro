using System;

namespace cs_tcpnode
{
    class Program
    {
        static Zoro.Net.TcpNodeIOCP clientNode = new Zoro.Net.TcpNodeIOCP();
        static void InitThread()
        {
            System.Threading.Thread t = new System.Threading.Thread(() =>
             {
                 while (true)
                 {
                     System.Threading.Thread.Sleep(1000);
                     var _in = clientNode.Connects.Count;
                     Console.Write("connect=" + _in);

                 }
             });
            t.Start();

        }
        static void Main(string[] args)
        {
            InitThread();
            Console.WriteLine("Hello World!");
            while (true)
            {
                var cmd = Console.ReadLine().Replace(" ", "").ToLower();
                if (cmd == "s")
                {
                    Console.WriteLine("start server");
                    clientNode.Listen("127.0.0.1", 12345);
                }
                if (cmd == "c")
                {
                    Console.WriteLine("start link 10000");
                    for (var i = 0; i < 10000; i++)
                    {
                        clientNode.Connect("127.0.0.1", 12345);
                    }
                }
                if (cmd == "cs")
                {
                    foreach (long i in clientNode.Connects.Keys)
                    {
                        clientNode.CloseConnect(i);
                    }

                }
                if (cmd == "css")
                {
                    foreach (var i in clientNode.Connects.Values)
                    {
                        if(i.InConnect==false)
                        {
                            clientNode.Send(i.Handle,System.Text.Encoding.UTF8.GetBytes("what a fuck."));
                        }
                       
                    }
                }
            }
        }
    }
}
