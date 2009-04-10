﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using QuickGraph.Contracts;
using QuickGraph.Collections;

namespace QuickGraph
{
    public delegate bool EdgeEqualityComporer<TVertex, TEdge>(TEdge edge, TVertex source, TVertex target)
        where TEdge : IEdge<TVertex>;

#if !SILVERLIGHT
    [Serializable]
#endif
    [DebuggerDisplay("VertexCount = {VertexCount}, EdgeCount = {EdgeCount}")]
    public class UndirectedGraph<TVertex, TEdge> 
        : IMutableUndirectedGraph<TVertex,TEdge>
        where TEdge : IEdge<TVertex>
    {
        private readonly bool allowParallelEdges = true;
        private readonly VertexEdgeDictionary<TVertex, TEdge> adjacentEdges =
            new VertexEdgeDictionary<TVertex, TEdge>();
        private readonly EdgeEqualityComporer<TVertex, TEdge> edgeEqualityComparer;
        private int edgeCount = 0;
        private int edgeCapacity = 4;

        public UndirectedGraph(bool allowParallelEdges, EdgeEqualityComporer<TVertex, TEdge> edgeEqualityComparer)
        {
            Contract.Requires(edgeEqualityComparer != null);

            this.allowParallelEdges = allowParallelEdges;
            this.edgeEqualityComparer = edgeEqualityComparer;
        }

        public UndirectedGraph(bool allowParallelEdges)
            :this(allowParallelEdges, EdgeExtensions.GetUndirectedVertexEquality<TVertex, TEdge>())
        {
            this.allowParallelEdges = allowParallelEdges;
        }

        public UndirectedGraph()
            :this(true)
        {}

        public EdgeEqualityComporer<TVertex, TEdge> EdgeEqualityComparer
        {
            get { return this.edgeEqualityComparer;}
        }

        public int EdgeCapacity
        {
            get { return this.edgeCapacity; }
            set { this.edgeCapacity = value; }
        }
    
        #region IGraph<Vertex,Edge> Members
        public bool  IsDirected
        {
        	get { return false; }
        }

        public bool  AllowParallelEdges
        {
        	get { return this.allowParallelEdges; }
        }
        #endregion

        #region IMutableUndirected<Vertex,Edge> Members
        public event VertexAction<TVertex> VertexAdded;
        protected virtual void OnVertexAdded(TVertex args)
        {
            Contract.Requires(args != null);

            var eh = this.VertexAdded;
            if (eh != null)
                eh(args);
        }

        public int AddVertexRange(IEnumerable<TVertex> vertices)
        {
            int count = 0;
            foreach (var v in vertices)
                if (this.AddVertex(v))
                    count++;
            return count;
        }

        public bool AddVertex(TVertex v)
        {
            if (this.ContainsVertex(v))
                return false;

            var edges = this.EdgeCapacity < 0 
                ? new EdgeList<TVertex, TEdge>() 
                : new EdgeList<TVertex, TEdge>(this.EdgeCapacity);
            this.adjacentEdges.Add(v, edges);
            this.OnVertexAdded(v);
            return true;
        }

        private List<TEdge> AddAndReturnEdges(TVertex v)
        {
            EdgeList<TVertex, TEdge> edges;
            if (!this.adjacentEdges.TryGetValue(v, out edges))
                this.adjacentEdges[v] = edges = this.EdgeCapacity < 0 
                    ? new EdgeList<TVertex, TEdge>() 
                    : new EdgeList<TVertex, TEdge>(this.EdgeCapacity);

            return edges;
        }

        public event VertexAction<TVertex> VertexRemoved;
        protected virtual void OnVertexRemoved(TVertex args)
        {
            Contract.Requires(args != null);

            var eh = this.VertexRemoved;
            if (eh != null)
                eh(args);
        }

        public bool RemoveVertex(TVertex v)
        {
            this.ClearAdjacentEdges(v);
            bool result = this.adjacentEdges.Remove(v);

            if (result)
                this.OnVertexRemoved(v);

            return result;
        }

        public int RemoveVertexIf(VertexPredicate<TVertex> pred)
        {
            List<TVertex> vertices = new List<TVertex>();
            foreach (var v in this.Vertices)
                if (pred(v))
                    vertices.Add(v);

            foreach (var v in vertices)
                RemoveVertex(v);
            return vertices.Count;
        }
        #endregion

        #region IMutableIncidenceGraph<Vertex,Edge> Members
        public int RemoveAdjacentEdgeIf(TVertex v, EdgePredicate<TVertex, TEdge> predicate)
        {
            var outEdges = this.adjacentEdges[v];
            var edges = new List<TEdge>(outEdges.Count);
            foreach (var edge in outEdges)
                if (predicate(edge))
                    edges.Add(edge);

            this.RemoveEdges(edges);
            return edges.Count;
        }

        [ContractInvariantMethod]
        protected void ObjectInvariant()
        {
            Contract.Invariant(this.edgeCount >= 0);
        }

        public void ClearAdjacentEdges(TVertex v)
        {
            var edges = this.adjacentEdges[v].Clone();
            this.edgeCount -= edges.Count;

            foreach (var edge in edges)
            {
                EdgeList<TVertex, TEdge> aEdges;
                if (this.adjacentEdges.TryGetValue(edge.Target, out aEdges))
                    aEdges.Remove(edge);
                if (this.adjacentEdges.TryGetValue(edge.Source, out aEdges))
                    aEdges.Remove(edge);
            }
        }
        #endregion

        #region IMutableGraph<Vertex,Edge> Members
        public void TrimEdgeExcess()
        {
            foreach (var edges in this.adjacentEdges.Values)
                edges.TrimExcess();
        }

        public void Clear()
        {
            this.adjacentEdges.Clear();
            this.edgeCount = 0;
        }
        #endregion

        #region IUndirectedGraph<Vertex,Edge> Members

        [Pure]
        public bool ContainsEdge(TVertex source, TVertex target)
        {
            foreach(var edge in this.AdjacentEdges(source))
            {
                if (this.edgeEqualityComparer(edge, source, target))
                    return true;
            }
            return false;
        }

        [Pure]
        public TEdge AdjacentEdge(TVertex v, int index)
        {
            return this.adjacentEdges[v][index];
        }

        public bool IsVerticesEmpty
        {
            get { return this.adjacentEdges.Count == 0; }
        }

        public int VertexCount
        {
            get { return this.adjacentEdges.Count; }
        }

        public IEnumerable<TVertex> Vertices
        {
            get { return this.adjacentEdges.Keys; }
        }


        [Pure]
        public bool ContainsVertex(TVertex vertex)
        {
            return this.adjacentEdges.ContainsKey(vertex);
        }
        #endregion

        #region IMutableEdgeListGraph<Vertex,Edge> Members
        public bool AddVerticesAndEdge(TEdge edge)
        {
            var sourceEdges = this.AddAndReturnEdges(edge.Source);
            var targetEdges = this.AddAndReturnEdges(edge.Target);

            if (!this.AllowParallelEdges)
            {
                if (this.ContainsEdgeBetweenVertices(sourceEdges, edge))
                    return false;
            }

            sourceEdges.Add(edge);
            targetEdges.Add(edge);
            this.edgeCount++;

            this.OnEdgeAdded(edge);

            return true;
        }

        public int AddVerticesAndEdgeRange(IEnumerable<TEdge> edges)
        {
            int count = 0;
            foreach (var edge in edges)
                if (this.AddVerticesAndEdge(edge))
                    count++;
            return count;
        }

        public bool AddEdge(TEdge edge)
        {
            var sourceEdges = this.adjacentEdges[edge.Source];
            if (!this.AllowParallelEdges)
            {
                if (this.ContainsEdgeBetweenVertices(sourceEdges, edge))
                    return false;
            }
            var targetEdges = this.adjacentEdges[edge.Target];

            sourceEdges.Add(edge);
            targetEdges.Add(edge);
            this.edgeCount++;

            this.OnEdgeAdded(edge);

            return true;
        }

        public int AddEdgeRange(IEnumerable<TEdge> edges)
        {
            int count = 0;
            foreach (var edge in edges)
                if (this.AddEdge(edge))
                    count++;
            return count;
        }

        public event EdgeAction<TVertex, TEdge> EdgeAdded;
        protected virtual void OnEdgeAdded(TEdge args)
        {
            var eh = this.EdgeAdded;
            if (eh != null)
                eh(args);
        }

        public bool RemoveEdge(TEdge edge)
        {
            this.adjacentEdges[edge.Source].Remove(edge);
            if (this.adjacentEdges[edge.Target].Remove(edge))
            {
                this.edgeCount--;
                Contract.Assert(this.edgeCount >= 0);
                this.OnEdgeRemoved(edge);
                return true;
            }
            else
                return false;
        }

        public event EdgeAction<TVertex, TEdge> EdgeRemoved;
        protected virtual void OnEdgeRemoved(TEdge args)
        {
            var eh = this.EdgeRemoved;
            if (eh != null)
                eh(args);
        }

        public int RemoveEdgeIf(EdgePredicate<TVertex, TEdge> predicate)
        {
            List<TEdge> edges = new List<TEdge>();
            foreach (var edge in this.Edges)
            {
                if (predicate(edge))
                    edges.Add(edge);
            }
            return this.RemoveEdges(edges);
        }

        public int RemoveEdges(IEnumerable<TEdge> edges)
        {
            int count = 0;
            foreach (var edge in edges)
            {
                if (RemoveEdge(edge))
                    count++;
            }
            return count;
        }
        #endregion

        #region IEdgeListGraph<Vertex,Edge> Members
        public bool IsEdgesEmpty
        {
            get { return this.EdgeCount==0; }
        }

        public int EdgeCount
        {
            get { return this.edgeCount; }
        }

        public IEnumerable<TEdge> Edges
        {
            get 
            {
                var edgeColors = new Dictionary<TEdge, GraphColor>(this.EdgeCount);
                foreach (var edges in this.adjacentEdges.Values)
                {
                    foreach(TEdge edge in edges)
                    {
                        GraphColor c;
                        if (edgeColors.TryGetValue(edge, out c))
                            continue;
                        edgeColors.Add(edge, GraphColor.Black);
                        yield return edge;
                    }
                }
            }
        }

        [Pure]
        public bool ContainsEdge(TEdge edge)
        {
            foreach (var e in this.Edges)
                if (e.Equals(edge))
                    return true;
            return false;
        }

        private bool ContainsEdgeBetweenVertices(IEnumerable<TEdge> edges, TEdge edge)
        {
            Contract.Requires(edges != null);
            Contract.Requires(edge != null);

            var source = edge.Source;
            var target= edge.Target;
            foreach (var e in edges)
                if (this.EdgeEqualityComparer(e,source, target))
                    return true;
            return false;
        }
        #endregion

        #region IUndirectedGraph<Vertex,Edge> Members

        [Pure]
        public IEnumerable<TEdge> AdjacentEdges(TVertex v)
        {
            return this.adjacentEdges[v];
        }

        [Pure]
        public int AdjacentDegree(TVertex v)
        {
            return this.adjacentEdges[v].Count;
        }

        [Pure]
        public bool IsAdjacentEdgesEmpty(TVertex v)
        {
            return this.adjacentEdges[v].Count == 0;
        }

        #endregion
    }
}
