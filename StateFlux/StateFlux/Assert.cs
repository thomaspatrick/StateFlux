using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using WebSocketSharp;
using WebSocketSharp.Server;
using System.Linq;
using WebSocketSharp.Net;
using StateFlux.Model;

namespace StateFlux.Service
{
    // throw an exception when things are wrong
    public static class Assert
    {
        static public void ThrowIfNull(object obj, string msg, AppWebSocketBehavior behavior)
        {
            if (obj == null) ThrowError(msg, behavior);
        }

        static public void ThrowIfNotNull(object obj, string msg, AppWebSocketBehavior behavior)
        {
            if (obj != null) ThrowError(msg, behavior);
        }

        static public void ThrowIfFalse(bool flag, string msg, AppWebSocketBehavior behavior)
        {
            if (!flag) ThrowError(msg, behavior);
        }

        static private void ThrowError(string msg, AppWebSocketBehavior behavior)
        {
            LogMessage($"{msg},{Environment.StackTrace}", behavior);
            throw new Exception(msg);
        }

        static private void LogMessage(string message, AppWebSocketBehavior behavior)
        {
            Player currentPlayer = behavior.GetCurrentSessionPlayer();
            string playerName = currentPlayer != null ? currentPlayer.Name : "unknown";
            string connection = "";
            try { connection = behavior.Context.UserEndPoint.ToString(); } catch { };
            Console.WriteLine($"{DateTime.Now},{connection},{playerName},{message}");
        }

    }
}