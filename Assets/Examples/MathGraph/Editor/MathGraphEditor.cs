using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Schema;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using XNode.Examples;
using XNode.Examples.MathNodes;

namespace XNodeEditor.Examples
{
    [CustomNodeGraphEditor(typeof(MathGraph))]
    public class MathGraphEditor : NodeGraphEditor
    {

        /// <summary> 
        /// Overriding GetNodeMenuName lets you control if and how nodes are categorized.
        /// In this example we are sorting out all node types that are not in the XNode.Examples namespace.
        /// </summary>
        public override string GetNodeMenuName(System.Type type)
        {
            if (type.Namespace == "XNode.Examples.MathNodes")
            {
                return base.GetNodeMenuName(type).Replace("X Node/Examples/Math Nodes/", "");
            }
            else return null;
        }

        public override void AddContextMenuItems(GenericMenu menu)
        {
            base.AddContextMenuItems(menu);
            menu.AddItem(new GUIContent("Format selection"), false, () => AutoFormat(Selection.objects.Where(x => x is XNode.Node).Select(x => x as XNode.Node).ToList()));
            menu.AddItem(new GUIContent("Format all"), false, () => AutoFormat(this.target.nodes.ToList()));
        }

        private void AutoFormat(List<XNode.Node> nodes)
        {
            if (nodes.Count <= 1) return;

            // Depth first search
            // false = not visited
            // true = visited
            // Also used to test if a node should be visited
            Dictionary<XNode.Node, bool> nodesToVisit = nodes.ToDictionary(n => n, n => false);
            int visitedNodesCount = 0;

            // While we have not processed every node, run the breadth first search
            while (visitedNodesCount < nodes.Count)
            {
                List<XNode.Node> output = new List<XNode.Node>();

                Queue<XNode.Node> toProcess = new Queue<XNode.Node>();

                XNode.Node start = nodesToVisit.First(kvp => !kvp.Value).Key;

                nodesToVisit[start] = true;
                toProcess.Enqueue(start);

                while (toProcess.Count > 0)
                {
                    var node = toProcess.Dequeue();
                    output.Add(node);
                    visitedNodesCount++;

                    foreach (var connection in node.Ports.SelectMany(p => p.GetConnections())) // all ports instead of just the inputs
                    {
                        if (nodesToVisit.TryGetValue(connection.node, out bool visited) && !visited)
                        {
                            nodesToVisit[connection.node] = true;
                            toProcess.Enqueue(connection.node);
                        }
                    }
                }

                // Call the internal auto-format method
                AutoFormatConnectedNodes(output, GetBoundingBox(output));
            }
        }

        private Rect GetBoundingBox(List<XNode.Node> nodes)
        {
            Vector2 min = nodes.Select(n => n.position).Aggregate((a, b) => Vector2.Min(a, b));
            Vector2 max = nodes.Select(n => n.position + GetSize(n)).Aggregate((a, b) => Vector2.Max(a, b));
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private Vector2 GetSize(XNode.Node node)
        {
            if (window.nodeSizes.TryGetValue(node, out Vector2 size))
            {
                return size;
            }
            return Vector2.zero;
        }

        private void AutoFormatConnectedNodes(List<XNode.Node> nodes, Rect boundingBox)
        {
            if (nodes.Count <= 1) return;

            // Assumes that all nodes are connected

            //nodes[0].position
            //this.window.nodeSizes.TryGetValue(node, out Vector2 size)

            // Storing extra data for each node
            Dictionary<XNode.Node, GraphNode> graphNodes = nodes.ToDictionary(n => n, n => new GraphNode() { });

            // Find rightmost nodes (nodes that don't have any of our nodes to the right)
            var endNodes = nodes
                .Where(n => !n.Outputs.SelectMany(o => o.GetConnections()).Any(c => graphNodes.ContainsKey(c.node)))
                .ToList();
            Highlight(endNodes); // TODO: I might need better debug tooling

            // Longest path layering
            int maxLayer = 0;
            var nodesStack = new Stack<XNode.Node>(endNodes);
            while (nodesStack.Count > 0)
            {
                var node = nodesStack.Pop();
                var graphNode = graphNodes[node];

                foreach (var input in node.Inputs)
                {
                    if (input.Connection != null && graphNodes.TryGetValue(input.Connection.node, out var inputGraphNode))
                    {
                        inputGraphNode.Layer = Math.Max(inputGraphNode.Layer, graphNode.Layer + 1);
                        maxLayer = Math.Max(maxLayer, inputGraphNode.Layer);
                        nodesStack.Push(input.Connection.node);
                    }
                }
            }
            foreach (var n in graphNodes)
            {
                n.Key.position.x = (maxLayer - n.Value.Layer) * 250;
            }

            // Question: What sort of datastructure would be most useful in the last step? (Where I'm horizontally aligning nodes and moving them properly apart)

            // In the FlaxEngine, the Elements are usually sorted, because they are sorted in the NodeArchetype
            // Here, we probably can't rely on that to be true. So, I'll have to write something like foreach(var input in Sort(node.Inputs))

            //NodeEditor.portPositions

            // Order the input nodes
            /* int[] layerNodeCount = new int[maxLayer + 1];
             nodesStack = new Stack<XNode.Node>(endNodes);
             while (nodesStack.Count > 0)
             {
                 var node = nodesStack.Pop();
                 var graphNode = graphNodes[node];

                 foreach (var input in node.Inputs)
                 {
                     if (input.Connection != null && graphNodes.TryGetValue(input.Connection.node, out var inputGraphNode) && !inputGraphNode.Visited)
                     {
                         inputGraphNode.Order = layerNodeCount[inputGraphNode.Layer];
                         layerNodeCount[inputGraphNode.Layer] += 1;
                         inputGraphNode.Visited = true;

                         nodesStack.Push(input.Connection.node);
                     }
                 }
             }
             foreach (var n in graphNodes.Values)
             {
                 n.Visited = false;
             }
             foreach (var n in graphNodes)
             {
                 n.Key.position.y = n.Value.Order * 150;
             }*/

            // Element 0 of every layer gets horizontally aligned with the topmost endNode
            // 
            // We also have to know how large (pixels, not elements) the maximum layer is 
            // (note: since it's pixels and not elements, you cannot rely on the fact that the layer with the most elements is the largest one)


            // (Special step for main node: Move it to the center)
        }

        public override void OnGUI()
        {
            base.OnGUI();
            Handles.DrawLine(window.GridToWindowPositionNoClipped(Vector2.zero), window.GridToWindowPositionNoClipped(Vector2.one * 100));

        }


        private List<XNode.NodePort> Sort(IEnumerable<XNode.NodePort> nodePorts)
        {
            return nodePorts.OrderBy(p =>
            {
                if (NodeEditor.portPositions.TryGetValue(p, out Vector2 value))
                {
                    return value.y;
                }
                else
                {
                    return float.MinValue;
                }
            }).ToList();
        }

        private void Highlight(List<XNode.Node> nodes)
        {
            Selection.objects = new UnityEngine.Object[] { };
            foreach (var node in nodes)
            {
                window.SelectNode(node, true);
            }
        }

        private class GraphNode
        {
            /// <summary>
            /// Starting from 0 at the main nodes
            /// </summary>
            public int Layer;

            /// <summary>
            /// Order in a layer, starting from 0
            /// </summary>
            public int Order;

            /// <summary>
            /// If this node has been visited
            /// </summary>
            //public bool Visited;
        }
    }
}