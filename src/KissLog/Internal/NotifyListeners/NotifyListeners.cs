﻿using KissLog.Web;
using System;

namespace KissLog.Internal
{
    public static class NotifyListeners
    {
        public static void NotifyFlush(ILogger[] loggers)
        {
            try
            {
                NotifyOnFlushService.Notify(loggers);
            }
            catch(Exception ex)
            {
                InternalHelpers.Log(ex.ToString(), LogLevel.Error);
            }
        }

        public static void NotifyBeginRequest(KissLog.Web.HttpRequest httpRequest, Logger logger)
        {
            try
            {
                NotifyOnBeginRequestService.Notify(httpRequest, logger);
            }
            catch(Exception ex)
            {
                InternalHelpers.Log(ex.ToString(), LogLevel.Error);
            }
        }

        public static void NotifyMessage(LogMessage message, Logger logger)
        {
            try
            {
                NotifyOnMessageService.Notify(message, logger);
            }
            catch(Exception ex)
            {
                InternalHelpers.Log(ex.ToString(), LogLevel.Error);
            }
        }
    }
}
