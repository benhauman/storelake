namespace StoreLake.Sdk.Cli
{
    using System;
    using System.Diagnostics;
    using System.Globalization;

    internal sealed class ColoredConsoleScope : IDisposable
    {
        private ConsoleColor oldBackgroundColor = Console.BackgroundColor;
        private ConsoleColor oldForegroundColor = Console.ForegroundColor;

        internal ColoredConsoleScope(ConsoleColor background, ConsoleColor foreground)
        {
            Console.BackgroundColor = background;
            Console.ForegroundColor = foreground;
        }

        public void Dispose()
        {
            Console.BackgroundColor = oldBackgroundColor;
            Console.ForegroundColor = oldForegroundColor;
            GC.SuppressFinalize(this);
        }
    }

    internal sealed class ColoredConsoleTraceListener : ConsoleTraceListener
    {
        public ColoredConsoleTraceListener(bool useErrorStream) : base(useErrorStream)
        {
        }

        public override void Write(string message)
        {
            OnBeginWrite();
            base.Write(message);
        }

        public override void Write(object o, string category)
        {
            OnBeginWrite();
            base.Write(o, category);
        }

        public override void WriteLine(string message, string category)
        {
            OnBeginWrite();
            base.WriteLine(message, category);
        }
        public override void WriteLine(object o)
        {
            OnBeginWrite();
            base.WriteLine(o);
        }
        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id)
        {
            base.TraceEvent(eventCache, source, eventType, id);
        }
        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
        {
            if (Filter == null || Filter.ShouldTrace(eventCache, source, eventType, id, format, args, null, null))
            {
                WriteTextLineCore(eventType, () =>
                {
                    //WriteHeader(source, eventType, id);
                    if (args != null)
                    {
                        WriteLine(string.Format(CultureInfo.InvariantCulture, format, args));
                    }
                    else
                    {
                        WriteLine(format);
                    }
                    //WriteFooter(eventCache);
                });
            }
        }
        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            WriteTextLineCore(eventType, () =>
            {
                WriteTextLineCore(eventType, () =>
                {
                    base.WriteLine(message);
                });
                //base.TraceEvent(eventCache, source, eventType, id, message);
            });
        }

        public override void TraceData(TraceEventCache eventCache, string source, TraceEventType eventType, int id, object data)
        {
            WriteTextLineCore(eventType, () =>
            {
                base.TraceData(eventCache, source, eventType, id, data);
            });
        }

        public override void TraceData(TraceEventCache eventCache, string source, TraceEventType eventType, int id, params object[] data)
        {
            WriteTextLineCore(eventType, () =>
            {
                base.TraceData(eventCache, source, eventType, id, data);
            });
        }

        private static Func<ColoredConsoleScope> Nop = () => null;
        private static Func<ColoredConsoleScope> Inf = () => new ColoredConsoleScope(ConsoleColor.Black, ConsoleColor.White);
        private static Func<ColoredConsoleScope> Wrn = () => new ColoredConsoleScope(ConsoleColor.Yellow, ConsoleColor.Black);
        private static Func<ColoredConsoleScope> Err = () => new ColoredConsoleScope(ConsoleColor.Red, ConsoleColor.White);
        private void WriteTextLineCore(TraceEventType eventType, Action handler)
        {
            Func<ColoredConsoleScope> how;
            if (eventType == TraceEventType.Information)
            {
                how = Inf;
            }
            else if (eventType == TraceEventType.Warning)
            {
                how = Wrn;
            }
            else if (eventType == TraceEventType.Error)
            {
                how = Err;
            }
            else
            {
                how = Nop;
            }

            using (ColoredConsoleScope ccs = how())
            {
                handler();
            }
        }

        public override void WriteLine(object o, string category)
        {
            OnBeginWrite();
            base.WriteLine(o, category);
        }

        public override void Write(object o)
        {
            base.Write(o);
        }

        public override void Write(string message, string category)
        {
            OnBeginWrite(); ;
            base.Write(message, category);
        }

        private void OnBeginWrite()
        {
        }

        public override void Fail(string message)
        {
            base.Fail(message);
        }

        public override void Fail(string message, string detailMessage)
        {
            base.Fail(message, detailMessage);
        }
    }
}
