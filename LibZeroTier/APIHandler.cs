using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Windows;
using Newtonsoft.Json;
using System.Diagnostics;

namespace LibZeroTier
{


    public class APIHandler
    {
        private static string authtoken;

        private static string url = null;

        private static object syncRoot = new Object();

        public delegate void NetworkListCallback(List<ZeroTierNetwork> networks);
        public delegate void StatusCallback(ZeroTierStatus status);

        private string ZeroTierAddress = "";


        private static bool initHandler(bool resetToken = false)
        {
            String localZtDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\ZeroTier\\One";
            String globalZtDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + "\\ZeroTier\\One";

            String authToken = "";
            Int32 port = 9993;

            if (resetToken)
            {
                if (File.Exists(localZtDir + "\\authtoken.secret"))
                {
                    File.Delete(localZtDir + "\\authtoken.secret");
                }

                if (File.Exists(localZtDir + "\\zerotier-one.port"))
                {
                    File.Delete(localZtDir + "\\zerotier-one.port");
                }
            }

            if (!File.Exists(localZtDir + "\\authtoken.secret") || !File.Exists(localZtDir + "\\zerotier-one.port"))
            {
                // launch external process to copy file into place
                String curPath = System.Reflection.Assembly.GetEntryAssembly().Location;
                int index = curPath.LastIndexOf("\\");
                curPath = curPath.Substring(0, index);
                ProcessStartInfo startInfo = new ProcessStartInfo(curPath + "\\copyutil.exe", "\"" + globalZtDir + "\"" + " " + "\"" + localZtDir + "\"");
                startInfo.Verb = "runas";


                var process = Process.Start(startInfo);
                process.WaitForExit();
            }

            authToken = readAuthToken(localZtDir + "\\authtoken.secret");

            if ((authToken == null) || (authToken.Length <= 0))
            {
                throw new LibZeroTierException("Unable to read ZeroTier One authtoken");
            }

            port = readPort(localZtDir + "\\zerotier-one.port");
            setVars(port, authToken);

            return true;
        }

        private static void setVars(int port, string auth)
        {
            url = "http://127.0.0.1:" + port;
            authtoken = auth;
        }
        private static String readAuthToken(String path)
        {
            String authToken = "";

            if (File.Exists(path))
            {
                try
                {
                    byte[] tmp = File.ReadAllBytes(path);
                    authToken = System.Text.Encoding.UTF8.GetString(tmp).Trim();
                }
                catch
                {
                    throw new LibZeroTierException("Unable to read ZeroTier One Auth Token from:\r\n" + path);
                }
            }

            return authToken;
        }

        private static Int32 readPort(String path)
        {
            Int32 port = 9993;

            try
            {
                byte[] tmp = File.ReadAllBytes(path);
                port = Int32.Parse(System.Text.Encoding.ASCII.GetString(tmp).Trim());
                if ((port <= 0) || (port > 65535))
                    port = 9993;
            }
            catch
            {
            }

            return port;
        }


        public APIHandler()
        {
            url = "http://127.0.0.1:9993";
            initHandler(true);
        }

        public APIHandler(int port, string authToken)
        {
            url = "http://127.0.0.1:" + port;
            authtoken = authToken;
        }



