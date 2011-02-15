﻿/*

Copyright Robert Vesse 2009-10
rvesse@vdesign-studios.com

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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VDS.RDF.Parsing.Tokens;
using VDS.RDF.Query;
using VDS.RDF.Query.Algebra;
using VDS.RDF.Query.Construct;
using VDS.RDF.Query.Patterns;

namespace VDS.RDF.Update.Commands
{
    /// <summary>
    /// Represents a SPARQL Update INSERT command
    /// </summary>
    public class InsertCommand : BaseModificationCommand
    {
        private GraphPattern _insertPattern, _wherePattern;

        /// <summary>
        /// Creates a new INSERT command
        /// </summary>
        /// <param name="insertions">Pattern to construct Triples to insert</param>
        /// <param name="where">Pattern to select data which is then used in evaluating the insertions</param>
        /// <param name="graphUri">URI of the affected Graph</param>
        public InsertCommand(GraphPattern insertions, GraphPattern where, Uri graphUri)
            : base(SparqlUpdateCommandType.Insert) 
        {
            this._insertPattern = insertions;
            this._wherePattern = where;
            this._graphUri = graphUri;

            //Optimise the WHERE
            this._wherePattern.Optimise(Enumerable.Empty<String>());
        }

        /// <summary>
        /// Creates a new INSERT command which operates on the Default Graph
        /// </summary>
        /// <param name="insertions">Pattern to construct Triples to insert</param>
        /// <param name="where">Pattern to select data which is then used in evaluating the insertions</param>
        public InsertCommand(GraphPattern insertions, GraphPattern where)
            : this(insertions, where, null) { }

        /// <summary>
        /// Gets whether the Command affects a single Graph
        /// </summary>
        public override bool AffectsSingleGraph
        {
            get
            {
                List<String> affectedUris = new List<string>();
                if (this.TargetUri != null)
                {
                    affectedUris.Add(this.TargetUri.ToString());
                }
                if (this._insertPattern.IsGraph) affectedUris.Add(this._insertPattern.GraphSpecifier.Value);
                if (this._insertPattern.HasChildGraphPatterns)
                {
                    affectedUris.AddRange(from p in this._insertPattern.ChildGraphPatterns
                                          where p.IsGraph
                                          select p.GraphSpecifier.Value);
                }

                return affectedUris.Distinct().Count() <= 1;
            }
        }

        /// <summary>
        /// Gets whether the Command affects a given Graph
        /// </summary>
        /// <param name="graphUri">Graph URI</param>
        /// <returns></returns>
        public override bool AffectsGraph(Uri graphUri)
        {
            if (graphUri.ToSafeString().Equals(GraphCollection.DefaultGraphUri)) graphUri = null;

            List<String> affectedUris = new List<string>();
            if (this.TargetUri != null)
            {
                affectedUris.Add(this.TargetUri.ToString());
            }
            if (this._insertPattern.IsGraph) affectedUris.Add(this._insertPattern.GraphSpecifier.Value);
            if (this._insertPattern.HasChildGraphPatterns)
            {
                affectedUris.AddRange(from p in this._insertPattern.ChildGraphPatterns
                                      where p.IsGraph
                                      select p.GraphSpecifier.Value);
            }
            if (affectedUris.Any(u => u.Equals(GraphCollection.DefaultGraphUri))) affectedUris.Add(null);

            return affectedUris.Contains(graphUri.ToSafeString());
        }

        /// <summary>
        /// Gets the URI of the Graph the insertions are made to
        /// </summary>
        public Uri TargetUri
        {
            get
            {
                return this._graphUri;
            }
        }

        /// <summary>
        /// Gets the pattern used for insertions
        /// </summary>
        public GraphPattern InsertPattern
        {
            get
            {
                return this._insertPattern;
            }
        }

        /// <summary>
        /// Gets the pattern used for the WHERE clause
        /// </summary>
        public GraphPattern WherePattern
        {
            get
            {
                return this._wherePattern;
            }
        }

        /// <summary>
        /// Optimises the Commands WHERE pattern
        /// </summary>
        public override void Optimise()
        {
            if (!this.IsOptimised)
            {
                this._wherePattern.Optimise(Enumerable.Empty<String>());
                this.IsOptimised = true;
            }
        }

        /// <summary>
        /// Evaluates the Command in the given Context
        /// </summary>
        /// <param name="context">Evaluation Context</param>
        public override void Evaluate(SparqlUpdateEvaluationContext context)
        {
            //First evaluate the WHERE pattern to get the affected bindings
            ISparqlAlgebra where = this._wherePattern.ToAlgebra();
            SparqlEvaluationContext queryContext = new SparqlEvaluationContext(null, context.Data);
            if (this.UsingUris.Any()) context.Data.SetActiveGraph(this._usingUris);
            BaseMultiset results = where.Evaluate(queryContext);
            if (this.UsingUris.Any()) context.Data.ResetActiveGraph();

            //Get the Graph to which we are inserting
            IGraph g = context.Data.GetModifiableGraph(this._graphUri);

            //Insert the Triples for each Solution
            foreach (Set s in queryContext.OutputMultiset.Sets)
            {
                List<Triple> insertedTriples = new List<Triple>();

                //Triples from raw Triple Patterns
                try
                {
                    ConstructContext constructContext = new ConstructContext(g, s, true);
                    foreach (ITriplePattern p in this._insertPattern.TriplePatterns)
                    {
                        insertedTriples.Add(((IConstructTriplePattern)p).Construct(constructContext));
                    }
                    g.Assert(insertedTriples);
                } 
                catch (RdfQueryException)
                {
                    //If we throw an error this means we couldn't construct for this solution so the
                    //solution is ignored for inserting into the standard graph
                }

                //Triples from GRAPH clauses
                foreach (GraphPattern gp in this._insertPattern.ChildGraphPatterns)
                {
                    insertedTriples.Clear();
                    try 
                    {
                        String graphUri;
                        switch (gp.GraphSpecifier.TokenType)
                        {
                            case Token.URI:
                                graphUri = gp.GraphSpecifier.Value;
                                break;
                            case Token.VARIABLE:
                                if (s.ContainsVariable(gp.GraphSpecifier.Value))
                                {
                                    INode temp = s[gp.GraphSpecifier.Value.Substring(1)];
                                    if (temp == null)
                                    {
                                        //If the Variable is not bound then skip
                                        continue;
                                    }
                                    else if (temp.NodeType == NodeType.Uri)
                                    {
                                        graphUri = temp.ToSafeString();
                                    }
                                    else
                                    {
                                        //If the Variable is not bound to a URI then skip
                                        continue;
                                    }
                                }
                                else
                                {
                                    //If the Variable is not bound for this solution then skip
                                    continue;
                                }
                                break;
                            default:
                                //Any other Graph Specifier we have to ignore this solution
                                continue;
                        }
                        IGraph h = context.Data.GetModifiableGraph(new Uri(graphUri));
                        ConstructContext constructContext = new ConstructContext(h, s, true);
                        foreach (ITriplePattern p in gp.TriplePatterns)
                        {
                            insertedTriples.Add(((IConstructTriplePattern)p).Construct(constructContext));
                        }
                        h.Assert(insertedTriples);
                    }
                    catch (RdfQueryException)
                    {
                        //If we throw an error this means we couldn't construct for this solution so the
                        //solution is discarded
                        continue;
                    }
                }

            }
        }

        /// <summary>
        /// Processes the Command using the given Update Processor
        /// </summary>
        /// <param name="processor">SPARQL Update Processor</param>
        public override void Process(ISparqlUpdateProcessor processor)
        {
            processor.ProcessInsertCommand(this);
        }

        /// <summary>
        /// Gets the String representation of the Command
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            StringBuilder output = new StringBuilder();
            if (this._graphUri != null)
            {
                output.Append("WITH <");
                output.Append(this._graphUri.ToString().Replace(">", "\\>"));
                output.AppendLine(">");
            }
            output.AppendLine("INSERT");
            output.AppendLine(this._insertPattern.ToString());
            if (this._usingUris != null)
            {
                foreach (Uri u in this._usingUris)
                {
                    output.AppendLine("USING <" + u.ToString().Replace(">", "\\>") + ">");
                }
            }
            output.AppendLine("WHERE");
            output.AppendLine(this._wherePattern.ToString());
            return output.ToString();
        }
    }
}
