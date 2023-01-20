using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UseDiagnosticSource
{
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
