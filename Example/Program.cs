using ISchemm.NLog.JsonTarget;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Example {
    class Program {
        static void Main(string[] args) {
            Logger logger = LogManager.GetCurrentClassLogger();
            LogManager.ThrowExceptions = true;

            JsonPostTarget.DefaultUrl = "http://localhost:51478/Log/Post";

            logger.Debug("Debug message");
            logger.Warn("Warning message");
            Thread.Sleep(2000);

            try {
                throw new Exception("Test exception");
            } catch (Exception e) {
                logger.Error(e);
            }
            Thread.Sleep(1000);

            logger.Trace("Trace message");
            logger.Info("Info message");

            Console.WriteLine("Complete");
            Thread.Sleep(1000);
        }
    }
}
