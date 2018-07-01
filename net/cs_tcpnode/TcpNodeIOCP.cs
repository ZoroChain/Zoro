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
        public Link(Socket socket, SocketAsyncEventArgs clientArgs = null)
        {
            this.Socket = socket;
            this.ClientArgs = clientArgs;
            this.IsHost = (clientArgs == null);
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
        }
        /// <summary>
        /// IsHost true 表示这是自己listen，对方connect 产生的连接，否则反之
        /// </summary>
        public bool IsHost
        {
            get;
        }
        public SocketAsyncEventArgs ClientArgs
        {
            get;
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

        public event Action<long> OnSocketAccept;//有连接进来
        public event Action<long> OnSocketLinked;//连接成了
        public event Action<Socket> OnSocketError;//连接出错
        public event Action<long, byte[]> OnSocketRecv;//收到数据
        public event Action<long> OnSocketSend;//发出数据
        public event Action<long> OnSocketClose;//socket 断开
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
            if (Connects.ContainsKey(handle) == false)
            {
                return;
            }
            try
            {
                Connects[handle].Socket.Shutdown(SocketShutdown.Both);
            }
            // throws if client process has already closed
            catch (Exception)
            {
            }
            if (Connects.ContainsKey(handle) == false)
            {
                return;
            }
            Connects[handle].Socket.Close();
            Connects[handle].ClientArgs.Dispose();
            Connects.TryRemove(handle, out Link link);
            OnSocketClose?.Invoke(handle);
            //SocketAsyncEventArgs eventArgs = GetFreeEventArgs();
            //eventArgs.UserToken = Connects[handle];
            //Connects[handle].Socket.DisconnectAsync(eventArgs);
        }

        public void Send(long handle, byte[] data)
        {
            var args = GetFreeEventArgs();
            //var args = new SocketAsyncEventArgs();
            //args.Completed += onCompletedSend;

            args.UserToken = Connects[handle];
            args.SetBuffer(data, 0, data.Length);
            //args.RemoteEndPoint = Connects[handle].Socket.RemoteEndPoint;
            try
            {
                bool basync = Connects[handle].Socket.SendToAsync(args);
                if (basync == false)
                {
                    ReuseSocketAsyncEventArgs(args);//复用这个发送参数
                    //这个操作同步完成了
                    OnSocketSend?.Invoke(handle);
                }
            }
            catch (Exception err)
            {
                ReuseSocketAsyncEventArgs(args);//复用这个发送参数

                CloseConnect(handle);//
            }
        }
        //空闲的SocketAsyncEventArgs对象，可以复用
        System.Collections.Concurrent.ConcurrentStack<SocketAsyncEventArgs> freeEventArgs = new System.Collections.Concurrent.ConcurrentStack<SocketAsyncEventArgs>();

        //空闲的接收专用SocketAsyncEventArgs 对象，因为接收缓冲区可以轻易复用，所以独立出来
        System.Collections.Concurrent.ConcurrentStack<SocketAsyncEventArgs> freeRecvEventArgs = new System.Collections.Concurrent.ConcurrentStack<SocketAsyncEventArgs>();
        List<byte[]> recvBuffers = new List<byte[]>();
        int recvBufferOffset = 0;

        //用来监听的socket对象
        Socket listenSocket = null;
        //已经接通的连接对象

        public System.Collections.Concurrent.ConcurrentDictionary<Int64, Link> Connects = new System.Collections.Concurrent.ConcurrentDictionary<Int64, Link>();


        //以下几个方法都是为了初始化EventArgs

        private void InitEventArgs()
        {
            for (var i = 0; i < 1000; i++)
            {
                ReuseSocketAsyncEventArgs(NewArgs());
                ReuseRecvSocketAsyncEventArgs(NewRecvArgs());
            }
        }
        private SocketAsyncEventArgs NewArgs()
        {
            var args = new SocketAsyncEventArgs();
            args.Completed += this._OnCompleted;
            return args;
        }

        private void ReuseSocketAsyncEventArgs(SocketAsyncEventArgs args)
        {
            freeEventArgs.Push(args);
        }
        private SocketAsyncEventArgs GetFreeEventArgs()
        {
            freeEventArgs.TryPop(out SocketAsyncEventArgs outea);
            if (outea == null)
            {
                outea = NewArgs();
            }
            return outea;
        }
        private SocketAsyncEventArgs NewRecvArgs()
        {
            var args = new SocketAsyncEventArgs();
            args.Completed += this._OnCompleted;

            lock (recvBuffers)
            {
                if (recvBuffers.Count == 0 || recvBufferOffset >= 1024 * 1024)
                {
                    recvBuffers.Add(new byte[1024 * 1014]);
                    recvBufferOffset = 0;
                }
                args.SetBuffer(recvBuffers[recvBuffers.Count - 1], recvBufferOffset, 1024);
                recvBufferOffset += 1024;
            }
            return args;
        }
        private void ReuseRecvSocketAsyncEventArgs(SocketAsyncEventArgs args)
        {
            freeRecvEventArgs.Push(args);
        }

        private SocketAsyncEventArgs GetFreerecvEventArgs()
        {
            freeRecvEventArgs.TryPop(out SocketAsyncEventArgs outea);
            if (outea == null)
            {
                outea = NewArgs();
            }
            return outea;
        }
        private void SetRecivce(Link info)
        {
            var recvargs = GetFreerecvEventArgs();
            recvargs.UserToken = info;
            info.Socket.ReceiveAsync(recvargs);
        }
        private void _OnCompleted(object sender, SocketAsyncEventArgs args)
        {
            //try
            {
                switch (args.LastOperation)
                {
                    case SocketAsyncOperation.Accept://accept a connect
                        {
                            if (args.SocketError != SocketError.Success)
                            {
                                //直接复用
                                args.AcceptSocket = null;
                                listenSocket.AcceptAsync(args);
                            }
                            if (args.SocketError == SocketError.Success)
                            {
                                var info = new Link(args.AcceptSocket, null);
                                Connects[info.Handle] = info;

                                //直接复用
                                args.AcceptSocket = null;
                                listenSocket.AcceptAsync(args);
                                if (args.SocketError == SocketError.Success)
                                {
                                    OnSocketAccept?.Invoke(info.Handle);
                                    SetRecivce(info);
                                }
                            }
                        }
                        return;
                    case SocketAsyncOperation.Connect://connect succ
                        {
                            if (args.SocketError != SocketError.Success)
                            {
                                OnSocketError?.Invoke(args.UserToken as Socket);
                                args.Dispose();
                            }
                            else
                            {
                                var info = new Link(args.ConnectSocket, args);
                                Connects[info.Handle] = info;

                                OnSocketLinked?.Invoke(info.Handle);

                                SetRecivce(info);
                            }
                            //connect 的这个args不能复用
                        }
                        return;
                    case SocketAsyncOperation.Send:
                        {
                            var hash = (args.UserToken as Link).Handle;
                            if (args.SocketError != SocketError.Success)
                            {
                                ReuseSocketAsyncEventArgs(args);
                                //断链，复用这个接受参数
                                CloseConnect(hash);
                            }
                            else
                            {
                                //发送成功，也复用这个发送参数
                                ReuseSocketAsyncEventArgs(args);
                                //断链，复用这个接受参数
                                OnSocketSend?.Invoke(hash);
                            }

                        }
                        return;
                    case SocketAsyncOperation.Receive:
                        {
                            var hash = (args.UserToken as Link).Handle;
                            if (args.BytesTransferred == 0 || args.SocketError != SocketError.Success)
                            {
                                ReuseRecvSocketAsyncEventArgs(args);//断链，复用这个接受参数
                                CloseConnect(hash);
                                return;
                            }
                            else
                            {
                                byte[] recv = new byte[args.BytesTransferred];
                                Buffer.BlockCopy(args.Buffer, args.Offset, recv, 0, args.BytesTransferred);
                                (args.UserToken as Link).Socket.ReceiveAsync(args);//直接复用
                                OnSocketRecv?.Invoke(hash, recv);
                            }
                        }
                        return;
                    default:
                        {

                            break;
                        }

                }

            }
        }

    }
}