        /// <summary>
        /// Gets the status response from the ZeroTier service.
        /// </summary>
        /// <returns></returns>
        public ZeroTierStatus GetStatus()
        {
            var request = WebRequest.Create(url + "/status" + "?auth=" + authtoken) as HttpWebRequest;
            request.Headers.Add("X-ZT1-Auth",authtoken);
            if (request != null)
            {
                request.Method = "GET";
                request.ContentType = "application/json";
            }

            try
            {
                var httpResponse = (HttpWebResponse)request.GetResponse();
                if (httpResponse.StatusCode == HttpStatusCode.OK)
                {
                    using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                    {
                        var responseText = streamReader.ReadToEnd();

                        ZeroTierStatus status = null;
                        try
                        {
                            status = JsonConvert.DeserializeObject<ZeroTierStatus>(responseText);

                            if (ZeroTierAddress != status.Address)
                            {
                                ZeroTierAddress = status.Address;
                            }
                        }
                        catch (JsonReaderException e)
                        {
                            Console.WriteLine(e.ToString());
                        }

                        return status;
                    }
                }
                else if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
                {
                    APIHandler.initHandler(true);
                    return null;
                }

            }
            catch (System.Net.Sockets.SocketException ex)
            {
                throw new LibZeroTierException("ZeroTier Exception:", ex);
            }
            catch (System.Net.WebException e)
            {
                HttpWebResponse res = (HttpWebResponse)e.Response;
                if (res != null && res.StatusCode == HttpStatusCode.Unauthorized)
                {
                    APIHandler.initHandler(true);
                    return null;
                }
                else
                {
                    throw new LibZeroTierException("ZeroTier Exception:", e);
                }
            }

            return null;
        }



        /// <summary>
        /// Returns the current networks you're connected to.
        /// </summary>
        /// <returns>List of ZeroTierNetwork objects</returns>
        public List<ZeroTierNetwork> GetNetworks()
        {
            var request = WebRequest.Create(url + "/network" + "?auth=" + authtoken) as HttpWebRequest;
            if (request == null)
            {
                throw new LibZeroTierException("ZeroTier Request Response Empty");
            }

            request.Method = "GET";
            request.ContentType = "application/json";
            request.Timeout = 10000;

            try
            {
                var httpResponse = (HttpWebResponse)request.GetResponse();

                if (httpResponse.StatusCode == HttpStatusCode.OK)
                {
                    using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                    {
                        var responseText = streamReader.ReadToEnd();

                        List<ZeroTierNetwork> networkList = null;
                        try
                        {
                            networkList = JsonConvert.DeserializeObject<List<ZeroTierNetwork>>(responseText);
                            foreach (ZeroTierNetwork n in networkList)
                            {
                                // all networks received via JSON are connected by definition
                                n.IsConnected = true;
                            }
                        }
                        catch (JsonReaderException e)
                        {
                            Console.WriteLine(e.ToString());
                        }

                        return networkList;
                    }
                }
                else if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
                {
                    APIHandler.initHandler(true);
                }
            }
            catch (System.Net.Sockets.SocketException)
            {
                throw new LibZeroTierException("ZeroTier Request Response Empty");
            }
            catch (System.Net.WebException e)
            {
                HttpWebResponse res = (HttpWebResponse)e.Response;
                if (res != null && res.StatusCode == HttpStatusCode.Unauthorized)
                {
                    APIHandler.initHandler(true);
                }
                else
                {
                    throw new LibZeroTierException("ZeroTier Request Response Empty");
                }
            }

            return null;
        }

        /// <summary>
        /// Sends the request to join a network
        /// </summary>
        /// <param name="nwid">Hexadecimal Network ID</param>
        /// <param name="allowManaged">Specify if it's managed</param>
        /// <param name="allowGlobal">Specify if it's global</param>
        /// <param name="allowDefault">Specify if it's allowed to be the default route</param>
        public void JoinNetwork(string nwid, bool allowManaged = true, bool allowGlobal = false, bool allowDefault = false)
        {
            Task.Factory.StartNew(() =>
            {
                var request = WebRequest.Create(url + "/network/" + nwid + "?auth=" + authtoken) as HttpWebRequest;
                if (request == null)
                {
                    return;
                }

                request.Method = "POST";
                request.ContentType = "applicaiton/json";
                request.Timeout = 30000;
                try
                {
                    using (var streamWriter = new StreamWriter(((HttpWebRequest)request).GetRequestStream()))
                    {
                        string json = "{\"allowManaged\":" + (allowManaged ? "true" : "false") + "," +
                                "\"allowGlobal\":" + (allowGlobal ? "true" : "false") + "," +
                                "\"allowDefault\":" + (allowDefault ? "true" : "false") + "}";
                        streamWriter.Write(json);
                        streamWriter.Flush();
                        streamWriter.Close();
                    }
                }
                catch (System.Net.WebException)
                {
                    throw new LibZeroTierException("Error Joining Network: Cannot connect to ZeroTier service.");
                }

                try
                {
                    var httpResponse = (HttpWebResponse)request.GetResponse();

                    if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        APIHandler.initHandler(true);
                    }
                    else if (httpResponse.StatusCode != HttpStatusCode.OK)
                    {
                        Console.WriteLine("Error sending join network message");
                    }
                }
                catch (System.Net.Sockets.SocketException)
                {
                    throw new LibZeroTierException("Error Joining Network: Cannot connect to ZeroTier service.");
                }
                catch (System.Net.WebException e)
                {
                    HttpWebResponse res = (HttpWebResponse)e.Response;
                    if (res != null && res.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        APIHandler.initHandler(true);
                    }
                    throw new LibZeroTierException("Error Joining Network: Cannot connect to ZeroTier service.");
                }
            });
        }

