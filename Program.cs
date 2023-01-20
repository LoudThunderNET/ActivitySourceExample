using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UseDiagnosticSource
{
    class Program
    {
        static TraceBuilder TraceBuilder = new TraceBuilder();

        static async Task Main(string[] args)
        {
            var activityListener = new ActivityListener()
            {
                ShouldListenTo = src => 
                {
                    return src.Name == "Source";
                },
                ActivityStopped = activity =>
                {
                    TraceBuilder.AddNode(activity.Id, activity.OperationName, activity.ParentId, activity.Duration);
                },
                SampleUsingParentId = (ref ActivityCreationOptions<string> activityOptions) => ActivitySamplingResult.AllData,
                Sample = (ref ActivityCreationOptions<ActivityContext> activityOptions) => ActivitySamplingResult.AllData
            };
            ActivitySource.AddActivityListener(activityListener);

            var activitySource = new ActivitySource("Source", "1.0.0");
            using (var rootActivity = activitySource.StartActivity("RootMethod", ActivityKind.Internal))
            {
                using (var activity = activitySource.StartActivity("Method1", ActivityKind.Internal))
                {
                    activity.AddBaggage("param1", "value1");
                    await Task.Delay(TimeSpan.FromMilliseconds(2500));
                }

                using (var activity = activitySource.StartActivity("Method2", ActivityKind.Internal))
                {
                    activity.AddBaggage("request1", "null");
                    await Task.Delay(TimeSpan.FromMilliseconds(2500));
                }
            }

            var jsonSerializer = JsonSerializer.Serialize(TraceBuilder.Build(), new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true
            });

            Console.WriteLine(jsonSerializer);
            Console.WriteLine("Press any key ...");
            Console.ReadKey();
        }
    }
}
