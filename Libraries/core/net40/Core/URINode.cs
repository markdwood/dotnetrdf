/*

Copyright dotNetRDF Project 2009-12
dotnetrdf-develop@lists.sf.net

------------------------------------------------------------------------

This file is part of dotNetRDF.

dotNetRDF is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

dotNetRDF is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with dotNetRDF.  If not, see <http://www.gnu.org/licenses/>.

------------------------------------------------------------------------

dotNetRDF may alternatively be used under the LGPL or MIT License

http://www.gnu.org/licenses/lgpl.html
http://www.opensource.org/licenses/mit-license.php

If these licenses are not suitable for your intended use please contact
us at the above stated email address to discuss alternative
terms.

*/

using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using VDS.RDF.Query;
using VDS.RDF.Query.Expressions;
using VDS.RDF.Nodes;

namespace VDS.RDF
{
    /// <summary>
    /// Abstract Base Class for URI Nodes
    /// </summary>
#if !SILVERLIGHT
    [Serializable,XmlRoot(ElementName="uri")]
#endif
    public abstract class BaseUriNode 
        : BaseNode, IUriNode, IEquatable<BaseUriNode>, IComparable<BaseUriNode>, IValuedNode
    {
        private Uri _uri;

        /// <summary>
        /// Internal Only Constructor for URI Nodes
        /// </summary>
        /// <param name="g">Graph this Node is in</param>
        /// <param name="uri">URI</param>
        protected internal BaseUriNode(Uri uri)
            : base(NodeType.Uri)
        {
            this._uri = uri;

            //Compute Hash Code
            this._hashcode = (this._nodetype + this.ToString()).GetHashCode();
        }

#if !SILVERLIGHT

        /// <summary>
        /// Deserialization Only Constructor
        /// </summary>
        protected BaseUriNode()
            : base(NodeType.Uri) { }

        /// <summary>
        /// Deserialization Constructor
        /// </summary>
        /// <param name="info">Serialization Information</param>
        /// <param name="context">Streaming Context</param>
        protected BaseUriNode(SerializationInfo info, StreamingContext context)
            : base(NodeType.Uri)
        {
            this._uri = UriFactory.Create(info.GetString("uri"));

            //Compute Hash Code
            this._hashcode = (this._nodetype + this.ToString()).GetHashCode();
        }

#endif

        /// <summary>
        /// Gets the Uri for this Node
        /// </summary>
        public virtual Uri Uri
        {
            get
            {
                return this._uri;
            }
        }

        /// <summary>
        /// Implementation of Equality for Uri Nodes
        /// </summary>
        /// <param name="obj">Object to compare with</param>
        /// <returns></returns>
        /// <remarks>
        /// URI Nodes are considered equal if the string form of their URIs match using Ordinal string comparison
        /// </remarks>
        public override bool Equals(object obj)
        {
            if (obj == null) return false;

            if (ReferenceEquals(this, obj)) return true;

            if (obj is INode)
            {
                return this.Equals((INode)obj);
            }
            else
            {
                //Can only be equal to other Nodes
                return false;
            }
        }

        /// <summary>
        /// Implementation of Equality for Uri Nodes
        /// </summary>
        /// <param name="other">Object to compare with</param>
        /// <returns></returns>
        /// <remarks>
        /// URI Nodes are considered equal if the string form of their URIs match using Ordinal string comparison
        /// </remarks>
        public override bool Equals(INode other)
        {
            if ((Object)other == null) return false;

            if (ReferenceEquals(this, other)) return true;

            if (other.NodeType == NodeType.Uri)
            {
                Uri temp = ((IUriNode)other).Uri;

                return EqualityHelper.AreUrisEqual(this._uri, temp);
            }
            else
            {
                //Can only be equal to UriNodes
                return false;
            }
        }

        /// <summary>
        /// Determines whether this Node is equal to a Blank Node (should always be false)
        /// </summary>
        /// <param name="other">Blank Node</param>
        /// <returns></returns>
        public override bool Equals(IBlankNode other)
        {
            if (ReferenceEquals(this, other)) return true;
            return false;
        }

        /// <summary>
        /// Determines whether this Node is equal to a Graph Literal Node (should always be false)
        /// </summary>
        /// <param name="other">Graph Literal Node</param>
        /// <returns></returns>
        public override bool Equals(IGraphLiteralNode other)
        {
            if (ReferenceEquals(this, other)) return true;
            return false;
        }

        /// <summary>
        /// Determines whether this Node is equal to a Literal Node (should always be false)
        /// </summary>
        /// <param name="other">Literal Node</param>
        /// <returns></returns>
        public override bool Equals(ILiteralNode other)
        {
            if (ReferenceEquals(this, other)) return true;
            return false;
        }

        /// <summary>
        /// Determines whether this Node is equal to a URI Node
        /// </summary>
        /// <param name="other">URI Node</param>
        /// <returns></returns>
        public override bool Equals(IUriNode other)
        {
            if ((Object)other == null) return false;

            if (ReferenceEquals(this, other)) return true;

            return EqualityHelper.AreUrisEqual(this._uri, other.Uri);
        }

        /// <summary>
        /// Determines whether this Node is equal to a Variable Node (should always be false)
        /// </summary>
        /// <param name="other">Variable Node</param>
        /// <returns></returns>
        public override bool Equals(IVariableNode other)
        {
            if (ReferenceEquals(this, other)) return true;
            return false;
        }

        /// <summary>
        /// Determines whether this Node is equal to a URI Node
        /// </summary>
        /// <param name="other">URI Node</param>
        /// <returns></returns>
        public bool Equals(BaseUriNode other)
        {
            return this.Equals((IUriNode)other);
        }

        /// <summary>
        /// Gets a String representation of a Uri as a plain text Uri
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return this._uri.AbsoluteUri;
        }

