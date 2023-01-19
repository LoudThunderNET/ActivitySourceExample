using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UseDiagnosticSource
{
    class Program
    {
        const string DiagnosticSourceName = "TestSource";
        static DiagnosticSource ds = new DiagnosticListener(DiagnosticSourceName);
        static ConcurrentDictionary<string, TimeSpan> Baggage = new ConcurrentDictionary<string, TimeSpan>();
        static TraceBuilder TraceBuilder = new TraceBuilder();

        static async Task Main(string[] args)
        {
            var al = new ActivityListener()
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
            ActivitySource.AddActivityListener(al);

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
            var jsonSerializer = JsonSerializer.Serialize(TraceBuilder.Traces, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true
            });
            Console.WriteLine(jsonSerializer);
        }
    }

    class TraceBuilder
    {
        private ICollection<Node> _traces = new List<Node>();

        public Node AddNode(string id, string name, string parentId, TimeSpan timeSpan)
        {
            var collection = _traces;
            if (!string.IsNullOrEmpty(parentId))
            {
                var parent = _traces.FirstOrDefault(n => n.Id == parentId);
                if (parent == null)
                {
                    parent = new Node(parentId, string.Empty, null, TimeSpan.Zero);
                    collection.Add(parent);
                }
                parent.Children = parent.Children ?? new List<Node>();
                collection = parent.Children;
            }
            else
            { 
                var lNode = _traces.FirstOrDefault(n => n.Id == id);
                if (lNode != null)
                {
                    lNode.Name = name;
                    lNode.Duration = timeSpan;

                    return lNode;
                }
            }
            var node = new Node(id, name, parentId, timeSpan);
            collection.Add(node);

            return node;
        }

        public ICollection<Node> Traces => _traces;
    }

    class Node
    {
        public Node(string id, string name, string parentId, TimeSpan duration)
        {
            Id = id;
            ParentId = parentId;
            Duration = duration;
            Name = name;
        }

        [JsonIgnore]
        public string Id { get; set; }

        [JsonIgnore]
        public string ParentId { get; set; }

        [JsonPropertyName("Method")]
        public string Name { get; set; }

        public TimeSpan Duration { get; set; }
        public ICollection<Node> Children { get; set; }
    }
}
