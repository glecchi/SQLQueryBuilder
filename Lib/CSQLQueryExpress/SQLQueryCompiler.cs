﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace CSQLQueryExpress
{
    /// <summary>
    /// Used to compile a <see cref="ISQLQuery"/> instance in a <see cref="SQLQueryCompiled"/>.
    /// </summary>
    public static class SQLQueryCompiler
    {
        /// <summary>
        /// Compile a <see cref="ISQLQuery"/> instance in a <see cref="SQLQueryCompiled"/>.
        /// </summary>
        /// <param name="query">SQL query expression built with <see cref="SQLQuery"/>,</param>
        /// <returns>A compiled expression <see cref="SQLQueryCompiled"/>.</returns>
        public static SQLQueryCompiled Compile(ISQLQuery query)
        {            
            var parameterBuilder = new SQLQueryExpressionParametersBuilder();
            var tableNameResolver = new SQLQueryExpressionTableNameResolver();
            
            var translatedQuery = CompileStatement(query, parameterBuilder, tableNameResolver);

            return new SQLQueryCompiled(translatedQuery, new ReadOnlyCollection<SQLQueryParameter>(parameterBuilder.Parameters.Select(p => p.Value).ToList()));
        }

        internal static string CompileStatement(
            ISQLQuery query, 
            ISQLQueryExpressionParametersBuilder parameterBuilder,
            ISQLQueryExpressionTableNameResolver tableNameResolver)
        {
            var cteList = query.Where(f => f.FragmentType == SQLQueryFragmentType.SelectCte).ToList();
            if (cteList.Count > 1 && cteList.Select(t => t.GetType().GenericTypeArguments[0]).Distinct().Count() != cteList.Count)
            {
                throw new NotSupportedException("Multiple declaration of CTE TABLEs for the same Entity is not supported");
            }

            var queryExpressionTranslator = new SQLQueryExpressionTranslator(parameterBuilder, tableNameResolver);

            var translatedQueryBuilder = new StringBuilder();

            if (cteList.Count > 0)
            {
                var idx = cteList.Count - 1;
                foreach (var cte in cteList)
                {
                    var withTableFragments = (ISQLQuery)cte;

                    foreach (var fragment in withTableFragments)
                    {
                        if (translatedQueryBuilder.Length == 0)
                        {
                            translatedQueryBuilder.Append("WITH ");
                        }
                        else
                        {
                            translatedQueryBuilder.Append(Environment.NewLine);
                        }

                        translatedQueryBuilder.Append(fragment.Translate(queryExpressionTranslator));
                    }

                    if (idx-- > 0)
                    {
                        translatedQueryBuilder.Append($"{Environment.NewLine}), ");
                    }
                    else
                    {
                        translatedQueryBuilder.Append($"{Environment.NewLine}) {Environment.NewLine}");
                    }
                }
            }

            var allFragments = query.Where(f => cteList.Count == 0 || !cteList.Contains(f));

            foreach (var fragment in allFragments)
            {
                var translatedFragment = fragment.Translate(queryExpressionTranslator);

                if (string.IsNullOrWhiteSpace(translatedFragment))
                {
                    continue;
                }

                if (translatedQueryBuilder.Length > 0 &&
                    fragment.FragmentType != SQLQueryFragmentType.Batch &&
                    fragment.FragmentType != SQLQueryFragmentType.MultipleResultSets)
                {
                    translatedQueryBuilder.Append($" {Environment.NewLine}");
                }

                translatedQueryBuilder.Append(translatedFragment);
            }

            var translatedQuery = translatedQueryBuilder.ToString();

            return translatedQuery;
        }
    }

    public static class CSQLQueryExpressExtensions
    {
        /// <summary>
        /// Compile a <see cref="ISQLQuery"/> instance in a <see cref="SQLQueryCompiled"/>.
        /// </summary>
        /// <param name="query">SQL query expression built with <see cref="SQLQuery"/>,</param>
        /// <returns>A compiled expression <see cref="SQLQueryCompiled"/>.</returns>
        public static SQLQueryCompiled Compile(this ISQLQuery query)
        {
            return SQLQueryCompiler.Compile(query);
        }
    }

    /// <summary>
    /// A compiled expression of <see cref="ISQLQuery"/> instance.
    /// </summary>
    public sealed class SQLQueryCompiled
    {
        internal SQLQueryCompiled(string statement, IList<SQLQueryParameter> parameters)
        {
            Statement = statement;
            Parameters = parameters;
        }

        /// <summary>
        /// The TSQL statement.
        /// </summary>
        public string Statement { get; }

        /// <summary>
        /// The list of parameters.
        /// </summary>
        public IList<SQLQueryParameter> Parameters { get; }
    }
}
