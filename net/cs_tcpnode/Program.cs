using System;

namespace cs_tcpnode
{
    class Program
    {
        static Zoro.Net.TcpNodeIOCP zoroNode = new Zoro.Net.TcpNodeIOCP();
        static void InitThread()
        {
            System.Threading.Thread t = new System.Threading.Thread(() =>
             {
                 while (true)
                 {
                     System.Threading.Thread.Sleep(1000);
                     var _in = zoroNode.Connects.Count;
                     Console.Write("connect=" + _in);

                     int creal = 0;
                     foreach(var k in zoroNode.Connects)
                     {
                         if(k.Value.Socket.Connected)
                         {
                             creal++;
                         }
                     }
                     Console.Write("connect real=" + creal);

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
                    zoroNode.Listen("127.0.0.1", 12345);
                }
                if (cmd == "c")
                {
                    Console.WriteLine("start link 10000");
                    for (var i = 0; i < 1; i++)
                    {
                        zoroNode.Connect("127.0.0.1", 12345);
                    }
                }
                if (cmd == "cs")
                {
                    foreach (long i in zoroNode.Connects.Keys)
                    {
                        if(zoroNode.Connects.ContainsKey(i))
                        {
                            if (zoroNode.Connects[i].IsHost == true)
                            {
                                zoroNode.CloseConnect(i);
                            }
                        }
                    }

                }
                if (cmd == "css")
                {
                    foreach (var i in zoroNode.Connects.Values)
                    {
                        if(i.IsHost==false)
                        {
                            zoroNode.Send(i.Handle,System.Text.Encoding.UTF8.GetBytes("what a fuck."));
                        }
                       
                    }
                }
            }
        }
    }
}
