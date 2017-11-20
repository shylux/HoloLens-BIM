using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Assets.Scripts.Graph {
    public class Node<T> {

        private T data;
        private List<Node<T>> neighbours;

        public Node() { }
        public Node(T data) : this(data, new List<Node<T>>()) { }
        public Node(T data, List<Node<T>> neighbours) {
            this.data = data;
            this.neighbours = neighbours;
        }

        public void AddNeighbour(Node<T> node) {
            if (!neighbours.Contains(node))
                neighbours.Add(node);
        }

        public T Value {
            get {
                return data;
            }
        }

        public List<Node<T>> Neighbours {
            get {
                return neighbours;
            }

            private set {
                this.neighbours = value;
            }
        }

    }
}
