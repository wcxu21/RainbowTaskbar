﻿using Newtonsoft.Json.Linq;
using RainbowTaskbar.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using WebSocketSharp.Server;

namespace RainbowTaskbar.HTTPAPI
{
    public static class HTTPAPIServer
    {
        public static void Get(object sender, HttpRequestEventArgs args)
        {
            var http = (HttpServer)sender;
            var req = args.Request;
            var res = args.Response;

            var uri = new Uri("http://localhost" + req.RawUrl);
            var query = HttpUtility.ParseQueryString(uri.Query);

            res.ContentType = "application/json; charset=utf-8";
            res.AddHeader("Access-Control-Allow-Origin", "*");
            res.StatusCode = 200;

            try
            {

                switch (uri.AbsolutePath)
                {
                    case "/config":
                        {
                            JObject json = new JObject();
                            json.Add("data", App.Config.ToJSON());
                            json.Add("success", JToken.FromObject(true));

                            byte[] data = Encoding.UTF8.GetBytes(json.ToString());
                            res.ContentLength64 = data.Length;
                            res.OutputStream.Write(data);
                            break;
                        }

                    case "/instruction":
                        {
                            byte[] data;
                            int position;

                            if (!int.TryParse(query["position"], out position) || position < 0 || position >= App.Config.Instructions.Count)
                            {
                                throw new Exception("Position out of bounds");
                            }

                            JObject json = new JObject();
                            json.Add("data", App.Config.Instructions[position].ToJSON());
                            json.Add("success", JToken.FromObject("true"));

                            data = Encoding.UTF8.GetBytes(json.ToString());
                            res.ContentLength64 = data.Length;
                            res.OutputStream.Write(data);
                            break;
                        }

                    case "/instructions":
                        {
                            JObject json = new JObject();
                            json.Add("data", JArray.FromObject(App.Config.Instructions.Select((i) => i.ToJSON())));
                            json.Add("success", JToken.FromObject("true"));

                            byte[] data = Encoding.UTF8.GetBytes(json.ToString());
                            res.ContentLength64 = data.Length;
                            res.OutputStream.Write(data);
                            break;
                        }

                    default:
                        res.StatusCode = 404;
                        break;
                }



            }
            catch (Exception e)
            {
                res.StatusCode = 500;

                byte[] data = Encoding.UTF8.GetBytes("{\"success\": \"false\", \"error\": \"" + e.Message.Replace("\"", "'") + "\"}");
                res.ContentLength64 = data.Length;
                res.OutputStream.Write(data);
            }

            res.Close();
        }

        public static void Options(object sender, HttpRequestEventArgs args)
        {
            var res = args.Response;
            res.AddHeader("Access-Control-Allow-Origin", "*");
            res.AddHeader("Access-Control-Allow-Headers", "*");
            res.AddHeader("Allow", "GET, POST");
            res.StatusCode = 200;
            res.Close();

        }

        public static void Post(object sender, HttpRequestEventArgs args)
        {
            var http = (HttpServer)sender;
            var req = args.Request;
            var res = args.Response;

            var uri = new Uri("http://localhost" + req.RawUrl);
            var query = HttpUtility.ParseQueryString(uri.Query);

            byte[] bodybuf = new byte[short.MaxValue];
            req.InputStream.Read(bodybuf, 0, short.MaxValue);

            

            res.ContentType = "application/json; charset=utf-8";
            res.AddHeader("Access-Control-Allow-Origin", "*");
            res.AddHeader("Access-Control-Allow-Headers", "*");
            res.StatusCode = 200;

            try
            {
                var body = JObject.Parse(Encoding.UTF8.GetString(bodybuf).TrimEnd('\0'));
                switch (uri.AbsolutePath)
                {
                    case "/clearinstruction":
                        {
                            byte[] data;
                            int position;

                            if (!int.TryParse((string)body["Position"], out position) || position < 0 || position >= App.Config.Instructions.Count)
                            {
                                throw new Exception("Position out of bounds");
                            }

                            App.Config.Instructions.RemoveAt(position);

                            data = Encoding.UTF8.GetBytes("{\"success\": \"true\"}");
                            res.ContentLength64 = data.Length;
                            res.OutputStream.Write(data);
                            break;
                        }
                    case "/clearinstructions":
                        {
                            App.Config.Instructions = new System.ComponentModel.BindingList<Configuration.Instruction>();

                            byte[] data = Encoding.UTF8.GetBytes("{\"success\": \"true\"}");
                            res.ContentLength64 = data.Length;
                            res.OutputStream.Write(data);
                            break;
                        }
                    case "/addinstruction":
                        {
                            byte[] data;
                            int position = 0;

                            if (body["Position"] is not null && (!int.TryParse((string)body["Position"], out position) || position < 0 || position >= App.Config.Instructions.Count))
                            {
                                throw new Exception("Position out of bounds");
                                break;
                            }

                            if (Type.GetType("RainbowTaskbar.Configuration.Instructions." + body["Name"]) is null)
                                throw new Exception("Unknown instruction class name.");

                            Instruction instruction = Instruction.FromJSON(Type.GetType("RainbowTaskbar.Configuration.Instructions." + body["Name"]), body);

                            App.Current.Dispatcher.Invoke(() =>
                                App.Config.Instructions.Insert(position, instruction)
                            );
                            break;

                        }
                    case "/executeinstruction":
                        {
                            byte[] data;

                            if(Type.GetType("RainbowTaskbar.Configuration.Instructions." + body["Name"]) is null)
                                throw new Exception("Unknown instruction class name.");

                            Instruction instruction = Instruction.FromJSON(Type.GetType("RainbowTaskbar.Configuration.Instructions." + body["Name"]), body);

                            App.taskbars.ForEach((taskbar) => instruction.Execute(taskbar));

                            data = Encoding.UTF8.GetBytes("{\"success\": \"true\"}");
                            res.ContentLength64 = data.Length;
                            res.OutputStream.Write(data);
                            break;

                        }
                    case "/goto":
                        {
                            byte[] data;

                            int position = 0;

                            if (body["Position"] is not null && (!int.TryParse((string)body["Position"], out position) || position < 0 || position >= App.Config.Instructions.Count))
                            {
                                throw new Exception("Position out of bounds");
                            }

                            App.taskbars.ForEach((taskbar) => taskbar.viewModel.ConfigStep = position);

                            data = Encoding.UTF8.GetBytes("{\"success\": \"true\"}");
                            res.ContentLength64 = data.Length;
                            res.OutputStream.Write(data);
                            break;

                        }
                    case "/saveconfig":
                        {
                            App.Config.ToFile();

                            byte[] data = Encoding.UTF8.GetBytes("{\"success\": \"true\"}");
                            res.ContentLength64 = data.Length;
                            res.OutputStream.Write(data);
                            break;
                        }
                    case "/loadconfig":
                        {
                            App.Config = Configuration.Config.FromFile();

                            byte[] data = Encoding.UTF8.GetBytes("{\"success\": \"true\"}");
                            res.ContentLength64 = data.Length;
                            res.OutputStream.Write(data);
                            break;
                        }
                }
            }
            
            catch (Exception e)
            {
                res.StatusCode = 500;

                byte[] data = Encoding.UTF8.GetBytes("{\"success\": \"false\", \"error\": \"" + e.Message.Replace("\"", "'") + "\"}");
                res.ContentLength64 = data.Length;
                res.OutputStream.Write(data);
            }
            
        }
    }
}
