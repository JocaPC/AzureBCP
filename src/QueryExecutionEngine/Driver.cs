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
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QueryExecutionEngine
{
    public class Driver
    {
        public event ProgressEventHandler WorkloadEnd;
        public event ProgressEventHandler Progress;
        public delegate void ProgressEventHandler(Driver sender, ProgressEventArgs e);

        public event SqlInfoEventHandler SqlInfo;
        public delegate void SqlInfoEventHandler(object sender, SqlInfoMessageEventArgs e);

        public event ErrorEventHandler Error;
        public delegate void ErrorEventHandler(object sender, ErrorEventArgs e);

        public event QueryIterationEventHandler QueryStart;
        public event QueryIterationEventHandler QueryEnd;
        public delegate void QueryIterationEventHandler(Driver sender, QueryIterationEventArgs e);

        public int NumberOfQueries { get { return this.config.Queries.Length; } }

        private static readonly object lockFailedQueriesLog = new object();
        private static object connectionLock = new object();
        private HashSet<SqlConnection> connections;

        private DateTime? startTime = null;       
        private Configuration config;
        private ICommandPostProcessor commandProcessor;

        public Driver(Configuration Config, ICommandPostProcessor parameterHandler = null)
        {
            this.config = Config;
            this.commandProcessor = parameterHandler;
            this.connections = new HashSet<SqlConnection>();
            for(int i=0; i<this.config.WorkerThreads; i++)
            {
                var connection = new SqlConnection(Config.ConnectionString);
                connection.InfoMessage += delegate (object sender, SqlInfoMessageEventArgs e)
                {
                    var handler = SqlInfo;
                    if(handler!=null)
                    {
                        handler(sender, e);
                    }                    
                };
                this.connections.Add(connection);
            }

            this.Error += (o, e) =>
            {
                if (!string.IsNullOrWhiteSpace(this.config.FailedQueriesLog))
                {
                    lock (lockFailedQueriesLog)
                    {
                        File.AppendAllText(this.config.FailedQueriesLog, JsonConvert.SerializeObject(e.Query));
                    }
                }
            };
        }

        private bool IsCompleted(long iteration)
        {           
            return (this.config.MaxIterations > 0 && iteration >= this.config.MaxIterations
                   || this.config.MaxIterations == 0 && iteration >= this.config.Queries.Length
                   || config.MaxDurationInSeconds > 0 && DateTime.Now.Subtract((DateTime)startTime).TotalSeconds > config.MaxDurationInSeconds);
        }

        public void Run()
        {
            ExecuteQuery(-1, config.Startup);
            SemaphoreSlim maxThread = new SemaphoreSlim(config.WorkerThreads);
            startTime = DateTime.Now;
            while (true)
            {
                maxThread.Wait();
                long iterationNumber;
                lock (queryLock)
                {
                    iterationNumber = iteration++;
                }
                if (iterationNumber > 0 && iterationNumber % 1000 == 0)
                {
                    var handler = Progress;
                    if (handler != null)
                    {
                        var arg = new ProgressEventArgs() { Iteration = iterationNumber, ElapsedTimeInSeconds = DateTime.Now.Subtract((DateTime)startTime).TotalSeconds };
                        handler(this, arg);
                    }
                }
                if (IsCompleted(iterationNumber))
                {
                    break;
                }
                Task.Factory.StartNew(() =>
                {
                    Query query = GetNextQuery(iterationNumber);
                    ExecuteQuery(iterationNumber, query);
                    maxThread.Release();
                }, TaskCreationOptions.LongRunning | TaskCreationOptions.AttachedToParent);
            }

            int remaining = 0;

            lock (queryLock)
            {
                remaining = config.WorkerThreads - connections.Count();
            }
            while (remaining > 0)
            {
                maxThread.Wait();
                lock (queryLock)
                {
                    remaining = config.WorkerThreads - connections.Count();
                }
            }

            lock (queryLock)
            {
                ExecuteQuery(iteration, config.Cleanup);
                var handler = WorkloadEnd;
                if (handler != null)
                {
                    var arg = new ProgressEventArgs() { Iteration = iteration, ElapsedTimeInSeconds = DateTime.Now.Subtract((DateTime)startTime).TotalSeconds };
                    handler(this, arg);
                }
            }
        }

        private void ExecuteQuery(long iterationNumber, Query query)
        {
            SqlConnection connection = null;
            DateTime start = DateTime.Now;
            long rowCount = 0;
            try
            {
                if (query == null)
                    return;

                var handler = this.QueryStart;
                if (handler != null)
                {
                    handler(this, new QueryIterationEventArgs() { Query = query, Iteration = iterationNumber, IsCompleted = false });
                }

                lock (connectionLock)
                {
                    connection = this.connections.First();
                    this.connections.Remove(connection);
                    if (connection.State == ConnectionState.Broken)
                    {
                        connection.Close();
                    }
                    if (connection.State == ConnectionState.Closed)
                    {
                        connection.Open();
                    }
                }
                
                {
                    var cmd = new SqlCommand
                    {
                        CommandText = query.Text,
                        CommandType = query.Type,
                        Connection = connection
                    };

                    if(commandProcessor!=null)
                        commandProcessor.Process(query, cmd);

                    if (query.IsReader==null || (bool)query.IsReader)
                    {
                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                rowCount++;
                            };
                        }
                    }
                    else
                    {
                        rowCount = cmd.ExecuteNonQuery();
                    }

                    {
                        var queryEndHandler = this.QueryEnd;
                        if (queryEndHandler != null)
                        {
                            queryEndHandler(this, new QueryIterationEventArgs()
                            {
                                Query = query,
                                Iteration = iterationNumber,
                                IsCompleted = true,
                                ElapsedTime = DateTime.Now.Subtract(start),
                                RowCount = rowCount
                            });
                        }
                    }
                }
            }
            catch (Exception ex) {
                Console.WriteLine(ex);
                var handler = this.Error;
                if (handler != null)
                {
                    handler(this, new ErrorEventArgs() {
                        Exception = ex,
                        Query = query,
                        Config = this.config
                    });
                }
            }
            finally
            {
                lock (connectionLock)
                {
                    if (connection != null)
                        this.connections.Add(connection);
                }
            }
        }

        private static int iteration = 0;
        private static readonly object queryLock = new object();

        private Query GetNextQuery(long iteration)
        {
            lock (queryLock)
            {
                return this.config.Queries[iteration% (this.config.Queries.Length)];
            }
        }
    }

    public class ProgressEventArgs : EventArgs
    {
        public long Iteration { get; set; }
        public double ElapsedTimeInSeconds { get; set; }
    }

    public class QueryIterationEventArgs : EventArgs
    {
        public long Iteration { get; set; }
        public Query Query { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        public long RowCount { get; set; }
        public bool IsCompleted { get; set; }
    }

    public class ErrorEventArgs : EventArgs
    {
        public Exception Exception { get; set; }
        public Query Query { get; set; }
        public Configuration Config { get; set; }
    }
}