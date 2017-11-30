//
// Copyright (c) 2017 Jovan Popovic
// Licence: MIT
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
// IN THE SOFTWARE.
// 
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;

namespace QueryExecutionEngine
{
    public class Configuration
    {
        public string ConnectionString;
        public short WorkerThreads = 4;
        public long MaxIterations = 0;
        public long MaxDurationInSeconds = 0;
        public Sequence[] Sequences;
        public Set[] Domains;
        public Query[] Queries;
        public Query Startup;
        public Query Cleanup;
        public string[] QueryList;
        public string FailedQueriesLog;

        public string Account;
        public string Container;
        public string Sas;
        public string DataSource;

        public static Configuration LoadFromFile(string file)
        {
            Configuration config = JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(file));
            
            if(config.Startup != null && config.Startup.File != null)
            {
                if (string.IsNullOrWhiteSpace(config.Startup.Text))
                {
                    config.Startup.Text = File.ReadAllText(config.Startup.File);
                } else
                {
                    throw new System.Exception("Cannot set both Text and File in StartUp query");
                }
            }

            if (config.Cleanup != null && config.Cleanup.File != null)
            {
                if (string.IsNullOrWhiteSpace(config.Cleanup.Text))
                {
                    config.Cleanup.Text = File.ReadAllText(config.Cleanup.File);
                }
                else
                {
                    throw new System.Exception("Cannot set both Text and File in Clenup query");
                }
            }

            if (config.QueryList != null)
            {
                var newQueries = new Query[config.QueryList.Length];
                for (int i = 0; i < config.QueryList.Length; i++)
                {
                    newQueries[i] = new Query() { Text = config.QueryList[i] };
                }

                if (config.Queries == null)
                {
                    config.Queries = newQueries;
                }
                else
                {
                    config.Queries = config.Queries.Union(newQueries).ToArray();
                }
            }
            if (config.Queries != null)
            {
                for (int i = 0; i < config.Queries.Length; i++)
                {
                    if (config.Queries[i].IsReader == null)
                    {
                        // HACKY LOGIC: USE EXECUTE for ExecuteReader, and EXEC for UPDATES
                        // Override with { IsReader: true|false }  in Configuration file.
                        if (config.Queries[i].Text.StartsWith("SELECT") || config.Queries[i].Text.StartsWith("EXECUTE "))
                        {
                            config.Queries[i].IsReader = true;
                        }
                        else
                        {
                            config.Queries[i].IsReader = false;
                        }
                    }
                }
            }
            return config;
        }
    }

    /// <summary>
    /// Represents a sequence of numbers.
    /// A new number will be generated each time a parameter of sequence type is used.
    /// </summary>
    public class Sequence
    {
        public string Name;
        public long Current;
        public long Start;
        public long End;
        public long Step;
    }

    /// <summary>
    /// Represents a set.
    /// A new value will be generated each time a parameter of set type is used.
    /// </summary>
    public class Set
    {
        public string Name;
        public int Start;
        public int End;
        public string Type;
        public string Values;
    }

    public interface IParameterValueProvider
    {
        object GetValue(Random r);
    }
}
