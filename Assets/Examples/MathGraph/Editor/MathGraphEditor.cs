/*
This is free and unencumbered software released into the public domain.

Anyone is free to copy, modify, publish, use, compile, sell, or distribute this software, either in source code form or as a compiled binary, for any purpose, commercial or non-commercial, and by any means.

In jurisdictions that recognize copyright laws, the author or authors of this software dedicate any and all copyright interest in the software to the public domain. We make this dedication for the benefit of the public at large and to the detriment of our heirs and successors. We intend this dedication to be an overt act of relinquishment in perpetuity of all present and future rights to this software under copyright law.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

For more information, please refer to https://unlicense.org
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Schema;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.Graphs;
using UnityEngine;
using XNode.Examples;
using XNode.Examples.MathNodes;

namespace XNodeEditor.Examples
{
    /// <summary>
    /// Contains rather inefficient code, but clarity comes first.
    /// </summary>
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

            // Storing extra data for each node
            Dictionary<XNode.Node, GraphNode> graphNodes = nodes.ToDictionary(n => n, n => new GraphNode() { });

            // Find rightmost nodes (nodes that don't have any of our nodes to the right)
            // TODO: Sort the endNodes
            var endNodes = nodes
                .Where(n => !n.Outputs.SelectMany(o => o.GetConnections()).Any(c => graphNodes.ContainsKey(c.node)))
                .ToList();

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

            // Sorted child nodes, as a tree
            nodesStack = new Stack<XNode.Node>(endNodes);
            var visitedChildNodes = new HashSet<XNode.Node>();
            while (nodesStack.Count > 0)
            {
                var node = nodesStack.Pop();
                var graphNode = graphNodes[node];

                graphNode.ChildNodes = Sort(node.Inputs)
                    .SelectMany(input => input.GetConnections())
                    .Select(c => c.node)
                    .Where(n => graphNodes.ContainsKey(n) && !visitedChildNodes.Contains(n))
                    .ToList();

                foreach (var childNode in graphNode.ChildNodes)
                {
                    visitedChildNodes.Add(childNode);
                    nodesStack.Push(childNode);
                }
            }

            int[] offsets = new int[maxLayer + 1];
            void SetOffsets(XNode.Node node, int offset, XNode.Node straightParent)
            {
                var graphNode = graphNodes[node];

                if (offsets[graphNode.Layer] > offset)
                {
                    graphNodes[straightParent].SubtreeOffset = Math.Max(graphNodes[straightParent].SubtreeOffset, offsets[graphNode.Layer] - offset);
                }

                int childOffset = offset;
                bool firstIteration = true;
                foreach (var childNode in graphNode.ChildNodes)
                {
                    SetOffsets(childNode, childOffset, firstIteration ? straightParent : childNode);
                    childOffset = graphNodes[childNode].Offset + 1;

                    firstIteration = false;
                }

                graphNode.Offset = offset + graphNodes[straightParent].SubtreeOffset;
                offsets[graphNode.Layer] = graphNode.Offset + 1;
            }

            // TODO: Do it for all endNodes
            SetOffsets(endNodes.First(), 0, endNodes.First());

            // TODO: Better node spacing (figure out how much space the biggest node in this row takes up and make the row a bit bigger than that)
            foreach (var n in graphNodes)
            {
                n.Key.position.y = n.Value.Offset * 250;
            }

            //nodes[0].position
            //this.window.nodeSizes.TryGetValue(node, out Vector2 size)

            //NodeEditor.portPositions
        }

        public override void OnGUI()
        {
            base.OnGUI();
            //Handles.DrawLine(window.GridToWindowPositionNoClipped(Vector2.zero), window.GridToWindowPositionNoClipped(Vector2.one * 100));
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

        // TODO: Rename this?
        private class GraphNode
        {
            /// <summary>
            /// Starting from 0 at the main nodes
            /// </summary>
            public int Layer;

            /// <summary>
            /// Child node cache
            /// </summary>
            /// <remarks>
            /// Only contains selected nodes. Also pretends that we have a tree instead of a graph.
            /// </remarks>
            public List<XNode.Node> ChildNodes;

            /// <summary>
            /// Position in the layer
            /// </summary>
            public int Offset;

            /// <summary>
            /// How far the subtree needs to be moved additionally
            /// </summary>
            public int SubtreeOffset;
        }
    }
}