        /// <summary>
        /// Implementation of Compare To for Uri Nodes
        /// </summary>
        /// <param name="other">Node to Compare To</param>
        /// <returns></returns>
        /// <remarks>
        /// Uri Nodes are greater than Blank Nodes and Nulls, they are less than Literal Nodes and Graph Literal Nodes.
        /// <br /><br />
        /// Uri Nodes are ordered based upon lexical ordering of the string value of their URIs
        /// </remarks>
        public override int CompareTo(INode other)
        {
            if (ReferenceEquals(this, other)) return 0;

            if (other == null)
            {
                //Everything is greater than a null
                //Return a 1 to indicate this
                return 1;
            }
            else if (other.NodeType == NodeType.Blank || other.NodeType == NodeType.Variable)
            {
                //Uri Nodes are greater than Blank and Variable Nodes
                //Return a 1 to indicate this
                return 1;
            }
            else if (other.NodeType == NodeType.Uri)
            {
                //Return the result of CompareTo using the IUriNode comparison method

                return this.CompareTo((IUriNode)other);
            }
            else
            {
                //Anything else is considered greater than a Uri Node
                //Return -1 to indicate this
                return -1;
            }
        }

        /// <summary>
        /// Returns an Integer indicating the Ordering of this Node compared to another Node
        /// </summary>
        /// <param name="other">Node to test against</param>
        /// <returns></returns>
        public override int CompareTo(IBlankNode other)
        {
            if (ReferenceEquals(this, other)) return 0;

            //URI Nodes are greater than nulls and Blank Nodes
            return 1;
        }

        /// <summary>
        /// Returns an Integer indicating the Ordering of this Node compared to another Node
        /// </summary>
        /// <param name="other">Node to test against</param>
        /// <returns></returns>
        public override int CompareTo(IGraphLiteralNode other)
        {
            if (ReferenceEquals(this, other)) return 0;

            if (other == null)
            {
                //Everything is greater than a null
                //Return a 1 to indicate this
                return 1;
            }
            else
            {
                //URI Nodes are less than Graph Literal Nodes
                return -1;
            }
        }

        /// <summary>
        /// Returns an Integer indicating the Ordering of this Node compared to another Node
        /// </summary>
        /// <param name="other">Node to test against</param>
        /// <returns></returns>
        public override int CompareTo(ILiteralNode other)
        {
            if (ReferenceEquals(this, other)) return 0;

            if (other == null)
            {
                //Everything is greater than a null
                //Return a 1 to indicate this
                return 1;
            }
            else
            {
                //URI Nodes are less than Literal Nodes
                return -1;
            }
        }

        /// <summary>
        /// Returns an Integer indicating the Ordering of this Node compared to another Node
        /// </summary>
        /// <param name="other">Node to test against</param>
        /// <returns></returns>
        public override int CompareTo(IVariableNode other)
        {
            if (ReferenceEquals(this, other)) return 0;

            //URI Nodes are greater than nulls and Variable Nodes
            return 1;
        }

        /// <summary>
        /// Returns an Integer indicating the Ordering of this Node compared to another Node
        /// </summary>
        /// <param name="other">Node to test against</param>
        /// <returns></returns>
        public override int CompareTo(IUriNode other)
        {
            if (ReferenceEquals(this, other)) return 0;

            return ComparisonHelper.CompareUris(this.Uri, other.Uri);
        }

        /// <summary>
        /// Returns an Integer indicating the Ordering of this Node compared to another Node
        /// </summary>
        /// <param name="other">Node to test against</param>
        /// <returns></returns>
        public int CompareTo(BaseUriNode other)
        {
            return this.CompareTo((IUriNode)other);
        }

#if !SILVERLIGHT

        #region ISerializable Members

