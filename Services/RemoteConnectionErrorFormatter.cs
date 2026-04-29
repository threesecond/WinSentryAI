using System;
using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows;

namespace WinSentryAI.Services
{
    public static class RemoteConnectionErrorFormatter
    {
        public static string Format(Exception exception)
        {
            var root = GetRootException(exception);
            string detail = root.Message;

            string key = root switch
            {
                ArgumentException => "Remote_Error_InvalidHost",
                UnauthorizedAccessException => "Remote_Error_AccessDenied",
                SecurityException => "Remote_Error_AccessDenied",
                SocketException socketEx => MapSocketError(socketEx),
                Win32Exception win32Ex => MapNativeCode(win32Ex.NativeErrorCode),
                COMException comEx => MapNativeCode(comEx.ErrorCode),
                EventLogException eventLogEx => MapNativeCode(eventLogEx.HResult),
                _ => MapMessage(detail)
            };

            return string.Format(GetString(key), detail);
        }

        private static Exception GetRootException(Exception exception)
        {
            var current = exception;
            while (current.InnerException != null)
            {
                current = current.InnerException;
            }

            return current;
        }

        private static string MapSocketError(SocketException exception)
        {
            return exception.SocketErrorCode switch
            {
                SocketError.HostNotFound or SocketError.NoData => "Remote_Error_HostNotFound",
                SocketError.TimedOut => "Remote_Error_Timeout",
                SocketError.ConnectionRefused or SocketError.HostUnreachable or SocketError.NetworkUnreachable => "Remote_Error_RpcUnavailable",
                _ => MapNativeCode(exception.NativeErrorCode)
            };
        }

        private static string MapNativeCode(int code)
        {
            int unsignedCode = unchecked((int)(uint)code);

            return unsignedCode switch
            {
                5 or unchecked((int)0x80070005) => "Remote_Error_AccessDenied",
                53 or unchecked((int)0x80070035) => "Remote_Error_HostNotFound",
                67 or unchecked((int)0x80070043) => "Remote_Error_HostNotFound",
                87 or unchecked((int)0x80070057) => "Remote_Error_InvalidHost",
                121 or 1460 or unchecked((int)0x80070079) or unchecked((int)0x800705B4) => "Remote_Error_Timeout",
                1722 or 1753 or unchecked((int)0x800706BA) or unchecked((int)0x800706D9) => "Remote_Error_RpcUnavailable",
                1326 or 1909 or unchecked((int)0x8007052E) or unchecked((int)0x80070775) => "Remote_Error_BadCredentials",
                _ => "Remote_Error_Generic"
            };
        }

        private static string MapMessage(string message)
        {
            string value = message.ToLowerInvariant();

            if (value.Contains("access is denied") || value.Contains("unauthorized") || value.Contains("拒絕存取") || value.Contains("拒绝访问"))
            {
                return "Remote_Error_AccessDenied";
            }

            if (value.Contains("logon failure") || value.Contains("user name or password") || value.Contains("使用者名稱") || value.Contains("用户名"))
            {
                return "Remote_Error_BadCredentials";
            }

            if (value.Contains("rpc server is unavailable") || value.Contains("rpc") || value.Contains("firewall") || value.Contains("防火牆") || value.Contains("防火墙"))
            {
                return "Remote_Error_RpcUnavailable";
            }

            if (value.Contains("network path") || value.Contains("host") || value.Contains("dns") || value.Contains("找不到") || value.Contains("无法找到"))
            {
                return "Remote_Error_HostNotFound";
            }

            if (value.Contains("timed out") || value.Contains("timeout") || value.Contains("逾時") || value.Contains("超时"))
            {
                return "Remote_Error_Timeout";
            }

            return "Remote_Error_Generic";
        }

        private static string GetString(string key) =>
            Application.Current.FindResource(key) as string ?? "{0}";
    }
}
