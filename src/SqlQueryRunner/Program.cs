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
using QueryExecutionEngine;
using System;

namespace SqlQueryRunner
{
    public class Program
    {
        static void Main(string[] args)
        {
            var config = Configuration.LoadFromFile(System.Configuration.ConfigurationManager.AppSettings["ConfigurationFile"]);
            var driver = new Driver(config);

            driver.SqlInfo += (sender, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Message))
                    Console.Out.WriteLine(e.Message);
                foreach (var error in e.Errors)
                {
                    Console.Error.WriteLine(error);
                }
            };

            //driver.Progress += (o, e) =>
            //{
            //    Console.WriteLine("Current iteration: " + e.Iteration + "\tSeconds:" + e.ElapsedTimeInSeconds);
            //};

            driver.WorkloadEnd += (o, e) =>
            {
                Console.WriteLine($"Finished {e.Iteration} iterations in {e.ElapsedTimeInSeconds} seconds");
            };

            driver.Error += (o, e) => {
                Console.Error.WriteLine(e.Query.Text + "\nError: " + e.Exception.Message + e.Exception.StackTrace + e.Query.Text);
            };

            //driver.QueryStart += (o, e) =>
            //{
            //    Console.WriteLine($"Iteration: {e.Iteration}\tExecuting: {e.Query.Text}");
            //};

            driver.QueryEnd += (o, e) =>
            {
                Console.WriteLine($"Executed: {e.Query.Text} in {e.ElapsedTime.TotalMilliseconds} ms.\tRow count {e.RowCount}.");
            };

            driver.Run();
        }
    }
}
