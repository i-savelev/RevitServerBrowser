using System;
using System.Net;

namespace RevitServerBrowser
{
    /// <summary>
    /// WebClient с настраиваемым таймаутом.
    /// </summary>
    public class TimeoutWebClient : WebClient
    {
        /// <summary>
        /// Таймаут в миллисекундах (по умолчанию 5 минут).
        /// </summary>
        public int Timeout { get; set; } = 300000;

        protected override WebRequest GetWebRequest(Uri address)
        {
            var request = base.GetWebRequest(address);
            if (request != null)
                request.Timeout = Timeout;
            return request;
        }
    }
}