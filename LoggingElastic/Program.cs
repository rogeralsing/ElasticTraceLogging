using System;
using System.Threading.Tasks;
using ElasticTrace;
using Serilog;
using Serilog.Sinks.Elasticsearch;

namespace LoggingElastic
{

    internal class MyData
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }

    internal class Program
    {
        private static void Main(string[] args)
        {
            var config = SerilogConfig.GetConfiguration("MyService");

            Log.Logger = config
             //   .WriteTo.ColoredConsole()
                .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new[] {new Uri("http://172.30.110.43:9200/")})
                {
                    AutoRegisterTemplate = true,
                    BatchPostingLimit = 1000,
                })
                .CreateLogger();

            FakeEndpoint("123","ParentSpan123","Span456").Wait();
            Console.ReadLine();
        }

        private static async Task FakeEndpoint(string traceId,string parentId,string spanId)
        {
            using (Spans.ContinueSpan(traceId, parentId, spanId))
            {                
                for (var i = 0; i < 300000; i++)
                {
                    await DoStuff();
                }
            }
        }

        private static async Task DoStuff()
        {
            using (Spans.NewSpan())
            {
                await Task.Delay(new Random().Next(500));
                Log.Warning("Inside DoStuff");
                //       await Task.Delay(1);
                await DoMoreStuff();
            }
        }

        private static async Task DoMoreStuff()
        {
            using (Spans.NewSpan())
            {
                //     await Task.Yield();
                Log.Warning("Inside DoMoreStuff");
            }
        }
    }
}