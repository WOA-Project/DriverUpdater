/*

Copyright (c) 2017-2021, The LumiaWOA Authors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/
using System;

namespace DriverUpdater
{
    internal static class Logging
    {
        public enum LoggingLevel
        {
            Information,
            Warning,
            Error
        }

        private static readonly object lockObj = new();

        public static void ShowProgress(long CurrentProgress, long TotalProgress, DateTime startTime, bool DisplayRed)
        {
            DateTime now = DateTime.Now;
            TimeSpan timeSoFar = now - startTime;

            TimeSpan remaining = new(0);

            try
            {
                remaining = TimeSpan.FromMilliseconds(timeSoFar.TotalMilliseconds / CurrentProgress * (TotalProgress - CurrentProgress));
            }
            catch { }

            if (TotalProgress == 0)
            {
                TotalProgress = 1;
                CurrentProgress = 1;
            }

            Log(string.Format("{0} {1:hh\\:mm\\:ss\\.f}", GetDismLikeProgBar((int)(CurrentProgress * 100 / TotalProgress)), remaining, remaining.TotalHours, remaining.Minutes, remaining.Seconds, remaining.Milliseconds), severity: DisplayRed ? LoggingLevel.Warning : LoggingLevel.Information, returnline: false);
        }

        private static string GetDismLikeProgBar(int perc)
        {
            int eqsLength = (int)((double)perc / 100 * 55);
            string bases = new string('=', eqsLength) + new string(' ', 55 - eqsLength);
            bases = bases.Insert(28, perc + "%");
            if (perc == 100)
            {
                bases = bases[1..];
            }
            else if (perc < 10)
            {
                bases = bases.Insert(28, " ");
            }

            return "[" + bases + "]";
        }

        public static void Log(string message, LoggingLevel severity = LoggingLevel.Information, bool returnline = true)
        {
            lock (lockObj)
            {
                if (message?.Length == 0)
                {
                    Console.WriteLine();
                    return;
                }

                string msg = "";

                switch (severity)
                {
                    case LoggingLevel.Warning:
                        msg = "  Warning  ";
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        break;
                    case LoggingLevel.Error:
                        msg = "   Error   ";
                        Console.ForegroundColor = ConsoleColor.Red;
                        break;
                    case LoggingLevel.Information:
                        msg = "Information";
                        Console.ForegroundColor = ConsoleColor.White;
                        break;
                }

                if (returnline)
                {
                    Console.WriteLine(DateTime.Now.ToString("'['HH':'mm':'ss']'") + "[" + msg + "] " + message);
                }
                else
                {
                    Console.Write("\r" + DateTime.Now.ToString("'['HH':'mm':'ss']'") + "[" + msg + "] " + message);
                }

                Console.ForegroundColor = ConsoleColor.White;
            }
        }
    }
}