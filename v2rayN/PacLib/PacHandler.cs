﻿using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PacLib;

public class PacHandler
{
    private static string _configPath;
    private static int _httpPort;
    private static int _pacPort;
    private static TcpListener? _tcpListener;
    private static string _pacText;
    private static bool _isRunning;
    private static bool _needRestart = true;

    public static void Start(string configPath, int httpPort, int pacPort)
    {
        _needRestart = (configPath != _configPath || httpPort != _httpPort || pacPort != _pacPort || !_isRunning);

        _configPath = configPath;
        _httpPort = httpPort;
        _pacPort = pacPort;

        InitText();

        if (_needRestart)
        {
            Stop();
            RunListener();
        }
    }

    private static void InitText()
    {
        var path = Path.Combine(_configPath, "pac.txt");
        if (!File.Exists(path))
        {
            File.AppendAllText(path, Resources.ResourceManager.GetString("pac"));
        }

        _pacText = File.ReadAllText(path).Replace("__PROXY__", $"PROXY 127.0.0.1:{_httpPort};DIRECT;");

        var userPac = LoadUserPac(_configPath);
        if (userPac != "")
        {
            _pacText = _pacText.Replace("        [],\n        []", userPac);
        }
    }

    private static void RunListener()
    {
        _tcpListener = TcpListener.Create(_pacPort);
        _isRunning = true;
        _tcpListener.Start();
        Task.Factory.StartNew(() =>
        {
            while (_isRunning)
            {
                try
                {
                    if (!_tcpListener.Pending())
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    var client = _tcpListener.AcceptTcpClient();
                    Task.Run(() =>
                    {
                        var stream = client.GetStream();
                        var sb = new StringBuilder();
                        sb.AppendLine("HTTP/1.0 200 OK");
                        sb.AppendLine("Content-type:application/x-ns-proxy-autoconfig");
                        sb.AppendLine("Connection:close");
                        sb.AppendLine("Content-Length:" + Encoding.UTF8.GetByteCount(_pacText));
                        sb.AppendLine();
                        sb.Append(_pacText);
                        var content = Encoding.UTF8.GetBytes(sb.ToString());
                        stream.Write(content, 0, content.Length);
                        stream.Flush();
                    });
                }
                catch (Exception e)
                {
                }
            }
        }, TaskCreationOptions.LongRunning);
    }

    public static void Stop()
    {
        if (_tcpListener != null)
        {
            try
            {
                _isRunning = false;
                _tcpListener.Stop();
                _tcpListener = null;
            }
            catch (Exception e)
            {
            }
        }
    }

    public static string LoadUserPac(string configPath)
    {
        var userPac = "";
        var userPacPath = Path.Combine(_configPath, "pacUser.txt");
        if (File.Exists(userPacPath))
        {
            userPac = File.ReadAllText(userPacPath);
        }
        return userPac;
    }

    public static void SaveUserPac(string directDomains, string proxyDomains, string configPath)
    {
        var directDomainsArray = directDomains.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var proxyDomainsArray = proxyDomains.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var userPac = directDomainsArray.Aggregate("[\n", (current, directDomain) => $"{current}            \"{directDomain}\",\n");

        userPac += "        ],\n        [\n";

        userPac = proxyDomainsArray.Aggregate(userPac, (current, proxyDomain) => $"{current}            \"{proxyDomain}\",\n");

        userPac += "]";

        var path = Path.Combine(configPath, "pacUser.txt");
        File.WriteAllText(path, userPac);
    }
}