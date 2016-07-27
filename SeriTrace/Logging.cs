using System;
using System.Collections.Immutable;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Messaging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace SeriTrace
{
    [Serializable]
    public class Span
    {
        public Span(string traceId, string parentId, string spanId)
        {
            TraceId = traceId;
            ParentId = parentId;
            SpanId = spanId;
        }
        public string SpanId { get; }
        public string ParentId { get;}
        public string TraceId { get;  }
    }
    public static class Spans
    {
        private static readonly string Name = Guid.NewGuid().ToString("N");

       
        private static string Encode(Guid guid)
        {
            var enc = Convert.ToBase64String(guid.ToByteArray());
            return enc.Substring(0, 22);
        }

        private sealed class Wrapper : MarshalByRefObject
        {
            public ImmutableStack<Span> Value { get; set; }
        }

        private static ImmutableStack<Span> CurrentContext
        {
            get
            {
                var ret = CallContext.LogicalGetData(Name) as Wrapper;
                return ret == null ? CreateEmptyContext() : ret.Value;
            }

            set
            {
                CallContext.LogicalSetData(Name, new Wrapper { Value = value });
            }
        }

        private static ImmutableStack<Span> CreateEmptyContext()
        {
            var stack = ImmutableStack.Create<Span>();
            stack = stack.Push(new Span("root trace","","root span"));
            return stack;
        }

        public static IDisposable ContinueSpan(string traceId,string parentId,string spanId)
        {
            CurrentContext = CurrentContext.Push(new Span(traceId, parentId, spanId));
            Log.Information("Continuing Span");
            return new PopWhenDisposed();
        }

        public static IDisposable NewSpan([CallerMemberName] string context = "")
        {
            var spanId = $"{context}_{Encode(Guid.NewGuid())}";
            Log.Information("Start span");
            var current = Current;
            CurrentContext = CurrentContext.Push(new Span(current.TraceId,current.SpanId,spanId));
            return new PopWhenDisposed();
        }

        private static void Pop()
        {
            CurrentContext = CurrentContext.Pop();
        }

        public static Span Current => CurrentContext.Peek();

        private sealed class PopWhenDisposed : IDisposable
        {
            public PopWhenDisposed()
            {
                _start = DateTime.UtcNow;
            }
            private bool _disposed;
            private readonly DateTime _start;

            public void Dispose()
            {
                if (_disposed)
                    return;
                var duration = DateTime.UtcNow - _start;
                Log.Information("End span, Duration {elapsed} ms", duration.TotalMilliseconds);
                Pop();
                _disposed = true;
            }
        }
    }

    public class TraceEnricher : ILogEventEnricher
    {

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var span = Spans.Current;
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("TraceId", span.TraceId));
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("SpanId", span.SpanId));
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("ParentId", span.ParentId));
        }
    }

    public static class SerilogConfig
    {
        public static LoggerConfiguration GetConfiguration(string application)
        {
            var config = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.WithProperty("HostName", Dns.GetHostName())
                .Enrich.WithProperty("Application", application)
                .Enrich.WithProperty("ApplicationInstanceID", application + "_" + Dns.GetHostName())
                .Enrich.With<TraceEnricher>();
            return config;
        }
    }
}
