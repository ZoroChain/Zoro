using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Zoro.Net
{
    /// <summary>
    /// 一个接通过的TCP连接
    /// </summary>
    public class Link
    {
        public Link(Socket socket, bool inConnect)
        {
            this.Socket = socket;
            this.InConnect = inConnect;

        }
        public long Handle
        {
            get
            {
                return Socket.Handle.ToInt64();
            }
        }
        public Socket Socket
        {
            get;
            private set;
        }
        /// <summary>
        /// Inconnect true 表示这是自己listen，对方connect 产生的连接，否则反之
        /// </summary>
        public bool InConnect
        {
            get;
            private set;
        }
    }
    /// <summary>
    /// TCP节点对象，使用SocketAsyncEventArgs封装，windows采用iocp，linux是epoll 等
    /// </summary>
    public class TcpNodeIOCP
    {
        public TcpNodeIOCP()
        {
            InitEventArgs();
        }

        //空闲的SocketAsyncEventArgs对象，可以复用
        System.Collections.Concurrent.ConcurrentStack<SocketAsyncEventArgs> freeEventArgs = new System.Collections.Concurrent.ConcurrentStack<SocketAsyncEventArgs>();
        //用来监听的socket对象
        Socket listenSocket = null;
        //已经接通的连接对象
        public System.Collections.Concurrent.ConcurrentDictionary<Int64, Link> Connects = new System.Collections.Concurrent.ConcurrentDictionary<Int64, Link>();
        public event Action<long> onSocketIn;//有连接进来
        public event Action<long> onSocketLinked;//连接成了
        public event Action<Socket> onSocketLinkedError;//连接出错
        public event Action<long, byte[]> onSocketRecv;//收到数据
        //监听
        public void Listen(string ip, int port)
        {
            if (this.listenSocket != null)
            {
                throw new Exception("already in listen");
            }
            listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            IPAddress ipAddress = IPAddress.Parse(ip);
            var endPoint = new IPEndPoint(ipAddress, port);
            var arg = GetFreeEventArgs();
            listenSocket.Bind(endPoint);
            listenSocket.Listen(1024);
            listenSocket.AcceptAsync(arg);
        }
        //连接到
        public void Connect(string ip, int port)
        {
            SocketAsyncEventArgs eventArgs = null;
            Socket socket = null;

            {
                eventArgs = GetFreeEventArgs();
                socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                eventArgs.UserToken = socket;
            }


            IPAddress ipAddress = IPAddress.Parse(ip);
            var endPoint = new IPEndPoint(ipAddress, port);
            eventArgs.RemoteEndPoint = endPoint;
            socket.ConnectAsync(eventArgs);
        }

        public void CloseConnect(long handle)
        {
            SocketAsyncEventArgs eventArgs = GetFreeEventArgs();
            eventArgs.UserToken = Connects[handle];
            Connects[handle].Socket.DisconnectAsync(eventArgs);
        }

        public void Send(long handle, byte[] data)
        {
            var args = GetFreeEventArgs();
            args.UserToken = Connects[handle];
            args.SetBuffer(data, 0, data.Length);
            Connects[handle].Socket.SendAsync(args);
        }

        //以下几个方法都是为了初始化EventArgs
        private SocketAsyncEventArgs NewArgs()
        {
            var args = new SocketAsyncEventArgs();
            args.Completed += this.onCompleted;
            return args;
        }
        private void InitEventArgs()
        {
            for (var i = 0; i < 1000; i++)
            {
                freeEventArgs.Push(NewArgs());
            }
        }
        SocketAsyncEventArgs GetFreeEventArgs()
        {
            SocketAsyncEventArgs outea = null;
            freeEventArgs.TryPop(out outea);
            if (outea == null)
            {
                outea = NewArgs();
            }
            return outea;
        }
        private void SetRecivce(Link info)
        {
            var recvargs = GetFreeEventArgs();
            if (recvargs.Buffer == null || recvargs.Buffer.Length != 1024)
            {
                byte[] buffer = new byte[1024];
                recvargs.SetBuffer(buffer, 0, 1024);
            }
            recvargs.UserToken = info;
            info.Socket.ReceiveAsync(recvargs);
        }
        private void onCompleted(object sender, SocketAsyncEventArgs args)
        {
            //try
            {
                switch (args.LastOperation)
                {
                    case SocketAsyncOperation.Accept:
                        {
                            var info = new Link(args.AcceptSocket, true);
                            Connects[info.Handle] = info;

                            //直接复用
                            args.AcceptSocket = null;
                            listenSocket.AcceptAsync(args);

                            onSocketIn?.Invoke(info.Handle);

                            SetRecivce(info);
                        }
                        break;
                    case SocketAsyncOperation.Connect:
                        {
                            if (args.SocketError != SocketError.Success)
                            {
                                onSocketLinkedError?.Invoke(args.UserToken as Socket);
                            }
                            else
                            {
                                var info = new Link(args.ConnectSocket, false);
                                Connects[info.Handle] = info;

                                onSocketLinked?.Invoke(info.Handle);

                                SetRecivce(info);
                            }
                            //connect 的这个args不能复用
                        }
                        break;
                    case SocketAsyncOperation.Disconnect:
                        {
                            var hash = (args.UserToken as Link).Handle;
                            Link socket = null;
                            Connects.TryRemove(hash, out socket);
                            socket.Socket.Dispose();

                            freeEventArgs.Push(args);//这个是可以复用的
                        }
                        break;
                    case SocketAsyncOperation.Receive:
                        {
                            var hash = (args.UserToken as Link).Handle;
                            byte[] recv = new byte[args.BytesTransferred];
                            Buffer.BlockCopy(args.Buffer, 0, recv, 0, args.BytesTransferred);

                            onSocketRecv?.Invoke(hash, recv);

                            freeEventArgs.Push(args);//这个是可以复用的
                        }
                        break;
                    default:
                        {

                        }
                        break;
                }
            }
            //catch (Exception err)
            //{

            //}
        }

    }
}
