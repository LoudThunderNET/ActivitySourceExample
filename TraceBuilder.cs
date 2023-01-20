using System;
using System.Collections.Generic;
using System.Linq;

namespace UseDiagnosticSource
{
    class TraceBuilder
    {
        private readonly object _sync = new object();
        private ICollection<Node> _traces = new List<Node>();

        public Node AddNode(string id, string name, string parentId, TimeSpan timeSpan)
        {
            lock (_sync)
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
        }

        public ICollection<Node> Build() => _traces;
    }
}