        /// <summary>
        /// Leaves a specified network
        /// </summary>
        /// <param name="nwid">Hexadecimal network ID</param>
        public void LeaveNetwork(string nwid)
        {
            Task.Factory.StartNew(() =>
            {
                var request = WebRequest.Create(url + "/network/" + nwid + "?auth=" + authtoken) as HttpWebRequest;
                if (request == null)
                {
                    return;
                }

                request.Method = "DELETE";
                request.Timeout = 30000;

                try
                {
                    var httpResponse = (HttpWebResponse)request.GetResponse();

                    if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        APIHandler.initHandler(true);
                    }
                    else if (httpResponse.StatusCode != HttpStatusCode.OK)
                    {
                        Console.WriteLine("Error sending leave network message");
                    }
                }
                catch (System.Net.Sockets.SocketException)
                {
                    throw new LibZeroTierException("Error Leaving Network: Cannot connect to ZeroTier service.");
                }
                catch (System.Net.WebException e)
                {
                    HttpWebResponse res = (HttpWebResponse)e.Response;
                    if (res != null && res.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        APIHandler.initHandler(true);
                    }
                    throw new LibZeroTierException("Error Leaving Network: Cannot connect to ZeroTier service.");
                }
                catch
                {
                    Console.WriteLine("Error leaving network: Unknown error");
                }
            });
        }

        //public delegate void PeersCallback(List<ZeroTierPeer> peers);

        /*public void GetPeers(PeersCallback cb)
        {
            var request = WebRequest.Create(url + "/peer" + "?auth=" + authtoken) as HttpWebRequest;
            if (request == null)
            {
                cb(null);
            }

            request.Method = "GET";
            request.ContentType = "application/json";

            try
            {
                var httpResponse = (HttpWebResponse)request.GetResponse();
                if (httpResponse.StatusCode == HttpStatusCode.OK)
                {
                    using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                    {
                        var responseText = streamReader.ReadToEnd();
                        //Console.WriteLine(responseText);
                        List<ZeroTierPeer> peerList = null;
                        try
                        {
                            peerList = JsonConvert.DeserializeObject<List<ZeroTierPeer>>(responseText);
                        }
                        catch (JsonReaderException e)
                        {
                            Console.WriteLine(e.ToString());
                        }
                        cb(peerList);
                    }
                }
                else if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
                {
                    APIHandler.initHandler(true);
                }
            }
            catch (System.Net.Sockets.SocketException)
            {
                cb(null);
            }
            catch (System.Net.WebException e)
            {
                HttpWebResponse res = (HttpWebResponse)e.Response;
                if (res != null && res.StatusCode == HttpStatusCode.Unauthorized)
                {
                    APIHandler.initHandler(true);
                }
                else
                {
                    cb(null);
                }
            }
        }*/

        public string NodeAddress()
        {
            return ZeroTierAddress;
        }
    }
}
