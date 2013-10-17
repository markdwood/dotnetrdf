/*
dotNetRDF is free and open source software licensed under the MIT License

-----------------------------------------------------------------------------

Copyright (c) 2009-2012 dotNetRDF Project (dotnetrdf-developer@lists.sf.net)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is furnished
to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
#if !NO_DATA
using System.Data;
#endif
using System.Linq;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using VDS.Common.Collections;
using VDS.RDF.Collections;
using VDS.RDF.Namespaces;
using VDS.RDF.Nodes;
using VDS.RDF.Parsing;
#if !SILVERLIGHT
using VDS.RDF.Writing.Serialization;
#endif

namespace VDS.RDF.Graphs
{
    /// <summary>
    /// Abstract Base Implementation of the <see cref="IGraph">IGraph</see> interface
    /// </summary>
#if !SILVERLIGHT
    [Serializable,XmlRoot(ElementName="graph")]
#endif
    public abstract class BaseGraph 
        : IGraph
#if !SILVERLIGHT
        ,ISerializable
#endif
    {
        #region Member Variables

        /// <summary>
        /// Collection of Triples in the Graph
        /// </summary>
        protected ITripleCollection _triples;
        /// <summary>
        /// Namespace Mapper
        /// </summary>
        protected readonly NamespaceMapper _nsmapper;

        /// <summary>
        /// Mapping from String IDs to GUIDs for Blank Nodes
        /// </summary>
        protected readonly MultiDictionary<String, Guid> _bnodes = new MultiDictionary<string, Guid>();

        private TripleEventHandler TripleAddedHandler, TripleRemovedHandler;
#if !SILVERLIGHT
        private GraphDeserializationInfo _dsInfo;
#endif

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new Base Graph using the given Triple Collection
        /// </summary>
        /// <param name="tripleCollection">Triple Collection to use</param>
        protected BaseGraph(ITripleCollection tripleCollection)
        {
            this._triples = tripleCollection;
            this._nsmapper = new NamespaceMapper();

            //Create Event Handlers and attach to the Triple Collection
            this.TripleAddedHandler = new TripleEventHandler(this.OnTripleAsserted);
            this.TripleRemovedHandler = new TripleEventHandler(this.OnTripleRetracted);
            this.AttachEventHandlers(this._triples);
        }

        /// <summary>
        /// Creates a new Base Graph which uses the default <see cref="TreeIndexedTripleCollection" /> as the Triple Collection
        /// </summary>
        protected BaseGraph()
            : this(new TreeIndexedTripleCollection()) { }

#if !SILVERLIGHT
        /// <summary>
        /// Creates a Graph from the given Serialization Information
        /// </summary>
        /// <param name="info">Serialization Information</param>
        /// <param name="context">Streaming Context</param>
        protected BaseGraph(SerializationInfo info, StreamingContext context)
            : this()
        {
            this._dsInfo = new GraphDeserializationInfo(info, context);   
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (this._dsInfo != null) this._dsInfo.Apply(this);
        }
#endif

        #endregion

        #region Properties

        /// <summary>
        /// Gets the set of Triples in this Graph
        /// </summary>
        public virtual IEnumerable<Triple> Triples
        {
            get
            {
                return this._triples;
            }
        }

        /// <summary>
        /// Gets the set of Quads in the graph
        /// </summary>
        /// <remarks>
        /// Since a graph has no name directly associated with it the resulting quads will have the null name assigned to them and so will appears as if in the default unnamed graph
        /// </remarks>
        public virtual IEnumerable<Quad> Quads
        {
            get
            {
                return this._triples.Select(t => t.AsQuad(null));
            }
        }

        /// <summary>
        /// Gets the nodes that are used as vertices in the graph i.e. those which occur in the subject or object position of a triple
        /// </summary>
        public virtual IEnumerable<INode> Vertices
        {
            get
            {
                return (from t in this._triples
                        select t.Subject).Concat(from t in this._triples
                                                 select t.Object).Distinct();
            }
        }

        /// <summary>
        /// Gets the nodes that are used as edges in the graph i.e. those which occur in the predicate position of a triple
        /// </summary>
        public virtual IEnumerable<INode> Edges
        {
            get
            {
                return (from t in this._triples
                        select t.Predicate).Distinct();
            }
        }

        /// <summary>
        /// Gets the Namespace Mapper for this Graph which contains all in use Namespace Prefixes and their URIs
        /// </summary>
        /// <returns></returns>
        public virtual INamespaceMapper Namespaces
        {
            get
            {
                return this._nsmapper;
            }
        }

        /// <summary>
        /// Gets the number of triples in the graph
        /// </summary>
        public virtual long Count
        {
            get
            {
                return this._triples.Count;
            }
        }

        /// <summary>
        /// Gets whether a Graph is Empty ie. Contains No Triples or Nodes
        /// </summary>
        public virtual bool IsEmpty
        {
            get
            {
                return (this._triples.Count == 0);
            }
        }

        #endregion

        #region Triple Assertion & Retraction

        /// <summary>
        /// Asserts a Triple in the Graph
        /// </summary>
        /// <param name="t">The Triple to add to the Graph</param>
        public abstract void Assert(Triple t);

        /// <summary>
        /// Asserts a List of Triples in the graph
        /// </summary>
        /// <param name="ts">List of Triples in the form of an IEnumerable</param>
        public abstract void Assert(IEnumerable<Triple> ts);

        /// <summary>
        /// Retracts a Triple from the Graph
        /// </summary>
        /// <param name="t">Triple to Retract</param>
        /// <remarks>Current implementation may have some defunct Nodes left in the Graph as only the Triple is retracted</remarks>
        public abstract void Retract(Triple t);

        /// <summary>
        /// Retracts a enumeration of Triples from the graph
        /// </summary>
        /// <param name="ts">Enumeration of Triples to retract</param>
        public abstract void Retract(IEnumerable<Triple> ts);

        /// <summary>
        /// Clears all Triples from the Graph
        /// </summary>
        /// <remarks>
        /// <para>
        /// The Graph will raise the <see cref="ClearRequested">ClearRequested</see> event at the start of the Clear operation which allows for aborting the operation if the operation is cancelled by an event handler.  On completing the Clear the <see cref="Cleared">Cleared</see> event will be raised.
        /// </para>
        /// </remarks>
        public virtual void Clear()
        {
            if (!this.RaiseClearRequested()) return;

            this.Retract(this.Triples.ToList());

            this.RaiseCleared();
        }

        #endregion

        #region Node Creation

        /// <summary>
        /// Creates a new blank node with an auto-generated ID
        /// </summary>
        /// <returns></returns>
        public virtual INode CreateBlankNode()
        {
            return new BlankNode(Guid.NewGuid());
        }

        /// <summary>
        /// Creates a new literal node with the given Value
        /// </summary>
        /// <param name="literal">String value of the Literal</param>
        /// <returns></returns>
        public virtual INode CreateLiteralNode(String literal)
        {
            return new LiteralNode(literal);
        }

        /// <summary>
        /// Creates a new literal node with the given value and language specifier
        /// </summary>
        /// <param name="literal">String value of the Literal</param>
        /// <param name="langspec">Language Specifier of the Literal</param>
        /// <returns></returns>
        public virtual INode CreateLiteralNode(String literal, String langspec)
        {
            return new LiteralNode(literal, langspec);
        }

        /// <summary>
        /// Creates a new literal node with the given value and data type
        /// </summary>
        /// <param name="literal">String value of the Literal</param>
        /// <param name="datatype">URI of the Data Type</param>
        /// <returns></returns>
        public virtual INode CreateLiteralNode(String literal, Uri datatype)
        {
            return new LiteralNode(literal, datatype);
        }

        /// <summary>
        /// Creates a new URI Node with the given URI
        /// </summary>
        /// <param name="uri">URI for the Node</param>
        /// <returns></returns>
        /// <remarks>
        /// Generally we expect to be passed an absolute URI, while relative URIs are permitted the behaviour is less well defined. In the case of relative URIs issues may occur when trying to serialize the data or when accurate round tripping is required.
        /// </remarks>
        public virtual INode CreateUriNode(Uri uri)
        {
            return new UriNode(uri);
        }

        /// <summary>
        /// Creates a new URI Node with the given QName
        /// </summary>
        /// <param name="qname">QName for the Node</param>
        /// <returns></returns>
        /// <remarks>Internally the Graph will resolve the QName to a full URI, this throws an exception when this is not possible</remarks>
        public virtual INode CreateUriNode(String qname)
        {
            return new UriNode(UriFactory.Create(Tools.ResolveQName(qname, this._nsmapper, null)));
        }

        /// <summary>
        /// Creates a new Variable Node
        /// </summary>
        /// <param name="varname">Variable Name</param>
        /// <returns></returns>
        public virtual INode CreateVariableNode(String varname)
        {
            return new VariableNode(varname);
        }

        /// <summary>
        /// Creates a new Graph Literal Node with its value being an Empty Subgraph
        /// </summary>
        /// <returns></returns>
        public virtual INode CreateGraphLiteralNode()
        {
            return new GraphLiteralNode(new Graph());
        }

        /// <summary>
        /// Creates a new Graph Literal Node with its value being the given Subgraph
        /// </summary>
        /// <param name="subgraph">Subgraph this Node represents</param>
        /// <returns></returns>
        public virtual INode CreateGraphLiteralNode(IGraph subgraph)
        {
            return new GraphLiteralNode(subgraph);
        }

        #endregion

        #region Triple Selection

        public virtual IEnumerable<Triple> Find(INode s, INode p, INode o)
        {
            if (ReferenceEquals(s, null))
            {
                // Wildcard Subject
                if (ReferenceEquals(p, null))
                {
                    // Wildcard Subject and Predicate
                    if (ReferenceEquals(o, null))
                    {
                        // Wildcard Subject, Predicate and Object
                        return this.Triples;
                    }
                    else
                    {
                        // Wildcard Subject and Predicate with Fixed Object
                        return this._triples.WithObject(o);
                    }
                }
                else
                {
                    // Fixed Predicate with Wildcard Subject
                    if (ReferenceEquals(o, null))
                    {
                        // Fixed Predicate with Wildcard Subject and Object
                        return this._triples.WithPredicate(p);
                    }
                    else
                    {
                        // Fixed Predicate and Object with Wildcard Subject
                        return this._triples.WithPredicateObject(p, o);
                    }
                }
            }
            else
            {
                // Fixed Subject
                if (ReferenceEquals(p, null))
                {
                    // Wildcard Predicate with Fixed Subject
                    if (ReferenceEquals(o, null))
                    {
                        // Wildcard Predicate and Object with Fixed Subject
                        return this._triples.WithSubject(s);
                    }
                    else
                    {
                        // Wildcard Predicate with Fixed Subject and Object
                        return this._triples.WithSubjectObject(s, o);
                    }
                }
                else
                {
                    // Fixed Subject and Predicate
                    if (ReferenceEquals(o, null))
                    {
                        // Fixed Subject and Predicate with Wildcard Object
                        return this._triples.WithSubjectPredicate(s, p);
                    }
                    else
                    {
                        // Fixed Subject, Predicate and Object
                        Triple t = new Triple(s, p, o);
                        return this._triples.Contains(t) ? t.AsEnumerable() : Enumerable.Empty<Triple>();
                    }
                }
            }
        }

        /// <summary>
        /// Gets whether a given Triple exists in this Graph
        /// </summary>
        /// <param name="t">Triple to test</param>
        /// <returns></returns>
        public virtual bool ContainsTriple(Triple t)
        {
            return this._triples.Contains(t);
        }

        #endregion

        #region Graph Merging

        /// <summary>
        /// Merges another Graph into the current Graph
        /// </summary>
        /// <param name="g">Graph to Merge into this Graph</param>
        /// <remarks>
        /// <para>
        /// The Graph on which you invoke this method will preserve its Blank Node IDs while the Blank Nodes from the Graph being merged in will be given new IDs as required in the scope of this Graph.
        /// </para>
        /// <para>
        /// The Graph will raise the <see cref="MergeRequested">MergeRequested</see> event before the Merge operation which gives any event handlers the oppurtunity to cancel this event.  When the Merge operation is completed the <see cref="Merged">Merged</see> event is raised
        /// </para>
        /// </remarks>
        public virtual void Merge(IGraph g)
        {
            if (ReferenceEquals(this, g)) throw new RdfException("You cannot Merge an RDF Graph with itself");

            //Check that the merge can go ahead
            if (!this.RaiseMergeRequested()) return;

            //First copy and Prefixes across which aren't defined in this Graph
            this._nsmapper.Import(g.Namespaces);

            //Since Blank Nodes are now truly scoped to their factory we can always just copy triples across directly
            this.Assert(g.Triples);

            this.RaiseMerged();
        }

        #endregion

        #region Graph Equality

        /// <summary>
        /// Determines whether a Graph is equal to another Object
        /// </summary>
        /// <param name="obj">Object to test</param>
        /// <returns></returns>
        /// <remarks>
        /// <para>
        /// A Graph can only be equal to another Object which is an <see cref="IGraph">IGraph</see>
        /// </para>
        /// <para>
        /// Graph Equality is determined by a somewhat complex algorithm which is explained in the remarks of the other overload for Equals
        /// </para>
        /// </remarks>
        public override bool Equals(object obj)
        {
            //Graphs can't be equal to null
            if (obj == null) return false;

            if (obj is IGraph)
            {
                IGraph g = (IGraph)obj;

                Dictionary<INode, INode> temp;
                return this.Equals(g, out temp);
            }
            else
            {
                //Graphs can only be equal to other Graphs
                return false;
            }
        }

        /// <summary>
        /// Determines whether this Graph is equal to the given Graph
        /// </summary>
        /// <param name="g">Graph to test for equality</param>
        /// <param name="mapping">Mapping of Blank Nodes iff the Graphs are equal and contain some Blank Nodes</param>
        /// <returns></returns>
        /// <remarks>
        /// See <see cref="GraphMatcher"/> for documentation of the equality algorithm used.
        /// </remarks>
        public virtual bool Equals(IGraph g, out Dictionary<INode, INode> mapping)
        {
            //Set the mapping to be null
            mapping = null;

            GraphMatcher matcher = new GraphMatcher();
            if (matcher.Equals(this, g))
            {
                mapping = matcher.Mapping;
                return true;
            }
            else
            {
                return false;
            }
        }

        #endregion

        #region Sub-Graph Matching

        /// <summary>
        /// Checks whether this Graph is a sub-graph of the given Graph
        /// </summary>
        /// <param name="g">Graph</param>
        /// <returns></returns>
        public bool IsSubGraphOf(IGraph g)
        {
            Dictionary<INode, INode> temp;
            return this.IsSubGraphOf(g, out temp);
        }

        /// <summary>
        /// Checks whether this Graph is a sub-graph of the given Graph
        /// </summary>
        /// <param name="g">Graph</param>
        /// <param name="mapping">Mapping of Blank Nodes</param>
        /// <returns></returns>
        public bool IsSubGraphOf(IGraph g, out Dictionary<INode, INode> mapping)
        {
            //Set the mapping to be null
            mapping = null;

            SubGraphMatcher matcher = new SubGraphMatcher();
            if (matcher.IsSubGraph(this, g))
            {
                mapping = matcher.Mapping;
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Checks whether this Graph has the given Graph as a sub-graph
        /// </summary>
        /// <param name="g">Graph</param>
        /// <returns></returns>
        public bool HasSubGraph(IGraph g)
        {
            return g.IsSubGraphOf(this);
        }

        /// <summary>
        /// Checks whether this Graph has the given Graph as a sub-graph
        /// </summary>
        /// <param name="g">Graph</param>
        /// <param name="mapping">Mapping of Blank Nodes</param>
        /// <returns></returns>
        public bool HasSubGraph(IGraph g, out Dictionary<INode, INode> mapping)
        {
            return g.IsSubGraphOf(this, out mapping);
        }

        #endregion

        #region Graph Difference

        /// <summary>
        /// Computes the Difference between this Graph the given Graph
        /// </summary>
        /// <param name="g">Graph</param>
        /// <returns></returns>
        /// <remarks>
        /// <para>
        /// Produces a report which shows the changes that must be made to this Graph to produce the given Graph
        /// </para>
        /// </remarks>
        public GraphDiffReport Difference(IGraph g)
        {
            GraphDiff differ = new GraphDiff();
            return differ.Difference(this, g);
        }

        #endregion

        #region Helper Functions

        /// <summary>
        /// Creates a new unused Blank Node ID and returns it
        /// </summary>
        /// <returns></returns>
        [Obsolete("Obsolete, no longer used", true)]
        public virtual String GetNextBlankNodeID()
        {
            throw new NotSupportedException();
        }

        #endregion

        #region Operators

#if !NO_DATA

        /// <summary>
        /// Converts a Graph into a DataTable using the explicit cast operator defined by this class
        /// </summary>
        /// <returns>
        /// A DataTable containing three Columns (Subject, Predicate and Object) all typed as <see cref="INode">INode</see> with a Row per Triple
        /// </returns>
        /// <remarks>
        /// <strong>Warning:</strong> Not available under builds which remove the Data Storage layer from dotNetRDF e.g. Silverlight
        /// </remarks>
        public virtual DataTable ToDataTable()
        {
            return (DataTable)this;
        }

        /// <summary>
        /// Casts a Graph to a DataTable with all Columns typed as <see cref="INode">INode</see> (Column Names are Subject, Predicate and Object
        /// </summary>
        /// <param name="g">Graph to convert</param>
        /// <returns>
        /// A DataTable containing three Columns (Subject, Predicate and Object) all typed as <see cref="INode">INode</see> with a Row per Triple
        /// </returns>
        /// <remarks>
        /// <strong>Warning:</strong> Not available under builds which remove the Data Storage layer from dotNetRDF e.g. Silverlight
        /// </remarks>
        public static explicit operator DataTable(BaseGraph g)
        {
            DataTable table = new DataTable();
            table.Columns.Add(new DataColumn("Subject", typeof(INode)));
            table.Columns.Add(new DataColumn("Predicate", typeof(INode)));
            table.Columns.Add(new DataColumn("Object", typeof(INode)));

            foreach (Triple t in g.Triples)
            {
                DataRow row = table.NewRow();
                row["Subject"] = t.Subject;
                row["Predicate"] = t.Predicate;
                row["Object"] = t.Object;
                table.Rows.Add(row);
            }

            return table;
        }

#endif

        #endregion

        #region Events

        /// <summary>
        /// Event which is raised when a Triple is asserted in the Graph
        /// </summary>
        public event TripleEventHandler TripleAsserted;

        /// <summary>
        /// Event which is raised when a Triple is retracted from the Graph
        /// </summary>
        public event TripleEventHandler TripleRetracted;

        /// <summary>
        /// Event which is raised when the Graph contents change
        /// </summary>
        public event GraphEventHandler Changed;

        /// <summary>
        /// Event which is raised just before the Graph is cleared of its contents
        /// </summary>
        public event CancellableGraphEventHandler ClearRequested;

        /// <summary>
        /// Event which is raised after the Graph is cleared of its contents
        /// </summary>
        public event GraphEventHandler Cleared;

        /// <summary>
        /// Event which is raised when a Merge operation is requested on the Graph
        /// </summary>
        public event CancellableGraphEventHandler MergeRequested;

        /// <summary>
        /// Event which is raised when a Merge operation is completed on the Graph
        /// </summary>
        public event GraphEventHandler Merged;

        /// <summary>
        /// Event Handler which handles the <see cref="BaseTripleCollection.TripleAdded">Triple Added</see> event from the underlying Triple Collection by raising the Graph's <see cref="TripleAsserted">TripleAsserted</see> event
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="args">Triple Event Arguments</param>
        protected virtual void OnTripleAsserted(Object sender, TripleEventArgs args)
        {
            this.RaiseTripleAsserted(args);
        }

        /// <summary>
        /// Helper method for raising the <see cref="TripleAsserted">Triple Asserted</see> event manually
        /// </summary>
        /// <param name="args">Triple Event Arguments</param>
        protected void RaiseTripleAsserted(TripleEventArgs args)
        {
            TripleEventHandler d = this.TripleAsserted;
            args.Graph = this;
            if (d != null)
            {
                d(this, args);
            }
            this.RaiseGraphChanged(args);
        }

        /// <summary>
        /// Helper method for raising the <see cref="TripleAsserted">Triple Asserted</see> event manually
        /// </summary>
        /// <param name="t">Triple</param>
        protected void RaiseTripleAsserted(Triple t)
        {
            TripleEventHandler d = this.TripleAsserted;
            GraphEventHandler e = this.Changed;
            if (d != null || e != null)
            {
                TripleEventArgs args = new TripleEventArgs(t, this);
                if (d != null) d(this, args);
                if (e != null) e(this, new GraphEventArgs(this, args));
            }
        }

        /// <summary>
        /// Event Handler which handles the <see cref="BaseTripleCollection.TripleRemoved">Triple Removed</see> event from the underlying Triple Collection by raising the Graph's <see cref="TripleRetracted">Triple Retracted</see> event
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="args">Triple Event Arguments</param>
        protected virtual void OnTripleRetracted(Object sender, TripleEventArgs args)
        {
            this.RaiseTripleRetracted(args);
        }

        /// <summary>
        /// Helper method for raising the <see cref="TripleRetracted">Triple Retracted</see> event manually
        /// </summary>
        /// <param name="args"></param>
        protected void RaiseTripleRetracted(TripleEventArgs args)
        {
            TripleEventHandler d = this.TripleRetracted;
            args.Graph = this;
            if (d != null)
            {
                d(this, args);
            }
            this.RaiseGraphChanged(args);
        }

        /// <summary>
        /// Helper method for raising the <see cref="TripleRetracted">Triple Retracted</see> event manually
        /// </summary>
        /// <param name="t">Triple</param>
        protected void RaiseTripleRetracted(Triple t)
        {
            TripleEventHandler d = this.TripleRetracted;
            GraphEventHandler e = this.Changed;
            if (d != null || e != null)
            {
                TripleEventArgs args = new TripleEventArgs(t, this, false);
                if (d != null) d(this, args);
                if (e != null) e(this, new GraphEventArgs(this, args));
            }
        }

        /// <summary>
        /// Helper method for raising the <see cref="Changed">Changed</see> event
        /// </summary>
        /// <param name="args">Triple Event Arguments</param>
        protected void RaiseGraphChanged(TripleEventArgs args)
        {
            GraphEventHandler d = this.Changed;
            if (d != null)
            {
                d(this, new GraphEventArgs(this, args));
            }
        }

        /// <summary>
        /// Helper method for raising the <see cref="Changed">Changed</see> event
        /// </summary>
        protected void RaiseGraphChanged()
        {
            GraphEventHandler d = this.Changed;
            if (d != null)
            {
                d(this, new GraphEventArgs(this));
            }
        }

        /// <summary>
        /// Helper method for raising the <see cref="ClearRequested">Clear Requested</see> event and returning whether any of the Event Handlers cancelled the operation
        /// </summary>
        /// <returns>True if the operation can continue, false if it should be aborted</returns>
        protected bool RaiseClearRequested()
        {
            CancellableGraphEventHandler d = this.ClearRequested;
            if (d != null)
            {
                CancellableGraphEventArgs args = new CancellableGraphEventArgs(this);
                d(this, args);
                return !args.Cancel;
            }
            else
            {
                return true;
            }
        }

        /// <summary>
        /// Helper method for raising the <see cref="Cleared">Cleared</see> event
        /// </summary>
        protected void RaiseCleared()
        {
            GraphEventHandler d = this.Cleared;
            if (d != null)
            {
                d(this, new GraphEventArgs(this));
            }
        }

        /// <summary>
        /// Helper method for raising the <see cref="MergeRequested">Merge Requested</see> event and returning whether any of the Event Handlers cancelled the operation
        /// </summary>
        /// <returns>True if the operation can continue, false if it should be aborted</returns>
        protected bool RaiseMergeRequested()
        {
            CancellableGraphEventHandler d = this.MergeRequested;
            if (d != null)
            {
                CancellableGraphEventArgs args = new CancellableGraphEventArgs(this);
                d(this, args);
                return !args.Cancel;
            }
            else
            {
                return true;
            }
        }

        /// <summary>
        /// Helper method for raising the <see cref="Merged">Merged</see> event
        /// </summary>
        protected void RaiseMerged()
        {
            GraphEventHandler d = this.Merged;
            if (d != null)
            {
                d(this, new GraphEventArgs(this));
            }
        }

        /// <summary>
        /// Helper method for attaching the necessary event Handlers to a Triple Collection
        /// </summary>
        /// <param name="tripleCollection">Triple Collection</param>
        /// <remarks>
        /// May be useful if you replace the Triple Collection after instantiation e.g. as done in <see cref="Query.SparqlView">SparqlView</see>'s
        /// </remarks>
        protected void AttachEventHandlers(ITripleCollection tripleCollection)
        {
            tripleCollection.TripleAdded += this.TripleAddedHandler;
            tripleCollection.TripleRemoved += this.TripleRemovedHandler;
        }

        /// <summary>
        /// Helper method for detaching the necessary event Handlers from a Triple Collection
        /// </summary>
        /// <param name="tripleCollection">Triple Collection</param>
        /// <remarks>
        /// May be useful if you replace the Triple Collection after instantiation e.g. as done in <see cref="Query.SparqlView">SparqlView</see>'s
        /// </remarks>
        protected void DetachEventHandlers(ITripleCollection tripleCollection)
        {
            tripleCollection.TripleAdded -= this.TripleAddedHandler;
            tripleCollection.TripleRemoved -= this.TripleRemovedHandler;
        }

        #endregion

        /// <summary>
        /// Disposes of a Graph
        /// </summary>
        public virtual void Dispose()
        {
            this.DetachEventHandlers(this._triples);
        }

#if !SILVERLIGHT

        #region ISerializable Members

        /// <summary>
        /// Gets the Serialization Information for serializing a Graph
        /// </summary>
        /// <param name="info">Serialization Information</param>
        /// <param name="context">Streaming Context</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("triples", this.Triples.ToList(), typeof(List<Triple>));
            IEnumerable<KeyValuePair<String,String>> ns = from p in this.Namespaces.Prefixes
                                                          select new KeyValuePair<String,String>(p, this.Namespaces.GetNamespaceUri(p).AbsoluteUri);
            info.AddValue("namespaces", ns.ToList(), typeof(List<KeyValuePair<String, String>>));
        }

        #endregion

        #region IXmlSerializable Members

        /// <summary>
        /// Gets the Schema for XML Serialization
        /// </summary>
        /// <returns></returns>
        public XmlSchema GetSchema()
        {
            return null;
        }

        /// <summary>
        /// Reads the data for XML deserialization
        /// </summary>
        /// <param name="reader">XML Reader</param>
        public void ReadXml(XmlReader reader)
        {
            XmlSerializer tripleDeserializer = new XmlSerializer(typeof(Triple));
            reader.Read();
            if (reader.Name.Equals("namespaces"))
            {
                if (!reader.IsEmptyElement)
                {
                    reader.Read();
                    while (reader.Name.Equals("namespace"))
                    {
                        if (reader.MoveToAttribute("prefix"))
                        {
                            String prefix = reader.Value;
                            if (reader.MoveToAttribute("uri"))
                            {
                                Uri u = UriFactory.Create(reader.Value);
                                this.Namespaces.AddNamespace(prefix, u);
                                reader.Read();
                            }
                            else
                            {
                                throw new RdfException("Expected a uri attribute on a <namespace> element");
                            }
                        }
                        else
                        {
                            throw new RdfException("Expected a prefix attribute on a <namespace> element");
                        }
                    }
                }
            }
            reader.Read();
            if (reader.Name.Equals("triples"))
            {
                if (!reader.IsEmptyElement)
                {
                    reader.Read();
                    while (reader.Name.Equals("triple"))
                    {
                        try
                        {
                            Object temp = tripleDeserializer.Deserialize(reader);
                            this.Assert((Triple)temp);
                            reader.Read();
                        }
                        catch
                        {
                            throw;
                        }
                    }
                }
            }
            else
            {
                throw new RdfException("Expected a <triples> element inside a <graph> element but got a <" + reader.Name + "> element instead");
            }
        }

        /// <summary>
        /// Writes the data for XML serialization
        /// </summary>
        /// <param name="writer">XML Writer</param>
        public void WriteXml(XmlWriter writer)
        {
            XmlSerializer tripleSerializer = new XmlSerializer(typeof(Triple));

            //Serialize Namespace Map
            writer.WriteStartElement("namespaces");
            foreach (String prefix in this.Namespaces.Prefixes)
            {
                writer.WriteStartElement("namespace");
                writer.WriteAttributeString("prefix", prefix);
                writer.WriteAttributeString("uri", this.Namespaces.GetNamespaceUri(prefix).AbsoluteUri);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();

            //Serialize Triples
            writer.WriteStartElement("triples");
            foreach (Triple t in this.Triples)
            {
                tripleSerializer.Serialize(writer, t);
            }
            writer.WriteEndElement();
        }

        #endregion

#endif
    }
}