        /// <summary>
        /// Gets the data for serialization
        /// </summary>
        /// <param name="info">Serialization Information</param>
        /// <param name="context">Streaming Context</param>
        public sealed override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("uri", this._uri.AbsoluteUri);
        }

        #endregion

        #region IXmlSerializable Members

        /// <summary>
        /// Reads the data for XML deserialization
        /// </summary>
        /// <param name="reader">XML Reader</param>
        public sealed override void ReadXml(XmlReader reader)
        {
            this._uri = UriFactory.Create(reader.ReadElementContentAsString());
            //Compute Hash Code
            this._hashcode = (this._nodetype + this.ToString()).GetHashCode();
        }

        /// <summary>
        /// Writes the data for XML serialization
        /// </summary>
        /// <param name="writer">XML Writer</param>
        public sealed override void WriteXml(XmlWriter writer)
        {
            writer.WriteString(this._uri.AbsoluteUri);
        }

        #endregion

#endif

        #region IValuedNode Members

        /// <summary>
        /// Gets the value of the node as a string
        /// </summary>
        /// <returns></returns>
        public string AsString()
        {
            return this._uri.AbsoluteUri;
        }

        /// <summary>
        /// Throws an error as URIs cannot be cast to numerics
        /// </summary>
        /// <returns></returns>
        public long AsInteger()
        {
            throw new RdfQueryException("Cannot cast a URI to a type");
        }

        /// <summary>
        /// Throws an error as URIs cannot be cast to numerics
        /// </summary>
        /// <returns></returns>
        public decimal AsDecimal()
        {
            throw new RdfQueryException("Cannot cast a URI to a type");
        }

        /// <summary>
        /// Throws an error as URIs cannot be cast to numerics
        /// </summary>
        /// <returns></returns>
        public float AsFloat()
        {
            throw new RdfQueryException("Cannot cast a URI to a type");
        }

        /// <summary>
        /// Throws an error as URIs cannot be cast to numerics
        /// </summary>
        /// <returns></returns>
        public double AsDouble()
        {
            throw new RdfQueryException("Cannot cast a URI to a type");
        }

        /// <summary>
        /// Throws an error as URIs cannot be cast to a boolean
        /// </summary>
        /// <returns></returns>
        public bool AsBoolean()
        {
            throw new RdfQueryException("Cannot cast a URI to a type");
        }

        /// <summary>
        /// Throws an error as URIs cannot be cast to a date time
        /// </summary>
        /// <returns></returns>
        public DateTimeOffset AsDateTime()
        {
            throw new RdfQueryException("Cannot cast a URI to a type");
        }

        /// <summary>
        /// Throws an error as URIs cannot be cast to a time span
        /// </summary>
        /// <returns></returns>
        public TimeSpan AsTimeSpan()
        {
            throw new RdfQueryException("Cannot case a URI to a type");
        }

        /// <summary>
        /// Gets the URI of the datatype this valued node represents as a String
        /// </summary>
        public String EffectiveType
        {
            get
            {
                return String.Empty;
            }
        }
        
        /// <summary>
        /// Gets the numeric type of the expression
        /// </summary>
        public SparqlNumericType NumericType
        {
            get 
            {
                return SparqlNumericType.NaN;
            }
        }

        #endregion
    }

    /// <summary>
    /// Class for representing URI Nodes
    /// </summary>
#if !SILVERLIGHT
    [Serializable,XmlRoot(ElementName="uri")]
#endif
    public class UriNode
        : BaseUriNode, IEquatable<UriNode>, IComparable<UriNode>
    {
        /// <summary>
        /// Internal Only Constructor for URI Nodes
        /// </summary>
        /// <param name="g">Graph this Node is in</param>
        /// <param name="uri">URI for the Node</param>
        protected internal UriNode(Uri uri)
            : base(uri) { }

        /// <summary>
        /// Deserilization Only Constructor
        /// </summary>
        protected UriNode()
            : base() { }

#if !SILVERLIGHT

        /// <summary>
        /// Deserialization Constructor
        /// </summary>
        /// <param name="info">Serialization Information</param>
        /// <param name="context">Streaming Context</param>
        protected UriNode(SerializationInfo info, StreamingContext context)
            : base(info, context) { }

#endif

        /// <summary>
        /// Implementation of Compare To for URI Nodes
        /// </summary>
        /// <param name="other">URI Node to Compare To</param>
        /// <returns></returns>
        /// <remarks>
        /// Simply invokes the more general implementation of this method
        /// </remarks>
        public int CompareTo(UriNode other)
        {
            return base.CompareTo((IUriNode)other);
        }

        /// <summary>
        /// Determines whether this Node is equal to a URI Node
        /// </summary>
        /// <param name="other">URI Node</param>
        /// <returns></returns>
        public bool Equals(UriNode other)
        {
            return base.Equals((IUriNode)other);
        }
    }
}