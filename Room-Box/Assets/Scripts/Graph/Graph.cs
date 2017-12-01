using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Assets.Scripts.Graph {
    public struct Graph<K, T> {

        private Func<T, K> idxFunc;
        private Dictionary<K, Node<T>> nodes;

        public Graph(Func<T, K> idxFunc) : this(idxFunc, null) { }
        public Graph(Func<T, K> idxFunc, IEqualityComparer<K> comparer) {
            this.idxFunc = idxFunc;
            this.nodes = new Dictionary<K, Node<T>>(comparer);
        }

        public Node<T> AddNode(T value) {
            Node<T> node;

            if (!nodes.ContainsKey(idxFunc.Invoke(value))) {
                node = new Node<T>(value);
                nodes.Add(idxFunc(node.Value), node);
            } else {
                nodes.TryGetValue(idxFunc.Invoke(value), out node);
            }

            return node;
        }

        public List<Node<T>> Nodes {
            get {
                return nodes.Values.ToList();
            }
        }

        public void AddUndirectedEdge(Node<T> from, Node<T> to) {
            from.AddNeighbour(to);
            to.AddNeighbour(from);
        }

        public int Count {
            get {
                return nodes.Count;
            }
        }
    }
}
